using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// FOXBOSS (2026-10-25): Tools/HNL/Set Up Forest Fox Boss. Puts the user's Forest Spirit Fox boss sheets on the Grass
/// boss (MotherBoss.prefab, the YokaiBoss in Z4BossGrass, until now King Mossback) and gives it the fox's three
/// attacks. Safe to run again (after re-running Tools/Art/forest_fox_boss.py).
///   1. imports ForestFoxBoss_Body.png (16 PPU, point, uncompressed) sliced into its three rows (idle, charge, beam),
///      pivot on the feet line at the middle of the torso. Sizes come from Tools/Art/forest_fox_boss_layout.txt.
///   2. one clip per state and ForestFoxBoss.controller (states Idle, Brace, Charge, Recover, BeamCharge, BeamFire,
///      Rear, Stomp, Collapse, Rise; YokaiBoss plays them by name, no transitions)
///   3. MotherBoss.prefab: the Body gets the Animator and the first idle frame at scale 1 (one art pixel per game
///      pixel) on the floor; heights, hurtbox width, the beam's origin, the antler glow and the stomp's reach are set
///      from the art; the three stages fight with Charge, SpiritBeam and RushStomp, each ending in an opening.
/// The scene's templates (the leaf shockwave the stomp sends, the beam), the arena, the pips, the chests and the
/// treasure are untouched; so are King Moss's sheet and controller (no longer used by the prefab).
/// </summary>
public static class ForestFoxBossSetup
{
    private const string Dir = "Assets/Sprites/Enemies/Grass/ForestSpiritFox/";
    private const string BodySheet = Dir + "ForestFoxBoss_Body.png";
    private const string AnimDir = Dir + "BossAnim/";
    private const string ControllerPath = Dir + "ForestFoxBoss.controller";
    private const string Layout = "Tools/Art/forest_fox_boss_layout.txt";
    private const string PrefabPath = "Assets/Prefabs/Enemy/Grass/MotherBoss.prefab";
    private const float Ppu = 16f;
    private const float BeamHeight = 1f; // a standing Harvey's head is in it, a jump clears it (the glowing antlers reach down to it)
    private const float BodyHeight = 1.75f; // JUMPBOOST: 28 px, the top of his head and shoulders in the idle frames

    // state, row, frames (in order; repeats hold a frame), fps, loops
    private static readonly (string state, string row, int[] frames, float fps, bool loop)[] States =
    {
        ("Idle", "idle", new[] { 0, 1, 2, 3, 4, 5 }, 8f, true),       // crouched, breathing
        ("Brace", "charge", new[] { 0, 1 }, 6f, false),               // the charge's warning: down low, braced
        ("Charge", "charge", new[] { 2, 3, 4, 5, 6 }, 12f, true),     // the burst and the low run (Charge, RushStomp's sprint)
        ("Recover", "charge", new[] { 1, 0 }, 8f, false),             // skids to a stop, crouched, and stands
        ("BeamCharge", "beam", new[] { 1, 2, 3 }, 5f, false),         // snarl, antlers lit, rears its head back
        ("BeamFire", "beam", new[] { 5, 5, 5, 5, 6 }, 10f, false),    // the roar with the antlers blazing, then stands
        ("Rear", "beam", new[] { 2, 3 }, 6f, false),                  // RushStomp: rears up
        ("Stomp", "beam", new[] { 4, 4, 4, 6 }, 8f, false),           // the front paws slam down (frame 0), then stands
        ("Collapse", "charge", new[] { 0, 1 }, 8f, false),            // an opening: slumped low, panting
        ("Rise", "charge", new[] { 1, 0 }, 8f, false),
    };

    [MenuItem("Tools/HNL/Set Up Forest Fox Boss")]
    public static void Run()
    {
        if (!File.Exists(BodySheet) || !File.Exists(Layout))
        {
            Debug.LogWarning("ForestFoxBossSetup: run Tools/Art/forest_fox_boss.py first.");
            return;
        }
        string[] lines = File.ReadAllLines(Layout);
        Dictionary<string, Sprite[]> rows = ImportBody(lines);
        AnimatorController ctrl = BuildAnimation(rows);
        BuildBoss(lines, rows, ctrl);
    }

    private static float[] Nums(string[] lines, string key) =>
        lines.First(l => l.StartsWith(key + " ")).Split(' ').Skip(1).Select(float.Parse).ToArray();

    // ---------------------------------------------------------------- 1. art

