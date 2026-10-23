#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Unity.Collections;
using UnityEditor;
using UnityEngine;

/// <summary>
/// DEMO (2026-10-22): records raw gameplay takes in Play mode for demos and trailers. HNLDemoBot.Begin(...) puts
/// Harvey on the ground at a start point (and the camera on him), then runs him right at his normal speed: he jumps at
/// walls, gaps and enemies, and with useTalismans he Spirit Dashes into enemies ahead (talisman slot 0) and Super Jumps
/// up walls he can't clear (slot 1). The take is saved as JPG frames (outPath + "_frames/") plus a WAV of the game
/// audio (outPath + ".wav"), to be joined into a video with ffmpeg. Frames are captured in LateUpdate from the main
/// camera (no HUD), not at WaitForEndOfFrame or with the Recorder package: in the editor those only work while the Game
/// view is drawn, which stops when Unity is behind other windows. Time.captureFramerate fixes game time to 30 fps.
/// Saves are swapped for an in-memory store while the bot exists (set up by PrepareShowcase or Begin), so nothing a
/// take does (checkpoints, skills, talismans) touches the real save slots. During a take Harvey has the Healing
/// Stone's shield and enemy immunity (PowerUpManager traits), so a scripted run can't die to hazards it can't see. Wrapped in UNITY_EDITOR: not in builds.
/// </summary>
[DefaultExecutionOrder(1000)] // after PlayerController, so its input (none) doesn't overwrite the run
public class HNLDemoBot : MonoBehaviour
{
    private sealed class MemStore : ISaveStorage
    {
        private readonly Dictionary<string, string> d = new Dictionary<string, string>();
        public bool Exists(string s) => d.ContainsKey(s);
        public string Read(string s) => d.TryGetValue(s, out var t) ? t : null;
        public void Write(string s, string t) => d[s] = t;
        public void Delete(string s) => d.Remove(s);
    }

    public static string LastLog = "";
    private static ISaveStorage realStorage;

    private const int Fps = 30, Width = 1280, Height = 720;
    private const BindingFlags Any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private float duration, t, lastX, stuck, jumpCd, dashCd;
    private bool useTalismans, exitWhenDone, done, superJumpPending;
    private string outPath, frameDir;
    private PlayerController pc;
    private Rigidbody2D rb;
    private Animator anim;
    private int groundMask, maxStage, frame;
    private RenderTexture rt;
    private Texture2D readback;
    private Camera cam;
    private readonly List<float> audio = new List<float>();
    private FieldInfo speedField, jSpeedField;

    /// <summary>Swaps saves for memory (once per Play session) and gives Harvey the skills and talismans to show off.</summary>
    public static void PrepareShowcase()
    {
        if (realStorage == null) { realStorage = SaveSystem.Storage; SaveSystem.Storage = new MemStore(); }
        Inventory.SetSkillLevel(SkillTree.StompPower, 3);
        Inventory.SetSkillLevel(SkillTree.DashPower, 3);
        Inventory.SetSkillLevel(SkillTree.ComboMastery, 4);
        HNLCatalog cat = Inventory.Catalog;
        if (cat == null || cat.talismans == null) return;
        Talisman dash = null, super = null;
        foreach (Talisman tal in cat.talismans)
        {
            if (tal == null) continue;
            if (dash == null && (tal.Traits & TalismanTrait.DefeatsEnemiesOnContact) != 0) dash = tal;
            if (super == null && tal is SuperJumpTalisman) super = tal;
        }
        if (dash != null) { Inventory.Unlock(dash); Inventory.SetEquipped(0, dash); }
        if (super != null) { Inventory.Unlock(super); Inventory.SetEquipped(1, super); }
    }

    public static string Begin(Vector2 start, float duration, string outPathNoExtension, bool useTalismans = false, bool exitWhenDone = true)
    {
        PlayerController pc = FindFirstObjectByType<PlayerController>();
        if (pc == null) return "no player";
        if (realStorage == null) { realStorage = SaveSystem.Storage; SaveSystem.Storage = new MemStore(); }
        foreach (HNLDemoBot old in FindObjectsByType<HNLDemoBot>(FindObjectsSortMode.None)) Destroy(old);
        LastLog = "";

        // stand him on the ground under the start point
        Vector3 p = new Vector3(start.x, start.y, 0f);
        RaycastHit2D hit = Physics2D.Raycast(new Vector2(start.x, start.y + 1.5f), Vector2.down, 12f, LayerMask.GetMask("Ground"));
        if (hit.collider != null) p.y = hit.point.y + 1f;
        pc.transform.position = p;
        Rigidbody2D body = pc.GetComponent<Rigidbody2D>();
        if (body != null) body.linearVelocity = Vector2.zero;
        PlayerController.chi = 90f;
        CameraController camCtl = FindFirstObjectByType<CameraController>();
        if (camCtl != null && camCtl.isActiveAndEnabled) camCtl.SnapToPlayer();

        var bot = pc.gameObject.AddComponent<HNLDemoBot>();
        bot.pc = pc;
        bot.duration = duration;
        bot.outPath = outPathNoExtension;
        bot.useTalismans = useTalismans;
        bot.exitWhenDone = exitWhenDone;
        return "started at " + p + " in " + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
    }

