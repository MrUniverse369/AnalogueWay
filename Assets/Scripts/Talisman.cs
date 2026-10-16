using System;
using UnityEngine;

/// <summary>What a talisman grants while active. Other scripts ask for these instead of reading statics.</summary>
[Flags]
public enum TalismanTrait
{
    None = 0,
    EnemyImmunity = 1 << 0,            // enemies can't hurt or knock back the player
    DefeatsEnemiesOnContact = 1 << 1,  // touching an enemy destroys it
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

    [Header("Timing (seconds)")]
    [SerializeField, Min(0.01f)] private float duration = 1f;
    [Tooltip("Wait after the effect ends before it can be used again.")]
    [SerializeField, Min(0f)] private float cooldown;
    [Tooltip("Keyboard only: keep re-triggering while the key is held. Gamepad always triggers on press.")]
    [SerializeField] private bool retriggerWhileHeld;

    [Header("Rules")]
    [SerializeField] private TalismanTrait traits;
    [Tooltip("Scene build indices where this talisman is disabled.")]
    [SerializeField] private int[] blockedBuildIndices;

    [Header("Feedback")]
    [Tooltip("Optional Animator bool set true while active.")]
    [SerializeField] private string animatorBool;
    [Tooltip("Optional. Falls back to AudioManager.powerUpSound.")]
    [SerializeField] private AudioClip activationSound;

    public string DisplayName => displayName;
    public float Duration => duration;
    public float Cooldown => cooldown;
    public bool RetriggerWhileHeld => retriggerWhileHeld;
    public TalismanTrait Traits => traits;
    public string AnimatorBool => animatorBool;
    public bool HasAnimatorBool => !string.IsNullOrEmpty(animatorBool);
    public AudioClip ActivationSound => activationSound;

    public bool IsAllowedInScene(int buildIndex) =>
        blockedBuildIndices == null || Array.IndexOf(blockedBuildIndices, buildIndex) < 0;

    /// <summary>Extra start conditions (e.g. must be grounded). Return false to refuse activation.</summary>
    public virtual bool CanActivate(PowerUpContext ctx) => true;

    public virtual void OnActivated(PowerUpContext ctx) { }

    public virtual void OnDeactivated(PowerUpContext ctx) { }
}
