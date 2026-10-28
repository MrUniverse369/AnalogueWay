using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// QA (2026-10-27): the Playtest agent's play-mode capture. QARunner (editor) stores a request in SessionState and
/// enters Play mode; this hooks the log from the first moment of play (so errors in Awake are caught), waits for the
/// scene to settle, samples frame times plus render, memory and GC counters, optionally runs an action, writes
/// Logs/QA/&lt;runId&gt;.json and leaves Play mode. A measurement this platform doesn't provide is listed under "missing",
/// never filled in. It does nothing in a build or in Play mode started by hand.
///
/// Actions: "respawn" forces a death through LevelManager.Respawn and checks that Harvey comes back at the respawn
/// point with three lives and full chi, and gets control back within MaxRespawnS. "boss:&lt;suite&gt;" (2026-10-27) runs a
/// scripted boss suite (QABossDriver) and writes its checks; "bounce:&lt;suite&gt;" (2026-10-28) a bounce suite
/// (QABounceDriver: the Jump Mushroom and the pads' routes); "arena:all" (2026-10-28) the Challenge Arena's lifecycle
/// (QAArenaDriver: entry, reset on a fall, every round, the win, its ways out).
/// </summary>
public class QAProbe : MonoBehaviour
{
#if UNITY_EDITOR
    [Serializable]
    private class Request
    {
        public string runId, scenario, scenePath, action, output;
        public float settle = 2f, sample = 10f;
    }

    public const string RequestKey = "HNL.QA.ProbeRequest";
    private const float MaxRespawnS = 3.5f; // LevelManager: 3 s from death to control, plus a few frames
    private static Request req;
    private static readonly List<string> logErrors = new List<string>();
    private static Stopwatch loadWatch;
    private static double loadMs = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Hook()
    {
        req = null;
        string json = UnityEditor.SessionState.GetString(RequestKey, "");
        if (string.IsNullOrEmpty(json)) return;
        UnityEditor.SessionState.EraseString(RequestKey);
        req = JsonUtility.FromJson<Request>(json);
        lock (logErrors) logErrors.Clear();
        loadMs = -1;
        Application.logMessageReceivedThreaded -= OnLog;
        Application.logMessageReceivedThreaded += OnLog;
        loadWatch = Stopwatch.StartNew();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Spawn()
    {
        if (req == null) return;
        var go = new GameObject("QAProbe");
        DontDestroyOnLoad(go);
        go.AddComponent<QAProbe>();
    }

    private static void OnLog(string message, string stack, LogType type)
    {
        if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
        string first = stack == null ? "" : stack.Split('\n')[0];
        lock (logErrors) if (logErrors.Count < 50) logErrors.Add(type + "\u001f" + message + "\u001f" + first);
    }

    private readonly List<float> frames = new List<float>();
    private readonly Dictionary<string, double> sums = new Dictionary<string, double>();
    private readonly Dictionary<string, int> counts = new Dictionary<string, int>();
    private readonly List<string> missing = new List<string>();

    private IEnumerator Start()
    {
        yield return null; // the first frame of the scene
        if (loadWatch != null) loadMs = loadWatch.Elapsed.TotalMilliseconds;
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, req.settle));

