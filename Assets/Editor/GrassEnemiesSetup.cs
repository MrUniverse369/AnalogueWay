using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// GRASSENEMIES (2026-10-23): builds the Grassy Green zone's three regular enemies, each its own prefab
/// (Tools/HNL/Set Up Grass Enemies; safe to run again after re-running Tools/Art/grass_enemies.py):
///   Floating Mushroom   FloatingMushroomFloat.png (from Floating Mushroom Sprite Sheet)   from CloudTrollHolder, a slow drift
///   Grassenemynonboss   GrassenemynonbossWalk.png (from Grassenemynonboss)                from CloudTrollHolder, the troll's walk
///   Forest Spirit Fox   ForestSpiritFoxRun.png (from Forest Spirit Fox Run Cycle)         from the Shadow Fox, a fast run
/// For each one:
///   1. the cleaned sheet imported like the other enemies (16 PPU, point, no compression) and sliced into equal frames
///      (sizes from Tools/Art/grass_enemies_layout.txt), centre pivot
///   2. a looping clip of every frame
///   3. a controller: the loop is the default state, Any State -> the shared smoke death (DeathAnimTwo) on the
///      DeathAnim bool with no exit time, like the Shadow Fox
///   4. Assets/Prefabs/Enemy/Grass/&lt;Enemy&gt;.prefab rebuilt from its base prefab (same patrol, stomp, hurt and respawn
///      set-up) with the new sprite, animator and collider and its own patrol speed
/// FlyingGrassBoss is the zone boss's art and isn't used here; Z4BossGrass is left alone.
/// </summary>
public static class GrassEnemiesSetup
{
    private const string Dir = "Assets/Sprites/Enemies/Grass/";
    private const string Layout = "Tools/Art/grass_enemies_layout.txt";
    private const string DeathClipPath = "Assets/Sprites/Enemies/Shared/DeathAnimTwo.anim";
    private const string PrefabDir = "Assets/Prefabs/Enemy/Grass/";
    private const float Scale = 0.8f; // art pixel = 0.05 units, as on the Cloud Troll and the Shadow Fox

    private sealed class Enemy
    {
        public string sheet, folder, prefab, basePrefab;
        public float fps, speed, halfWidth;
    }

    private static readonly Enemy[] Enemies =
    {
        new Enemy { sheet = "FloatingMushroomFloat", folder = "FloatingMushroom", prefab = "FloatingMushroom",
            basePrefab = PrefabDir + "CloudTrollHolder.prefab", fps = 8f, speed = 1.2f, halfWidth = 2.5f },
        new Enemy { sheet = "GrassenemynonbossWalk", folder = "Grassenemynonboss", prefab = "Grassenemynonboss",
            basePrefab = PrefabDir + "CloudTrollHolder.prefab", fps = 10f, speed = 2f, halfWidth = 3f },
        new Enemy { sheet = "ForestSpiritFoxRun", folder = "ForestSpiritFox", prefab = "ForestSpiritFox",
            basePrefab = "Assets/Prefabs/Enemy/Tomb/ShadowFox.prefab", fps = 12f, speed = 4f, halfWidth = 3f },
    };

    [MenuItem("Tools/HNL/Set Up Grass Enemies")]
    public static void Run()
    {
        if (!File.Exists(Layout))
        {
            Debug.LogWarning("GrassEnemiesSetup: run Tools/Art/grass_enemies.py first (no layout).");
            return;
        }
        var sizes = File.ReadAllLines(Layout).Select(l => l.Split(' ')).Where(p => p.Length == 4)
            .ToDictionary(p => p[0], p => (frames: int.Parse(p[1]), w: int.Parse(p[2]), h: int.Parse(p[3])));
        foreach (Enemy e in Enemies)
        {
            if (!sizes.TryGetValue(e.sheet, out var size))
            {
                Debug.LogWarning($"GrassEnemiesSetup: {e.sheet} is missing from {Layout}.");
                continue;
            }
            Build(e, size.w, size.h, Box(e.sheet));
        }
        AssetDatabase.SaveAssets();
    }

    // COLLIDERS: the hitbox Tools/Art/grass_enemies.py measured inside the art on every frame ("box <sheet> cx cy w h")
    private static Rect Box(string sheet)
    {
        string line = File.ReadAllLines(Layout).First(l => l.StartsWith("box " + sheet + " "));
        float[] v = line.Split(' ').Skip(2).Select(float.Parse).ToArray();
        return new Rect(v[0] - v[2] / 2f, v[1] - v[3] / 2f, v[2], v[3]);
    }

