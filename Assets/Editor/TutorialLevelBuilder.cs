using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// TUTORIAL (2026-10-20): builds Assets/Scenes/Tutorial_BasicControls.unity, the Basic Controls level opened from the
/// main menu's TUTORIAL button. It starts from a copy of Z4A1GrassGreen so it has the real Harvey, camera, HUD, pause
/// menu, level manager and talisman manager, strips everything else, and lays out a plain level: one background
/// colour, white box platforms (Tools/Art/tutorial_art.py) and red box enemies (EnemyHolder with a box sprite).
/// Six steps, each with its TutorialGoal: move, jump, stomp, super jump, dash, then all of them together.
/// Harvey's numbers (speed 4.5, jump 5, gravity 1.1): a jump clears 1 tile, a super jump about 2.6, so the ledges
/// are 2 tiles high. Safe to re-run: the scene is rebuilt from scratch (it keeps its GUID) and the build list entry and
/// the main menu button are only added once.
/// </summary>
public static class TutorialLevelBuilder
{
    public const string ScenePath = "Assets/Scenes/" + MenueControl.TutorialSceneName + ".unity";
    private const string BaseScene = "Assets/Scenes/Z4A1GrassGreen.unity";
    private const string ArtDir = "Assets/Sprites/Tutorial/";
    private const string ControllerPath = ArtDir + "BoxEnemy.controller";
    private const string DeathClipPath = ArtDir + "BoxEnemyDeath.anim";
    private static readonly Color Background = new Color32(0x3b, 0x34, 0x68, 255); // UI PANEL: the one background colour
    private static readonly Color FinishYellow = new Color32(0xff, 0xe4, 0x78, 255);

    private static readonly string[] KeepRoots =
        { "Player", "Main Camera", "LevelManager", "RespawnPos", "LevelComplete", "Deck", "Timer", "Pause", "EventSystem", "AudioManager", "ActiveCardManager", "SafetyKillPlane" };

    private static readonly TutorialGuide.Step[] Steps =
    {
        new TutorialGuide.Step { title = "MOVE", keyboard = "WALK RIGHT: ARROW KEYS OR A / D", controller = "LEFT STICK" },
        new TutorialGuide.Step { title = "JUMP", keyboard = "PRESS SPACE TO JUMP UP AND OVER THE GAP", controller = "TRIANGLE TO JUMP" },
        new TutorialGuide.Step { title = "STOMP", keyboard = "JUMP ON THE BOXES TO STOMP THEM", controller = "TRIANGLE TO JUMP, LAND ON TOP" },
        new TutorialGuide.Step { title = "SUPER JUMP", keyboard = "HOLD C, THEN SPACE: CLIMB THE LEDGES", controller = "CIRCLE, THEN TRIANGLE" },
        new TutorialGuide.Step { title = "DASH", keyboard = "HOLD X TO DASH THROUGH THE BOXES", controller = "CROSS TO DASH" },
        new TutorialGuide.Step { title = "ALL TOGETHER", keyboard = "JUMP, STOMP, SUPER JUMP AND DASH TO THE END", controller = "EVERYTHING YOU'VE LEARNED" },
    };

