using TMPro;
using UnityEngine;

/// <summary>
/// TITLE (2026-10-19): a crisp one-pixel navy drop under a TextMeshPro label, the pixel-art way, so the main menu's
/// labels read over the bright sky of the reference title screen. It draws copies of the text in the drop colour,
/// nudged one art pixel down and down-right, just behind the label. A full ring outline was tried first and doesn't
/// suit PerfectDOSVGA437 at its pixel size: its strokes and gaps are one pixel wide, so any outline fills the letters'
/// insides and turns each letter into a block (TMP's own SDF outline did the same). The copies follow the label's text,
/// font, size, alignment and visibility; the label's own colour stays free to change (MainMenuScene turns the selected
/// label yellow and skips the copies with IsCopy). The copies are never saved with the scene.
/// </summary>
[RequireComponent(typeof(TMP_Text))]
public class PixelTextOutline : MonoBehaviour
{
    /// <summary>The copies' names end with this, so code looking for the real label can skip them.</summary>
    public const string CopySuffix = " (drop)";

    [SerializeField] private Color colour = new Color32(0x21, 0x34, 0x47, 0xff); // the title's navy
    [Tooltip("Where each copy sits, in art pixels (the menu canvas is one unit per art pixel).")]
    [SerializeField] private Vector2[] offsets = { new Vector2(0f, -1f), new Vector2(1f, -1f) };

    private TMP_Text label;
    private TMP_Text[] copies;
    private string lastText;

    public static bool IsCopy(Component c) => c != null && c.name.EndsWith(CopySuffix);

    private void Start()
    {
        label = GetComponent<TMP_Text>();
        var src = (RectTransform)transform;
        copies = new TMP_Text[offsets.Length];
        for (int i = 0; i < offsets.Length; i++)
        {
            var go = new GameObject(name + CopySuffix, typeof(RectTransform)) { hideFlags = HideFlags.DontSave };
            var rt = (RectTransform)go.transform;
            rt.SetParent(src.parent, false);
            rt.SetSiblingIndex(src.GetSiblingIndex()); // just before the label, so it draws underneath
            var t = go.AddComponent<TextMeshProUGUI>();
            t.raycastTarget = false;
            copies[i] = t;
        }
        Sync(true);
    }

    private void LateUpdate() => Sync(false);

    private void Sync(bool all)
    {
        if (label == null || copies == null) return;
        var src = (RectTransform)transform;
        bool shown = label.isActiveAndEnabled && label.alpha > 0f;
        bool textChanged = label.text != lastText;
        lastText = label.text;
        for (int i = 0; i < copies.Length; i++)
        {
            TMP_Text t = copies[i];
            if (t == null) continue;
            if (t.gameObject.activeSelf != shown) t.gameObject.SetActive(shown);
            if (all || textChanged)
            {
                t.font = label.font;
                t.fontSharedMaterial = label.fontSharedMaterial;
                t.fontSize = label.fontSize;
                t.fontStyle = label.fontStyle;
                t.alignment = label.alignment;
                t.characterSpacing = label.characterSpacing;
                t.textWrappingMode = label.textWrappingMode;
                t.overflowMode = label.overflowMode;
                t.text = label.text;
            }
            Color c = colour;
            c.a *= label.color.a;
            t.color = c;
            var rt = (RectTransform)t.transform;
            rt.anchorMin = src.anchorMin;
            rt.anchorMax = src.anchorMax;
            rt.pivot = src.pivot;
            rt.sizeDelta = src.sizeDelta;
            rt.localScale = src.localScale;
            rt.anchoredPosition = src.anchoredPosition + offsets[i];
        }
    }

    private void OnDestroy()
    {
        if (copies == null) return;
        foreach (TMP_Text t in copies)
            if (t != null) Destroy(t.gameObject);
    }
}