    private static Dictionary<string, Sprite[]> ImportBody(string[] lines)
    {
        float[] cell = Nums(lines, "cell"), pivot = Nums(lines, "pivot");
        var rowLines = lines.Where(l => l.StartsWith("row ")).Select(l => l.Split(' ')).ToList();
        var metas = new List<SpriteMetaData>();
        foreach (string[] r in rowLines)
        {
            int index = int.Parse(r[2]), count = int.Parse(r[3]);
            for (int i = 0; i < count; i++) // the first row is at the top of the texture
                metas.Add(new SpriteMetaData
                {
                    name = "ForestFoxBoss_" + r[1] + "_" + i,
                    rect = new Rect(i * cell[0], (rowLines.Count - 1 - index) * cell[1], cell[0], cell[1]),
                    alignment = (int)SpriteAlignment.Custom,
                    pivot = new Vector2(pivot[0] / cell[0], pivot[1] / cell[1]),
                });
        }
        var ti = (TextureImporter)AssetImporter.GetAtPath(BodySheet);
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Multiple;
        ti.spritePixelsPerUnit = Ppu;
        ti.filterMode = FilterMode.Point;
        ti.mipmapEnabled = false;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.isReadable = true; // SPIRITBLAST: SpiritBlastFX.TryLitPoint reads the lit antlers off each frame (458 KB, once)
        ti.alphaIsTransparency = true;
#pragma warning disable 618 // still the simplest way to slice from code
        ti.spritesheet = metas.ToArray();
#pragma warning restore 618
        ti.SaveAndReimport();
        Sprite[] all = AssetDatabase.LoadAllAssetsAtPath(BodySheet).OfType<Sprite>().ToArray();
        return rowLines.ToDictionary(r => r[1], r => Enumerable.Range(0, int.Parse(r[3]))
            .Select(i => all.First(s => s.name == "ForestFoxBoss_" + r[1] + "_" + i)).ToArray());
    }

    // ---------------------------------------------------------------- 2. clips and controller

