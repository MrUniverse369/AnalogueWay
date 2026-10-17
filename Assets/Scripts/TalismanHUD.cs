using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HUD: the talisman gem strip next to the health and chi meters. Shows the "all ready" sprite
/// normally, and the sprite for the active slot (its gem glowing, the others greyed) while a talisman
/// runs. Long talismans blink near the end so the player knows the effect is about to wear off.
/// Sprites are made by Tools/UI/talisman_hud.py.
/// </summary>
public class TalismanHUD : MonoBehaviour
{
    [SerializeField] private Image icon;
    [Tooltip("All gems lit: every talisman is ready.")]
    [SerializeField] private Sprite readySprite;
    [Tooltip("One per PowerUpManager slot, in slot order (Z, X, C).")]
    [SerializeField] private Sprite[] activeSprites;
    [Tooltip("Blink during this last fraction of a talisman's duration.")]
    [SerializeField, Range(0f, 1f)] private float blinkFraction = 0.3f;
    [Tooltip("Talismans shorter than this don't blink (a 0.5 s dash would just flicker).")]
    [SerializeField] private float minBlinkDuration = 2f;
    [SerializeField] private float blinkRate = 8f;

    private void Reset()
    {
        icon = GetComponent<Image>();
    }

    private void Update()
    {
        if (icon == null) return;
        icon.sprite = CurrentSprite();
    }

    private Sprite CurrentSprite()
    {
        PowerUpManager manager = PowerUpManager.Instance;
        if (manager == null) return readySprite;

        for (int i = 0; i < manager.SlotCount && i < activeSprites.Length; i++)
        {
            if (!manager.IsSlotActive(i)) continue;

            Talisman talisman = manager.TalismanIn(i);
            float duration = talisman != null ? talisman.Duration : 0f;
            bool endingSoon = duration >= minBlinkDuration && manager.TimeLeft(i) <= duration * blinkFraction;
            // flicking back to the "ready" strip reads as "nearly ready again"
            if (endingSoon && Mathf.Repeat(Time.unscaledTime * blinkRate, 1f) < 0.5f) return readySprite;
            return activeSprites[i] != null ? activeSprites[i] : readySprite;
        }
        return readySprite;
    }
}
