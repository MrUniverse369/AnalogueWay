using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// HUD: the talisman gem strip next to the health and chi meters. Shows the "all ready" sprite
/// normally, and the sprite for the active slot (its gem glowing, the others greyed) while a talisman
/// runs. Long talismans blink near the end so the player knows the effect is about to wear off.
/// Sprites are made by Tools/UI/talisman_hud.py.
/// FX: each gem also gets a radial fill: a dark wedge sweeping away while the talisman cools down, and a faint
/// cream wedge showing how much of an active talisman's time is left (Resources/FX/Shapes/GemMask, built in code).
/// L2CYCLE (2026-10-24): the first gem is slot 1, which L2 cycles. While slot 1 holds its home talisman (the one the
/// Talisman Holder put there) the red gem shows as drawn; when L2 has swapped another talisman in, that talisman's own
/// gem is drawn over it (Resources/UI/HudGems, Tools/UI/talisman_hud.py gems): its colours, lit while it runs, greyed
/// with the strip while another slot runs, with a little pop as it changes.
/// SUMMON (2026-10-25): a gem whose slot is empty, or whose talisman is blocked in this level (Water Breathing in the
/// first level, so only the Spirit Dash shows usable there), is covered by a full dark wedge, matching PowerUpManager.
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
    private static readonly Color UnusableColour = new Color(0.08f, 0.09f, 0.12f, 0.8f); // SUMMON: empty or blocked here
    private Image[] radials;
    private string sceneName; // SUMMON:

    private void Reset()
    {
        icon = GetComponent<Image>();
    }

    private void Start()
    {
        sceneName = SceneManager.GetActiveScene().name; // SUMMON: the same scene name PowerUpManager checks
        BuildSlotGem(); // L2CYCLE: under the cooldown wedges
        BuildRadials();
    }

    // ---------------------------------------------------------------- L2CYCLE: slot 1's gem
    private Image slotGem;
    private Talisman shownInSlot1;
    private float gemPop;
    private readonly System.Collections.Generic.Dictionary<string, Sprite> gemSprites = new System.Collections.Generic.Dictionary<string, Sprite>();

    private void BuildSlotGem()
    {
        if (icon == null) return;
        RectTransform strip = icon.rectTransform;
        Rect r = strip.rect;
        float px = r.width / StripWidth, py = r.height / StripHeight;
        var go = new GameObject("Slot1Gem", typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(strip, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
        rt.pivot = new Vector2(0.5f, 0.5f); // pops about its middle
        rt.anchoredPosition = new Vector2(GemSize * px / 2f, GemSize * py / 2f);
        rt.sizeDelta = new Vector2(GemSize * px, GemSize * py);
        slotGem = go.AddComponent<Image>();
        slotGem.raycastTarget = false;
        slotGem.enabled = false;
    }

    private Sprite GemSprite(Talisman t, bool lit)
    {
        string key = t.name + (lit ? "_Active" : "");
        if (!gemSprites.TryGetValue(key, out Sprite s)) gemSprites[key] = s = Resources.Load<Sprite>("UI/HudGems/" + key);
        return s;
    }

    private void UpdateSlotGem()
    {
        if (slotGem == null) return;
        PowerUpManager manager = PowerUpManager.Instance;
        Talisman inSlot = manager != null && manager.SlotCount > 0 ? manager.TalismanIn(0) : null;
        bool swapped = inSlot != null && PowerUpManager.UsesHolderLoadout && inSlot != Inventory.QuickHome;
        if (inSlot != shownInSlot1)
        {
            if (shownInSlot1 != null || swapped) gemPop = 1f; // not on the level's first frame
            shownInSlot1 = inSlot;
        }
        // the strip shows gem 1 lit (its active sprite), as drawn (ready), or greyed (another slot is running)
        bool lit = activeSprites.Length > 0 && icon.sprite == activeSprites[0];
        Sprite s = swapped && (lit || icon.sprite == readySprite) ? GemSprite(inSlot, lit) : null;
        slotGem.enabled = s != null;
        if (s == null) return;
        slotGem.sprite = s;
        slotGem.color = icon.color; // dims with the strip when chi runs out
        gemPop = Mathf.Max(0f, gemPop - Time.unscaledDeltaTime * 5f);
        slotGem.rectTransform.localScale = Vector3.one * (1f + 0.35f * gemPop);
    }

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
        UpdateSlotGem(); // L2CYCLE:
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
            if (manager != null && (t == null || !t.IsAllowedInScene(sceneName))) // SUMMON: nothing usable in this slot here
            {
                img.color = UnusableColour;
                fill = 1f;
            }
            else if (t != null && manager.IsSlotActive(i) && SkillTree.DurationOf(t) > 0f) // SKILLS: the learned duration
            {
                img.color = ActiveColour;
                fill = manager.TimeLeft(i) / SkillTree.DurationOf(t);
            }
            else if (t != null && manager.CooldownLeft(i) > 0f && t.Cooldown * SkillTree.CooldownScale > 0f) // SKILLS: Quick Recharge
            {
                img.color = CooldownColour;
                fill = manager.CooldownLeft(i) / (t.Cooldown * SkillTree.CooldownScale);
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
            float duration = talisman != null ? SkillTree.DurationOf(talisman) : 0f; // SKILLS:
            bool endingSoon = duration >= minBlinkDuration && manager.TimeLeft(i) <= duration * blinkFraction;
            // flicking back to the "ready" strip reads as "nearly ready again"
            if (endingSoon && Mathf.Repeat(Time.unscaledTime * blinkRate, 1f) < 0.5f) return readySprite;
            return activeSprites[i] != null ? activeSprites[i] : readySprite;
        }
        return readySprite;
    }
}
