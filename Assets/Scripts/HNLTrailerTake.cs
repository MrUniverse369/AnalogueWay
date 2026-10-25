#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

/// <summary>
/// TRAILER (2026-10-25): records a raw gameplay take in Play mode as 1920x1080 PNG frames at a fixed 60 fps of game
/// time (Time.captureFramerate) plus a WAV of the game's sound effects, for the 60-second trailer. Unlike HNLDemoBot
/// the HUD is in the frames: every overlay canvas is drawn by the main camera for the take (camera space, on top),
/// and the camera has the pixel-perfect camera's 1080p size (6x zoom), so frames look like the game at 1080p. The
/// level music is muted for the take (the trailer has one soundtrack under the effects).
/// Harvey is driven through the game's own controls: PlayerController.BotX/BotY (the stick), Jump() (a jump button
/// press) and Talisman(slot) (a talisman button press). A take's choreography is the Brain callback, called every
/// frame with the take's time; it can come from an MCP RunCommand. The player loop is pumped while Unity is in the
/// background. Saves go to memory while a take runs. Editor only.
/// Usage: HNLTrailerTake.Begin("/path/take01", t => { ... }, 9f);
/// </summary>
[DefaultExecutionOrder(32000)] // capture after everything else has moved
public class HNLTrailerTake : MonoBehaviour
{
    private sealed class MemStore : ISaveStorage
    {
        private readonly Dictionary<string, string> d = new Dictionary<string, string>();
        public bool Exists(string s) => d.ContainsKey(s);
        public string Read(string s) => d.TryGetValue(s, out var t) ? t : null;
        public void Write(string s, string t) => d[s] = t;
        public void Delete(string s) => d.Remove(s);
    }

    public const int Width = 1920, Height = 1080, Fps = 60;
    private const BindingFlags Any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    public static HNLTrailerTake Current;
    public static System.Action<float> Brain;
    public static string LastLog = "";
    private static ISaveStorage realStorage;

    public float T => t;
    public int Frame => frame;
    public bool Done => done;
    public bool Recording = true; // false = the brain runs but no frames are kept (warm-up)

    private float t, duration;
    private bool done;
    private int frame;
    private string outDir;
    private Camera cam;
    private RenderTexture rt;
    private Texture2D readback;
    private readonly List<float> audio = new List<float>();
    private readonly List<(Canvas canvas, Camera camera, float distance, int layer, int order)> canvases = new List<(Canvas, Camera, float, int, int)>();
    private readonly HashSet<Canvas> converted = new HashSet<Canvas>();
    private UnityEngine.Rendering.Universal.PixelPerfectCamera ppc;
    private bool ppcWasOn;
    private AudioSource music;
    private bool musicWasMuted;
    private static int pending;
    private readonly System.Text.StringBuilder sounds = new System.Text.StringBuilder(); // sfx.txt: what the effects track is rebuilt from

    /// <summary>Starts a take: frames go to outDir/00000.png..., sound to outDir/audio.wav. Ends after duration s.</summary>
    public static string Begin(string outDir, System.Action<float> brain, float duration, bool record = true)
    {
        PlayerController pc = FindFirstObjectByType<PlayerController>();
        if (pc == null) return "no player";
        UseMemorySaves();
        foreach (HNLTrailerTake old in FindObjectsByType<HNLTrailerTake>(FindObjectsSortMode.None)) { old.Finish(); Destroy(old); }
        var take = pc.gameObject.AddComponent<HNLTrailerTake>();
        take.outDir = outDir;
        take.duration = duration;
        take.Recording = record;
        Time.captureFramerate = Fps; // from the very next frame (Start would leave one frame of wall-clock time)
        Brain = brain;
        Current = take;
        LastLog = "";
        return "take started in " + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
    }

    public static void UseMemorySaves()
    {
        if (realStorage == null) { realStorage = SaveSystem.Storage; SaveSystem.Storage = new MemStore(); }
    }

    // ------------------------------------------------------------------ controls

    public static PlayerController Player => FindFirstObjectByType<PlayerController>();

    /// <summary>One press of the jump button (on the next frame).</summary>
    public static void Jump()
    {
        PlayerController pc = Player;
        if (pc != null) pc.SendMessage("QueueJumpPress");
    }

    /// <summary>One press of a talisman slot's button (0..2).</summary>
    public static void Talisman(int slot)
    {
        PowerUpManager m = PowerUpManager.Instance;
        if (m == null || slot >= m.SlotCount || m.TalismanIn(slot) == null) return;
        if (!(FieldOf(typeof(PowerUpManager), "slots")?.GetValue(m) is System.Array slots) || slot >= slots.Length) return;
        object s = slots.GetValue(slot);
        FieldOf(s.GetType(), "pressQueued")?.SetValue(s, true);
    }

    public static bool TalismanReady(int slot)
    {
        PowerUpManager m = PowerUpManager.Instance;
        return m != null && slot < m.SlotCount && m.TalismanIn(slot) != null && !m.IsSlotActive(slot) && m.CooldownLeft(slot) <= 0f;
    }

