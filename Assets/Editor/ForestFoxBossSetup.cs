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
        // BLAST (2026-10-25): BeamCharge and BeamFire mix rows now, see Mixed below
        ("Rear", "beam", new[] { 2, 3 }, 6f, false),                  // RushStomp: rears up
        ("Stomp", "beam", new[] { 4, 4, 4, 6 }, 8f, false),           // the front paws slam down (frame 0), then stands
        ("Collapse", "charge", new[] { 0, 1 }, 8f, false),            // an opening: slumped low, panting
        ("Rise", "charge", new[] { 1, 0 }, 8f, false),
    };

    // BLAST (2026-10-25): states drawn from more than one row of the sheet: the user's blast sheet (row "blast": 0
    // neutral, 1-3 the orb growing at the mouth, 4-7 the beam from the open mouth, the band itself cut away) joins the
    // older rearing frames. The charge rears back as before, then the orb builds; the fire plays the four blast frames
    // (the hazard draws the beam) and he stands.
    private static readonly (string state, (string row, int frame)[] frames, float fps, bool loop)[] Mixed =
    {
        ("BeamCharge", new[] { ("beam", 1), ("beam", 2), ("beam", 3), ("blast", 1), ("blast", 2), ("blast", 3) }, 8.6f, false), // 0.70 s = beamWarnTime
        ("BeamFire", new[] { ("blast", 4), ("blast", 5), ("blast", 6), ("blast", 7), ("beam", 6) }, 10f, false),
    };

    private const string FxSheet = Dir + "ForestFoxBoss_FX.png"; // BLAST: the beam's Tail (burst), Body (tile) and Head (taper)

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
        Dictionary<string, Sprite> fx = ImportFx(lines); // BLAST:
        AnimatorController ctrl = BuildAnimation(rows);
        BuildBoss(lines, rows, fx, ctrl);
    }

    // BLAST: the beam sprites, sliced as forest_fox_blast.pieces laid them out ("fx name x y w h pivotY")
    private static Dictionary<string, Sprite> ImportFx(string[] lines)
    {
        var result = new Dictionary<string, Sprite>();
        if (!File.Exists(FxSheet)) return result;
        var metas = new List<SpriteMetaData>();
        foreach (string[] f in lines.Where(l => l.StartsWith("fx ")).Select(l => l.Split(' ')))
            metas.Add(new SpriteMetaData
            {
                name = "ForestFoxBoss_Beam" + char.ToUpper(f[1][0]) + f[1].Substring(1),
                rect = new Rect(float.Parse(f[2]), float.Parse(f[3]), float.Parse(f[4]), float.Parse(f[5])),
                alignment = (int)SpriteAlignment.Custom,
                pivot = new Vector2(0f, float.Parse(f[6])), // the left edge, on the band's centre line
            });
        var ti = (TextureImporter)AssetImporter.GetAtPath(FxSheet);
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Multiple;
        ti.spritePixelsPerUnit = Ppu;
        ti.filterMode = FilterMode.Point;
        ti.mipmapEnabled = false;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.alphaIsTransparency = true;
        ti.wrapMode = TextureWrapMode.Clamp;
        var settings = new TextureImporterSettings();
        ti.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect; // the Body is drawn Tiled: tiling needs full-rect sprites
        ti.SetTextureSettings(settings);
#pragma warning disable 618
        ti.spritesheet = metas.ToArray();
#pragma warning restore 618
        ti.SaveAndReimport();
        foreach (Sprite s in AssetDatabase.LoadAllAssetsAtPath(FxSheet).OfType<Sprite>()) result[s.name] = s;
        return result;
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
        var all = States.Select(s => (s.state, frames: s.frames.Select(i => (row: s.row, frame: i)).ToArray(), s.fps, s.loop)).Concat(Mixed); // BLAST:
        foreach (var st in all)
        {
            Sprite[] frames = st.frames.Select(f => rows[f.row][f.frame]).ToArray();
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

    private static void BuildBoss(string[] lines, Dictionary<string, Sprite[]> rows, Dictionary<string, Sprite> fx, AnimatorController ctrl)
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

            // SPIRITBLAST: the sparks and the antler glow in the fox's greens
            SetColours(so, "blastColours", new[] { Hex("99e550"), Hex("6abe30"), Hex("ffffeb"), Hex("d5ffb0") });
            // BLAST (2026-10-25): the beam is the user's own art now (ForestFoxBoss_FX: burst, tile, taper), drawn at
            // one art pixel per game pixel from his open mouth; no recolouring of King Moss's beam any more
            so.FindProperty("blastPalette").arraySize = 0;
            SetSprites(so, "blastTailFrames", fx, "ForestFoxBoss_BeamTail");
            SetSprites(so, "blastBodyFrames", fx, "ForestFoxBoss_BeamBody");
            SetSprites(so, "blastHeadFrames", fx, "ForestFoxBoss_BeamHead");
            so.FindProperty("blastFps").floatValue = 10f;
            so.FindProperty("blastFlowFps").floatValue = 20f;
            if (lines.Any(l => l.StartsWith("mouth ")))
            {
                // the sheet fires from his open mouth (20 px ahead of the pivot, 15.9 up: the beam's height, 1.0,
                // to a sixth of a pixel), so the beam starts there; its height and collider stay as they were
                float[] mouth = Nums(lines, "mouth");
                so.FindProperty("handOffset").vector2Value = new Vector2(Round(mouth[0] * u), BeamHeight);
            }
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

    // BLAST: prefix0, prefix1, ... in order, as far as they go
    private static void SetSprites(SerializedObject so, string field, Dictionary<string, Sprite> sprites, string prefix)
    {
        var frames = new List<Sprite>();
        while (sprites.TryGetValue(prefix + frames.Count, out Sprite s)) frames.Add(s);
        SerializedProperty p = so.FindProperty(field);
        p.arraySize = frames.Count;
        for (int i = 0; i < frames.Count; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
    }

    private static void SetPattern(SerializedObject so, int stage, YokaiBoss.Move[] moves)
    {
        SerializedProperty p = so.FindProperty($"stages.Array.data[{stage}].pattern");
        p.arraySize = moves.Length;
        for (int i = 0; i < moves.Length; i++) p.GetArrayElementAtIndex(i).enumValueIndex = (int)moves[i];
    }
}
