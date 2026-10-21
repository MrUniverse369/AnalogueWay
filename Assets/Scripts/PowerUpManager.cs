using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

/// <summary>
/// Replaces ActiveCardManager. Owns input, timers, activation and expiry for every talisman.
/// Rename ActiveCardManager.cs to PowerUpManager.cs inside the Unity Editor so the GUID is kept.
/// </summary>
public class PowerUpManager : MonoBehaviour
{
    public static PowerUpManager Instance { get; private set; }

    /// <summary>Slot index and talisman. A UI can subscribe to these later.</summary>
    public event Action<int, Talisman> TalismanActivated;
    public event Action<int, Talisman> TalismanDeactivated;

    [Serializable]
    private class Slot
    {
        public Talisman talisman;
        public KeyCode keyboardKey;

        [NonSerialized] public bool isActive;
        [NonSerialized] public float timeLeft;
        [NonSerialized] public float cooldownLeft;
        [NonSerialized] public bool pressQueued;
    }

    [Header("Setup")]
    [FormerlySerializedAs("pScriptRef")]
    [SerializeField] private PlayerController player;
    [Tooltip("Slots map in order to the gamepad actions powerUpOne, powerUpTwo and powerUpThree.")]
    [SerializeField] private Slot[] slots;
    [Tooltip("When on, a talisman can't start while another one is active.")]
    [SerializeField] private bool oneAtATime = true;

    private PlayerControls controls;
    private PowerUpContext context;
    private string sceneName;
    private TalismanTrait activeTraits;

    /// <summary>True while any active talisman grants the trait. Safe to call from any scene.</summary>
    public static bool Has(TalismanTrait trait) =>
        Instance != null && (Instance.activeTraits & trait) != TalismanTrait.None;

    public int SlotCount => slots.Length;

    public bool IsSlotActive(int slotIndex) =>
        (uint)slotIndex < (uint)slots.Length && slots[slotIndex].isActive;

    /// <summary>HUD: seconds left on an active slot (0 when not active).</summary>
    public float TimeLeft(int slotIndex) =>
        IsSlotActive(slotIndex) ? slots[slotIndex].timeLeft : 0f;

    /// <summary>HUD: seconds of cooldown left on a slot (0 when ready or out of range).</summary>
    public float CooldownLeft(int slotIndex) =>
        (uint)slotIndex < (uint)slots.Length ? Mathf.Max(0f, slots[slotIndex].cooldownLeft) : 0f;

    /// <summary>HUD: the talisman equipped in a slot (null when empty or out of range).</summary>
    public Talisman TalismanIn(int slotIndex) =>
        (uint)slotIndex < (uint)slots.Length ? slots[slotIndex].talisman : null;

    public bool IsActive(Talisman talisman)
    {
        foreach (Slot slot in slots)
            if (slot.isActive && slot.talisman == talisman) return true;
        return false;
    }

    private void Awake()
    {
        Instance = this;
        sceneName = SceneManager.GetActiveScene().name;
        if (slots == null) slots = new Slot[0];
        if (player == null) player = FindObjectOfType<PlayerController>();

        if (player == null)
        {
            Debug.LogError("PowerUpManager: no PlayerController found in the scene.", this);
            enabled = false;
            return;
        }

        context = new PowerUpContext(player, AudioManager.Instance); // PERF:

        controls = new PlayerControls();
        BindInput(0, controls.Gameplay.powerUpOne);
        BindInput(1, controls.Gameplay.powerUpTwo);
        BindInput(2, controls.Gameplay.powerUpThree);

        ApplyLoadout(); // MENUS: the talismans the player equipped in the Talisman Holder
        Inventory.LoadoutChanged += ApplyLoadout;
    }

    private void OnEnable() => controls?.Gameplay.Enable();