        var rec = new Dictionary<string, ProfilerRecorder>
        {
            { "drawCalls", ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count") },
            { "batches", ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count") },
            { "setPass", ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count") },
            { "triangles", ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count") },
            { "gcAlloc", ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame") },
            { "mainNs", ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 15) },
        };
        var sysMem = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "System Used Memory");
        var gcUsed = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Used Memory");
        var gcReserved = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Reserved Memory");
        int gc0 = GC.CollectionCount(0);
        var timings = new FrameTiming[1];
        float until = Time.realtimeSinceStartup + Mathf.Max(1f, req.sample);
        while (Time.realtimeSinceStartup < until)
        {
            yield return null;
            frames.Add(Time.unscaledDeltaTime * 1000f);
            foreach (var kv in rec) if (kv.Value.Valid && kv.Value.LastValue > 0) Add(kv.Key, kv.Value.LastValue);
            FrameTimingManager.CaptureFrameTimings();
            if (FrameTimingManager.GetLatestTimings(1, timings) > 0)
            {
                if (timings[0].cpuMainThreadFrameTime > 0) Add("cpuMain", timings[0].cpuMainThreadFrameTime);
                if (timings[0].cpuRenderThreadFrameTime > 0) Add("cpuRender", timings[0].cpuRenderThreadFrameTime);
                if (timings[0].gpuFrameTime > 0) Add("gpu", timings[0].gpuFrameTime);
            }
        }
        int gcRuns = GC.CollectionCount(0) - gc0;
        double sysMB = sysMem.Valid ? sysMem.LastValue / 1048576.0 : -1, usedMB = gcUsed.Valid ? gcUsed.LastValue / 1048576.0 : -1, resMB = gcReserved.Valid ? gcReserved.LastValue / 1048576.0 : -1;
        foreach (var r in rec.Values) r.Dispose();
        sysMem.Dispose(); gcUsed.Dispose(); gcReserved.Dispose();

        string actionJson = "null";
        if (req.action == "respawn") { var box = new string[1]; yield return Respawn(box); actionJson = box[0]; }
        else if (req.action != null && req.action.StartsWith("boss:")) { var box = new string[1]; yield return QABossDriver.Run(req.action.Substring(5), box); actionJson = box[0]; } // BOSSQA: scripted boss suites
        else if (req.action != null && req.action.StartsWith("bounce:")) { var box = new string[1]; yield return QABounceDriver.Run(req.action.Substring(7), box); actionJson = box[0]; } // JUMPMUSHROOM: scripted bounce suites
        else if (req.action != null && req.action.StartsWith("arena:")) { var box = new string[1]; yield return QAArenaDriver.Run(req.action.Substring(6), box); actionJson = box[0]; } // WAVEMUSHROOM: the Challenge Arena's lifecycle

        Write(gcRuns, sysMB, usedMB, resMB, actionJson);
        Application.logMessageReceivedThreaded -= OnLog;
        req = null;
        UnityEditor.EditorApplication.isPlaying = false;
    }

    private void Add(string key, double v)
    {
        sums[key] = (sums.TryGetValue(key, out double s) ? s : 0) + v;
        counts[key] = (counts.TryGetValue(key, out int c) ? c : 0) + 1;
    }

    private double Avg(string key) => counts.TryGetValue(key, out int c) && c > 0 ? sums[key] / c : -1;

    private IEnumerator Respawn(string[] result)
    {
        var lm = FindAnyObjectByType<LevelManager>();
        var pc = FindAnyObjectByType<PlayerController>();
        if (lm == null || pc == null || lm.ReSpawnPos == null)
        {
            result[0] = Obj(("name", Str("respawn")), ("ok", "false"), ("detail", Str(lm == null ? "no LevelManager" : pc == null ? "no PlayerController" : "LevelManager has no ReSpawnPos")));
            yield break;
        }
        Vector3 target = lm.ReSpawnPos.position;
        // the first solid surface under the respawn point: where Harvey should come to rest
        float groundY = float.NaN;
        foreach (RaycastHit2D h in Physics2D.RaycastAll(target, Vector2.down, 30f))
            if (!h.collider.isTrigger && h.collider.GetComponentInParent<PlayerController>() == null) { groundY = h.point.y; break; }
        HurtPlayer.pLivesCount = 1;
        PlayerController.chi = 10;
        float t0 = Time.realtimeSinceStartup;
        lm.Respawn();
        yield return null;
        bool held = true;
        Vector3 appear = Vector3.positiveInfinity;
        while (Time.realtimeSinceStartup - t0 < MaxRespawnS + 3f)
        {
            yield return null;
            if (pc.gameObject.activeInHierarchy && float.IsInfinity(appear.x)) appear = pc.transform.position;
            if (pc.gameObject.activeInHierarchy && !pc.RespawnHeld) { held = false; break; }
        }
        float back = Time.realtimeSinceStartup - t0;
        // where he goes from there: 15 samples over 1.5 s
        var trace = new StringBuilder("[");
        for (int i = 0; i < 15; i++)
        {
            yield return new WaitForSecondsRealtime(0.1f);
            trace.Append(i > 0 ? "," : "").Append($"[{Num(Time.realtimeSinceStartup - t0)},{Num(pc.transform.position.x)},{Num(pc.transform.position.y)},{(pc.GetIsGrounded ? 1 : 0)}]");
        }
        trace.Append(']');
        float appearDist = float.IsInfinity(appear.x) ? -1f : Vector2.Distance(appear, target);
        float restDx = Mathf.Abs(pc.transform.position.x - target.x);
        Collider2D body = null; // Harvey's solid collider sits on a child, not on the PlayerController object
        foreach (Collider2D c in pc.GetComponentsInChildren<Collider2D>()) if (c.enabled && !c.isTrigger) { body = c; break; }
        float feetY = body != null ? body.bounds.min.y : float.NaN;
        bool onGround = float.IsNaN(groundY) || float.IsNaN(feetY) || feetY >= groundY - 0.2f;
        bool placed = appearDist >= 0f && appearDist <= 0.25f;
        bool ok = !held && back <= MaxRespawnS && placed && restDx <= 1f && onGround && HurtPlayer.pLivesCount == 3 && PlayerController.chi >= 90f;
        string why = held ? "control never came back"
            : !placed ? $"reappeared {appearDist:0.00} units from the respawn point"
            : !onGround ? $"came to rest with his feet at y={feetY:0.00}, {groundY - feetY:0.00} below the ground under the respawn point (y={groundY:0.00})"
            : restDx > 1f ? $"drifted {restDx:0.00} units sideways after reappearing"
            : $"reappeared at the respawn point and came to rest on the ground below it";
        result[0] = Obj(("name", Str("respawn")), ("ok", ok ? "true" : "false"), ("controlBackS", Num(back)), ("appearDistance", Num(appearDist)),
            ("respawnPoint", $"[{Num(target.x)},{Num(target.y)}]"), ("groundY", float.IsNaN(groundY) ? "null" : Num(groundY)), ("feetY", float.IsNaN(feetY) ? "null" : Num(feetY)),
            ("restDx", Num(restDx)), ("trace", trace.ToString()),
            ("lives", HurtPlayer.pLivesCount.ToString(CultureInfo.InvariantCulture)), ("chi", Num(PlayerController.chi)), ("limitS", Num(MaxRespawnS)),
            ("detail", Str($"control back after {back:0.00}s; {why}")));
    }

    private void Write(int gcRuns, double sysMB, double usedMB, double resMB, string actionJson)
    {
        var sorted = new List<float>(frames);
        sorted.Sort();
        double avg = 0; foreach (float f in frames) avg += f;
        avg = frames.Count > 0 ? avg / frames.Count : -1;
        double Pct(double p) => sorted.Count == 0 ? -1 : sorted[Mathf.Clamp((int)Math.Ceiling(p * sorted.Count) - 1, 0, sorted.Count - 1)];
        int spikes = 0; foreach (float f in frames) if (f > 33.3f) spikes++;

        var m = new List<(string, string)>();
        void Metric(string key, double v) { if (v >= 0) m.Add((key, Num(v))); else missing.Add(key); }
        Metric("frames", frames.Count);
        Metric("durationS", req.sample);
        Metric("frameMsAvg", avg);
        Metric("fpsAvg", avg > 0 ? 1000.0 / avg : -1);
        Metric("frameMsP95", Pct(0.95));
        Metric("frameMsP99", Pct(0.99));
        Metric("frameMsMax", sorted.Count > 0 ? sorted[sorted.Count - 1] : -1);
        Metric("spikes", frames.Count > 0 ? spikes : -1);
        Metric("cpuMainMs", Avg("cpuMain") >= 0 ? Avg("cpuMain") : Avg("mainNs") >= 0 ? Avg("mainNs") / 1e6 : -1);
        Metric("cpuRenderMs", Avg("cpuRender"));
        Metric("gpuMs", Avg("gpu"));
        Metric("memSystemMB", sysMB);
        Metric("memGcUsedMB", usedMB);
        Metric("memGcReservedMB", resMB);
        Metric("gcAllocPerFrameKB", Avg("gcAlloc") >= 0 ? Avg("gcAlloc") / 1024.0 : counts.ContainsKey("gcAlloc") ? 0 : -1);
        Metric("gcCollections", gcRuns);
        Metric("drawCalls", Avg("drawCalls"));
        Metric("batches", Avg("batches"));
        Metric("setPass", Avg("setPass"));
        Metric("triangles", Avg("triangles"));
        Metric("loadMs", loadMs);

        var pc = FindAnyObjectByType<PlayerController>();
        string player = pc == null ? Obj(("found", "false"))
            : Obj(("found", pc.gameObject.activeInHierarchy ? "true" : "false"), ("position", $"[{Num(pc.transform.position.x)},{Num(pc.transform.position.y)}]"),
                  ("grounded", pc.GetIsGrounded ? "true" : "false"), ("noControl", pc.NoControl ? "true" : "false"));

        var errs = new StringBuilder("[");
        lock (logErrors)
            for (int i = 0; i < logErrors.Count; i++)
            {
                string[] p = logErrors[i].Split('\u001f');
                errs.Append(i > 0 ? "," : "").Append(Obj(("type", Str(p[0])), ("message", Str(p[1])), ("at", Str(p.Length > 2 ? p[2] : ""))));
            }
        errs.Append(']');

        var miss = new StringBuilder("[");
        for (int i = 0; i < missing.Count; i++) miss.Append(i > 0 ? "," : "").Append(Str(missing[i]));
        miss.Append(']');

        var metricsSb = new StringBuilder("{");
        for (int i = 0; i < m.Count; i++) metricsSb.Append(i > 0 ? "," : "").Append(Str(m[i].Item1)).Append(':').Append(m[i].Item2);
        metricsSb.Append('}');

        Scene scene = SceneManager.GetActiveScene();
        string env = Obj(("machine", Str($"{SystemInfo.deviceModel} / {SystemInfo.graphicsDeviceName}")), ("os", Str(SystemInfo.operatingSystem)),
            ("unity", Str(Application.unityVersion)), ("target", Str("editor-play")), ("buildConfig", Str("editor")),
            ("resolution", Str($"{Screen.width}x{Screen.height}")), ("quality", Str(QualitySettings.names[QualitySettings.GetQualityLevel()])),
            ("vsync", QualitySettings.vSyncCount.ToString(CultureInfo.InvariantCulture)), ("targetFps", Application.targetFrameRate.ToString(CultureInfo.InvariantCulture)));

        string json = Obj(("tool", Str("QAProbe")), ("version", "1"), ("runId", Str(req.runId)), ("scenario", Str(req.scenario)),
            ("scene", Str(scene.name)), ("scenePath", Str(scene.path)), ("requestedScene", Str(req.scenePath)), ("playStarted", "true"),
            ("capturedAt", Str(DateTime.UtcNow.ToString("o"))), ("settleS", Num(req.settle)), ("durationS", Num(req.sample)),
            ("env", env), ("metrics", metricsSb.ToString()), ("missing", miss.ToString()), ("player", player), ("errors", errs.ToString()), ("action", actionJson));
        Directory.CreateDirectory(Path.GetDirectoryName(req.output));
        File.WriteAllText(req.output + ".tmp", json);
        if (File.Exists(req.output)) File.Delete(req.output);
        File.Move(req.output + ".tmp", req.output);
    }

    private static string Num(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);

    private static string Str(string s)
    {
        if (s == null) return "null";
        var sb = new StringBuilder("\"");
        foreach (char c in s)
        {
            if (c == '"' || c == '\\') sb.Append('\\').Append(c);
            else if (c == '\n') sb.Append("\\n");
            else if (c == '\r') sb.Append("\\r");
            else if (c == '\t') sb.Append("\\t");
            else if (c < 32) sb.Append("\\u").Append(((int)c).ToString("x4"));
            else sb.Append(c);
        }
        return sb.Append('"').ToString();
    }

    private static string Obj(params (string key, string json)[] fields)
    {
        var sb = new StringBuilder("{");
        for (int i = 0; i < fields.Length; i++) sb.Append(i > 0 ? "," : "").Append(Str(fields[i].key)).Append(':').Append(fields[i].json);
        return sb.Append('}').ToString();
    }
#endif
}