    private static FieldInfo FieldOf(System.Type type, string name)
    {
        for (System.Type ty = type; ty != null; ty = ty.BaseType)
        {
            FieldInfo f = ty.GetField(name, Any | BindingFlags.DeclaredOnly);
            if (f != null) return f;
        }
        return null;
    }

    private float Speed => speedField != null ? (float)speedField.GetValue(pc) : 4.5f;
    private float JumpSpeed => jSpeedField != null ? Mathf.Max((float)jSpeedField.GetValue(pc), pc.GetJSpeedDefault) : pc.GetJSpeedDefault;

    // a talisman slot press, the same as its button (PowerUpManager reads pressQueued next Update)
    private static void Press(int slot)
    {
        PowerUpManager m = PowerUpManager.Instance;
        if (m == null) return;
        if (!(FieldOf(typeof(PowerUpManager), "slots")?.GetValue(m) is System.Array slots) || slot >= slots.Length) return;
        object s = slots.GetValue(slot);
        FieldOf(s.GetType(), "pressQueued")?.SetValue(s, true);
    }

    private static bool Ready(int slot)
    {
        PowerUpManager m = PowerUpManager.Instance;
        return m != null && slot < m.SlotCount && m.TalismanIn(slot) != null && !m.IsSlotActive(slot) && m.CooldownLeft(slot) <= 0f;
    }

