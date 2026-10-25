using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// RANKS (2026-10-24): the rank on the level complete card (LevelUIStyle builds the card and adds this). The run's
/// numbers sit on the left (time against par, coins, enemies, hits and deaths, best combo) and the rank on a plate on
/// the right, stamped in a moment after the card appears. Lower ranks are plain and easy to read (Cream or Muted on
/// the dark plate); an S gets the selected plate (Yellow rim), a Yellow letter, a cream shine sweeping across it and a
/// few stars twinkling round its rim. NEW BEST shows when the run beats the level's saved rank.
/// Everything runs on unscaled time: the game is frozen while the card is up (NextLevel).
/// </summary>
public class LevelGradeView : MonoBehaviour
{
    private const float StampDelay = 0.5f, StampTime = 0.18f;

    private LevelComplete panel;
    private TMP_Text enemies, survival, combo, rankLabel, letter, shadow, note;
    private Image plate, shine;
    private readonly List<Image> stars = new List<Image>();
    private GradeResult result;
    private float shownAt = -1f;
    private bool stamped;

    public static LevelGradeView Build(RectTransform card, LevelComplete lc)
    {
        var view = card.gameObject.AddComponent<LevelGradeView>();
        view.panel = lc;
        HNLCatalog cat = Inventory.Catalog;
        view.enemies = Line(card, 0);
        view.survival = Line(card, -10);
        view.combo = Line(card, -20);

        view.rankLabel = MenuUI.Text("RankLabel", card, new Vector2(56, 27), new Vector2(52, 7), MenuUI.SmallSize, MenuUI.Muted, TextAlignmentOptions.Center, "RANK");
        view.plate = MenuUI.Image("RankPlate", card, new Vector2(56, -3), new Vector2(48, 48), cat.card, true);
        view.plate.gameObject.AddComponent<RectMask2D>(); // keeps the shine inside the plate
        view.shine = MenuUI.Image("Shine", view.plate.transform, Vector2.zero, new Vector2(6, 90), Pixel());
        view.shine.color = new Color(1f, 1f, 0.92f, 0.22f);
        view.shine.rectTransform.localRotation = Quaternion.Euler(0, 0, -28f);
        view.shine.enabled = false;
        view.shadow = MenuUI.Text("RankShadow", view.plate.transform, new Vector2(1, -2), new Vector2(48, 40), MenuUI.TitleSize * 4f, MenuUI.Deep, TextAlignmentOptions.Center, "");
        view.letter = MenuUI.Text("Rank", view.plate.transform, new Vector2(0, -1), new Vector2(48, 40), MenuUI.TitleSize * 4f, MenuUI.Cream, TextAlignmentOptions.Center, "");
        view.note = MenuUI.Text("Note", card, new Vector2(56, -34), new Vector2(64, 7), MenuUI.SmallSize, MenuUI.Yellow, TextAlignmentOptions.Center, "");
        for (int i = 0; i < 6; i++)
        {
            Image star = MenuUI.Image("Star" + i, card, Vector2.zero, new Vector2(1, 1), Pixel());
            star.enabled = false;
            view.stars.Add(star);
        }
        return view;
    }

    // a left-hand stat line, like the time and coins lines LevelUIStyle places above them
    private static TMP_Text Line(RectTransform card, float y)
    {
        TMP_Text t = MenuUI.Text("Stat", card, new Vector2(-44, y), new Vector2(104, 9), MenuUI.BodySize, MenuUI.Cream, TextAlignmentOptions.Left, "");
        t.fontStyle = FontStyles.UpperCase;
        return t;
    }

    private static Sprite pixel;
    private static Sprite Pixel()
    {
        if (pixel == null)
        {
            Texture2D white = Texture2D.whiteTexture;
            pixel = Sprite.Create(white, new Rect(0, 0, white.width, white.height), new Vector2(0.5f, 0.5f), white.width);
        }
        return pixel;
    }