    public static bool TalismanActive(int slot)
    {
        PowerUpManager m = PowerUpManager.Instance;
        return m != null && slot < m.SlotCount && m.IsSlotActive(slot);
    }

    /// <summary>A private field or property of any object (for brains, which can't use reflection).</summary>
    public static object Get(object o, string name)
    {
        if (o == null) return null;
        FieldInfo f = FieldOf(o.GetType(), name);
        if (f != null) return f.GetValue(o);
        for (System.Type ty = o.GetType(); ty != null; ty = ty.BaseType)
        {
            PropertyInfo p = ty.GetProperty(name, Any | BindingFlags.DeclaredOnly);
            if (p != null) return p.GetValue(o);
        }
        return null;
    }

    public static void Set(object o, string name, object value) => FieldOf(o.GetType(), name)?.SetValue(o, value);

    public static object GetStatic(System.Type type, string name)
    {
        for (System.Type ty = type; ty != null; ty = ty.BaseType)
        {
            FieldInfo f = ty.GetField(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if (f != null) return f.GetValue(null);
            PropertyInfo p = ty.GetProperty(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if (p != null) return p.GetValue(null);
        }
        return null;
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

    public static void Log(string s)
    {
        LastLog += s + "\n";
        if (Current != null && !string.IsNullOrEmpty(Current.outDir))
            try { File.AppendAllText(Current.outDir + "/log.txt", s + "\n"); } catch { }
    }

    // ------------------------------------------------------------------ take

    private void Start()
    {
        Directory.CreateDirectory(outDir);
        foreach (string f in Directory.GetFiles(outDir)) File.Delete(f);
        cam = Camera.main;
        rt = new RenderTexture(Width, Height, 24);
        readback = new Texture2D(Width, Height, TextureFormat.RGB24, false);
        Time.captureFramerate = Fps;
        AudioRenderer.Start();
        AudioManager.OneShotPlayed += OnOneShot;
        EditorApplication.update += Pump;
        Transform m = AudioManager.Instance != null ? AudioManager.Instance.transform.Find("Music") : null;
        music = m != null ? m.GetComponent<AudioSource>() : null;
        if (music != null) { musicWasMuted = music.mute; music.mute = true; }
    }

    private static void Pump()
    {
        if (!EditorApplication.isPlaying) { EditorApplication.update -= Pump; return; }
        if (EditorApplication.isPaused) EditorApplication.isPaused = false; // Error Pause from an unrelated log
        EditorApplication.QueuePlayerLoopUpdate();
    }

    private void Update()
    {
        if (done) return;
        t += Time.deltaTime;
        if (music != null) music.mute = true; // a level track that restarts stays quiet too
        try { Brain?.Invoke(t); }
        catch (System.Exception e) { Log("brain error: " + e); Brain = null; }
        if (t >= duration) Finish();
    }

    private void LateUpdate()
    {
        if (done) return;
        if (cam == null) cam = Camera.main;
        if (cam == null) return;
        if (Recording) LogPlayingSources();
        PrepareCamera();
        ConvertCanvases();
        RenderTexture prev = cam.targetTexture;
        cam.targetTexture = rt;
        Canvas.ForceUpdateCanvases();
        cam.Render();
        cam.targetTexture = prev;
        int n = AudioRenderer.GetSampleCountForCaptureFrame() * Channels;
        if (n > 0)
        {
            var buf = new NativeArray<float>(n, Allocator.Temp);
            AudioRenderer.Render(buf);
            if (Recording) audio.AddRange(buf);
            buf.Dispose();
        }
        if (!Recording) return;
        RenderTexture.active = rt;
        readback.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
        RenderTexture.active = null;
        byte[] raw = readback.GetRawTextureData<byte>().ToArray();
        string path = Path.Combine(outDir, (frame++).ToString("00000") + ".png");
        while (Volatile.Read(ref pending) > 12) Thread.Sleep(2);
        Interlocked.Increment(ref pending);
        ThreadPool.QueueUserWorkItem(_ =>
        {
            try { File.WriteAllBytes(path, ImageConversion.EncodeArrayToPNG(raw, GraphicsFormat.R8G8B8_UNorm, Width, Height)); }
            finally { Interlocked.Decrement(ref pending); }
        });
    }

    // The game's sound effects, logged against the frame they start in, because the AudioRenderer records nothing
    // while Unity is in the background. "O frame volume pitch path" is a one-shot from AudioManager ("X": PlaySfx, cut
    // off by the next X); "P frame volume pitch time path" is another source playing a clip this frame (a looping
    // boulder roll...). With the audio clock stopped, a source's time stays 0, so P lines mark runs, not positions.
    // The level music is left out (the trailer has its own soundtrack).
    private void OnOneShot(AudioClip clip, float volume, float pitch, bool exclusive)
    {
        if (!Recording || clip == null) return;
        sounds.Append(exclusive ? "X\t" : "O\t").Append(frame).Append('\t').Append(volume.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture))
            .Append('\t').Append(pitch.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture)).Append('\t').Append(AssetDatabase.GetAssetPath(clip)).Append('\n');
    }

