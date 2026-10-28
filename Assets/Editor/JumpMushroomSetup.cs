using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// <summary>
/// JUMPMUSHROOM (2026-10-28): the Jump Mushroom, Grassy Green's ground launcher, and the pass that puts it where the
/// zone's jump boxes were (Tools/HNL/Jump Mushroom/...). Both steps are safe to run again.
///   Build Prefab           Assets/Prefabs/LevelMechanics/GrassyGreen/JumpMushroom.prefab, filled in place so its GUID
///                          and the instances' overrides survive a rebuild. One job: Harvey lands on the cap and is
///                          bounced up. The root stands where the stem meets the floor and holds the trigger (the cap,
///                          reaching down to just above the floor so a fast fall can't step past it) and
///                          MushroomBounce (bounce speed per instance, catchFastLandings on). The child "Art" draws the
///                          Floating Mushroom's sheet and float animation at the Floating Mushroom's size, on the
///                          JumpBox sorting layer: behind Harvey, enemies and the ground, so the root tips it tucks
///                          under the grass line stay hidden. No float, no enemy parts, untagged, Default layer (a
///                          trigger there never counts as ground).
///   Replace Jump Boxes     every JumpBox.prefab instance in the Z4 scenes that is a jump pad (not a cloud, a positive
///                          force, at most 3 wide, standing on a floor) becomes a JumpMushroom on that floor at the same
///                          x, with the same parent and place in the hierarchy, its bounce speed the box's force. The
///                          rest is left and logged: bounce clouds (JumpBoxCloud), the wide bounce floors and their
///                          push-down ceilings (Z4A9, Z4A10), a box with no floor under it. A scene the user has unsaved
///                          changes in is skipped with a warning; save it and run again.
/// </summary>
public static class JumpMushroomSetup
{
    public const string PrefabPath = "Assets/Prefabs/LevelMechanics/GrassyGreen/JumpMushroom.prefab";
    private const string FloatingPrefab = "Assets/Prefabs/Enemy/Grass/FloatingMushroom.prefab";
    private const string BoxPrefab = "Assets/Prefabs/LevelMechanics/JumpBox.prefab";
    private const string ArtName = "Art";

    // the art, measured on FloatingMushroomFloat.png (41 x 44 px frames, pivot at the centre)
    private const float RootTipPx = 21f;   // the lowest root pixel below the pivot (frame 0, the low point of the bob)
    private const float CapTopPx = 18f;    // the cap top above the pivot, the middle of the bob (15 to 21)
    private const float CapWidthPx = 37f;  // the cap, less a pixel at each end
    private const float Sink = 0.225f;     // root tips under the grass line: hides the lowest roots, cap top 0.55 up
    private const float TriggerBottom = 0.05f;
    private const float MaxPadWidth = 3f;  // wider is a bounce floor, not a pad

