#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// QA (2026-10-29): chains QARunner play captures over a list of scenes without anyone driving each step.
/// Each item is one Play session: QAProbe samples the scene (errors, frame times) and then runs the item's action
/// ("respawn", "boss:...", "bounce:...", "arena:..." or none). When bot > 0 a scripted run also drives Harvey during
/// the sample: he runs right, jumps walls, gaps and enemies, Spirit Dashes enemies ahead (slot 0), Super Jumps when
/// stuck (slot 1), tries the dragon and the fox where the level allows them, and is moved on to each checkpoint in
/// turn so the whole level is visited. At action moments it saves a clean 1920x1080 still (HUD hidden) through
/// HNLTrailerTake. Everything goes to Logs/QA/&lt;prefix&gt;/&lt;scene&gt;[-n]/ (log.txt, 00000.png...).
/// Usage: QAPlaytestBatch.Start("pt29", new[]{"Assets/Scenes/Z1A1Tomb.unity|respawn|30", ...}); Status(); Stop().
/// Editor only.
/// </summary>
[InitializeOnLoad]
public static class QAPlaytestBatch
{
    [Serializable]
    private class State
    {
        public string prefix;
        public string[] items; // "scenePath|action|botSeconds"
        public int index;
        public bool waiting, botStarted, done;
        public string current;
        public List<string> log = new List<string>();
    }

    private const string Key = "HNL.QA.Batch";
    private static int coolFrames;

