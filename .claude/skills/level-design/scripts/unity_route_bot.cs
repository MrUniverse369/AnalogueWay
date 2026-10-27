// Unity MCP RunCommand template: plays a route in Play mode with Harvey's real controller, as the 26 October
// Greybox_GrassyGreen_Level3 runs did. Do not paste it by hand: make_unity_bot.py fills in the route and the enemies switch
// from a route file, then send the output as Unity_RunCommand's code while the level is in Play mode.
// It swaps the save store for an in-memory one first (never touches the slot files), caps the frame rate (60) and
// fixes the step (captureFramerate 50), lifts Error Pause, and logs LDBOT DONE / DEATH / STUCK to the console
// (read it with Unity_GetConsoleLogs). Top-level internal types only: the RunCommand rewriter breaks nested private
// types. Stop: exit Play mode; the driver unhooks itself.
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

internal sealed class LdMem : ISaveStorage
{
    private readonly Dictionary<string, string> d = new Dictionary<string, string>();
    public bool Exists(string s) => d.ContainsKey(s);
    public string Read(string s) => d.TryGetValue(s, out var t) ? t : null;
    public void Write(string s, string t) => d[s] = t;
    public void Delete(string s) => d.Remove(s);
}

internal struct LdStep { public string k; public int d; public float v; public string until; }

internal class CommandScript : IRunCommand
{
    private static LdStep S(string k, int d, float v, string until = null) => new LdStep { k = k, d = d, v = v, until = until };
    private static LdStep[] route;
    private static int i, pressTries, lastOops;
    private static float stepStart, t0;
    private static bool pressed, air, fell;
    private static PlayerController pc;
    private static Rigidbody2D rb;

    public void Execute(ExecutionResult result)
    {
        if (!EditorApplication.isPlaying) { result.LogError("not playing yet"); return; }
        SaveSystem.Storage = new LdMem();
        EditorApplication.update -= Unpause; EditorApplication.update += Unpause;
        pc = Object.FindFirstObjectByType<PlayerController>();
        rb = pc.GetComponent<Rigidbody2D>();
        var e = GameObject.Find("Enemies"); if (e != null) e.SetActive(/*ENEMIES*/);
        Application.targetFrameRate = 60;
        Time.captureFramerate = 50;
        route = new[]
        {
/*ROUTE*/
        };
        i = 0; pressed = air = fell = false; t0 = Time.time; stepStart = Time.time; lastOops = HurtPlayer.oopsCount;
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
        Debug.Log("LDBOT START");
        result.Log("route bot started");
    }

    private static void Unpause()
    {
        if (!EditorApplication.isPlaying) { EditorApplication.update -= Unpause; return; }
        if (EditorApplication.isPaused) { EditorApplication.isPaused = false; Debug.Log("LDBOT note: error pause lifted"); }
    }

    private static void Log(string s) => Debug.Log("LDBOT " + s);
    private static void Stop(string why) { PlayerController.BotX = float.NaN; Time.captureFramerate = 0; EditorApplication.update -= Tick; Log(why); }

    private static void Tick()
    {
        try { Advance(); }
        catch (System.Exception ex) { Stop("EXCEPTION " + ex.Message); }
    }

    private static void Advance()
    {
        if (!EditorApplication.isPlaying || pc == null) { EditorApplication.update -= Tick; PlayerController.BotX = float.NaN; return; }
        if (EditorApplication.isPaused) return;
        Vector2 p = pc.transform.position;
        float vy = rb.linearVelocity.y;
        bool g = pc.GetIsGrounded;
        if (HurtPlayer.oopsCount != lastOops) { Stop($"DEATH at step {i} pos=({p.x:0.00},{p.y:0.00}) t={Time.time - t0:0.00}"); return; }
        if (i >= route.Length) { Stop($"DONE t={Time.time - t0:0.00} pos=({p.x:0.00},{p.y:0.00})"); return; }
        if (Time.time - stepStart > 12f) { Stop($"STUCK at step {i} pos=({p.x:0.00},{p.y:0.00})"); return; }
        LdStep st = route[i];
        int move = st.d;
        bool done = false;
        switch (st.k)
        {
            case "go": done = st.d > 0 ? p.x >= st.v : p.x <= st.v; break;
            case "jump":
                if (!pressed) { bool crossed = st.d > 0 ? p.x >= st.v : p.x <= st.v; if (g && crossed) { pc.SendMessage("QueueJumpPress"); pressed = true; pressTries = 0; } }
                else if (vy > 0.5f || !g) done = true;
                else if (++pressTries > 6) pressed = false;
                break;
            case "air": done = st.d > 0 ? p.x >= st.v : p.x <= st.v; break;
            case "until_y": done = p.y >= st.v; break;
            case "until_vy": done = vy >= st.v; break;
            case "land": if (!g) air = true; done = g && vy <= 0.01f && (air || Time.time - stepStart > 0.1f); break;
            case "wait": done = Time.time - stepStart >= st.v; break;
            case "aim":
                if (vy < 0f) fell = true;
                if (st.until == "launch") done = fell && vy > 6f; else { if (!g) air = true; done = air && g && vy <= 0.01f; }
                float dx = st.v - p.x; move = Mathf.Abs(dx) < 0.15f ? 0 : (dx > 0 ? 1 : -1);
                break;
        }
        if (done) { i++; stepStart = Time.time; pressed = air = fell = false; return; }
        PlayerController.BotX = move;
    }
}
