using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// ENCOUNTERS (2026-10-29, visual pass): a landmark light over each optional Grassy Green encounter, so it reads from a
/// distance and goes dark once it's done. Added automatically when a Grassy Green level ("Z4..." scene) loads, to every
/// TimeTrail (at its start post, pink), ChestShuffleGame (over the middle chest, gold) and ChallengeArena (on its floor,
/// aqua); no scene holds one. Each is:
///   - a soft vertical light shaft: one procedural sprite (made once, shared by every landmark), its falloff an ordered
///     dither in whole art pixels so it stays crisp under the pixel-perfect camera, drawn with the shared additive
///     SpriteGlow material (Resources/FX); the colour breathes slowly (renderer colour only: no material instances)
///   - a few motes drifting up through it (PixelFX), only while it is on screen
/// When the encounter is finished for this level run (the trail spent, the chest game won or lost, the arena cleared)
/// the shaft fades out over a second and the motes stop. Nothing is allocated per frame.
/// </summary>
public class EncounterLandmark : MonoBehaviour
{
    private const int TexW = 12, TexH = 64; // art pixels: 0.75 x 4 tiles at 16 PPU
    private static Sprite shaft;
    private static Material glow;
    private static readonly int[] Bayer = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };

    private SpriteRenderer sr;
    private Color colour;
    private Color[] motes;
    private System.Func<bool> finished;
    private float fade = 1f, moteTimer, phase;
    private Camera cam;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Hook()
    {
        SceneManager.sceneLoaded -= OnLoaded;
        SceneManager.sceneLoaded += OnLoaded;
        OnLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private static void OnLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!scene.name.StartsWith("Z4")) return;
        foreach (TimeTrail t in FindObjectsByType<TimeTrail>(FindObjectsSortMode.None))
        {
            Transform post = t.transform.Find("Posts/StartPost");
            Add(t.gameObject, post != null ? post.position : t.transform.position, PixelFX.Hex("ff6b97"), () => t == null || t.Spent);
        }
        foreach (ChestShuffleGame g in FindObjectsByType<ChestShuffleGame>(FindObjectsSortMode.None))
            Add(g.gameObject, g.transform.position, PixelFX.Hex("ffe478"),
                () => g == null || g.Current == ChestShuffleGame.State.Won || g.Current == ChestShuffleGame.State.Lost);
        foreach (ChallengeArena a in FindObjectsByType<ChallengeArena>(FindObjectsSortMode.None))
        {
            Collider2D area = a.GetComponent<Collider2D>();
            Vector3 at = area != null ? new Vector3(area.bounds.center.x, area.bounds.min.y, 0f) : a.transform.position;
            Add(a.gameObject, at, PixelFX.Hex("66ffe3"), () => a == null || a.IsCleared);
        }
    }

    private static void Add(GameObject owner, Vector3 foot, Color colour, System.Func<bool> finished)
    {
        if (owner.GetComponentInChildren<EncounterLandmark>() != null) return;
        if (!Ready()) return;
        var go = new GameObject("EncounterLandmark");
        go.transform.SetParent(owner.transform, true);
        go.transform.position = new Vector3(Mathf.Round(foot.x * 16f) / 16f, Mathf.Round(foot.y * 16f) / 16f, 0f);
        var l = go.AddComponent<EncounterLandmark>();
        l.colour = colour;
        l.finished = finished;
        l.motes = new[] { colour, Color.Lerp(colour, Color.white, 0.5f), PixelFX.Hex("ffffeb") };
        l.phase = Random.value * 6f;
    }

    private static bool Ready()
    {
        if (glow == null) glow = Resources.Load<Material>("FX/SpriteGlow");
        if (glow == null) return false;
        if (shaft != null) return true;
        var tex = new Texture2D(TexW, TexH, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave, name = "EncounterShaft" };
        var px = new Color32[TexW * TexH];
        for (int y = 0; y < TexH; y++)
            for (int x = 0; x < TexW; x++)
            {
                float across = 1f - Mathf.Abs((x + 0.5f) / TexW * 2f - 1f);    // brightest in the middle column
                float up = 1f - (float)y / TexH;                               // fading to nothing at the top
                float a = Mathf.Clamp01(across * 1.4f) * up * up;
                float threshold = (Bayer[(y & 3) * 4 + (x & 3)] + 0.5f) / 16f;
                byte v = a > threshold ? (byte)255 : (byte)0;                  // 1-bit ordered dither: crisp pixels
                px[y * TexW + x] = new Color32(255, 255, 255, v);
            }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        shaft = Sprite.Create(tex, new Rect(0, 0, TexW, TexH), new Vector2(0.5f, 0f), 16f, 0, SpriteMeshType.FullRect);
        shaft.hideFlags = HideFlags.DontSave;
        return true;
    }

    private void Start()
    {
        sr = gameObject.AddComponent<SpriteRenderer>();
        sr.sprite = shaft;
        sr.sharedMaterial = glow;
        sr.sortingLayerName = "Player";
        sr.sortingOrder = -20; // over the ground and scenery, under Harvey, enemies and the encounter's own props
        cam = Camera.main;
    }

    private void Update()
    {
        bool done = finished != null && finished();
        fade = Mathf.MoveTowards(fade, done ? 0f : 1f, Time.deltaTime);
        float breathe = 0.5f + 0.5f * Mathf.Sin((Time.time + phase) * 1.6f); // a slow, even swell: no flicker
        Color c = colour;
        c.a = (0.16f + 0.1f * breathe) * fade;
        sr.color = c;
        sr.enabled = fade > 0.001f;
        if (done || cam == null) return;

        Vector3 v = cam.WorldToViewportPoint(transform.position);
        if (v.x < -0.1f || v.x > 1.1f || v.y < -0.3f || v.y > 1.1f) return; // only on screen
        moteTimer -= Time.deltaTime;
        if (moteTimer > 0f) return;
        moteTimer = Random.Range(0.25f, 0.45f);
        Vector3 at = transform.position + new Vector3(Random.Range(-0.35f, 0.35f), Random.Range(0.1f, 1.2f), 0f);
        PixelFX.Emit(at, new Vector2(Random.Range(-0.1f, 0.1f), Random.Range(0.5f, 0.9f)), PixelFX.Pick(motes), Random.Range(1.4f, 2.2f), 1, PixelShape.Square, PixelMotion.Drift, true, PixelLayer.Front);
    }
}