    static QAPlaytestBatch()
    {
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    private static State Load()
    {
        string j = SessionState.GetString(Key, "");
        return string.IsNullOrEmpty(j) ? null : JsonUtility.FromJson<State>(j);
    }

    private static void Save(State s) => SessionState.SetString(Key, JsonUtility.ToJson(s));

    public static string Start(string prefix, string[] items, bool compose = false)
    {
        SessionState.SetBool("HNL.QA.Batch.Compose", compose);
        if (EditorApplication.isPlayingOrWillChangePlaymode) return "error: leave Play mode first";
        var s = new State { prefix = prefix, items = items, index = 0 };
        Directory.CreateDirectory(Path.Combine(QARunner.OutDir, prefix));
        Save(s);
        coolFrames = 0;
        return "batch of " + items.Length + " started";
    }

    public static void Stop()
    {
        SessionState.EraseString(Key);
    }

    public static string Status()
    {
        State s = Load();
        if (s == null) return "no batch";
        return (s.done ? "DONE " : "RUNNING ") + s.index + "/" + s.items.Length + " current=" + s.current + " waiting=" + s.waiting + " bot=" + s.botStarted + " playing=" + EditorApplication.isPlaying + "\n" + string.Join("\n", s.log);
    }

    private static string RunId(State s, int i)
    {
        string[] p = s.items[i].Split('|');
        string name = Path.GetFileNameWithoutExtension(p[0]);
        int dup = 0;
        for (int k = 0; k < i; k++) if (s.items[k].Split('|')[0] == p[0]) dup++;
        return name + (dup > 0 ? "-" + (dup + 1) : "");
    }

    private static void Tick()
    {
        State s = Load();
        if (s == null || s.done) return;
        if (EditorApplication.isPlaying)
        {
            if (EditorApplication.isPaused) EditorApplication.isPaused = false; // Error Pause from an unrelated log must not stall the batch
            if (!s.botStarted && !EditorApplication.isPaused)
            {
                string[] p = s.items[s.index].Split('|');
                float bot = p.Length > 2 ? float.Parse(p[2], System.Globalization.CultureInfo.InvariantCulture) : 0f;
                string act = p.Length > 1 ? p[1] : "";
                if (act.StartsWith("tt:") || act.StartsWith("chest:")) // a QATimeTrailDriver / QAChestDriver mode (bot = how long the session is given)
                {
                    if (UnityEngine.Object.FindFirstObjectByType<PlayerController>() == null || Time.timeSinceLevelLoad < 1.2f) return;
                    HNLTrailerTake.UseMemorySaves();
                    string logFile = Path.Combine(QARunner.OutDir, s.prefix, RunId(s, s.index) + ".log");
                    if (act.StartsWith("tt:")) QATimeTrailDriver.Run(act.Substring(3), logFile); else QAChestDriver.Run(act.Substring(6), logFile);
                    s.botStarted = true;
                    Save(s);
                    return;
                }
                if (bot <= 0f) { s.botStarted = true; Save(s); return; }
                PlayerController pc = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
                if (pc == null || Time.timeSinceLevelLoad < 1.2f) return;
                HNLDemoBot.PrepareShowcase();
                Bot.Compose = SessionState.GetBool("HNL.QA.Batch.Compose", false);
                Bot.Reset(bot);
                string dir = Path.Combine(QARunner.OutDir, s.prefix, RunId(s, s.index));
                HNLTrailerTake.Begin(dir, Bot.Tick, bot, false);
                s.botStarted = true;
                Save(s);
            }
            return;
        }
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (++coolFrames < 30) return; // let QARunner put the scenes back first
        coolFrames = 0;

        if (s.waiting)
        {
            string json = Path.Combine(QARunner.OutDir, s.prefix + "-" + RunId(s, s.index) + ".json");
            if (!File.Exists(json)) return;
            s.log.Add(RunId(s, s.index) + " done");
            s.index++;
            s.waiting = false;
            s.botStarted = false;
        }
        if (s.index >= s.items.Length) { s.done = true; s.current = ""; Save(s); return; }
        string[] it = s.items[s.index].Split('|');
        string action = it.Length > 1 ? it[1] : "";
        float botS = it.Length > 2 ? float.Parse(it[2], System.Globalization.CultureInfo.InvariantCulture) : 0f;
        float sample = Mathf.Max(6f, botS * 0.6f + 8f);
        s.current = RunId(s, s.index);
        string probeAction = action.StartsWith("tt:") || action.StartsWith("chest:") ? "" : action; // a driver mode runs from Tick, not QAProbe
        string r = QARunner.StartPlayCapture(it[0], s.prefix + "-" + s.current, "batch" + (botS > 0 ? "+bot" : ""), 1.5f, sample, probeAction);
        if (r.StartsWith("error"))
        {
            s.log.Add(s.current + " NOT STARTED: " + r);
            if (r.Contains("unsaved")) { s.done = true; Save(s); return; }
            s.index++;
            Save(s);
            return;
        }
        s.waiting = true;
        s.botStarted = false;
        Save(s);
    }

    // ------------------------------------------------------------------ the bot

    private static class Bot
    {
        private static float last, maxX, stuck, jumpCd, backT, lastSnap, logT, segStart, rideT, pressCd0, pressCd1;
        private static int snaps, seg, startOops, startLives, deaths;
        private static bool hudHidden, triedDragon, triedFox, riding;
        private static List<Vector3> stops;
        private static readonly List<Canvas> hidden = new List<Canvas>();
        private static Vector2 lastPos;
        private static string scene;
        private static CameraController pausedCam;
        public static bool Compose; // photo pass: centre the camera on Harvey for the still's frame, action moments only
        private const int MaxSnaps = 10;

        private static float total = 30f;

        public static void Reset(float duration = -1f)
        {
            if (duration > 0f) total = duration;
            last = 0; maxX = -1e9f; stuck = 0; jumpCd = 0; backT = 0; lastSnap = -99; logT = 0; segStart = 0; rideT = 0; pressCd0 = 0; pressCd1 = 0;
            snaps = 0; seg = 0; deaths = 0; hudHidden = false; triedDragon = false; triedFox = false; riding = false;
            startOops = HurtPlayer.oopsCount; startLives = HurtPlayer.pLivesCount;
            scene = SceneManager.GetActiveScene().name;
            stops = UnityEngine.Object.FindObjectsByType<CheckPoint>(FindObjectsSortMode.None).Select(c => c.transform.position).OrderBy(p => p.x).ToList();
            lastPos = Vector2.zero;
        }

        private static bool Ground(Vector2 o, Vector2 d, float len) => Physics2D.Raycast(o, d, len, LayerMask.GetMask("Ground")).collider != null;

        private static void ShowHud()
        {
            foreach (Canvas c in hidden) if (c != null) c.enabled = true;
            hidden.Clear();
            hudHidden = false;
            if (pausedCam != null) { pausedCam.enabled = true; pausedCam = null; }
        }

        private static void Teleport(PlayerController pc, Vector3 to)
        {
            RaycastHit2D hit = Physics2D.Raycast(new Vector2(to.x, to.y + 1.5f), Vector2.down, 12f, LayerMask.GetMask("Ground"));
            Vector3 p = new Vector3(to.x, hit.collider != null ? hit.point.y + 1f : to.y + 0.5f, 0f);
            pc.transform.position = p;
            var body = pc.GetComponent<Rigidbody2D>();
            if (body != null) body.linearVelocity = Vector2.zero;
            CameraController cc = UnityEngine.Object.FindFirstObjectByType<CameraController>();
            if (cc != null && cc.isActiveAndEnabled) cc.SnapToPlayer();
            maxX = p.x; stuck = 0;
        }

        public static void Tick(float t)
        {
            HNLTrailerTake take = HNLTrailerTake.Current;
            if (take != null) take.Recording = false;
            if (hudHidden) ShowHud();
            PlayerController pc = HNLTrailerTake.Player;
            if (pc == null) return;
            float dt = Mathf.Max(0f, t - last); last = t;
            string now = SceneManager.GetActiveScene().name;
            if (now != scene) { HNLTrailerTake.Log($"t={t:F1} SCENE CHANGED {scene} -> {now} (level exit reached)"); scene = now; Reset(); return; }

            Vector2 p = pc.transform.position;
            if (lastPos != Vector2.zero && (p - lastPos).magnitude > 6f && pc.NoControl) { deaths++; HNLTrailerTake.Log($"t={t:F1} respawned (death {deaths}) from ({lastPos.x:F1},{lastPos.y:F1})"); }
            lastPos = p;
            var rb = pc.GetComponent<Rigidbody2D>();
            Vector2 v = rb != null ? rb.linearVelocity : Vector2.zero;
            bool grounded = pc.GetIsGrounded;
            if (p.x > maxX + 0.05f) { maxX = p.x; stuck = 0; } else stuck += dt;
            jumpCd -= dt; pressCd0 -= dt; pressCd1 -= dt;

            // move on to the next checkpoint every segment, so the whole level gets visited
            float segLen = Mathf.Max(5f, total / Mathf.Max(1, stops.Count + 1));
            if (t - segStart > segLen && seg < stops.Count)
            {
                HNLTrailerTake.Log($"t={t:F1} segment {seg} ended at x={p.x:F1} (maxX {maxX:F1}); moving to checkpoint {seg} at {stops[seg]}");
                if (riding) { if (pc.IsMounted) pc.SummonDragon(false); if (pc.IsRidingFox) pc.RideFox(false); riding = false; }
                Teleport(pc, stops[seg]);
                seg++; segStart = t;
                return;
            }

            float dir = 1f;
            if (backT > 0) { backT -= dt; dir = -1f; }
            PlayerController.BotX = dir;

            // mounts: the dragon and the fox, where the level allows them
            if (!triedDragon && pc.CanSummonDragon && t > 3f) { triedDragon = true; riding = true; rideT = 0; pc.SummonDragon(true); HNLTrailerTake.Log($"t={t:F1} DRAGON summoned ok={pc.IsMounted}"); }
            else if (!triedFox && !riding && pc.CanRideFox && t > 12f) { triedFox = true; riding = true; rideT = 0; pc.RideFox(true); HNLTrailerTake.Log($"t={t:F1} FOX ride ok={pc.IsRidingFox}"); }
            if (riding) { rideT += dt; if (rideT > 4f) { if (pc.IsMounted) pc.SummonDragon(false); if (pc.IsRidingFox) pc.RideFox(false); riding = false; HNLTrailerTake.Log($"t={t:F1} mount off"); } }

            var enemies = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
                .Where(m => m != null && m.isActiveAndEnabled && (m is EnemyController || m is GhostEnemy || m is StompEnemy))
                .Select(m => (Vector2)m.transform.position).ToList();
            bool enemyAhead = enemies.Any(e => e.x > p.x && e.x - p.x < 5f && Mathf.Abs(e.y - p.y) < 2.5f);
            bool enemyNear = enemies.Any(e => Vector2.Distance(e, p) < 7f);

            bool wall = Ground(p + Vector2.up * 0.2f, Vector2.right * dir, 0.9f);
            bool gap = !Ground(p + new Vector2(1.3f * dir, 0.5f), Vector2.down, 7f);
            if (grounded && jumpCd <= 0 && (wall || gap || enemyAhead || (riding && t % 1.5f < dt))) { HNLTrailerTake.Jump(); jumpCd = 0.35f; }
            if (enemyAhead && pressCd0 <= 0 && HNLTrailerTake.TalismanReady(0)) { HNLTrailerTake.Talisman(0); pressCd0 = 1f; HNLTrailerTake.Log($"t={t:F1} dash pressed x={p.x:F1}"); }
            if (stuck > 1.2f && grounded && pressCd1 <= 0 && HNLTrailerTake.TalismanReady(1)) { HNLTrailerTake.Talisman(1); HNLTrailerTake.Jump(); jumpCd = 0.4f; pressCd1 = 1f; HNLTrailerTake.Log($"t={t:F1} superjump pressed x={p.x:F1}"); }
            if (stuck > 3f && backT <= 0) { backT = 0.5f; stuck = 1.5f; HNLTrailerTake.Log($"t={t:F1} stuck at ({p.x:F1},{p.y:F1})"); }

            bool dashing = HNLTrailerTake.TalismanActive(0), superJ = HNLTrailerTake.TalismanActive(1);
            bool action = dashing || superJ || riding || (enemyNear && !grounded) || v.y > 4f;
            if (take != null && snaps < MaxSnaps && !pc.NoControl && t - lastSnap > 2f && t - segStart > 0.6f && (action || (!Compose && t - lastSnap > 7f)))
            {
                foreach (Canvas c in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                    if (c.enabled && c.isRootCanvas) { c.enabled = false; hidden.Add(c); }
                hudHidden = true;
                if (Compose && Camera.main != null)
                {
                    CameraController cc = UnityEngine.Object.FindFirstObjectByType<CameraController>();
                    if (cc != null && cc.enabled) { cc.enabled = false; pausedCam = cc; }
                    float face = Mathf.Abs(v.x) > 0.5f ? Mathf.Sign(v.x) : 1f;
                    Transform ct = Camera.main.transform;
                    ct.position = new Vector3(p.x + 2.5f * face, p.y + 1.2f, ct.position.z);
                }
                take.Recording = true;
                HNLTrailerTake.Log($"t={t:F1} SNAP {snaps:00000} at ({p.x:F1},{p.y:F1}) dash={dashing} super={superJ} mount={riding} enemyNear={enemyNear} air={!grounded} vy={v.y:F1}");
                snaps++; lastSnap = t;
            }
            logT += dt;
            if (logT > 2f) { logT = 0; HNLTrailerTake.Log($"t={t:F1} pos=({p.x:F1},{p.y:F1}) maxX={maxX:F1} lives={HurtPlayer.pLivesCount} hits={HurtPlayer.oopsCount - startOops} noControl={pc.NoControl}"); }
        }
    }
}
#endif
