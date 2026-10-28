using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// QA (2026-10-27): editor entry points for the Playtest agent, called through the Unity MCP. Every report is a JSON
/// file under Logs/QA/ (git-ignored, survives editor restarts) that `qa probe-result` / `qa perf` turn into records.
///
///   StructureReport(scenePath, kind)   opens the scene additively, checks what that kind of level needs, closes it.
///                                      Nothing is played: results count as "editor-check".
///   StartPlayCapture(scenePath, runId, scenario, settle, sample, action)
///                                      backs up the save slots, opens the scene, enters Play mode with a QAProbe
///                                      request, and on the way back restores the slots and reopens what was open.
///                                      Refuses (returns "error: ...") while playing, compiling, or with unsaved scenes,
///                                      so a capture can never discard work.
/// </summary>
[InitializeOnLoad]
public static class QARunner
{
    public const string OutDir = "Logs/QA";
    private const string ReturnKey = "HNL.QA.Return";
    private const string ReopenKey = "HNL.QA.Reopen";

    [Serializable]
    private class ReturnState
    {
        public string runId, output, saveBackup;
        public string[] scenes;
        public string requestedAt;
    }

    [Serializable]
    private class ProbeRequest
    {
        public string runId, scenario, scenePath, action, output;
        public float settle = 2f, sample = 10f;
    }

    static QARunner()
    {
        EditorApplication.playModeStateChanged -= OnPlayMode;
        EditorApplication.playModeStateChanged += OnPlayMode;
        if (!string.IsNullOrEmpty(SessionState.GetString(ReopenKey, ""))) { EditorApplication.update -= Reopen; EditorApplication.update += Reopen; }
    }

    // ---------------------------------------------------------------- play capture

