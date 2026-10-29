using System.Collections;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEngine;

/// <summary>
/// DIALOGUEBAR (2026-10-29): scripted dialogue-box checks for the Playtest agent, started from the Unity MCP in Play
/// mode on a level with a villager: QADialogueDriver.Run("&lt;log file&gt;", "&lt;screenshot prefix&gt;"). It walks
/// Harvey up to the nearest villager, opens that villager's own conversation (the same DialogueBox.Open the Up/E press
/// makes), lets the first line type out, saves a screenshot, and measures in screen pixels how much of the box
/// covers Harvey, the villager and the HUD's health bar and level clock. Then it lays every line of every dialogue
/// file (Resources-independent: the TextAssets the scene's villagers and the chest game use, plus Assets/Dialogue in
/// the editor) into the open box and reports any that overflow it. It closes the conversation the way Esc does.
/// Lines go to the log file and the console as "[QADLG] ...".
/// </summary>
public class QADialogueDriver : MonoBehaviour
{
    private const BindingFlags Any = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    private string logPath, shotPrefix;
    private readonly StringBuilder log = new StringBuilder();

    public static string Run(string logPath, string shotPrefix)
    {
        var go = new GameObject("QADialogueDriver");
        var d = go.AddComponent<QADialogueDriver>();
        d.logPath = logPath;
        d.shotPrefix = shotPrefix;
        return "started";
    }

    private void Line(string s)
    {
        log.AppendLine("[QADLG] " + s);
        Debug.Log("[QADLG] " + s);
        if (!string.IsNullOrEmpty(logPath)) System.IO.File.WriteAllText(logPath, log.ToString());
    }

    private static object Get(object o, string field) => o.GetType().GetField(field, Any)?.GetValue(o);

