using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// FX: each zone's atmosphere, set up automatically when a level ("Z..." scene with a player) loads, so no scene
/// needs editing. It is decided by the scene name:
///   Z1 Tomb     darkness with a light circle around Harvey and any torch/lamp/lantern/checkpoint (TombDarkness),
///               dust motes drifting in that light, and embers rising from torches
///   Z2 Egypt    blowing sand wisps, and heat haze near the horizon on the far background layers
///   Z3 Japan    sakura petals; in the village also fireflies
///   Z4 Green    drifting leaves and pollen, and pixel-snapped wind sway on the tops of trees and bushes
/// Every level: scrolling clouds inside the parallax skies, a glint sweeping over coins, chests, spikes and
/// floating traps. Material swaps only touch sprites still on the default sprite material, and use the shared
/// HNLSprite variants in Resources/FX. Ambient particles spawn around the camera view only, at low rates.
/// </summary>
public class ZoneAtmosphere : MonoBehaviour
{
    private enum Zone { None, Tomb, Egypt, JapanSun, JapanSnow, Village, Green }

    private Zone zone;
    private Camera cam;
    private float timerA, timerB;
    private readonly List<Transform> embers = new List<Transform>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single || !scene.name.StartsWith("Z") || scene.name == ZoneSelectMap.SceneName) return;
        if (FindFirstObjectByType<PlayerController>() == null) return;
        var go = new GameObject("ZoneAtmosphere");
        SceneManager.MoveGameObjectToScene(go, scene);
        go.AddComponent<ZoneAtmosphere>().zone = ZoneOf(scene.name);
    }

    private static Zone ZoneOf(string n)
    {
        if (n.StartsWith("Z1")) return Zone.Tomb;
        if (n.StartsWith("Z2")) return Zone.Egypt;
        if (n.StartsWith("Z3")) return n.Contains("Village") ? Zone.Village : n.Contains("A1") || n.Contains("A2") ? Zone.JapanSun : Zone.JapanSnow;
        if (n.StartsWith("Z4")) return Zone.Green;
        return Zone.None;
    }

    // ---------------------------------------------------------------- materials
    private void Start()
    {
        cam = Camera.main;
        Material shine = Resources.Load<Material>("FX/HNLSprite_Shine");
        Material glint = Resources.Load<Material>("FX/HNLSprite_Glint");
        Material sway = Resources.Load<Material>("FX/HNLSprite_Sway");
        Material sky = Resources.Load<Material>("FX/HNLSprite_Sky");
        Material haze = Resources.Load<Material>("FX/HNLSprite_Haze");
        var block = new MaterialPropertyBlock();

        foreach (SpriteRenderer r in FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!IsDefault(r) || r.sprite == null) continue;
            string n = r.name.ToLowerInvariant();
            string layer = r.sortingLayerName;

            if (r.CompareTag("Coin") || r.GetComponentInParent<OpenChest>() != null)
            {
                r.sharedMaterial = shine;
            }
            else if (r.CompareTag("Fspike") || r.CompareTag("worldOneFloatingTrap") || (n.Contains("spike") && r.GetComponentInParent<HurtPlayer>() != null))
            {
                r.sharedMaterial = glint;
            }
            else if (n.Contains("sky"))
            {
                r.sharedMaterial = sky;
                Vector3 size = r.bounds.size; // world units, so scaled box skies get clouds at the right pixel size
                r.GetPropertyBlock(block);
                block.SetVector("_CloudArea", new Vector4(size.x, size.y, 0, 0));
                block.SetVector("_CloudBand", new Vector4(size.y * 0.3f, size.y - 0.3f, 0, 0)); // up from the sprite's bottom
                r.SetPropertyBlock(block);
            }
            else if (zone == Zone.Green && (layer == "Tree" || layer == "TreeFront" || layer == "Bush" || n.Contains("tree") || n.Contains("bush")))
            {
                r.sharedMaterial = sway;
                Bounds lb = r.sprite.bounds;
                r.GetPropertyBlock(block);
                block.SetFloat("_SwayBase", lb.min.y + lb.size.y * 0.45f);
                block.SetFloat("_SwayHeight", lb.size.y * 0.55f);
                block.SetFloat("_SwayPixels", n.Contains("bush") ? 1f : 1.5f);
                r.SetPropertyBlock(block);
            }
            else if (zone == Zone.Egypt && (layer == "PBackGroundFive" || layer == "PBackGroundSix" || layer == "PBacGroundFour"))
            {
                r.sharedMaterial = haze;
                Bounds wb = r.bounds; // world: shimmer around the top edge of the far dunes
                r.GetPropertyBlock(block);
                block.SetFloat("_HazeY", wb.max.y - wb.size.y * 0.15f);
                block.SetFloat("_HazeHeight", Mathf.Max(1f, wb.size.y * 0.25f));
                r.SetPropertyBlock(block);
            }

            if (zone == Zone.Tomb && (n.Contains("torch") || n.Contains("lamp") || n.Contains("lantern"))) embers.Add(r.transform);
        }

        if (zone == Zone.Tomb && !SceneManager.GetActiveScene().name.Contains("Village")) gameObject.AddComponent<TombDarkness>(); // VILLAGES: the dig-site camp is out in the daylight

        // levels with a living/spirit world switch start with the colour of the world Harvey is in
        hasWorlds = FindFirstObjectByType<AnalogueWay.changeWorld>() != null;
        if (hasWorlds) ScreenGrade.SpiritWorld(!AnalogueWay.changeWorld.lWorld, true);
    }

    private bool hasWorlds;
    private float wispTimer;
    private static readonly Color[] Wisps = { PixelFX.Hex("b7f5ff"), PixelFX.Hex("c9b8ff"), PixelFX.Hex("8fd3ff") };

    private static bool IsDefault(SpriteRenderer r)
    {
        Material m = r.sharedMaterial;
        return m != null && m.shader != null && (m.shader.name == "Sprites/Default" || m.shader.name == "Universal Render Pipeline/2D/Sprite-Unlit-Default");
    }

    // ---------------------------------------------------------------- ambient particles
    private static readonly Color[] Leaves = { PixelFX.Hex("88ce6e"), PixelFX.Hex("52ab61"), PixelFX.Hex("99e550") };
    private static readonly Color[] Pollen = { PixelFX.Hex("ffe478"), PixelFX.Hex("fff3b0") };
    private static readonly Color[] Petals = { PixelFX.Hex("ffb7d5"), PixelFX.Hex("ff8fb8"), PixelFX.Hex("ffe0ec") };
    private static readonly Color[] Firefly = { PixelFX.Hex("e6ff8f"), PixelFX.Hex("ffe478") };
    private static readonly Color[] Sand = { PixelFX.Hex("e1d089"), PixelFX.Hex("e3ba66"), PixelFX.Hex("fff3b0") };
    private static readonly Color[] Motes = { PixelFX.Hex("b9d2ca"), PixelFX.Hex("ffffeb"), PixelFX.Hex("98b2b1") };
    private static readonly Color[] Embers = { PixelFX.Hex("ffe478"), PixelFX.Hex("f2a65e"), PixelFX.Hex("eb564b") };

    private void Update()
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) return;
        float dt = Time.deltaTime;
        timerA -= dt;
        timerB -= dt;
        float h = cam.orthographicSize, w = h * cam.aspect;
        Vector3 c = cam.transform.position;

        // spirit world: pale wisps float up through the view
        if (hasWorlds && !AnalogueWay.changeWorld.lWorld)
        {
            wispTimer -= dt;
            if (wispTimer <= 0f)
            {
                wispTimer = Random.Range(0.25f, 0.5f);
                PixelFX.Emit(new Vector3(c.x + Random.Range(-w, w), c.y - h + Random.Range(0f, h), 0f), new Vector2(Random.Range(-0.15f, 0.15f), Random.Range(0.3f, 0.7f)),
                    PixelFX.Pick(Wisps), Random.Range(2.5f, 4f), Random.value < 0.3f ? 2 : 1, PixelShape.Square, PixelMotion.Drift, true, PixelLayer.Back);
            }
        }

        switch (zone)
        {
            case Zone.Green:
                if (timerA <= 0f)
                {
                    timerA = Random.Range(0.35f, 0.7f);
                    // leaves blow in from the upper left and tumble across
                    PixelFX.Emit(new Vector3(c.x - w - 0.3f, c.y + Random.Range(-h * 0.2f, h), 0f), new Vector2(Random.Range(0.8f, 1.6f), Random.Range(-0.5f, -0.2f)),
                        PixelFX.Pick(Leaves), Random.Range(7f, 10f), 1, PixelShape.Leaf, PixelMotion.Drift, false, PixelLayer.Front);
                }
                if (timerB <= 0f)
                {
                    timerB = Random.Range(0.25f, 0.5f);
                    PixelFX.Emit(RandomInView(c, w, h), new Vector2(Random.Range(0.05f, 0.25f), Random.Range(0.02f, 0.12f)),
                        PixelFX.Pick(Pollen), Random.Range(3f, 5f), 1, PixelShape.Square, PixelMotion.Drift, true, PixelLayer.Back);
                }
                break;

            case Zone.Egypt:
                if (timerA <= 0f && !HarveyUnderwater()) // no wind under the water
                {
                    timerA = Random.Range(0.06f, 0.14f);
                    // sand wisps: short fast streaks low across the screen, in little gusts
                    float gust = 0.6f + 0.4f * Mathf.Sin(Time.time * 0.7f);
                    if (Random.value < gust)
                        PixelFX.Emit(new Vector3(c.x - w - 0.2f, c.y + Random.Range(-h, h * 0.3f), 0f), new Vector2(Random.Range(5f, 8f), Random.Range(-0.2f, 0.2f)),
                            PixelFX.Pick(Sand), Random.Range(1.6f, 2.6f), 1, PixelShape.Streak, PixelMotion.Straight, false, PixelLayer.Front);
                }
                break;

            case Zone.JapanSun:
            case Zone.JapanSnow:
            case Zone.Village:
                if (timerA <= 0f)
                {
                    timerA = Random.Range(0.3f, 0.6f);
                    PixelFX.Emit(new Vector3(c.x + Random.Range(-w, w + 2f), c.y + h + 0.3f, 0f), new Vector2(Random.Range(-0.8f, -0.3f), Random.Range(-0.6f, -0.3f)),
                        PixelFX.Pick(Petals), Random.Range(7f, 10f), 1, PixelShape.Petal, PixelMotion.Drift, false, PixelLayer.Front);
                }
                if (zone == Zone.Village && timerB <= 0f)
                {
                    timerB = Random.Range(0.3f, 0.6f);
                    PixelFX.Emit(new Vector3(c.x + Random.Range(-w, w), c.y + Random.Range(-h, h * 0.4f), 0f), new Vector2(Random.Range(-0.2f, 0.2f), Random.Range(-0.1f, 0.2f)),
                        PixelFX.Pick(Firefly), Random.Range(2.5f, 4f), 1, PixelShape.Square, PixelMotion.Drift, true, PixelLayer.Back);
                }
                break;

            case Zone.Tomb:
                if (timerA <= 0f)
                {
                    // dust motes drifting in Harvey's light
                    timerA = Random.Range(0.2f, 0.35f);
                    Vector3 around = PlayerFX.Instance != null ? PlayerFX.Instance.transform.position : c;
                    Vector2 off = Random.insideUnitCircle * 2.4f;
                    PixelFX.Emit(around + (Vector3)off, new Vector2(Random.Range(-0.08f, 0.08f), Random.Range(-0.05f, 0.1f)),
                        PixelFX.Pick(Motes), Random.Range(2.5f, 4f), 1, PixelShape.Square, PixelMotion.Drift, true, PixelLayer.Front);
                }
                if (timerB <= 0f && embers.Count > 0)
                {
                    timerB = Random.Range(0.15f, 0.3f);
                    Transform t = embers[Random.Range(0, embers.Count)];
                    if (t != null && t.gameObject.activeInHierarchy && Mathf.Abs(t.position.x - c.x) < w + 2f)
                        PixelFX.Emit(t.position + Vector3.up * 0.3f, new Vector2(Random.Range(-0.2f, 0.2f), Random.Range(0.5f, 1.1f)),
                            PixelFX.Pick(Embers), Random.Range(0.8f, 1.4f), 1, PixelShape.Square, PixelMotion.Drift, true, PixelLayer.Front);
                }
                break;
        }
    }

    private PlayerWater water;
    private bool HarveyUnderwater()
    {
        if (water == null && PlayerFX.Instance != null) water = PlayerFX.Instance.GetComponent<PlayerWater>();
        return water != null && water.HeadUnderwater;
    }

    private static Vector3 RandomInView(Vector3 c, float w, float h) =>
        new Vector3(c.x + Random.Range(-w, w), c.y + Random.Range(-h, h), 0f);
}