    public static string StartPlayCapture(string scenePath, string runId, string scenario, float settle = 2f, float sample = 10f, string action = "")
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return "error: the editor is already in Play mode";
        if (EditorApplication.isCompiling) return "error: scripts are compiling; try again when they finish";
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene s = SceneManager.GetSceneAt(i);
            if (s.isDirty) return $"error: {(string.IsNullOrEmpty(s.path) ? s.name : s.path)} has unsaved changes; save or discard them before a capture";
        }
        if (!File.Exists(scenePath)) return $"error: no scene at {scenePath}";
        if (string.IsNullOrEmpty(runId) || runId.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return "error: runId must be a plain file name";

        Directory.CreateDirectory(OutDir);
        string output = Path.Combine(OutDir, runId + ".json");
        if (File.Exists(output)) File.Delete(output);
        var back = new ReturnState
        {
            runId = runId, output = output, saveBackup = BackupSaves(runId), requestedAt = DateTime.UtcNow.ToString("o"),
            scenes = Enumerable.Range(0, SceneManager.sceneCount).Select(i => SceneManager.GetSceneAt(i).path).Where(p => !string.IsNullOrEmpty(p)).ToArray(),
        };
        SessionState.SetString(ReturnKey, JsonUtility.ToJson(back));
        EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        SessionState.SetString(QAProbe.RequestKey, JsonUtility.ToJson(new ProbeRequest { runId = runId, scenario = scenario, scenePath = scenePath, action = action ?? "", output = output, settle = settle, sample = sample }));
        EditorApplication.isPlaying = true;
        return output;
    }

    private static void OnPlayMode(PlayModeStateChange change)
    {
        if (change != PlayModeStateChange.EnteredEditMode) return;
        string json = SessionState.GetString(ReturnKey, "");
        if (string.IsNullOrEmpty(json)) return;
        SessionState.EraseString(ReturnKey);
        SessionState.EraseString(QAProbe.RequestKey);
        var back = JsonUtility.FromJson<ReturnState>(json);
        string restored = RestoreSaves(back.saveBackup);
        if (!File.Exists(back.output))
        {
            // Play mode ended without a capture (a compile error, an exception before the probe, or someone pressed Stop)
            File.WriteAllText(back.output, "{\"tool\":\"QAProbe\",\"version\":1,\"runId\":" + Q(back.runId) + ",\"playStarted\":false,\"reason\":" +
                Q("Play mode ended before the probe wrote its capture (compile error, early exception, or Play stopped by hand). Saves: " + restored) + ",\"errors\":[],\"metrics\":{},\"missing\":[]}");
        }
        if (back.scenes != null && back.scenes.Length > 0)
        {
            // a one-shot delayCall can run before Unity has put back its own scene setup from before Play, which then
            // wins; keep trying from update until the editor is settled in Edit mode (survives a domain reload too)
            SessionState.SetString(ReopenKey, string.Join("\n", back.scenes));
            EditorApplication.update -= Reopen;
            EditorApplication.update += Reopen;
        }
    }

    private static int reopenFrames;

    private static void Reopen()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (++reopenFrames < 10) return; // let Unity finish restoring the pre-Play scenes first
        reopenFrames = 0;
        string[] scenes = SessionState.GetString(ReopenKey, "").Split('\n').Where(p => !string.IsNullOrEmpty(p)).ToArray();
        EditorApplication.update -= Reopen;
        SessionState.EraseString(ReopenKey);
        if (scenes.Length == 0) return;
        var open = Enumerable.Range(0, SceneManager.sceneCount).Select(i => SceneManager.GetSceneAt(i)).ToArray();
        if (open.Select(s => s.path).SequenceEqual(scenes)) return;
        if (open.Any(s => s.isDirty)) { Debug.LogWarning("QARunner: not reopening " + scenes[0] + " because an open scene has unsaved changes."); return; }
        EditorSceneManager.OpenScene(scenes[0], OpenSceneMode.Single);
        for (int i = 1; i < scenes.Length; i++) EditorSceneManager.OpenScene(scenes[i], OpenSceneMode.Additive);
    }

    // ---------------------------------------------------------------- save slots

    /// <summary>Copies every file in persistentDataPath to Logs/QA/saves-&lt;runId&gt;; returns that folder.</summary>
    public static string BackupSaves(string runId)
    {
        string dir = Path.Combine(OutDir, "saves-" + runId);
        Directory.CreateDirectory(dir);
        foreach (string f in Directory.GetFiles(Application.persistentDataPath)) File.Copy(f, Path.Combine(dir, Path.GetFileName(f)), true);
        File.WriteAllText(Path.Combine(dir, ".manifest"), string.Join("\n", Directory.GetFiles(Application.persistentDataPath).Select(Path.GetFileName)));
        return dir;
    }

    /// <summary>Puts the save files back as they were and removes slot files the run created.</summary>
    public static string RestoreSaves(string dir)
    {
        if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return "no backup to restore";
        var before = new HashSet<string>(File.Exists(Path.Combine(dir, ".manifest")) ? File.ReadAllLines(Path.Combine(dir, ".manifest")) : new string[0]);
        int removed = 0, restored = 0;
        foreach (string f in Directory.GetFiles(Application.persistentDataPath))
            if (!before.Contains(Path.GetFileName(f)) && Path.GetFileName(f).StartsWith("slot")) { File.Delete(f); removed++; }
        foreach (string f in Directory.GetFiles(dir))
        {
            if (Path.GetFileName(f) == ".manifest") continue;
            File.Copy(f, Path.Combine(Application.persistentDataPath, Path.GetFileName(f)), true);
            restored++;
        }
        return $"restored {restored} file(s), removed {removed} new slot file(s)";
    }

    // ---------------------------------------------------------------- structure report

    public static string StructureReport(string scenePath, string kind)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return "error: leave Play mode first";
        Scene already = SceneManager.GetSceneByPath(scenePath);
        bool wasOpen = already.IsValid() && already.isLoaded;
        Scene scene = wasOpen ? already : EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
        var comps = new Dictionary<string, int>();
        int missingScripts = 0, emptySprites = 0, objects = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                objects++;
                missingScripts += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
                foreach (Component c in t.GetComponents<Component>())
                {
                    if (c == null) continue;
                    string n = c.GetType().Name;
                    comps[n] = (comps.TryGetValue(n, out int k) ? k : 0) + 1;
                    if (c is SpriteRenderer sr && sr.enabled && sr.gameObject.activeInHierarchy && sr.sprite == null) emptySprites++;
                }
            }
        int Count(params string[] names) => names.Sum(n => comps.TryGetValue(n, out int k) ? k : 0);
        bool respawnPoint = false;
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (LevelManager lm in root.GetComponentsInChildren<LevelManager>(true)) respawnPoint |= lm.ReSpawnPos != null;

        var checks = new List<string>();
        void Check(string name, bool ok, string detail) => checks.Add("{\"name\":" + Q(name) + ",\"ok\":" + (ok ? "true" : "false") + ",\"detail\":" + Q(detail) + "}");
        Check("no missing scripts", missingScripts == 0, missingScripts + " missing script reference(s)");
        if (kind != "menu")
        {
            Check("a player", Count("PlayerController") > 0, Count("PlayerController") + " PlayerController");
        }
        if (kind == "act" || kind == "boss")
        {
            Check("a LevelManager with a respawn point", Count("LevelManager") > 0 && respawnPoint, Count("LevelManager") + " LevelManager, respawn point " + (respawnPoint ? "set" : "missing"));
        }
        if (kind == "act")
        {
            Check("at least one checkpoint", Count("CheckPoint") > 0, Count("CheckPoint") + " CheckPoint");
            Check("a level exit", Count("NextLevel", "LevelComplete") > 0, Count("NextLevel") + " NextLevel, " + Count("LevelComplete") + " LevelComplete");
        }
        if (kind == "boss") Check("a boss", Count("YokaiBoss", "SamuraiBoss", "VoidShrineBoss", "LandsharkBoss", "SandfinMiniBoss") > 0, Count("YokaiBoss") + " YokaiBoss, " + Count("SamuraiBoss") + " SamuraiBoss, " + Count("VoidShrineBoss") + " VoidShrineBoss, " + Count("LandsharkBoss") + " LandsharkBoss, " + Count("SandfinMiniBoss") + " SandfinMiniBoss"); // LANDSHARK: // SANDFIN:
        if (kind == "village")
        {
            Check("villagers", Count("Villager") > 0, Count("Villager") + " Villager");
            Check("a way out", Count("TownExit", "NextLevel") > 0, Count("TownExit") + " TownExit, " + Count("NextLevel") + " NextLevel");
        }
        if (kind == "tutorial") Check("tutorial guide and goals", Count("TutorialGuide") > 0 && Count("TutorialGoal") > 0, Count("TutorialGuide") + " TutorialGuide, " + Count("TutorialGoal") + " TutorialGoal");

        string notes = emptySprites > 0 ? $"{emptySprites} enabled SpriteRenderer(s) with no sprite (not a failure on its own)" : "";
        bool allOk = checks.All(c => c.Contains("\"ok\":true"));
        string json = "{\"tool\":\"QAStructure\",\"version\":1,\"scene\":" + Q(scene.name) + ",\"scenePath\":" + Q(scenePath) + ",\"kind\":" + Q(kind) +
            ",\"capturedAt\":" + Q(DateTime.UtcNow.ToString("o")) + ",\"objects\":" + objects.ToString(CultureInfo.InvariantCulture) +
            ",\"ok\":" + (allOk ? "true" : "false") + ",\"checks\":[" + string.Join(",", checks) + "],\"notes\":" + Q(notes) + "}";
        if (!wasOpen) EditorSceneManager.CloseScene(scene, true);
        Directory.CreateDirectory(OutDir);
        string output = Path.Combine(OutDir, "struct-" + Path.GetFileNameWithoutExtension(scenePath) + ".json"); // not scene.name: it's empty once the scene is closed
        File.WriteAllText(output, json);
        return output;
    }

    private static string Q(string s)
    {
        if (s == null) return "null";
        var sb = new StringBuilder("\"");
        foreach (char c in s)
        {
            if (c == '"' || c == '\\') sb.Append('\\').Append(c);
            else if (c == '\n') sb.Append("\\n");
            else if (c < 32) sb.Append(' ');
            else sb.Append(c);
        }
        return sb.Append('"').ToString();
    }
}
