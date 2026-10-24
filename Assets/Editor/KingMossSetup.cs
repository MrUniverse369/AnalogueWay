using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// KINGMOSS (2026-10-23): Tools/HNL/Set Up King Moss. Puts the FlyingGrassBoss sheet on King Mossback (Z4BossGrass's
/// YokaiBoss) and gives him the attacks it draws. Safe to run again (after re-running Tools/Art/flying_grass_boss.py).
///   1. imports KingMoss_Body.png (16 PPU, point, uncompressed) sliced into its seven rows, pivot on the feet line
///      under the robe, and KingMoss_FX.png sliced into the leaf, crescent, orb, boulder and the beam's tail, body
///      (tiled) and head. Sizes come from Tools/Art/flying_grass_boss_layout.txt.
///   2. one clip per state and KingMoss.controller (states Idle, Fly, Cast, Leaf, Slash, BeamCharge, BeamFire,
///      BoulderLift, BoulderThrow, Collapse, Rise; YokaiBoss plays them by name, no transitions)
///   3. in Z4BossGrass (opened additively): the Body gets the Animator and the first idle frame at BodyScale; the
///      Volley's seed becomes the sheet's leaf; crescent, beam and boulder templates are built from the projectile
///      template (same layer, rigidbody and trigger set-up); the hurtbox, hand and boulder anchors are set from the
///      art; and the three stages keep every move they had, with one new attack each (all three by the last stage).
/// The leap's leaf shockwaves, the thorn roots (GroundSpikes), the openings, the chests and the treasure are untouched.
/// </summary>
public static class KingMossSetup
{
    private const string Dir = "Assets/Sprites/Enemies/Bosses/KingMoss/";
    private const string BodySheet = Dir + "KingMoss_Body.png";
    private const string FxSheet = Dir + "KingMoss_FX.png";
    private const string AnimDir = Dir + "Anim/";
    private const string ControllerPath = Dir + "KingMoss.controller";
    private const string Layout = "Tools/Art/flying_grass_boss_layout.txt";
    private const string ScenePath = "Assets/Scenes/Z4BossGrass.unity";
    private const float Ppu = 16f;
    private const float BodyScale = 1.25f;   // he stands about 2.7 units, as tall as the old King Mossback
    private const float Hover = 0.2f;

    // state, row, first frame, last frame (last < first plays backwards), fps, loops
    private static readonly (string state, string row, int from, int to, float fps, bool loop)[] States =
    {
        ("Idle", "idle", 0, 3, 8f, true),
        ("Fly", "fly", 0, 5, 10f, true),
        ("Cast", "leaf", 0, 2, 10f, false),          // the roots: arm raised (no root row on the sheet)
        ("Leaf", "leaf", 0, 7, 14f, false),          // the Volley: 8 frames in its 0.57 s per shot
        ("Slash", "slash", 0, 8, 14f, false),
        ("BeamCharge", "beam", 0, 2, 4f, false),      // the orb grows over the warning
        ("BeamFire", "beam", 3, 7, 10f, false),
        ("BoulderLift", "boulder", 0, 2, 8f, false),
        ("BoulderThrow", "boulder", 3, 7, 10f, false),
        ("Collapse", "hurt", 2, 7, 12f, false),       // an opening: he drops out of the air and lies flat
        ("Rise", "hurt", 7, 2, 12f, false),
    };

    [MenuItem("Tools/HNL/Set Up King Moss")]
    public static void Run()
    {
        if (!File.Exists(BodySheet) || !File.Exists(Layout))
        {
            Debug.LogWarning("KingMossSetup: run Tools/Art/flying_grass_boss.py first.");
            return;
        }
        string[] lines = File.ReadAllLines(Layout);
        Dictionary<string, Sprite[]> rows = ImportBody(lines);
        Dictionary<string, Sprite> fx = ImportFx(lines);
        AnimatorController ctrl = BuildAnimation(rows);
        BuildRig(lines, rows, fx, ctrl);
    }

    private static float[] Nums(string[] lines, string key) =>
        lines.First(l => l.StartsWith(key + " ")).Split(' ').Skip(1).Select(float.Parse).ToArray();

    // ---------------------------------------------------------------- 1. art