    private IEnumerator Start()
    {
        PlayerController pc = FindFirstObjectByType<PlayerController>();
        Villager best = null;
        foreach (Villager v in FindObjectsByType<Villager>(FindObjectsSortMode.None))
            if (best == null || Mathf.Abs(v.transform.position.x - pc.transform.position.x) < Mathf.Abs(best.transform.position.x - pc.transform.position.x)) best = v;
        if (best == null) { Line("no villager in the scene"); yield break; }

        // stand Harvey on the ground a tile and a half from the villager, facing it
        SpriteRenderer vs = best.GetComponent<SpriteRenderer>();
        Vector3 vfeet = vs != null ? new Vector3(vs.bounds.center.x, vs.bounds.min.y, 0f) : best.transform.position;
        Vector2 at = new Vector2(vfeet.x - 1.5f, vfeet.y + 1.2f);
        RaycastHit2D hit = Physics2D.Raycast(at, Vector2.down, 4f, LayerMask.GetMask("Ground"));
        if (hit.collider != null) at.y = hit.point.y + 0.53f;
        pc.transform.position = at;
        pc.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
        var cam = FindFirstObjectByType<CameraController>();
        if (cam != null && cam.isActiveAndEnabled) cam.SnapToPlayer();
        PlayerController.BotX = 0.2f; // a nudge to face the villager
        yield return new WaitForSeconds(0.2f);
        PlayerController.BotX = 0f;
        yield return new WaitForSeconds(1.2f);

        var script = (DialogueScript)typeof(Villager).GetMethod("Script", Any).Invoke(best, null);
        string start = (string)Get(best, "startNode");
        bool opened = DialogueBox.Open(script, start, best);
        Line($"villager {best.DisplayName} ({best.name}) in {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}: opened {opened} at node '{start}'");
        if (!opened) yield break;
        yield return new WaitForSecondsRealtime(2.5f); // the first line types out (the game is paused, so real time)

        DialogueBox box = FindFirstObjectByType<DialogueBox>();
        var boxRect = (RectTransform)Get(box, "box");
        var body = (TMP_Text)Get(box, "bodyText");
        Rect b = ScreenRect(boxRect);
        Camera c = Camera.main;
        Rect harvey = WorldRect(c, pc.GetComponent<SpriteRenderer>().bounds);
        Rect villager = vs != null ? WorldRect(c, vs.bounds) : new Rect();
        Line($"screen {Screen.width}x{Screen.height}; box {Fmt(b)}; Harvey {Fmt(harvey)}; villager {Fmt(villager)}");
        Line($"box covers {Cover(b, harvey):0}% of Harvey and {Cover(b, villager):0}% of the villager");
        foreach (string hud in new[] { "HealthUI", "PlayerHealth", "Timer" })
        {
            GameObject h = GameObject.Find(hud);
            if (h == null || !(h.transform is RectTransform hr)) continue;
            Rect r = ScreenRect(hr);
            Line($"HUD {hud} {Fmt(r)}: box covers {Cover(b, r):0}% of it");
        }
        foreach (TMP_Text t in FindObjectsByType<TMP_Text>(FindObjectsSortMode.None))
            if (t.text.StartsWith("Time:") || t.text.StartsWith("TIME:")) Line($"HUD clock '{t.text}' {Fmt(ScreenRect(t.rectTransform))}: box covers {Cover(b, ScreenRect(t.rectTransform)):0}% of it");
        foreach (HealthUIManager hm in FindObjectsByType<HealthUIManager>(FindObjectsSortMode.None))
        {
            var img = (UnityEngine.UI.Image)Get(hm, "playerHealthUI");
            if (img != null) Line($"HUD health bar {Fmt(ScreenRect(img.rectTransform))}: box covers {Cover(b, ScreenRect(img.rectTransform)):0}% of it");
        }
        Line($"first line on screen: '{body.text}' ({body.textInfo.lineCount} lines, overflowing {body.isTextOverflowing})");
        if (!string.IsNullOrEmpty(shotPrefix)) ScreenCapture.CaptureScreenshot(shotPrefix + "_villager.png");
        yield return new WaitForSecondsRealtime(0.5f);

        // every line of every dialogue file, laid into the open box: at its width now, then at the narrowest the box
        // gets (176, a 16:10 screen) if that is narrower
        string keep = body.text;
        Vector2 size = body.rectTransform.sizeDelta;
        foreach (float width in new[] { size.x, 164f })
        {
            if (width > size.x) continue;
            body.rectTransform.sizeDelta = new Vector2(width, size.y);
            int lines = 0, over = 0, maxLines = 0;
            string worst = "";
            foreach (TextAsset ta in DialogueAssets())
            {
                DialogueScript s = DialogueScript.Parse(ta.text, ta.name);
                foreach (var node in s.nodes)
                    foreach (DialogueScript.Step step in node.Value)
                    {
                        if (step.kind != DialogueScript.StepKind.Line) continue;
                        body.text = step.text.Replace("{name}", "Villager").Replace("{player}", "Harvey");
                        body.maxVisibleCharacters = 99999;
                        body.ForceMeshUpdate();
                        lines++;
                        if (body.textInfo.lineCount > maxLines) { maxLines = body.textInfo.lineCount; worst = ta.name + ": " + body.text; }
                        if (body.isTextOverflowing) { over++; Line($"OVERFLOW at text width {width:0} {ta.name}:{step.lineNumber} ({body.textInfo.lineCount} lines) '{body.text}'"); }
                    }
            }
            Line($"text width {width:0} x {size.y:0}: laid out {lines} lines from the dialogue files: {over} overflow; the most lines one needs is {maxLines} ('{worst}')");
        }
        body.rectTransform.sizeDelta = size;
        body.text = keep;
        box.SendMessage("Close");
        yield return null;
        Line($"closed: IsOpen {DialogueBox.IsOpen}, timeScale {Time.timeScale}");
        Line("END");
    }

    private static TextAsset[] DialogueAssets()
    {
#if UNITY_EDITOR
        var list = new System.Collections.Generic.List<TextAsset>();
        foreach (string guid in UnityEditor.AssetDatabase.FindAssets("t:TextAsset", new[] { "Assets/Dialogue" }))
            list.Add(UnityEditor.AssetDatabase.LoadAssetAtPath<TextAsset>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid)));
        return list.ToArray();
#else
        return new TextAsset[0];
#endif
    }

    private static Rect ScreenRect(RectTransform rt)
    {
        var c = new Vector3[4];
        rt.GetWorldCorners(c); // overlay canvases: world corners are screen pixels
        return Rect.MinMaxRect(c[0].x, c[0].y, c[2].x, c[2].y);
    }

    private static Rect WorldRect(Camera cam, Bounds b)
    {
        Vector3 a = cam.WorldToScreenPoint(b.min), z = cam.WorldToScreenPoint(b.max);
        return Rect.MinMaxRect(a.x, a.y, z.x, z.y);
    }

    private static float Cover(Rect box, Rect r)
    {
        if (r.width <= 0f || r.height <= 0f) return 0f;
        float w = Mathf.Max(0f, Mathf.Min(box.xMax, r.xMax) - Mathf.Max(box.xMin, r.xMin));
        float h = Mathf.Max(0f, Mathf.Min(box.yMax, r.yMax) - Mathf.Max(box.yMin, r.yMin));
        return 100f * w * h / (r.width * r.height);
    }

    private static string Fmt(Rect r) => $"x {r.xMin:0}..{r.xMax:0} y {r.yMin:0}..{r.yMax:0}";
}