    [MenuItem("Tools/HNL/Jump Mushroom/Build Prefab")]
    public static void BuildPrefab()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
        {
            // first build: an empty root saved as the prefab (in a preview scene, so no open scene is touched)
            Scene preview = EditorSceneManager.NewPreviewScene();
            var empty = new GameObject("JumpMushroom");
            SceneManager.MoveGameObjectToScene(empty, preview);
            PrefabUtility.SaveAsPrefabAsset(empty, PrefabPath);
            EditorSceneManager.ClosePreviewScene(preview);
        }
        GameObject source = PrefabUtility.LoadPrefabContents(FloatingPrefab);
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            Fill(root, source);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
            PrefabUtility.UnloadPrefabContents(source);
        }
        Debug.Log("JumpMushroom: prefab built at " + PrefabPath);
    }

    private static void Fill(GameObject root, GameObject source)
    {
        SpriteRenderer from = source.GetComponentInChildren<SpriteRenderer>(true);
        Animator fromAnimator = from.GetComponent<Animator>();
        float scale = from.transform.lossyScale.x;     // the Floating Mushroom's size on screen
        float px = scale / from.sprite.pixelsPerUnit;  // one art pixel in world units

        root.name = "JumpMushroom";
        root.tag = "Untagged";
        root.layer = 0;
        root.transform.localScale = Vector3.one;

        Transform art = root.transform.Find(ArtName);
        if (art == null)
        {
            art = new GameObject(ArtName).transform;
            art.SetParent(root.transform, false);
        }
        art.gameObject.tag = "Untagged";
        art.gameObject.layer = 0;
        art.localPosition = new Vector3(0f, RootTipPx * px - Sink, 0f);
        art.localRotation = Quaternion.identity;
        art.localScale = new Vector3(scale, scale, 1f);

        SpriteRenderer sr = GetOrAdd<SpriteRenderer>(art.gameObject);
        sr.sprite = from.sprite;
        sr.sharedMaterial = from.sharedMaterial;
        sr.color = Color.white;
        sr.sortingLayerName = "JumpBox";
        sr.sortingOrder = 0;
        Animator animator = GetOrAdd<Animator>(art.gameObject);
        animator.runtimeAnimatorController = fromAnimator.runtimeAnimatorController;
        animator.cullingMode = fromAnimator.cullingMode;
        animator.updateMode = fromAnimator.updateMode;
        animator.applyRootMotion = false;

        float capTop = art.localPosition.y + CapTopPx * px;
        BoxCollider2D cap = GetOrAdd<BoxCollider2D>(root);
        cap.isTrigger = true;
        cap.size = new Vector2(CapWidthPx * px, capTop - TriggerBottom);
        cap.offset = new Vector2(0f, (capTop + TriggerBottom) * 0.5f);

        var so = new SerializedObject(GetOrAdd<MushroomBounce>(root));
        so.FindProperty("catchFastLandings").boolValue = true;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static T GetOrAdd<T>(GameObject go) where T : Component
    {
        T c = go.GetComponent<T>();
        return c != null ? c : go.AddComponent<T>();
    }

    [MenuItem("Tools/HNL/Jump Mushroom/Replace Grass Jump Boxes")]
    public static void ReplaceJumpBoxesMenu() => ReplaceJumpBoxes();

    /// <summary>Runs the replacement over every Z4 scene in Build Settings and returns the log of what it did.</summary>
    public static string ReplaceJumpBoxes()
    {
        GameObject mushroom = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        GameObject box = AssetDatabase.LoadAssetAtPath<GameObject>(BoxPrefab);
        if (mushroom == null) return Report("JumpMushroom: build the prefab first (Tools/HNL/Jump Mushroom/Build Prefab).");
        var log = new List<string>();
        var userDirty = new HashSet<string>();
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene s = SceneManager.GetSceneAt(i);
            if (s.isLoaded && s.isDirty) userDirty.Add(s.path);
        }
        foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes)
        {
            string name = System.IO.Path.GetFileNameWithoutExtension(s.path);
            if (!name.StartsWith("Z4") || !System.IO.File.Exists(s.path)) continue;
            if (userDirty.Contains(s.path))
            {
                log.Add($"{name}: skipped, it has unsaved changes; save it and run again");
                continue;
            }
            WithScene(s.path, scene => ReplaceIn(scene, box, mushroom, log));
        }
        return Report(string.Join("\n", log.Select(l => "JumpMushroom: " + l)));
    }

    private static string Report(string text)
    {
        Debug.Log(text);
        return text;
    }

    private static bool ReplaceIn(Scene scene, GameObject boxPrefab, GameObject mushroomPrefab, List<string> log)
    {
        int replaced = 0;
        var parents = new HashSet<Transform>();
        foreach (JumpBox jb in All<JumpBox>(scene).ToList())
        {
            GameObject go = jb.gameObject;
            if (PrefabUtility.GetCorrespondingObjectFromSource(go) != boxPrefab || !PrefabUtility.IsOutermostPrefabInstanceRoot(go)) continue;
            float force = new SerializedObject(jb).FindProperty("jForce").floatValue;
            var col = go.GetComponent<BoxCollider2D>();
            Vector3 c = go.transform.TransformPoint(col.offset);
            Vector2 size = Vector2.Scale(col.size, go.transform.lossyScale);
            float width = Mathf.Abs(size.x), bottom = c.y - Mathf.Abs(size.y) * 0.5f;
            string at = $"{go.name} at ({c.x:0.00}, {c.y:0.00}), {width:0.00} wide, force {force}";
            float floor = 0f;
            string why = null;
            if (BouncyCloud.LooksLikeCloud(go)) why = "a bounce cloud";
            else if (force <= 0f) why = "pushes Harvey down (a bounce corridor's ceiling)";
            else if (width > MaxPadWidth) why = "a bounce floor, not a pad";
            else if (!FloorUnder(scene, c.x, bottom, out floor) || Mathf.Abs(bottom - floor) > 0.3f) why = "no floor under it";
            if (why != null)
            {
                log.Add($"{scene.name}: left {at}: {why}");
                continue;
            }

            var m = (GameObject)PrefabUtility.InstantiatePrefab(mushroomPrefab, scene);
            m.name = go.name.Replace("JumpBox", "JumpMushroom");
            m.transform.SetParent(go.transform.parent, false);
            m.transform.SetSiblingIndex(go.transform.GetSiblingIndex());
            m.transform.position = new Vector3(c.x, floor, go.transform.position.z);
            var so = new SerializedObject(m.GetComponent<MushroomBounce>());
            so.FindProperty("bounceSpeed").floatValue = force;
            so.ApplyModifiedPropertiesWithoutUndo();
            if (go.transform.parent != null) parents.Add(go.transform.parent);
            float half = m.GetComponent<BoxCollider2D>().size.x * 0.5f;
            log.Add($"{scene.name}: {at} -> {m.name} standing at ({c.x:0.00}, {floor:0.00}), bounce {force}" + Footing(scene, c.x, floor, half));
            Object.DestroyImmediate(go);
            replaced++;
        }
        // a holder named for the boxes is named for what it holds now
        foreach (Transform p in parents)
            if (p.name.Contains("JumpBox") && p.GetComponentsInChildren<JumpBox>(true).Length == 0) p.name = p.name.Replace("JumpBox", "JumpMushroom");
        if (replaced == 0) log.Add($"{scene.name}: no jump pads");
        return replaced > 0;
    }

    // the top of the first filled cell at or below (x, y) on a tilemap that collides (the floor the box stands on)
    private static bool FloorUnder(Scene scene, float x, float y, out float top)
    {
        top = float.NegativeInfinity;
        foreach (TilemapCollider2D tc in All<TilemapCollider2D>(scene))
        {
            Tilemap map = tc.GetComponent<Tilemap>();
            if (map == null || !tc.enabled) continue;
            Vector3Int cell = map.WorldToCell(new Vector3(x, y + 0.3f, 0f));
            for (int i = 0; i < 3; i++, cell.y--)
                if (map.HasTile(cell))
                {
                    top = Mathf.Max(top, map.CellToWorld(cell + Vector3Int.up).y);
                    break;
                }
        }
        return !float.IsNegativeInfinity(top);
    }

    // a warning when the mushroom's foot overhangs its floor or its art reaches into a wall
    private static string Footing(Scene scene, float x, float floor, float half)
    {
        bool left = FloorUnder(scene, x - half, floor, out float l) && Mathf.Abs(l - floor) < 0.01f;
        bool right = FloorUnder(scene, x + half, floor, out float r) && Mathf.Abs(r - floor) < 0.01f;
        bool wall = Solid(scene, x - half, floor + 0.3f) || Solid(scene, x + half, floor + 0.3f);
        return (left && right ? "" : " (overhangs its floor)") + (wall ? " (art reaches into a wall)" : "");
    }

    private static bool Solid(Scene scene, float x, float y)
    {
        foreach (TilemapCollider2D tc in All<TilemapCollider2D>(scene))
        {
            Tilemap map = tc.GetComponent<Tilemap>();
            if (map != null && tc.enabled && map.HasTile(map.WorldToCell(new Vector3(x, y, 0f)))) return true;
        }
        return false;
    }

    private static IEnumerable<T> All<T>(Scene scene) where T : Component
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (T c in root.GetComponentsInChildren<T>(true))
                yield return c;
    }

    // opens the scene if needed, edits it, saves it if anything changed, and closes it again if it opened it
    private static void WithScene(string path, System.Func<Scene, bool> edit)
    {
        Scene scene = SceneManager.GetSceneByPath(path);
        bool opened = !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        if (edit(scene))
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        if (opened) EditorSceneManager.CloseScene(scene, true);
    }
}