    [MenuItem("Tools/HNL/Build Basic Controls Tutorial")]
    public static void BuildFromMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Debug.Log(Build());
    }

    public static string Build()
    {
        ImportArt();
        BoxEnemyController();

        CopyFresh(BaseScene, ScenePath);
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        // loaded after the scene opens: opening a scene unloads unused assets, which left the enemy sprite reference dead
        RuntimeAnimatorController controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
        Sprite white = AssetDatabase.LoadAssetAtPath<Sprite>(ArtDir + "WhiteBox.png");
        Sprite enemySprite = AssetDatabase.LoadAssetAtPath<Sprite>(ArtDir + "BoxEnemy.png");
        foreach (GameObject root in scene.GetRootGameObjects())
            if (!KeepRoots.Contains(root.name)) Object.DestroyImmediate(root);

        var player = Object.FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
        var pso = new SerializedObject(player);
        pso.FindProperty("canSummonDragon").boolValue = false;
        pso.ApplyModifiedPropertiesWithoutUndo();
        player.transform.position = new Vector3(-5f, 0.6f, 0f);
        GameObject respawn = scene.GetRootGameObjects().First(g => g.name == "RespawnPos");
        respawn.transform.position = new Vector3(-5f, 1f, 0f);

        Camera cam = Object.FindFirstObjectByType<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Background;
        cam.transform.position = new Vector3(-1f, 2f, cam.transform.position.z);

        var level = new Level(scene, white, enemySprite, controller, player.GetComponent<SpriteRenderer>());
        Layout(level);

        GameObject kill = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "SafetyKillPlane");
        if (kill != null)
        {
            kill.transform.position = new Vector3(60f, -12f, 0f);
            kill.transform.localScale = Vector3.one;
            var box = kill.GetComponent<BoxCollider2D>();
            if (box != null) { box.offset = Vector2.zero; box.size = new Vector2(220f, 4f); }
        }

        var guideGo = new GameObject("TutorialGuide");
        SceneManager.MoveGameObjectToScene(guideGo, scene);
        var guide = guideGo.AddComponent<TutorialGuide>();
        var gso = new SerializedObject(guide);
        var steps = gso.FindProperty("steps");
        steps.arraySize = Steps.Length;
        for (int i = 0; i < Steps.Length; i++)
        {
            var e = steps.GetArrayElementAtIndex(i);
            e.FindPropertyRelative("title").stringValue = Steps[i].title;
            e.FindPropertyRelative("keyboard").stringValue = Steps[i].keyboard;
            e.FindPropertyRelative("controller").stringValue = Steps[i].controller;
        }
        var goals = gso.FindProperty("goals");
        goals.arraySize = level.goals.Count;
        for (int i = 0; i < level.goals.Count; i++) goals.GetArrayElementAtIndex(i).objectReferenceValue = level.goals[i];
        var cat = AssetDatabase.LoadAssetAtPath<HNLCatalog>("Assets/Resources/HNLCatalog.asset");
        gso.FindProperty("dash").objectReferenceValue = cat.TalismanById("Talisman_SpeedBoost");
        gso.FindProperty("superJump").objectReferenceValue = cat.TalismanById("alisman_SuperJump");
        gso.ApplyModifiedPropertiesWithoutUndo();

        // the enemies' box sprite, set last: set while the enemies were being made it didn't stick (they kept the template's)
        foreach (var e in Object.FindObjectsByType<EnemyController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            e.GetComponent<SpriteRenderer>().sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ArtDir + "BoxEnemy.png");

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        bool added = AddToBuildSettings();
        bool button = MainMenuButton.Ensure();
        return $"Tutorial built: {level.boxes} boxes, {level.enemies} enemies, {level.goals.Count} goals; build list {(added ? "added" : "already there")}; menu button {(button ? "added" : "already there")}.";
    }

    // ---------------------------------------------------------------- the layout (floor top is y 0; 1 unit = 1 tile)
    private static void Layout(Level l)
    {
        const float bottom = -6f;
        l.Box(-10f, -8f, 9f, bottom);           // left wall
        // 0 MOVE: a flat run to the first goal
        l.Box(-8f, 16f, 0f, bottom);
        l.Goal(0, 13f, 0f, 1f, 9f);
        // 1 JUMP: a one-tile step, a shallow gap (one tile deep, so a miss is just a hop back out), then a crate
        l.Box(16f, 19f, 1f, bottom);
        l.Box(19f, 22f, -1f, bottom);
        l.Box(22f, 34f, 0f, bottom);
        l.Box(26f, 27f, 1f, bottom);
        l.Goal(1, 31f, 0f, 1f, 9f);
        // 2 STOMP: two box enemies pacing on the flat
        l.Box(34f, 52f, 0f, bottom);
        l.Goal(2, l.Enemy(37.5f, 41f, 0f), l.Enemy(45f, 49f, 0f));
        // 3 SUPER JUMP: two ledges two tiles high each (a normal jump only clears one)
        l.Box(52f, 58f, 0f, bottom);
        l.Box(58f, 63f, 2f, bottom);
        l.Box(63f, 68f, 4f, bottom);
        l.Goal(3, 65.5f, 4f, 3f, 3f);
        // 4 DASH: a row of three box enemies, close together
        l.Box(68f, 90f, 0f, bottom);
        l.Goal(4, l.Enemy(75.5f, 76.5f, 0f), l.Enemy(79.5f, 80.5f, 0f), l.Enemy(83.5f, 84.5f, 0f));
        // 5 ALL TOGETHER: a gap, an enemy to stomp, a ledge to super jump, two enemies to dash through, the finish
        l.Box(90f, 93f, -1f, bottom);
        l.Box(93f, 100f, 0f, bottom);
        l.Enemy(95f, 99f, 0f);
        l.Box(100f, 112f, 2f, bottom);
        l.Enemy(104f, 105f, 2f);
        l.Enemy(107.5f, 108.5f, 2f);
        l.Box(112f, 126f, 0f, bottom);
        l.Box(126f, 128f, 9f, bottom);          // right wall
        l.Finish(121f);
        l.Goal(5, 121f, 0f, 1f, 9f);
    }

    private class Level
    {
        private readonly Scene scene;
        private readonly Sprite white, enemySprite;
        private readonly RuntimeAnimatorController controller;
        private readonly Transform boxRoot, enemyRoot, goalRoot;
        private readonly int ground = LayerMask.NameToLayer("Ground");
        private readonly int sortingLayer, sortingOrder;
        private readonly GameObject enemyTemplate;
        public readonly List<TutorialGoal> goals = new List<TutorialGoal>();
        public int boxes, enemies;

        public Level(Scene scene, Sprite white, Sprite enemySprite, RuntimeAnimatorController controller, SpriteRenderer harvey)
        {
            this.scene = scene;
            this.white = white;
            this.enemySprite = enemySprite;
            this.controller = controller;
            sortingLayer = harvey.sortingLayerID;
            sortingOrder = harvey.sortingOrder;
            enemyTemplate = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemy/EnemyHolder.prefab");

            // the boxes share one static body and a merged outline, so Harvey never catches on the seams between them
            boxRoot = Root("WhiteBoxes").transform;
            boxRoot.gameObject.layer = ground;
            var rb = boxRoot.gameObject.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Static;
            var composite = boxRoot.gameObject.AddComponent<CompositeCollider2D>();
            composite.geometryType = CompositeCollider2D.GeometryType.Polygons;
            enemyRoot = Root("BoxEnemies").transform;
            goalRoot = Root("TutorialGoals").transform;
        }

        private GameObject Root(string name)
        {
            var go = new GameObject(name);
            SceneManager.MoveGameObjectToScene(go, scene);
            return go;
        }

        /// <summary>A white box filling x0..x1 and bottom..top.</summary>
        public void Box(float x0, float x1, float top, float bottom)
        {
            var go = new GameObject("Box" + boxes++);
            go.layer = ground;
            go.transform.SetParent(boxRoot, false);
            go.transform.position = new Vector3((x0 + x1) / 2f, (top + bottom) / 2f, 0f);
            var size = new Vector2(x1 - x0, top - bottom);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = white;
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.size = size;
            sr.sortingLayerID = sortingLayer;
            sr.sortingOrder = sortingOrder - 10; // behind Harvey and the enemies
            var col = go.AddComponent<BoxCollider2D>();
            col.size = size;
            col.compositeOperation = Collider2D.CompositeOperation.Merge;
        }

        /// <summary>A box enemy pacing between x0 and x1 on the floor at floorTop. Returns the enemy object.</summary>
        public GameObject Enemy(float x0, float x1, float floorTop)
        {
            var holder = (GameObject)PrefabUtility.InstantiatePrefab(enemyTemplate, scene);
            PrefabUtility.UnpackPrefabInstance(holder, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            holder.name = "BoxEnemy" + enemies++;
            holder.transform.SetParent(enemyRoot, false);
            holder.transform.position = new Vector3(x0, floorTop, 0f);
            holder.transform.localScale = Vector3.one;
            var mt = holder.GetComponent<MovingTiles>();
            var mso = new SerializedObject(mt);
            var a = (Transform)mso.FindProperty("pointA").objectReferenceValue;
            var b = (Transform)mso.FindProperty("pointB").objectReferenceValue;
            var enemy = ((GameObject)mso.FindProperty("objToMove").objectReferenceValue);
            mso.FindProperty("speed").floatValue = 1.5f; // slow and readable
            mso.ApplyModifiedPropertiesWithoutUndo();
            const float scale = 0.75f, half = 0.5f * scale; // 0.75 tiles: a jump clears it comfortably
            float y = floorTop + half;
            a.position = new Vector3(x1, y, 0f);
            b.position = new Vector3(x0, y, 0f);
            enemy.transform.position = new Vector3((x0 + x1) / 2f, y, 0f);
            enemy.transform.localScale = new Vector3(scale, scale, 1f);
            enemy.name = "Box";
            var sr = enemy.GetComponent<SpriteRenderer>();
            sr.sprite = enemySprite;
            sr.color = Color.white;
            sr.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
            sr.sortingLayerID = sortingLayer;
            sr.sortingOrder = sortingOrder - 1;
            var col = enemy.GetComponent<BoxCollider2D>();
            col.size = Vector2.one;
            col.offset = Vector2.zero;
            enemy.GetComponent<Animator>().runtimeAnimatorController = controller;
            var eso = new SerializedObject(enemy.GetComponent<EnemyController>());
            eso.FindProperty("bounceSpeed").floatValue = 5f;
            eso.ApplyModifiedPropertiesWithoutUndo();
            return enemy;
        }

        /// <summary>A trigger goal: Harvey reaches the w x h box whose bottom centre is (x, floorTop).</summary>
        public void Goal(int step, float x, float floorTop, float w, float h)
        {
            var go = new GameObject($"Goal{step}");
            go.transform.SetParent(goalRoot, false);
            go.transform.position = new Vector3(x, floorTop + h / 2f, 0f);
            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(w, h);
            Configure(go.AddComponent<TutorialGoal>(), step, null);
        }

        /// <summary>An enemy goal: every one of these enemies has to be beaten.</summary>
        public void Goal(int step, params GameObject[] targets)
        {
            var go = new GameObject($"Goal{step}_Enemies");
            go.transform.SetParent(goalRoot, false);
            Configure(go.AddComponent<TutorialGoal>(), step, targets);
        }

        private void Configure(TutorialGoal goal, int step, GameObject[] targets)
        {
            var so = new SerializedObject(goal);
            so.FindProperty("step").intValue = step;
            var list = so.FindProperty("enemies");
            list.arraySize = targets != null ? targets.Length : 0;
            for (int i = 0; i < list.arraySize; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = targets[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            goals.Add(goal);
        }

        /// <summary>A plain yellow post marking the end (no collider).</summary>
        public void Finish(float x)
        {
            var go = new GameObject("FinishPost");
            SceneManager.MoveGameObjectToScene(go, scene);
            go.transform.position = new Vector3(x, 1.5f, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = white;
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.size = new Vector2(0.5f, 3f);
            sr.color = FinishYellow;
            sr.sortingLayerID = sortingLayer;
            sr.sortingOrder = sortingOrder - 5;
        }
    }

    // ---------------------------------------------------------------- assets
    private static void ImportArt()
    {
        AssetDatabase.Refresh();
        foreach (string file in new[] { "WhiteBox.png", "BoxEnemy.png" })
        {
            var ti = AssetImporter.GetAtPath(ArtDir + file) as TextureImporter;
            if (ti == null) continue;
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.spritePixelsPerUnit = 16;
            ti.filterMode = FilterMode.Point;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            ti.wrapMode = TextureWrapMode.Clamp;
            var s = new TextureImporterSettings();
            ti.ReadTextureSettings(s);
            s.spriteMeshType = SpriteMeshType.FullRect; // needed for 9-slicing
            s.spriteAlignment = (int)SpriteAlignment.Center;
            ti.SetTextureSettings(s);
            ti.spriteBorder = file == "WhiteBox.png" ? new Vector4(2, 2, 2, 2) : Vector4.zero;
            ti.SaveAndReimport();
        }
    }

    // the box enemy's animator: idle (just the sprite) and a quick fade on defeat. EnemyController needs a DeathAnim
    // parameter to time the defeat by the clip.
    private static RuntimeAnimatorController BoxEnemyController()
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(DeathClipPath);
        if (clip == null)
        {
            clip = new AnimationClip { frameRate = 12 };
            AssetDatabase.CreateAsset(clip, DeathClipPath);
        }
        clip.ClearCurves();
        var binding = EditorCurveBinding.FloatCurve("", typeof(SpriteRenderer), "m_Color.a");
        AnimationUtility.SetEditorCurve(clip, binding, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.08f, 1f), new Keyframe(0.33f, 0f)));
        EditorUtility.SetDirty(clip);

        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller != null) return controller;
        controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        controller.AddParameter("DeathAnim", AnimatorControllerParameterType.Bool);
        var machine = controller.layers[0].stateMachine;
        var idle = machine.AddState("Idle");
        var death = machine.AddState("Death");
        death.motion = clip;
        machine.defaultState = idle;
        var toDeath = idle.AddTransition(death);
        toDeath.hasExitTime = false;
        toDeath.duration = 0f;
        toDeath.AddCondition(AnimatorConditionMode.If, 0f, "DeathAnim");
        AssetDatabase.SaveAssets();
        return controller;
    }

    // a fresh copy of the base level (an existing tutorial scene keeps its GUID/.meta)
    private static void CopyFresh(string from, string to)
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(to) == null)
        {
            if (!AssetDatabase.CopyAsset(from, to)) throw new System.Exception($"Could not copy {from} to {to}");
            return;
        }
        System.IO.File.Copy(from, to, true);
        AssetDatabase.ImportAsset(to, ImportAssetOptions.ForceUpdate);
    }

    // appended at the end, so no other level's build index (and FinishLevel's "next level") moves
    private static bool AddToBuildSettings()
    {
        var list = EditorBuildSettings.scenes.ToList();
        if (list.Any(s => s.path == ScenePath)) return false;
        list.Add(new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = list.ToArray();
        return true;
    }

    // ---------------------------------------------------------------- the main menu's TUTORIAL button
    private static class MainMenuButton
    {
        private const string MenuScene = "Assets/Scenes/MainMenue.unity";

        /// <summary>Adds a TUTORIAL button under ARCADE (a copy of it, calling MenueControl.StartTutorial). False if it is there already.</summary>
        public static bool Ensure()
        {
            Scene menu = EditorSceneManager.OpenScene(MenuScene, OpenSceneMode.Single);
            var buttons = Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (buttons.Any(b => Calls(b, "StartTutorial"))) return false;
            var arcade = buttons.FirstOrDefault(b => Calls(b, "StartArcade"));
            if (arcade == null) throw new System.Exception("No ARCADE button (MenueControl.StartArcade) found in the main menu.");

            // every button below ARCADE moves down one step to make room
            var siblings = arcade.transform.parent.GetComponentsInChildren<UnityEngine.UI.Button>(true)
                .Where(b => b.transform.parent == arcade.transform.parent && b.gameObject.activeSelf).ToList(); // the hidden QUIT button stays put
            var ordered = siblings.OrderByDescending(b => ((RectTransform)b.transform).anchoredPosition.y).ToList();
            int at = ordered.IndexOf(arcade);
            float step = ordered.Count > 1
                ? Mathf.Abs(((RectTransform)ordered[0].transform).anchoredPosition.y - ((RectTransform)ordered[1].transform).anchoredPosition.y)
                : 14f;
            // four buttons in the space of three would reach Harvey's head, so the column moves up half a step
            // and closes up (14 -> 12 apart), keeping it between the title and Harvey
            float top = ((RectTransform)ordered[0].transform).anchoredPosition.y + step / 2f - 1f;
            float newStep = Mathf.Round(step * 6f / 7f);
            for (int i = 0, slot = 0; i < ordered.Count; i++, slot++)
            {
                if (i == at + 1) slot++; // the new button's place
                var rt = (RectTransform)ordered[i].transform;
                rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, top - newStep * slot);
            }

            GameObject copy = Object.Instantiate(arcade.gameObject, arcade.transform.parent);
            copy.name = "TutorialButton";
            copy.transform.SetSiblingIndex(arcade.transform.GetSiblingIndex() + 1);
            ((RectTransform)copy.transform).anchoredPosition = ((RectTransform)arcade.transform).anchoredPosition - new Vector2(0f, newStep);
            foreach (var t in copy.GetComponentsInChildren<TMPro.TMP_Text>(true)) t.text = "TUTORIAL";
            var button = copy.GetComponent<UnityEngine.UI.Button>();
            var control = (MenueControl)arcade.onClick.GetPersistentTarget(0);
            while (button.onClick.GetPersistentEventCount() > 0) UnityEditor.Events.UnityEventTools.RemovePersistentListener(button.onClick, 0);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(button.onClick, control.StartTutorial);

            // keyboard / controller navigation: explicit links follow the new order, automatic ones sort themselves
            var newOrder = siblings.Append(button).OrderByDescending(b => ((RectTransform)b.transform).anchoredPosition.y).ToList();
            if (newOrder.Any(b => b.navigation.mode == UnityEngine.UI.Navigation.Mode.Explicit))
                for (int i = 0; i < newOrder.Count; i++)
                    MenuUI.Link(newOrder[i], newOrder[(i + newOrder.Count - 1) % newOrder.Count], newOrder[(i + 1) % newOrder.Count], null, null);

            EditorSceneManager.MarkSceneDirty(menu);
            EditorSceneManager.SaveScene(menu);
            return true;
        }

        private static bool Calls(UnityEngine.UI.Button b, string method)
        {
            for (int i = 0; i < b.onClick.GetPersistentEventCount(); i++)
                if (b.onClick.GetPersistentMethodName(i) == method) return true;
            return false;
        }
    }
}