    private static AnimatorController BuildAnimation(Dictionary<string, Sprite[]> rows)
    {
        if (!AssetDatabase.IsValidFolder(AnimDir.TrimEnd('/'))) AssetDatabase.CreateFolder(Dir.TrimEnd('/'), "BossAnim");
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath)
                   ?? AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        AnimatorStateMachine sm = ctrl.layers[0].stateMachine;
        int n = 0;
        foreach (var st in States)
        {
            Sprite[] frames = st.frames.Select(i => rows[st.row][i]).ToArray();
            string path = AnimDir + "ForestFoxBoss_" + st.state + ".anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null)
            {
                clip = new AnimationClip();
                AssetDatabase.CreateAsset(clip, path);
            }
            clip.frameRate = st.fps;
            // a loop needs no closing key; a one-shot gets one, so its length is every frame's full time and it rests
            // on its last frame (as KingMossSetup)
            int keyCount = frames.Length + (st.loop ? 0 : 1);
            var keys = new ObjectReferenceKeyframe[keyCount];
            for (int i = 0; i < keyCount; i++)
                keys[i] = new ObjectReferenceKeyframe { time = i / st.fps, value = frames[Mathf.Min(i, frames.Length - 1)] };
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

    // ---------------------------------------------------------------- 3. the boss prefab

    private static void BuildBoss(string[] lines, Dictionary<string, Sprite[]> rows, AnimatorController ctrl)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            YokaiBoss boss = root.GetComponentInChildren<YokaiBoss>(true);
            var so = new SerializedObject(boss);

            // the body: animator first (swapping it rebinds the renderer), then the sprite; one art pixel per game
            // pixel, standing on the floor (King Moss hovered)
            var body = (Transform)so.FindProperty("body").objectReferenceValue;
            var bodySprite = (SpriteRenderer)so.FindProperty("bodySprite").objectReferenceValue;
            Animator anim = body.GetComponent<Animator>();
            if (anim == null) anim = body.gameObject.AddComponent<Animator>();
            anim.runtimeAnimatorController = ctrl;
            bodySprite.sprite = rows["idle"][0];
            body.localScale = Vector3.one;
            body.localPosition = Vector3.zero;
            so.FindProperty("bodyAnimator").objectReferenceValue = anim;
            so.FindProperty("spriteFacesRight").boolValue = true; // the sheets face right
            so.FindProperty("hoverHeight").floatValue = 0f;

            // sizes and anchors from the art (game pixels -> units)
            float u = 1f / Ppu;
            float[] antler = Nums(lines, "antler");
            float stance = Nums(lines, "stance")[0], glowLow = Nums(lines, "glowlow")[0];
            float nose = Nums(lines, "nose")[0];
            // JUMPBOOST: the head and shoulders; the antler tips and the raised tails (up to 'stand') are art only, so Jump
            // Power II (peak 2.6) clears him with room to spare. A normal jump (1.16) still can't reach his back.
            so.FindProperty("standingHeight").floatValue = BodyHeight;
            so.FindProperty("chargingHeight").floatValue = 0.8f;                   // the low charge: jump it
            so.FindProperty("slumpedHeight").floatValue = 1f;                      // slumped low: stomp his back
            so.FindProperty("hurtboxWidth").floatValue = Round(stance * u);       // paw to paw: the torso (not the tails or the snout)
            so.FindProperty("handOffset").vector2Value = new Vector2(Round(antler[0] * u), BeamHeight); // the beam leaves the antlers' front
            so.FindProperty("beamHeight").floatValue = BeamHeight;
            so.FindProperty("beamGlowOffset").vector2Value = new Vector2(Round(antler[0] * u * 0.8f), Round((antler[1] + glowLow) * 0.5f * u));
            so.FindProperty("stompReach").floatValue = Round(stance * 0.5f * u);   // where the front paws come down
            so.FindProperty("limitInset").floatValue = Round(Mathf.Max(0f, nose * u - 0.9f)); // his nose stops short of the walls
            so.FindProperty("rushDistance").floatValue = 3.5f;
            so.FindProperty("rushStopDistance").floatValue = Round(stance * 0.5f * u + 1.2f); // the paws land just short of Harvey
            so.FindProperty("stompImpactFrame").intValue = 0;

            // the stages: the fox's three attacks, building up; each ends in an opening (stomp or dash him then)
            SetPattern(so, 0, new[] { YokaiBoss.Move.Charge, YokaiBoss.Move.RushStomp, YokaiBoss.Move.Opening });
            SetPattern(so, 1, new[] { YokaiBoss.Move.Charge, YokaiBoss.Move.SpiritBeam, YokaiBoss.Move.RushStomp, YokaiBoss.Move.Opening });
            SetPattern(so, 2, new[] { YokaiBoss.Move.SpiritBeam, YokaiBoss.Move.Charge, YokaiBoss.Move.RushStomp, YokaiBoss.Move.SpiritBeam, YokaiBoss.Move.Opening });
            so.FindProperty("stages.Array.data[0].beams").intValue = 1;
            so.FindProperty("stages.Array.data[1].beams").intValue = 1;
            so.FindProperty("stages.Array.data[2].beams").intValue = 2;

            // SPIRITBLAST: the beam (King Moss's beam art recoloured by brightness), its sparks and the antler glow in
            // the fox's greens: the grass palette's 37946e / 6abe30 / 99e550 with a pale core
            SetColours(so, "blastPalette", new[] { Hex("37946e"), Hex("6abe30"), Hex("eefcd0") });
            SetColours(so, "blastColours", new[] { Hex("99e550"), Hex("6abe30"), Hex("ffffeb"), Hex("d5ffb0") });
            so.FindProperty("antlerGlow").colorValue = new Color(0.6f, 0.9f, 0.31f, 0.35f);
            so.FindProperty("chargeMoteRate").floatValue = 50f;
            so.FindProperty("beamMoteRate").floatValue = 70f;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Debug.Log($"ForestFoxBossSetup: {rows.Values.Sum(r => r.Length)} frames, {States.Length} states; {PrefabPath} is the Forest Spirit Fox.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static float Round(float v) => Mathf.Round(v * 100f) / 100f;

    // SPIRITBLAST:
    private static Color Hex(string hex) => ColorUtility.TryParseHtmlString("#" + hex, out Color c) ? c : Color.magenta;

    private static void SetColours(SerializedObject so, string field, Color[] colours)
    {
        SerializedProperty p = so.FindProperty(field);
        p.arraySize = colours.Length;
        for (int i = 0; i < colours.Length; i++) p.GetArrayElementAtIndex(i).colorValue = colours[i];
    }

    private static void SetPattern(SerializedObject so, int stage, YokaiBoss.Move[] moves)
    {
        SerializedProperty p = so.FindProperty($"stages.Array.data[{stage}].pattern");
        p.arraySize = moves.Length;
        for (int i = 0; i < moves.Length; i++) p.GetArrayElementAtIndex(i).enumValueIndex = (int)moves[i];
    }
}
