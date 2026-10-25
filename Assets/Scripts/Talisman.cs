using System;
using UnityEngine;

/// <summary>What a talisman grants while active. Other scripts ask for these instead of reading statics.</summary>
[Flags]
public enum TalismanTrait
{
    None = 0,
    EnemyImmunity = 1 << 0,            // enemies can't hurt or knock back the player
    DefeatsEnemiesOnContact = 1 << 1,  // touching an enemy destroys it
    WaterBreathing = 1 << 2,           // WATER: no breath is used underwater
    HealShield = 1 << 3,               // HEAL: SHIELD (2026-10-21): nothing can hurt or knock back the player: enemies,
                                       // bosses, spikes, kill planes, chest traps, poison, drowning (a pit lifts him to safe ground)
}

/// <summary>Everything a talisman is allowed to touch.</summary>
public sealed class PowerUpContext
{
    public PlayerController Player { get; }
    public Animator Animator { get; }
    public AudioManager Audio { get; }

    public PowerUpContext(PlayerController player, AudioManager audio)
    {
        Player = player;
        Animator = player.PlayerAnimator;
        Audio = audio;
    }
}

/// <summary>
/// A talisman with no behaviour of its own. Timing, traits, animator bool and sound are all
/// set in the inspector. Subclass it only when you need custom behaviour.
/// </summary>
[CreateAssetMenu(menuName = "Talismans/Plain Talisman", fileName = "NewTalisman")]
public class Talisman : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string displayName;
    [Tooltip("MENUS: shown in the Talisman Holder.")]
    [SerializeField] private Sprite icon;
    [SerializeField, TextArea(2, 4)] private string description;
    [Tooltip("MENUS: how a locked talisman is found, shown in the Talisman Holder.")]
    [SerializeField] private string unlockHint;

    [Header("Timing (seconds)")]
    [SerializeField, Min(0.01f)] private float duration = 1f;
    [Tooltip("Wait after the effect ends before it can be used again.")]
    [SerializeField, Min(0f)] private float cooldown;
    [Tooltip("Keyboard only: keep re-triggering while the key is held. Gamepad always triggers on press.")]
    [SerializeField] private bool retriggerWhileHeld;

    [Header("Chi")]
    [Tooltip("CHI: chi spent per second while active (10 chi = one unit on the meter, 90 = all nine). " +
             "0 = free. With no chi left the talisman can't start, and an active one ends.")]
    [SerializeField, Min(0f)] private float chiPerSecond;

    [Header("Rules")]
    [SerializeField] private TalismanTrait traits;
    [Tooltip("Scene names (e.g. Z4A1GrassGreen) where this talisman is disabled. Names, not build indices, so reordering or disabling scenes in Build Settings can't move the block.")]
    [SerializeField] private string[] blockedScenes;

    [Header("Feedback")]
    [Tooltip("Optional Animator bool set true while active.")]
    [SerializeField] private string animatorBool;
    [Tooltip("Optional. Falls back to AudioManager.powerUpSound.")]
    [SerializeField] private AudioClip activationSound;

    public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;
    public Sprite Icon => icon;
    public string Description => description;
    public string UnlockHint => unlockHint;
    public float Duration => duration;
    public float Cooldown => cooldown;
    public bool RetriggerWhileHeld => retriggerWhileHeld;
    public float ChiPerSecond => chiPerSecond; // CHI:
    public TalismanTrait Traits => traits;
    public string AnimatorBool => animatorBool;
    public bool HasAnimatorBool => !string.IsNullOrEmpty(animatorBool);
    public AudioClip ActivationSound => activationSound;

    public bool IsAllowedInScene(string sceneName) =>
        blockedScenes == null || Array.IndexOf(blockedScenes, sceneName) < 0;

    /// <summary>HEAL: chi paid once when the talisman starts (on top of ChiPerSecond). 0 = none. Without enough chi
    /// it can't start.</summary>
    public virtual float ChiOnActivate => 0f;

    /// <summary>Extra start conditions (e.g. must be grounded). Return false to refuse activation.</summary>
    public virtual bool CanActivate(PowerUpContext ctx) => true;

    public virtual void OnActivated(PowerUpContext ctx) { }

    public virtual void OnDeactivated(PowerUpContext ctx) { }

    /// <summary>Called every frame while active, for effects that need continuous work (pulling coins, slowing a fall).</summary>
    public virtual void OnTick(PowerUpContext ctx, float dt) { }

    /// <summary>FOXMOUNT: false ends the talisman early (the mount ends when Harvey gets off some other way).</summary>
    public virtual bool KeepRunning(PowerUpContext ctx) => true;

    /// <summary>FOXMOUNT: pressing its button again while active ends it (the mount: press again to get off).</summary>
    public virtual bool EndsOnPress => false;

    /// <summary>FOXMOUNT: runs alongside other talismans: it neither waits for nor blocks them (PowerUpManager oneAtATime).</summary>
    public virtual bool RunsAlongside => false;
}