    private static TextureImporter Importer(string path)
    {
        var ti = (TextureImporter)AssetImporter.GetAtPath(path);
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Multiple;
        ti.spritePixelsPerUnit = Ppu;
        ti.filterMode = FilterMode.Point;
        ti.mipmapEnabled = false;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.alphaIsTransparency = true;
        var settings = new TextureImporterSettings();
        ti.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect; // the beam body is tiled
        ti.SetTextureSettings(settings);
        return ti;
    }

    private static SpriteMetaData Meta(string name, float x, float y, float w, float h, Vector2 pivot) =>
        new SpriteMetaData { name = name, rect = new Rect(x, y, w, h), alignment = (int)SpriteAlignment.Custom, pivot = pivot };

    private static Dictionary<string, Sprite[]> ImportBody(string[] lines)
    {
        float[] cell = Nums(lines, "cell"), pivot = Nums(lines, "pivot");
        var rowLines = lines.Where(l => l.StartsWith("row ")).Select(l => l.Split(' ')).ToList();
        var metas = new List<SpriteMetaData>();
        foreach (string[] r in rowLines)
        {
            int index = int.Parse(r[2]), count = int.Parse(r[3]);
            for (int i = 0; i < count; i++) // the first row is at the top of the texture
                metas.Add(Meta("KingMoss_" + r[1] + "_" + i, i * cell[0], (rowLines.Count - 1 - index) * cell[1], cell[0], cell[1],
                    new Vector2(pivot[0] / cell[0], pivot[1] / cell[1])));
        }
        TextureImporter ti = Importer(BodySheet);
#pragma warning disable 618 // still the simplest way to slice from code
        ti.spritesheet = metas.ToArray();
#pragma warning restore 618
        ti.SaveAndReimport();
        Sprite[] all = AssetDatabase.LoadAllAssetsAtPath(BodySheet).OfType<Sprite>().ToArray();
        return rowLines.ToDictionary(r => r[1], r => Enumerable.Range(0, int.Parse(r[3]))
            .Select(i => all.First(s => s.name == "KingMoss_" + r[1] + "_" + i)).ToArray());
    }

    private static Dictionary<string, Sprite> ImportFx(string[] lines)
    {
        var metas = new List<SpriteMetaData>();
        foreach (string[] f in lines.Where(l => l.StartsWith("fx ")).Select(l => l.Split(' ')))
        {
            float x = float.Parse(f[2]), y = float.Parse(f[3]), w = float.Parse(f[4]), h = float.Parse(f[5]);
            if (f[1] != "beam")
            {
                metas.Add(Meta("KingMoss_" + f[1], x, y, w, h, new Vector2(0.5f, 0.5f)));
                continue;
            }
            // the beam points right: a rounded tail at the hand, a straight run that tiles, the burst at the far end
            // (rows 8-13 of its 21 are the beam itself; the tiled run is a stretch whose top and bottom rows are whole)
            metas.Add(Meta("KingMoss_BeamTail", x, y + 7, 18, 8, new Vector2(0f, 0.5f)));
            metas.Add(Meta("KingMoss_BeamBody", x + 18, y + 8, 11, 6, new Vector2(0f, 0.5f)));
            metas.Add(Meta("KingMoss_BeamHead", x + 30, y, w - 30, h, new Vector2(0f, 0.5f)));
        }
        TextureImporter ti = Importer(FxSheet);
#pragma warning disable 618
        ti.spritesheet = metas.ToArray();
#pragma warning restore 618
        ti.SaveAndReimport();
        return AssetDatabase.LoadAllAssetsAtPath(FxSheet).OfType<Sprite>().ToDictionary(s => s.name.Substring("KingMoss_".Length), s => s);
    }

    // ---------------------------------------------------------------- 2. clips and controller