    private void OnDisable() => controls?.Gameplay.Disable();

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        Inventory.LoadoutChanged -= ApplyLoadout; // MENUS:
        controls?.Dispose();
    }

    /// <summary>
    /// TUTORIAL (2026-10-20): a fixed loadout that replaces the Talisman Holder's while set (the Basic Controls level
    /// gives only the Spirit Dash and Super Jump, in their usual X and C slots). null = the holder's loadout.
    /// </summary>
    public static void SetLoadoutOverride(Talisman[] loadout)
    {
        loadoutOverride = loadout;
        if (Instance != null) Instance.ApplyLoadout();
    }

    private static Talisman[] loadoutOverride;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => loadoutOverride = null;

    // MENUS: slot i holds the talisman equipped in holder slot i. Without a catalog (Resources/HNLCatalog) the
    // inspector slots are kept as they are.
    private void ApplyLoadout()
    {
        if (!Inventory.HasCatalog && loadoutOverride == null) return;
        for (int i = 0; i < slots.Length && i < Inventory.SlotCount; i++)
        {
            Talisman wanted = loadoutOverride != null ? (i < loadoutOverride.Length ? loadoutOverride[i] : null) : Inventory.Equipped(i); // TUTORIAL:
            if (slots[i].talisman != wanted) Equip(i, wanted);
        }
    }

    private void Update()
    {
        float dt = Time.deltaTime;

        // Expire first, then read input, so a held key re-triggers in the same frame
        // with no one-frame gap in the effect.
        for (int i = 0; i < slots.Length; i++) Tick(slots[i], dt, i);

        // PAUSE: menu presses (Cross is also "submit") must not queue a talisman for when the game resumes
        if (MenueControl.isGamePaused)
        {
            foreach (Slot slot in slots) slot.pressQueued = false;
            return;
        }
        for (int i = 0; i < slots.Length; i++) HandleInput(i);
    }

    // ---------- Public API ----------

    /// <summary>Ends everything and clears cooldowns. Call on respawn.</summary>
    public void ResetAll()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i].isActive) End(i);
            slots[i].cooldownLeft = 0f;
        }
    }

    /// <summary>CLASH: ends every active talisman that grants the trait (a samurai clash stops the Spirit Dash).</summary>
    public void EndTrait(TalismanTrait trait)
    {
        for (int i = 0; i < slots.Length; i++)
            if (slots[i].isActive && slots[i].talisman != null && (slots[i].talisman.Traits & trait) != TalismanTrait.None) End(i);
    }

    /// <summary>PAUSE: drop presses that arrived this frame (e.g. the Cross that clicked Resume).</summary>
    public void CancelQueuedPresses()
    {
        foreach (Slot slot in slots) slot.pressQueued = false;
    }

    /// <summary>Swap the talisman in a slot at runtime (pickups, shops, unlocks).</summary>
    public void Equip(int slotIndex, Talisman talisman)
    {
        if ((uint)slotIndex >= (uint)slots.Length) return;

        if (slots[slotIndex].isActive && slots[slotIndex].talisman != null) End(slotIndex);
        slots[slotIndex].talisman = talisman;
        slots[slotIndex].cooldownLeft = 0f;
    }

    // ---------- Internals ----------

    private void BindInput(int slotIndex, InputAction action)
    {
        if (slotIndex < slots.Length) action.performed += _ => slots[slotIndex].pressQueued = true;
    }

    private void Tick(Slot slot, float dt, int index)
    {
        if (slot.talisman == null) return;

        if (slot.isActive)
        {
            slot.timeLeft -= dt;
            // CHI: a running talisman spends chi at its own rate and ends the moment the last unit is gone
            bool chiLeft = slot.talisman.ChiPerSecond <= 0f || Chi.IsFree // CHIFREE: free at the final combo stage,
                           || Chi.Spend(slot.talisman.ChiPerSecond * Chi.CostScale * SkillTree.TalismanChiScale * dt); // cheaper up the ladder // SKILLS: Chi Focus
            if (slot.timeLeft <= 0f || !chiLeft) End(index);
            else slot.talisman.OnTick(context, dt); // MENUS: Feather Fall, Coin Magnet
        }
        else if (slot.cooldownLeft > 0f)
        {
            slot.cooldownLeft -= dt;
        }
    }

    private void HandleInput(int index)
    {
        Slot slot = slots[index];
        if (slot.talisman == null) return;

        bool pressed = slot.pressQueued || KeyboardPressed(slot);
        slot.pressQueued = false;
        if (pressed) TryActivate(index);
    }

    private static bool KeyboardPressed(Slot slot)
    {
        if (slot.keyboardKey == KeyCode.None) return false;
        return slot.talisman.RetriggerWhileHeld
            ? Input.GetKey(slot.keyboardKey)
            : Input.GetKeyDown(slot.keyboardKey);
    }

    private void TryActivate(int index)
    {
        Slot slot = slots[index];
        Talisman talisman = slot.talisman;

        if (slot.isActive || slot.cooldownLeft > 0f) return;
        if (player.IsStunned) return; // STUN: dazed by a samurai's block: no dashing (or any talisman) until it wears off
        if (oneAtATime && IsAnyActive()) return;
        if (!talisman.IsAllowedInScene(sceneName) || !talisman.CanActivate(context)) return;
        if (talisman.ChiPerSecond > 0f && Chi.IsEmpty && !Chi.IsFree) // CHI: all nine units spent: nothing starts until chi is restored // CHIFREE: unless at the final combo stage
        {
            DenyFeedback();
            return;
        }
        float upfront = talisman.ChiOnActivate * Chi.CostScale * SkillTree.TalismanChiScale; // SKILLS: Chi Focus // HEAL: a one-off cost (cheaper up the combo ladder, free at the top)
        if (upfront > 0f && PlayerController.chi < upfront - 0.01f)
        {
            DenyFeedback();
            return;
        }
        if (upfront > 0f) Chi.Spend(upfront);

        slot.isActive = true;
        slot.timeLeft = SkillTree.DurationOf(talisman); // SKILLS: Lasting Charms (and Dash Power for Spirit Dash)

        PlayActivationFeedback(talisman);
        talisman.OnActivated(context);

        RebuildTraits();
        TalismanActivated?.Invoke(index, talisman);
    }

    private void End(int index)
    {
        Slot slot = slots[index];
        Talisman talisman = slot.talisman;

        slot.isActive = false;
        slot.timeLeft = 0f;
        slot.cooldownLeft = talisman.Cooldown * SkillTree.CooldownScale; // SKILLS: Quick Recharge

        talisman.OnDeactivated(context);
        if (talisman.HasAnimatorBool && context.Animator != null)
            context.Animator.SetBool(talisman.AnimatorBool, false);

        RebuildTraits();
        TalismanDeactivated?.Invoke(index, talisman);
    }

    private void PlayActivationFeedback(Talisman talisman)
    {
        if (talisman.HasAnimatorBool && context.Animator != null)
            context.Animator.SetBool(talisman.AnimatorBool, true);

        if (context.Audio == null) return;
        AudioClip clip = talisman.ActivationSound != null ? talisman.ActivationSound : context.Audio.powerUpSound;
        context.Audio.PlaySfx(clip);
    }

    // CHI: a low fizzle and a shake of the chi meter. A held key retries every frame, so at most every DenyInterval.
    private const float DenyInterval = 0.4f;
    private float nextDeny;

    private void DenyFeedback()
    {
        if (Time.unscaledTime < nextDeny) return;
        nextDeny = Time.unscaledTime + DenyInterval;
        Chi.Deny();
        if (context.Audio != null) context.Audio.PlaySfxOneShotPitched(context.Audio.powerUpSound, 0.55f, 0.5f);
    }

    private bool IsAnyActive()
    {
        foreach (Slot slot in slots)
            if (slot.isActive) return true;
        return false;
    }

    private void RebuildTraits()
    {
        activeTraits = TalismanTrait.None;
        foreach (Slot slot in slots)
            if (slot.isActive) activeTraits |= slot.talisman.Traits;
    }
}