    private void LogPlayingSources()
    {
        foreach (AudioSource s in FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
        {
            if (!s.isPlaying || s.clip == null || s.mute || s.GetComponentInParent<AudioManager>() != null) continue; // AudioManager's are events
            string path = AssetDatabase.GetAssetPath(s.clip);
            if (path.StartsWith("Assets/Music/")) continue;
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            sounds.Append("P\t").Append(frame).Append('\t').Append(s.volume.ToString("0.####", ci)).Append('\t').Append(s.pitch.ToString("0.####", ci))
                .Append('\t').Append(s.time.ToString("0.#####", ci)).Append('\t').Append(path).Append('\n');
        }
    }

    // the pixel-perfect camera's size for a 1920x1080 screen (the Game view may be any size)
    private void PrepareCamera()
    {
        var p = cam.GetComponent<UnityEngine.Rendering.Universal.PixelPerfectCamera>();
        if (p != ppc)
        {
            if (ppc != null && ppcWasOn) ppc.enabled = true;
            ppc = p;
            ppcWasOn = p != null && p.enabled;
        }
        if (ppc == null || !ppcWasOn) return;
        int zoom = Mathf.Max(1, Mathf.Min(Width / ppc.refResolutionX, Height / ppc.refResolutionY));
        ppc.enabled = false;
        cam.orthographicSize = Height / (2f * ppc.assetsPPU * zoom);
    }

    private void ConvertCanvases()
    {
        SortingLayer[] layers = SortingLayer.layers;
        int top = layers[layers.Length - 1].id;
        bool any = false;
        foreach (Canvas c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if (!c.isRootCanvas || c.renderMode != RenderMode.ScreenSpaceOverlay || converted.Contains(c)) continue;
            canvases.Add((c, c.worldCamera, c.planeDistance, c.sortingLayerID, c.sortingOrder));
            converted.Add(c);
            c.renderMode = RenderMode.ScreenSpaceCamera;
            c.worldCamera = cam;
            c.planeDistance = 1f;
            c.sortingLayerID = top;
            c.sortingOrder += 30000;
            any = true;
        }
        if (!any) return;
        Canvas.ForceUpdateCanvases();
        foreach (TMPro.TMP_Text tx in FindObjectsByType<TMPro.TMP_Text>(FindObjectsSortMode.None)) tx.ForceMeshUpdate(true, true);
    }

    private static int Channels => AudioSettings.speakerMode == AudioSpeakerMode.Mono ? 1 : 2;

    public void Finish()
    {
        if (done) return;
        done = true;
        Brain = null;
        PlayerController.BotX = float.NaN;
        PlayerController.BotY = float.NaN;
        AudioRenderer.Stop();
        Time.captureFramerate = 0;
        EditorApplication.update -= Pump;
        while (Volatile.Read(ref pending) > 0) Thread.Sleep(5);
        if (audio.Count > 0) WriteWav(Path.Combine(outDir, "audio.wav"));
        AudioManager.OneShotPlayed -= OnOneShot;
        File.WriteAllText(Path.Combine(outDir, "sfx.txt"), sounds.ToString());
        foreach (var (canvas, camera, distance, layer, order) in canvases)
        {
            if (canvas == null) continue;
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.worldCamera = camera;
            canvas.planeDistance = distance;
            canvas.sortingLayerID = layer;
            canvas.sortingOrder = order;
        }
        canvases.Clear();
        converted.Clear();
        if (ppc != null && ppcWasOn) ppc.enabled = true;
        if (music != null) music.mute = musicWasMuted;
        if (rt != null) rt.Release();
        string msg = "[HNLTrailerTake] done: " + frame + " frames, " + t.ToString("0.00") + " s";
        Log(msg);
        File.WriteAllText(Path.Combine(outDir, "done.txt"), frame + "\n");
        if (Current == this) Current = null;
    }

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

    private void OnDestroy() { if (!done) Finish(); } // the level reloaded or Play mode ended

    /// <summary>Capture sessions only (SessionState "HNLTrailer.MemSaves", set by the capture tools): saves go to
    /// memory from the first scene on, before anything in the level can save. Normal Play sessions are untouched.</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void MemorySavesForCaptureSessions()
    {
        if (SessionState.GetBool("HNLTrailer.MemSaves", false)) UseMemorySaves();
    }

    [InitializeOnLoadMethod]
    private static void RestoreSavesOnExit()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            // not at ExitingPlayMode: the game still saves while the level tears down, and that must stay in memory
            if (state != PlayModeStateChange.EnteredEditMode) return;
            PlayerController.BotX = float.NaN;
            PlayerController.BotY = float.NaN;
            if (realStorage != null) { SaveSystem.Storage = realStorage; realStorage = null; }
        };
    }
}
#endif