    private void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        groundMask = LayerMask.GetMask("Ground");
        speedField = FieldOf(typeof(PlayerController), "speed");
        jSpeedField = FieldOf(typeof(PlayerController), "jSpeed");
        lastX = transform.position.x;
        frameDir = outPath + "_frames";
        if (Directory.Exists(frameDir)) foreach (string f in Directory.GetFiles(frameDir)) File.Delete(f);
        Directory.CreateDirectory(frameDir);
        cam = Camera.main;
        rt = new RenderTexture(Width, Height, 24);
        readback = new Texture2D(Width, Height, TextureFormat.RGB24, false);
        Time.captureFramerate = Fps;
        AudioRenderer.Start();
    }

    private void Update()
    {
        if (done) return;
        Shield(); // the take can't be lost to a hazard: Harvey has the Healing Stone's shield throughout
        // a level entered straight from the editor can be left frozen; unfreeze it unless a menu or the loader holds it
        if (Time.timeScale == 0f && !MenueControl.isGamePaused && !LevelLoader.Busy) Time.timeScale = 1f;
        t += Time.deltaTime;
        jumpCd -= Time.deltaTime;
        dashCd -= Time.deltaTime;
        bool grounded = Physics2D.Raycast(transform.position, Vector2.down, 1.6f, groundMask);
        PowerUpManager m = PowerUpManager.Instance;
        bool dashing = m != null && m.SlotCount > 0 && m.IsSlotActive(0) && Has(m, 0, TalismanTrait.DefeatsEnemiesOnContact);

        if (t > 0.5f) // a moment standing first
        {
            Vector2 v = rb.linearVelocity;
            if (!dashing) { v.x = Speed; rb.linearVelocity = v; } // the dash moves him itself
            Vector3 s = transform.localScale;
            s.x = Mathf.Abs(s.x);
            transform.localScale = s;
            if (anim != null) anim.SetFloat("Speed", 1f);

            Vector2 p = transform.position;
            bool wall = Physics2D.Raycast(p + Vector2.down * 0.3f, Vector2.right, 1.4f, groundMask);
            bool gap = !Physics2D.Raycast(p + Vector2.right * 1.6f, Vector2.down, 4f, groundMask);
            float nearest = float.MaxValue;
            foreach (GameObject e in GameObject.FindGameObjectsWithTag("Enemy")) // every kind of enemy is tagged Enemy
            {
                if (!e.activeInHierarchy) continue;
                float dx = e.transform.position.x - p.x;
                if (dx > 0.3f && Mathf.Abs(e.transform.position.y - p.y) < 2.5f) nearest = Mathf.Min(nearest, dx);
            }
            bool lowCeiling = Physics2D.Raycast(p, Vector2.up, 3f, groundMask); // jumping here would meet the ceiling (and its spikes)
            stuck = Mathf.Abs(p.x - lastX) < 0.005f && !dashing ? stuck + Time.deltaTime : 0f;

            if (superJumpPending && grounded)
            {
                superJumpPending = false;
                Jump();
            }
            else if (useTalismans && !dashing && dashCd <= 0f && nearest < 6f && Ready(0) && grounded)
            {
                Press(0); // Spirit Dash through the enemies ahead
                dashCd = 1.2f;
            }
            else if (useTalismans && grounded && stuck > 0.25f && Ready(1))
            {
                Press(1); // Super Jump: up the next frame, once the talisman has raised his jump
                superJumpPending = true;
                stuck = 0f;
            }
            else if (grounded && jumpCd <= 0f && !dashing && (wall || gap || (nearest < 3.2f && !lowCeiling) || stuck > 0.15f))
            {
                Jump();
            }
        }
        lastX = transform.position.x;
        if (t >= duration) Finish();
    }

    // runs after PowerUpManager each frame, so the shield is on for the next physics step's hazard checks too
    private static void Shield()
    {
        PowerUpManager m = PowerUpManager.Instance;
        FieldInfo f = m != null ? FieldOf(typeof(PowerUpManager), "activeTraits") : null;
        if (f != null) f.SetValue(m, (TalismanTrait)f.GetValue(m) | TalismanTrait.HealShield | TalismanTrait.EnemyImmunity);
    }

    private static bool Has(PowerUpManager m, int slot, TalismanTrait trait)
    {
        Talisman tal = m.TalismanIn(slot);
        return tal != null && (tal.Traits & trait) != 0;
    }

    private void Jump()
    {
        Vector2 v = rb.linearVelocity;
        v.y = JumpSpeed;
        rb.linearVelocity = v;
        jumpCd = 0.35f;
    }

    private void LateUpdate()
    {
        maxStage = Mathf.Max(maxStage, StompCombo.Stage);
        if (!done && rt != null) CaptureFrame();
    }

    private void CaptureFrame()
    {
        if (cam == null) cam = Camera.main;
        if (cam != null)
        {
            RenderTexture prev = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = prev;
            RenderTexture.active = rt;
            readback.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            readback.Apply(false);
            RenderTexture.active = null;
            File.WriteAllBytes(Path.Combine(frameDir, (frame++).ToString("00000") + ".jpg"), readback.EncodeToJPG(92));
        }
        int n = AudioRenderer.GetSampleCountForCaptureFrame() * Channels;
        if (n > 0)
        {
            var buf = new NativeArray<float>(n, Allocator.Temp);
            AudioRenderer.Render(buf);
            audio.AddRange(buf);
            buf.Dispose();
        }
    }

    private static int Channels => AudioSettings.speakerMode == AudioSpeakerMode.Mono ? 1 : 2;

    private void WriteWav(string path)
    {
        int rate = AudioSettings.outputSampleRate, ch = Channels;
        using (var w = new BinaryWriter(File.Create(path)))
        {
            int bytes = audio.Count * 2;
            w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); w.Write(36 + bytes);
            w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); w.Write(16); w.Write((short)1); w.Write((short)ch);
            w.Write(rate); w.Write(rate * ch * 2); w.Write((short)(ch * 2)); w.Write((short)16);
            w.Write(System.Text.Encoding.ASCII.GetBytes("data")); w.Write(bytes);
            foreach (float f in audio) w.Write((short)(Mathf.Clamp(f, -1f, 1f) * 32767f));
        }
    }

    private void EndCapture()
    {
        AudioRenderer.Stop();
        Time.captureFramerate = 0;
        if (audio.Count > 0) WriteWav(outPath + ".wav");
        if (rt != null) rt.Release();
    }

    private void Finish()
    {
        done = true;
        EndCapture();
        LastLog = "[HNLDemoBot] done at x=" + transform.position.x.ToString("0.0") + ", highest combo stage " + maxStage + ", " + frame + " frames";
        Debug.Log(LastLog);
        if (exitWhenDone) EditorApplication.ExitPlaymode();
    }

    private void OnDestroy() // the level reloaded or Play mode ended early
    {
        if (!done) { done = true; EndCapture(); }
    }

    [InitializeOnLoadMethod]
    private static void RestoreSavesOnExit()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.ExitingPlayMode && realStorage != null) { SaveSystem.Storage = realStorage; realStorage = null; }
        };
    }
}
#endif
