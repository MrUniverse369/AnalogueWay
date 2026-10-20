using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HUD: the talisman gem strip next to the health and chi meters. Shows the "all ready" sprite
/// normally, and the sprite for the active slot (its gem glowing, the others greyed) while a talisman
/// runs. Long talismans blink near the end so the player knows the effect is about to wear off.
/// Sprites are made by Tools/UI/talisman_hud.py.
/// FX: each gem also gets a radial fill: a dark wedge sweeping away while the talisman cools down, and a faint
/// cream wedge showing how much of an active talisman's time is left (Resources/FX/Shapes/GemMask, built in code).
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

    // FX: the strip is 21x6 art pixels; gem i covers columns 7i..7i+5
    private const int StripWidth = 21, StripHeight = 6, GemSize = 6, GemStep = 7;
    private static readonly Color CooldownColour = new Color(0.2f, 0.22f, 0.25f, 0.75f);
    private static readonly Color ActiveColour = new Color(1f, 1f, 0.92f, 0.4f);
    private Image[] radials;

    private void Reset()
    {
        icon = GetComponent<Image>();
    }

    private void Start() => BuildRadials();

    private void BuildRadials()
    {
        Sprite mask = Resources.Load<Sprite>("FX/Shapes/GemMask");
        if (icon == null || mask == null) return;
        RectTransform strip = icon.rectTransform;
        Rect r = strip.rect;
        float px = r.width / StripWidth, py = r.height / StripHeight;
        radials = new Image[3];
        for (int i = 0; i < radials.Length; i++)
        {
            var go = new GameObject("CooldownRadial" + i, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(strip, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = new Vector2(r.xMin - strip.rect.xMin + i * GemStep * px, 0f) + new Vector2(0f, r.yMin - strip.rect.yMin);
            rt.sizeDelta = new Vector2(GemSize * px, GemSize * py);
            var img = go.AddComponent<Image>();
            img.sprite = mask;
            img.type = Image.Type.Filled;
            img.fillMethod = Image.FillMethod.Radial360;
            img.fillOrigin = (int)Image.Origin360.Top;
            img.fillClockwise = false;
            img.raycastTarget = false;
            img.enabled = false;
            radials[i] = img;
        }
    }

    private static readonly Color NoChiTint = new Color(0.5f, 0.5f, 0.56f, 1f); // CHI:

    private void Update()
    {
        if (icon == null) return;
        icon.sprite = CurrentSprite();
        icon.color = Chi.IsEmpty && !Chi.IsFree ? NoChiTint : Color.white; // CHIFREE: usable at the final combo stage // CHI: no chi left: the gems dim until it is restored
        UpdateRadials();
    }

    private void UpdateRadials()
    {
        PowerUpManager manager = PowerUpManager.Instance;
        if (radials == null) return;
        for (int i = 0; i < radials.Length; i++)
        {
            Image img = radials[i];
            Talisman t = manager != null && i < manager.SlotCount ? manager.TalismanIn(i) : null;
            float fill = 0f;
            if (t != null && manager.IsSlotActive(i) && t.Duration > 0f)
            {
                img.color = ActiveColour;
                fill = manager.TimeLeft(i) / t.Duration;
            }
            else if (t != null && manager.CooldownLeft(i) > 0f && t.Cooldown > 0f)
            {
                img.color = CooldownColour;
                fill = manager.CooldownLeft(i) / t.Cooldown;
            }
            img.enabled = fill > 0.001f;
            img.fillAmount = Mathf.Clamp01(fill);
        }
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