    /// <summary>NextLevel: show this run on the card (the rank stamps in a moment later).</summary>
    public void Show(GradeResult r)
    {
        result = r;
        if (panel.GetSetFinalTimerText != null)
            panel.GetSetFinalTimerText.text = $"TIME {Clock(r.seconds)}   PAR {Clock(r.par)}";
        if (panel.GetCoinsCountText != null) panel.GetCoinsCountText.text = r.totalCoins > 0 ? $"COINS {r.coins}/{r.totalCoins}" : "COINS -";
        enemies.text = r.totalEnemies > 0 ? $"ENEMIES {r.kills}/{r.totalEnemies}" : "ENEMIES -";
        survival.text = $"HITS {r.hits}   DEATHS {r.deaths}";
        combo.text = $"BEST COMBO {r.bestCombo}";
        letter.text = shadow.text = "";
        note.text = "";
        note.color = MenuUI.Yellow;
        plate.sprite = Inventory.Catalog.card;
        plate.rectTransform.localScale = Vector3.one;
        shine.enabled = false;
        foreach (Image s in stars) s.enabled = false;
        stamped = false;
        shownAt = Time.unscaledTime;
    }

    private static string Clock(float seconds)
    {
        int s = Mathf.Max(0, Mathf.FloorToInt(seconds));
        return $"{s / 60:00}:{s % 60:00}";
    }

    private void Update()
    {
        if (result == null || shownAt < 0f) return;
        float t = Time.unscaledTime - shownAt;
        if (t < StampDelay) return;
        if (!stamped) Stamp();

        // the stamp: in from twice the size, easing out
        float k = Mathf.Clamp01((t - StampDelay) / StampTime);
        float scale = Mathf.Lerp(2.2f, 1f, 1f - (1f - k) * (1f - k));
        letter.rectTransform.localScale = shadow.rectTransform.localScale = Vector3.one * Mathf.Round(scale * 8f) / 8f;
        if (result.grade != Grade.S) return;

        // S: a cream shine sweeping across the plate every 1.6 s, and stars twinkling round its rim
        float sweep = Mathf.Repeat(t - StampDelay, 1.6f) / 0.7f;
        shine.enabled = sweep < 1f;
        if (shine.enabled) shine.rectTransform.anchoredPosition = new Vector2(Mathf.Round(Mathf.Lerp(-40f, 40f, sweep)), 0f);
        for (int i = 0; i < stars.Count; i++)
        {
            float phase = Mathf.Repeat(t * 0.9f + i / (float)stars.Count, 1f);
            Image s = stars[i];
            s.enabled = phase < 0.5f;
            if (!s.enabled) continue;
            float size = phase < 0.25f ? 1f : 2f; // a pixel that swells, then winks out
            s.rectTransform.sizeDelta = new Vector2(size, size);
            s.color = i % 2 == 0 ? MenuUI.Yellow : MenuUI.Cream;
        }
    }

    private void Stamp()
    {
        stamped = true;
        HNLCatalog cat = Inventory.Catalog;
        Grade g = result.grade;
        letter.text = shadow.text = g.ToString();
        letter.color = LevelGrade.ColourOf(g);
        rankLabel.color = g == Grade.S ? MenuUI.Yellow : MenuUI.Muted;
        if (g == Grade.S)
        {
            plate.sprite = cat.cardSelected; // the Yellow-rimmed plate
            // stars at fixed spots round the plate's rim, so they never cover the letter
            Vector2 c = plate.rectTransform.anchoredPosition;
            Vector2[] spots = { new Vector2(-26, 18), new Vector2(25, 25), new Vector2(27, -8), new Vector2(-27, -20), new Vector2(10, -27), new Vector2(-12, 26) };
            for (int i = 0; i < stars.Count; i++) stars[i].rectTransform.anchoredPosition = c + spots[i % spots.Length];
        }
        if (GameSession.IsTutorial) note.text = "";
        else if (result.newBest) note.text = "NEW BEST!";
        else if (result.previousBest != Grade.None) { note.text = "BEST " + result.previousBest; note.color = MenuUI.Muted; }
        HarveyMenus.PlayUiSound(g == Grade.S ? cat.equipSound : cat.selectSound);
    }
}