    private static void Build(Enemy e, int frameW, int frameH, Rect hitbox)
    {
        string dir = Dir + e.folder + "/";
        string sheet = dir + e.sheet + ".png";

        // 1. import and slice
        var ti = (TextureImporter)AssetImporter.GetAtPath(sheet);
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Multiple;
        ti.spritePixelsPerUnit = 16f;
        ti.filterMode = FilterMode.Point;
        ti.mipmapEnabled = false;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.alphaIsTransparency = true;
        ti.GetSourceTextureWidthAndHeight(out int width, out _);
        int count = width / frameW;
        var metas = new SpriteMetaData[count];
        for (int i = 0; i < count; i++)
            metas[i] = new SpriteMetaData
            {
                name = e.sheet + "_" + i,
                rect = new Rect(i * frameW, 0, frameW, frameH),
                alignment = (int)SpriteAlignment.Center,
                pivot = new Vector2(0.5f, 0.5f),
            };
#pragma warning disable 618 // still the simplest way to slice from code
        ti.spritesheet = metas;
#pragma warning restore 618
        ti.SaveAndReimport();
        Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(sheet).OfType<Sprite>()
            .OrderBy(s => int.Parse(s.name.Substring(s.name.LastIndexOf('_') + 1))).ToArray();

        // 2. the looping clip
        string clipPath = dir + e.sheet + ".anim";
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
        if (clip == null)
        {
            clip = new AnimationClip { frameRate = e.fps };
            AssetDatabase.CreateAsset(clip, clipPath);
        }
        clip.frameRate = e.fps;
        var keys = new ObjectReferenceKeyframe[sprites.Length]; // Unity holds the last key for one frame, so no closing key
        for (int i = 0; i < sprites.Length; i++) keys[i] = new ObjectReferenceKeyframe { time = i / e.fps, value = sprites[i] };
        AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), keys);
        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = true;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        EditorUtility.SetDirty(clip);

        // 3. the controller
        string ctrlPath = dir + e.folder + ".controller";
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ctrlPath)
                   ?? AnimatorController.CreateAnimatorControllerAtPath(ctrlPath);
        if (ctrl.parameters.All(p => p.name != "DeathAnim")) ctrl.AddParameter("DeathAnim", AnimatorControllerParameterType.Bool);
        AnimatorStateMachine sm = ctrl.layers[0].stateMachine;
        AnimatorState loop = sm.states.Select(s => s.state).FirstOrDefault(s => s.name == e.sheet)
                             ?? sm.AddState(e.sheet, new Vector3(300, 100, 0));
        loop.motion = clip;
        sm.defaultState = loop;
        AnimatorState death = sm.states.Select(s => s.state).FirstOrDefault(s => s.name == "DeathAnimTwo")
                              ?? sm.AddState("DeathAnimTwo", new Vector3(300, 200, 0));
        death.motion = AssetDatabase.LoadAssetAtPath<AnimationClip>(DeathClipPath);
        if (sm.anyStateTransitions.All(t => t.destinationState != death))
        {
            AnimatorStateTransition toDeath = sm.AddAnyStateTransition(death);
            toDeath.AddCondition(AnimatorConditionMode.If, 0f, "DeathAnim");
        }
        foreach (AnimatorStateTransition t in sm.anyStateTransitions.Where(t => t.destinationState == death))
        {
            t.hasExitTime = false;
            t.duration = 0f;
            t.canTransitionToSelf = false;
        }
        EditorUtility.SetDirty(ctrl);
        AssetDatabase.SaveAssets();

        // 4. the prefab, from its base
        string prefabPath = PrefabDir + e.prefab + ".prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(e.basePrefab);
        try
        {
            root.name = e.prefab;
            root.transform.localScale = Vector3.one;
            Transform body = root.transform.Find("Enemy");
            body.localPosition = Vector3.zero;
            body.localRotation = Quaternion.identity; // facing comes from MovingTiles' scale flip; a turned body runs backwards
            body.localScale = new Vector3(Scale, Scale, 1f);
            root.transform.Find("PointA").localPosition = new Vector3(-e.halfWidth, 0f, 0f);
            root.transform.Find("PointB").localPosition = new Vector3(e.halfWidth, 0f, 0f);

            body.GetComponent<Animator>().runtimeAnimatorController = ctrl; // first: swapping it rebinds and puts the old sprite back
            var sr = body.GetComponent<SpriteRenderer>();
            sr.sprite = sprites[0];
            sr.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
            var ghost = body.GetComponent<GhostEnemy>();
            if (ghost != null) Object.DestroyImmediate(ghost);

            var box = body.GetComponent<BoxCollider2D>();
            box.size = hitbox.size; // COLLIDERS: inside the art on every frame
            box.offset = hitbox.center;

            var so = new SerializedObject(root.GetComponent<MovingTiles>());
            so.FindProperty("speed").floatValue = e.speed;
            so.ApplyModifiedPropertiesWithoutUndo();

            if (File.Exists(prefabPath)) // keep the scale the prefab has been given since (the mushroom's root is 0.4)
            {
                GameObject was = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                root.transform.localScale = was.transform.localScale;
            }
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
        Debug.Log($"GrassEnemiesSetup: {e.prefab}: {sprites.Length} frames of {frameW}x{frameH}, prefab at {prefabPath}.");
    }

    // ---------------------------------------------------------------- RESKIN (2026-10-23)
    // Tools/HNL/Reskin Grass Zone Enemies: the zone's older enemies take the new art, keeping everything they do.
    //   - Cloud Trolls (CloudTrollHolder, Z4A8) become Forest Spirit Foxes: the fox's run loop, controller and hitbox;
    //     the troll's patrol, speed, bounce, toughness and damage stay. The fox is shorter, so each one is set down on
    //     the ground under its patrol (MovingTiles moves the body to its patrol points' height).
    //   - the bounce boxes (BounceEnemyHolder and BounceEnemyHolder 1, Z4A7) become Floating Mushrooms: the float loop,
    //     controller and cap hitbox; the bounce, patrol and damage stay. The holders are stretched (0.41 x 1.41), so the
    //     body is counter-scaled to draw the mushroom unstretched, at the size whose cap is as wide as the box was, its
    //     top where the box's top was (lifted if its roots would sink into the floor).
    //   - every Grass enemy's hitbox is the box measured inside its art on every frame (the old boxes reached past it).
    // Safe to run again. Only Z4A7 and Z4A8 use these two prefabs.
    private const string FoxSheet = "ForestSpiritFoxRun", MushroomSheet = "FloatingMushroomFloat";
    private static readonly (string prefab, string sheet)[] Reskins =
    {
        ("CloudTrollHolder", FoxSheet), ("BounceEnemyHolder", MushroomSheet), ("BounceEnemyHolder 1", MushroomSheet),
    };
    private const float MinMushroomScale = 0.32f, MaxMushroomScale = 0.8f; // 0.32: the mushroom prefab's own size

    [MenuItem("Tools/HNL/Reskin Grass Zone Enemies")]
    public static void Reskin()
    {
        foreach (Enemy e in Enemies) FitCollider(PrefabDir + e.prefab + ".prefab", Box(e.sheet));
        var scenes = new[] { "Assets/Scenes/Z4A7GrassGreen.unity", "Assets/Scenes/Z4A8GrassGreen.unity" };
        // read what each placed enemy looks like now, before the prefabs change under it
        var before = new System.Collections.Generic.Dictionary<string, (float width, float top)>();
        foreach (string path in scenes)
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            foreach (EnemyController ec in Placed(scene))
            {
                SpriteRenderer sr = ec.GetComponent<SpriteRenderer>();
                float y = PatrolY(ec);
                before[Key(ec)] = (Mathf.Abs(ec.transform.lossyScale.x) * sr.sprite.bounds.size.x,
                                   y + Mathf.Abs(ec.transform.lossyScale.y) * sr.sprite.bounds.extents.y);
            }
        }
        foreach (var (prefab, sheet) in Reskins) ReskinPrefab(PrefabDir + prefab + ".prefab", sheet);
        foreach (string path in scenes)
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            Physics2D.SyncTransforms();
            int n = 0;
            foreach (EnemyController ec in Placed(scene))
            {
                SpriteRenderer sr = ec.GetComponent<SpriteRenderer>();
                foreach (Component c in new Component[] { sr, ec.GetComponent<Animator>(), ec.GetComponent<BoxCollider2D>() })
                    PrefabUtility.RevertObjectOverride(c, InteractionMode.AutomatedAction); // the prefab's new art and hitbox
                bool fox = sr.sprite.name.StartsWith(FoxSheet);
                Transform parent = ec.transform.parent;
                Vector3 ps = parent.lossyScale;
                float centre;
                if (fox)
                {
                    float half = Mathf.Abs(ec.transform.lossyScale.y) * sr.sprite.bounds.extents.y;
                    float ground = GroundBelow(scene, ec.transform.position.x, PatrolY(ec) + 0.5f, PatrolY(ec) - 4f);
                    centre = ground + half;
                }
                else
                {
                    var (width, top) = before[Key(ec)];
                    Rect cap = Box(MushroomSheet);
                    float k = Mathf.Clamp(width / cap.width, MinMushroomScale, MaxMushroomScale);
                    ec.transform.localScale = new Vector3(k / ps.x, k / ps.y, 1f);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(ec.transform);
                    centre = top - cap.yMax * k;                     // the cap's top where the box's top was
                    float bottom = centre - sr.sprite.bounds.extents.y * k;
                    float ground = GroundBelow(scene, ec.transform.position.x, top, top - 6f);
                    if (bottom < ground + 0.05f) centre += ground + 0.05f - bottom; // roots clear of the floor
                }
                SetPatrolY(ec, centre);
                n++;
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"GrassEnemiesSetup: reskinned {n} enemies in {scene.name}.");
        }
        AssetDatabase.SaveAssets();
    }

    private static string Key(EnemyController ec) => GlobalObjectId.GetGlobalObjectIdSlow(ec.gameObject).ToString(); // the same after a reload

    // the Grass prefabs being reskinned that are placed in this scene
    private static System.Collections.Generic.IEnumerable<EnemyController> Placed(UnityEngine.SceneManagement.Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (EnemyController ec in root.GetComponentsInChildren<EnemyController>(true))
            {
                GameObject holder = PrefabUtility.GetOutermostPrefabInstanceRoot(ec.gameObject);
                string src = holder != null ? Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(holder))) : null;
                if (Reskins.Any(r => r.prefab == src)) yield return ec;
            }
    }

    // MovingTiles moves the body to its patrol points, so their height is where the body really is
    private static float PatrolY(EnemyController ec)
    {
        Transform a = ec.transform.parent.Find("PointA");
        return a != null ? a.position.y : ec.transform.position.y;
    }

    private static void SetPatrolY(EnemyController ec, float y)
    {
        foreach (Transform t in new[] { ec.transform.parent.Find("PointA"), ec.transform.parent.Find("PointB"), ec.transform })
        {
            if (t == null) continue;
            t.position = new Vector3(t.position.x, y, t.position.z);
            PrefabUtility.RecordPrefabInstancePropertyModifications(t);
        }
    }

    // the top of the first ground (Ground layer, this scene) below 'fromY', or 'fallback' if there is none
    private static float GroundBelow(UnityEngine.SceneManagement.Scene scene, float x, float fromY, float fallback)
    {
        foreach (RaycastHit2D hit in Physics2D.RaycastAll(new Vector2(x, fromY), Vector2.down, 12f, LayerMask.GetMask("Ground")))
            if (hit.collider.gameObject.scene == scene && hit.normal.y > 0.5f) return hit.point.y;
        return fallback;
    }

    private static void FitCollider(string prefabPath, Rect hitbox)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            var box = root.GetComponentInChildren<EnemyController>(true).GetComponent<BoxCollider2D>();
            box.size = hitbox.size;
            box.offset = hitbox.center;
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static void ReskinPrefab(string prefabPath, string sheet)
    {
        Enemy e = Enemies.First(x => x.sheet == sheet);
        string dir = Dir + e.folder + "/";
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(dir + e.folder + ".controller");
        Sprite first = AssetDatabase.LoadAllAssetsAtPath(dir + sheet + ".png").OfType<Sprite>().First(x => x.name == sheet + "_0");
        Rect hitbox = Box(sheet);
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            EnemyController ec = root.GetComponentInChildren<EnemyController>(true);
            Transform body = ec.transform;
            Animator anim = body.GetComponent<Animator>();
            anim.runtimeAnimatorController = ctrl; // first: swapping it rebinds and puts the old sprite back
            var sr = body.GetComponent<SpriteRenderer>();
            sr.sprite = first;
            sr.color = Color.white; // the boxes were tinted
            sr.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
            var so = new SerializedObject(ec);
            so.FindProperty("animator").objectReferenceValue = anim; // the death smoke plays on it
            so.ApplyModifiedPropertiesWithoutUndo();
            var box = body.GetComponent<BoxCollider2D>();
            box.size = hitbox.size;
            box.offset = hitbox.center;
            Vector3 rs = root.transform.localScale; // unstretched on a stretched holder (the fox's holder isn't)
            float k = sheet == MushroomSheet ? 0.5f : 0.8f;
            body.localScale = new Vector3(k / rs.x, k / rs.y, 1f);
            body.localRotation = Quaternion.identity;
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}