    private static AnimatorController BuildAnimation(Dictionary<string, Sprite[]> rows)
    {
        if (!AssetDatabase.IsValidFolder(AnimDir.TrimEnd('/'))) AssetDatabase.CreateFolder(Dir.TrimEnd('/'), "Anim");
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath)
                   ?? AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        AnimatorStateMachine sm = ctrl.layers[0].stateMachine;
        int n = 0;
        foreach (var st in States)
        {
            Sprite[] row = rows[st.row];
            int step = st.to >= st.from ? 1 : -1;
            var frames = new List<Sprite>();
            for (int i = st.from; ; i += step)
            {
                frames.Add(row[i]);
                if (i == st.to) break;
            }
            string path = AnimDir + "KingMoss_" + st.state + ".anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null)
            {
                clip = new AnimationClip();
                AssetDatabase.CreateAsset(clip, path);
            }
            clip.frameRate = st.fps;
            // a loop needs no closing key (Unity holds the last key for a frame); a one-shot gets one, so its length
            // is every frame's full time and it rests on its last frame
            int keyCount = frames.Count + (st.loop ? 0 : 1);
            var keys = new ObjectReferenceKeyframe[keyCount];
            for (int i = 0; i < keyCount; i++)
                keys[i] = new ObjectReferenceKeyframe { time = i / st.fps, value = frames[Mathf.Min(i, frames.Count - 1)] };
            AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), keys);
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = st.loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);

            AnimatorState state = sm.states.Select(s => s.state).FirstOrDefault(s => s.name == st.state)
                                  ?? sm.AddState(st.state, new Vector3(300 + 220 * (n % 3), 60 * (n / 3), 0));
            state.motion = clip;
            state.writeDefaultValues = false;
            if (st.state == "Idle") sm.defaultState = state;
            n++;
        }
        EditorUtility.SetDirty(ctrl);
        AssetDatabase.SaveAssets();
        return ctrl;
    }

    // ---------------------------------------------------------------- 3. the boss in Z4BossGrass

    private static void BuildRig(string[] lines, Dictionary<string, Sprite[]> rows, Dictionary<string, Sprite> fx, AnimatorController ctrl)
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        YokaiBoss boss = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<YokaiBoss>(true)).First(b => b.name == "KingMossback");
        var so = new SerializedObject(boss);

        // the body: animator first (swapping it rebinds the renderer), then the sprite and the scale
        var body = (Transform)so.FindProperty("body").objectReferenceValue;
        var bodySprite = (SpriteRenderer)so.FindProperty("bodySprite").objectReferenceValue;
        Animator anim = body.GetComponent<Animator>();
        if (anim == null) anim = body.gameObject.AddComponent<Animator>();
        anim.runtimeAnimatorController = ctrl;
        bodySprite.sprite = rows["idle"][0];
        body.localScale = new Vector3(BodyScale, BodyScale, 1f);
        body.localPosition = new Vector3(0f, Hover, 0f);
        so.FindProperty("bodyAnimator").objectReferenceValue = anim;
        so.FindProperty("spriteFacesRight").boolValue = false; // the sheet faces left
        so.FindProperty("hoverHeight").floatValue = Hover;

        // sizes and anchors from the art (art pixels -> units at BodyScale)
        float u = BodyScale / Ppu;
        float[] hand = Nums(lines, "hand"), rock = Nums(lines, "boulder");
        float stand = Nums(lines, "stand")[0], robe = Nums(lines, "robe")[0];
        so.FindProperty("standingHeight").floatValue = Mathf.Round((stand - 4f) * u * 100f) / 100f + Hover; // the raised wing tips don't count
        so.FindProperty("slumpedHeight").floatValue = 1f;   // the top of his back as he lies flat (a stomp lands there)
        so.FindProperty("hurtboxWidth").floatValue = Mathf.Round(robe * u * 1.4f * 100f) / 100f; // robe and arms, not the wings
        so.FindProperty("handOffset").vector2Value = new Vector2(hand[0] * u, hand[1] * u);
        so.FindProperty("boulderHold").vector2Value = new Vector2(rock[0] * u, rock[1] * u + Hover);
        // he lands to fire, so the beam leaves the orb's lower half: through a standing Harvey's head, under a jump
        so.FindProperty("beamHeight").floatValue = Mathf.Round((hand[1] * u - 0.2f) * 100f) / 100f;

        // hazards: the Volley's seed becomes the sheet's leaf; the new ones are built from the projectile template
        var projectile = (BossHazard)so.FindProperty("projectileTemplate").objectReferenceValue;
        SetVisual(projectile, fx["leaf"]);
        so.FindProperty("crescentTemplate").objectReferenceValue = Template(projectile, "CrescentTemplate", fx["crescent"], 0.6f, 0.7f, 1f);
        so.FindProperty("boulderTemplate").objectReferenceValue = Template(projectile, "BoulderTemplate", fx["boulder"], 0.8f, 0.8f, BodyScale);
        so.FindProperty("beamTemplate").objectReferenceValue = BeamTemplate(projectile, fx);

        // the stages: every move they had, plus the sheet's attacks
        SetPattern(so, 0, new[] { YokaiBoss.Move.LeapSlam, YokaiBoss.Move.WindSlash, YokaiBoss.Move.LeapSlam, YokaiBoss.Move.Opening });
        SetPattern(so, 1, new[] { YokaiBoss.Move.GroundSpikes, YokaiBoss.Move.BoulderThrow, YokaiBoss.Move.LeapSlam, YokaiBoss.Move.WindSlash, YokaiBoss.Move.Opening });
        SetPattern(so, 2, new[] { YokaiBoss.Move.GroundSpikes, YokaiBoss.Move.SpiritBeam, YokaiBoss.Move.Volley, YokaiBoss.Move.BoulderThrow, YokaiBoss.Move.LeapSlam, YokaiBoss.Move.Opening });
        SetCounts(so, 0, 1, 1, 1);
        SetCounts(so, 1, 2, 1, 1);
        SetCounts(so, 2, 2, 1, 2);
        // a perfect fight is a few seconds longer with the new attacks, so the best chest tier moves with it
        so.FindProperty("chestsFastTime").floatValue = 48f;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        if (EditorSceneManager.sceneCount > 1) EditorSceneManager.CloseScene(scene, true);
        Debug.Log($"KingMossSetup: {rows.Values.Sum(r => r.Length)} frames, {States.Length} states; King Mossback rebuilt in {ScenePath}.");
    }

    private static void SetPattern(SerializedObject so, int stage, YokaiBoss.Move[] moves)
    {
        SerializedProperty p = so.FindProperty($"stages.Array.data[{stage}].pattern");
        p.arraySize = moves.Length;
        for (int i = 0; i < moves.Length; i++) p.GetArrayElementAtIndex(i).enumValueIndex = (int)moves[i];
    }

    private static void SetCounts(SerializedObject so, int stage, int slashes, int beams, int boulders)
    {
        so.FindProperty($"stages.Array.data[{stage}].slashes").intValue = slashes;
        so.FindProperty($"stages.Array.data[{stage}].beams").intValue = beams;
        so.FindProperty($"stages.Array.data[{stage}].boulders").intValue = boulders;
    }

    private static void SetVisual(BossHazard h, Sprite sprite)
    {
        SpriteRenderer sr = h.GetComponentInChildren<SpriteRenderer>(true);
        sr.sprite = sprite;
    }

    // a copy of the projectile template (same layer, rigidbody, trigger and sorting) with another sprite and box
    private static BossHazard Template(BossHazard from, string name, Sprite sprite, float boxW, float boxH, float scale)
    {
        Transform parent = from.transform.parent;
        Transform old = parent != null ? parent.Find(name) : null;
        if (old != null) Object.DestroyImmediate(old.gameObject);
        BossHazard h = Object.Instantiate(from, from.transform.position, Quaternion.identity, parent);
        h.name = name;
        SpriteRenderer sr = h.GetComponentInChildren<SpriteRenderer>(true);
        sr.sprite = sprite;
        sr.transform.localScale = new Vector3(scale, scale, 1f);
        var box = h.GetComponent<BoxCollider2D>();
        Vector2 size = sprite.bounds.size * scale;
        box.size = new Vector2(size.x * boxW, size.y * boxH);
        box.offset = Vector2.zero;
        h.gameObject.SetActive(false);
        return h;
    }

    private static BossHazard BeamTemplate(BossHazard from, Dictionary<string, Sprite> fx)
    {
        BossHazard h = Template(from, "BeamTemplate", fx["BeamTail"], 1f, 1f, 1f);
        Transform visual = h.GetComponentInChildren<SpriteRenderer>(true).transform;
        SpriteRenderer tail = visual.GetComponent<SpriteRenderer>();
        visual.name = "Tail";
        visual.localPosition = Vector3.zero;
        SpriteRenderer Part(string partName, Sprite sprite)
        {
            SpriteRenderer sr = Object.Instantiate(tail, h.transform);
            sr.name = partName;
            sr.sprite = sprite;
            return sr;
        }
        SpriteRenderer run = Part("Body", fx["BeamBody"]);
        run.drawMode = SpriteDrawMode.Tiled;
        run.tileMode = SpriteTileMode.Continuous;
        run.size = new Vector2(4f, fx["BeamBody"].bounds.size.y);
        Part("Head", fx["BeamHead"]);
        return h;
    }
}
