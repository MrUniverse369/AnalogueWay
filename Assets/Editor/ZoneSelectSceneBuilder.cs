using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ZONEMAP: rebuilds Assets/Scenes/ZoneSelect.unity from Tools/Art/zone_map_layout.txt (written by
/// Tools/Art/zone_map.py together with the map art), so the nodes and the walkable paths always match the picture.
/// Edit the Nodes table below to rename levels or point a node at a different scene.
/// </summary>
public static class ZoneSelectSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/ZoneSelect.unity";
    private const string Art = "Assets/Sprites/ZoneMap/";
    private const string Layout = "Tools/Art/zone_map_layout.txt";
    private const string MenuMusicGuid = "9669960e415ea604d81fb0784634df79";

    private static readonly Color Tomb = Hex("#66ffe3"), Sand = Hex("#ffe478"), Snow = Hex("#eb564b"), Grass = Hex("#8fde5d");

    // layout id -> scene, zone name, level name, kind, colour, gem (null = the kind's own icon)
    private static readonly Dictionary<string, (string scene, string zone, string level, ZoneNodeKind kind, Color colour, string gem)> Nodes =
        new Dictionary<string, (string, string, string, ZoneNodeKind, Color, string)>
        {
            { "Z1A1", ("Z1A1Tomb", "The Old Tomb", "Act 1", ZoneNodeKind.Level, Tomb, "Blue") },
            { "Z1A2", ("Z1A2Tomb", "The Old Tomb", "Act 2", ZoneNodeKind.Level, Tomb, "Blue") },
            { "Z1A3", ("Z1A3Tomb", "The Old Tomb", "Act 3", ZoneNodeKind.Level, Tomb, "Blue") },
            { "Z1A4", ("Z1A4Tomb", "The Old Tomb", "Act 4", ZoneNodeKind.Level, Tomb, "Blue") },
            { "Z1A5", ("Z1A5Tomb", "The Old Tomb", "Act 5", ZoneNodeKind.Level, Tomb, "Blue") },
            { "Z1Village", ("Z1TombVillage", "The Old Tomb", "Act 6: Lantern Camp", ZoneNodeKind.Village, Tomb, null) }, // VILLAGES:
            { "Z1Boss", ("Z1BossTomb", "The Old Tomb", "Boss: The Grave Warden", ZoneNodeKind.Boss, Tomb, null) },
            { "Z2A1", ("Z2A1Sandy", "Sandy Dunes", "Act 1", ZoneNodeKind.Level, Sand, "Gold") },
            { "Z2A2", ("Z2A2Sandy", "Sandy Dunes", "Act 2", ZoneNodeKind.Level, Sand, "Gold") },
            { "Z2A3", ("Z2A3Sandy", "Sandy Dunes", "Act 3", ZoneNodeKind.Level, Sand, "Gold") },
            { "Z2A4", ("Z2A4Sandy", "Sandy Dunes", "Act 4", ZoneNodeKind.Level, Sand, "Gold") },
            { "Z2A5", ("Z2A5Sandy", "Sandy Dunes", "Act 5", ZoneNodeKind.Level, Sand, "Gold") },
            { "Z2Village", ("Z2SandyVillage", "Sandy Dunes", "Act 6: Oasis Bazaar", ZoneNodeKind.Village, Sand, null) }, // VILLAGES:
            { "Z2Boss", ("Z2BossSandy", "Sandy Dunes", "Boss: The Sandscale Scarab", ZoneNodeKind.Boss, Sand, null) },
            { "Z3A1", ("Z3A1Japan", "Sunlit Shrine Road", "Act 1", ZoneNodeKind.Level, Snow, "Red") },
            { "Z3A2", ("Z3A2Japan", "Sunlit Shrine Road", "Act 2", ZoneNodeKind.Level, Snow, "Red") },
            { "Z3A3", ("Z3A3Japan", "Sunlit Shrine Road", "Act 3", ZoneNodeKind.Level, Snow, "Red") },
            { "Z3A4", ("Z3A4Japan", "Sunlit Shrine Road", "Act 4", ZoneNodeKind.Level, Snow, "Red") },
            { "Village", ("Z3JapanVillage", "Snow Temple Pass", "Act 5: Mountain Village", ZoneNodeKind.Village, Snow, null) },
            { "Boss", ("Z3BossBattle", "Snow Temple Pass", "Boss: The Yokai Warrior", ZoneNodeKind.Boss, Snow, null) },
            { "Z4A1", ("Z4A6GrassGreen", "Grassy Meadow", "Act 1", ZoneNodeKind.Level, Grass, "Green") },
            { "Z4A2", ("Z4A7GrassGreen", "Grassy Meadow", "Act 2", ZoneNodeKind.Level, Grass, "Green") },
            { "Z4A3", ("Z4A8GrassGreen", "Grassy Meadow", "Act 3", ZoneNodeKind.Level, Grass, "Green") },
            { "Z4A4", ("Z4A9GrassGreen", "Grassy Meadow", "Act 4", ZoneNodeKind.Level, Grass, "Green") },
            { "Z4A5", ("Z4A10GrassGreen", "Grassy Meadow", "Act 5", ZoneNodeKind.Level, Grass, "Green") },
            { "Z4Village", ("Z4GrassVillage", "Grassy Meadow", "Act 6: Meadowbrook", ZoneNodeKind.Village, Grass, null) }, // VILLAGES:
            { "Z4Boss", ("Z4BossGrass", "Grassy Meadow", "Boss: King Mossback", ZoneNodeKind.Boss, Grass, null) },
            { "Win", ("Win", "Journey's End", "Home at Last", ZoneNodeKind.Goal, Grass, null) },
        };

    private static Material spriteMat;
    private static TMP_FontAsset font;

    [MenuItem("Tools/HNL/Rebuild Zone Select Scene")]
    private static void RebuildFromMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Debug.Log(Build());
    }

    public static string Build()
    {
        ImportArt();
        var (nodePos, routes, gatePos) = ReadLayout();
        spriteMat = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/PerfectDOSVGA437 SDF.asset");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
        var cam = camGo.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 5.625f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Hex("#314358");
        camGo.transform.position = new Vector3(0, 0, -10);
        camGo.AddComponent<AudioListener>();

        SpriteObject("Map", S("ZoneMap"), Vector3.zero, 0, null);
        var root = new GameObject("ZoneSelect");
        var ctrl = root.AddComponent<ZoneSelectMap>();

        var nodesRoot = new GameObject("Nodes").transform;
        nodesRoot.SetParent(root.transform);
        var nodes = new List<ZoneMapNode>();
        for (int i = 0; i < nodePos.Count; i++)
        {
            var d = Nodes[nodePos[i].id];
            Sprite icon = d.kind == ZoneNodeKind.Boss ? S("Node_Boss") : d.kind == ZoneNodeKind.Village ? S("Node_Village")
                        : d.kind == ZoneNodeKind.Goal ? S("Node_Goal") : S("Node_Open_" + d.gem);
            var go = SpriteObject(d.scene, icon, nodePos[i].pos, 2, nodesRoot);
            var n = go.AddComponent<ZoneMapNode>();
            n.sceneName = d.scene;
            n.zoneName = d.zone;
            n.levelName = d.level;
            n.kind = d.kind;
            n.zoneColour = d.colour;
            if (d.gem != null) n.openIcon = S("Node_Open_" + d.gem);
            n.bendsToNext = routes.TryGetValue(i, out var r) ? r : new Vector2[0];
            nodes.Add(n);
        }

        var cursor = SpriteObject("SelectRing", S("Marker"), nodes[0].transform.position, 1, root.transform).GetComponent<SpriteRenderer>();

        // Harvey uses the in-game Player's idle and run frames
        var anim = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab").GetComponentInChildren<Animator>().runtimeAnimatorController;
        Sprite[] Frames(string clipName)
        {
            var clip = anim.animationClips.First(x => x.name == clipName);
            var binding = AnimationUtility.GetObjectReferenceCurveBindings(clip).First(x => x.type == typeof(SpriteRenderer));
            return AnimationUtility.GetObjectReferenceCurve(clip, binding).Select(k => (Sprite)k.value).ToArray();
        }
        var idle = Frames("PlayerIdle");
        var run = Frames("PlayerRun");
        var player = new GameObject("Harvey").transform;
        player.SetParent(root.transform);
        player.position = nodes[0].transform.position;
        var body = SpriteObject("Sprite", idle[0], Vector3.zero, 5, player);
        body.transform.localScale = new Vector3(0.5f, 0.5f, 1f);
        body.transform.localPosition = new Vector3(0, -idle[0].bounds.min.y * 0.5f - 0.25f, 0);

        // torii Harvey walks through: above Harvey (order 5), so the beams pass over him between the pillars
        var gates = new List<Transform>();
        var gatesRoot = new GameObject("Gates").transform;
        gatesRoot.SetParent(root.transform);
        foreach (var pos in gatePos) gates.Add(SpriteObject("ToriiGate", S("ToriiGate"), pos, 6, gatesRoot).transform);

        var cloudsRoot = new GameObject("Clouds").transform;
        cloudsRoot.SetParent(root.transform);
        var clouds = new List<Transform>();
        foreach (var (name, pos) in new[] { ("Cloud_A", new Vector3(-3f, 3.9f, 0)), ("Cloud_B", new Vector3(6.5f, -3.4f, 0)), ("Cloud_A", new Vector3(-10f, -1.2f, 0)) })
        {
            var cloud = SpriteObject("Cloud", S(name), pos, 8, cloudsRoot);
            cloud.GetComponent<SpriteRenderer>().color = new Color(1, 1, 1, 0.85f);
            clouds.Add(cloud.transform);
        }

        // UI: a HUD-style label over the selected node, coin and controls plates, and the fade
        var canvasGo = new GameObject("Map UI", typeof(RectTransform));
        canvasGo.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(320, 180);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1f;
        scaler.referencePixelsPerUnit = 16;
        var canvasRt = (RectTransform)canvasGo.transform;
        var frame = S("LabelFrame");

        var label = Rect("NodeLabel", canvasRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(80, 24));
        Img(label, frame);
        var zoneText = Text("ZoneName", label, new Vector2(0, 0.5f), Vector2.one, new Vector2(0, -1), new Vector2(0, -3), 7, Hex("#ffe478"), "THE OLD TOMB"); // UI: title role (Yellow)
        var levelText = Text("LevelName", label, Vector2.zero, new Vector2(1, 0.5f), new Vector2(0, 4), new Vector2(0, 1), 7, Hex("#ffffeb"), "ACT 1"); // UI: Cream, same size as the zone name

        var coinBox = Rect("Coins", canvasRt, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(6, -6), new Vector2(70, 12)); // UI: width fits the text at runtime
        Img(coinBox, frame);
        var coins = Text("CoinsText", coinBox, Vector2.zero, Vector2.one, new Vector2(0, 2), Vector2.zero, 6, Hex("#ffffeb"), "COINS 0"); // UI: body size

        var controls = Rect("ControlsPlate", canvasRt, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-6, 6), new Vector2(116, 12));
        Img(controls, frame);
        Text("Controls", controls, Vector2.zero, Vector2.one, new Vector2(0, 2), Vector2.zero, 5, Hex("#a59bd0"), "CROSS/ENTER: PLAY   CIRCLE/ESC: MENU"); // UI: hint style (Muted, small), same wording as the menus

        var fadeRt = Rect("Fade", canvasRt, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        var fade = fadeRt.gameObject.AddComponent<Image>();
        fade.color = Hex("#14202b");

        var audio = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/AudioManager.prefab"), scene);
        audio.GetComponent<AudioManager>().backgroundMusic = AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(MenuMusicGuid));
        PrefabUtility.RecordPrefabInstancePropertyModifications(audio.GetComponent<AudioManager>());

        var so = new SerializedObject(ctrl);
        void Set(string field, Object value) => so.FindProperty(field).objectReferenceValue = value;
        void SetArray(string field, Object[] values)
        {
            var p = so.FindProperty(field);
            p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
        SetArray("nodes", nodes.ToArray());
        Set("player", player);
        Set("playerSprite", body.GetComponent<SpriteRenderer>());
        SetArray("idleFrames", idle);
        SetArray("runFrames", run);
        SetArray("clouds", clouds.ToArray());
        SetArray("gates", gates.ToArray());
        Set("cursor", cursor);
        Set("openIcon", S("Node_Open_Gold"));
        Set("clearedIcon", S("Node_Cleared"));
        Set("lockedIcon", S("Node_Locked"));
        Set("label", label);
        Set("zoneText", zoneText);
        Set("levelText", levelText);
        Set("coinsText", coins);
        Set("fade", fade);
        AudioClip Sound(string p) => AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound/" + p);
        Set("moveSound", Sound("Btn_Common.wav"));
        Set("blockedSound", Sound("Trap_05.wav"));
        Set("selectSound", Sound("ReadyGo.wav"));
        Set("backSound", Sound("Transition_Out.wav"));
        Set("gateSound", Sound("Japan/ToriGate_ToSpirit.wav"));
        so.ApplyModifiedPropertiesWithoutUndo();
        ApplyZoneMusic(ctrl); // ZONEMUSIC:

        EditorSceneManager.SaveScene(scene, ScenePath);
        return $"ZoneSelect rebuilt: {nodes.Count} nodes";
    }

    // ZONEMUSIC: each zone's map track: name, scene-name prefixes, clip. A missing file leaves the slot empty, so that
    // zone plays the map's own music until the track is added under this name and the menu below is run again.
    private static readonly (string name, string[] prefixes, string path)[] ZoneTracks =
    {
        ("The Old Tomb", new[] { "Z1" }, "Assets/Music/TombZoneSelectSoundtrack.wav"),
        ("Sandy Dunes", new[] { "Z2" }, "Assets/Music/SandZoneSelectSoundtrack.wav"),
        ("Japan (Shrine Road + Snow Temple Pass)", new[] { "Z3" }, "Assets/Music/JapanZoneSelectSoundTrack.wav"),
        // WINTERMUSIC: past the torii (Snow Temple Pass). Later entries win, so this overrides the Japan track there.
        // PLACEHOLDER: SnowTemple_Modern until the real winter track is made; swap the path and re-run the menu below.
        ("Japan winter side (Snow Temple Pass) - PLACEHOLDER", new[] { "Z3JapanVillage", "Z3Boss" }, "Assets/Music/SnowTemple_Modern.wav"),
        ("Grassy Meadow + Journey's End", new[] { "Z4", "Win" }, "Assets/Music/GrassZoneSelectSoundtrack.wav"),
    };

    /// <summary>ZONEMUSIC: puts the zone tracks on the open ZoneSelect scene's map, without rebuilding the scene.</summary>
    [MenuItem("Tools/HNL/Set Up Zone Select Music")]
    private static void ZoneMusicFromMenu()
    {
        var ctrl = Object.FindObjectOfType<ZoneSelectMap>();
        if (ctrl == null)
        {
            Debug.LogWarning("Open the ZoneSelect scene first.");
            return;
        }
        ApplyZoneMusic(ctrl);
        EditorSceneManager.MarkSceneDirty(ctrl.gameObject.scene);
        EditorSceneManager.SaveScene(ctrl.gameObject.scene);
    }

    private static void ApplyZoneMusic(ZoneSelectMap ctrl)
    {
        var so = new SerializedObject(ctrl);
        var list = so.FindProperty("zoneMusic");
        list.arraySize = ZoneTracks.Length;
        var report = new List<string>();
        for (int i = 0; i < ZoneTracks.Length; i++)
        {
            var (name, prefixes, path) = ZoneTracks[i];
            // streamed like the rest of the game's music
            if (AssetImporter.GetAtPath(path) is AudioImporter ai)
            {
                AudioImporterSampleSettings ss = ai.defaultSampleSettings;
                if (ss.loadType != AudioClipLoadType.Streaming)
                {
                    ss.loadType = AudioClipLoadType.Streaming;
                    ai.defaultSampleSettings = ss;
                    ai.SaveAndReimport();
                }
            }
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            var e = list.GetArrayElementAtIndex(i);
            e.FindPropertyRelative("name").stringValue = name;
            var pre = e.FindPropertyRelative("scenePrefixes");
            pre.arraySize = prefixes.Length;
            for (int j = 0; j < prefixes.Length; j++) pre.GetArrayElementAtIndex(j).stringValue = prefixes[j];
            e.FindPropertyRelative("clip").objectReferenceValue = clip;
            report.Add(name + ": " + (clip != null ? clip.name : "(map music)"));
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        Debug.Log("Zone music: " + string.Join(", ", report));
    }

    private static void ImportArt()
    {
        AssetDatabase.Refresh();
        foreach (var path in System.IO.Directory.GetFiles(Art, "*.png"))
        {
            var ti = AssetImporter.GetAtPath(path.Replace('\\', '/')) as TextureImporter;
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
            s.spriteMeshType = SpriteMeshType.FullRect;
            s.spriteAlignment = (int)SpriteAlignment.Center;
            ti.SetTextureSettings(s);
            ti.spriteBorder = path.EndsWith("LabelFrame.png") ? new Vector4(4, 4, 4, 4) : Vector4.zero;
            ti.SaveAndReimport();
        }
    }

    private static (List<(string id, Vector3 pos)>, Dictionary<int, Vector2[]>, List<Vector3>) ReadLayout()
    {
        float F(string s) => float.Parse(s, CultureInfo.InvariantCulture);
        var nodes = new List<(string, Vector3)>();
        var routes = new Dictionary<int, Vector2[]>();
        var gates = new List<Vector3>();
        foreach (var raw in System.IO.File.ReadAllLines(Layout))
        {
            var parts = raw.Trim().Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0 || parts[0].StartsWith("#")) continue;
            if (parts[0] == "node") nodes.Add((parts[1], new Vector3(F(parts[2]), F(parts[3]), 0)));
            else if (parts[0] == "gate") gates.Add(new Vector3(F(parts[1]), F(parts[2]), 0));
            else if (parts[0] == "route")
                routes[int.Parse(parts[1])] = parts.Skip(2).Select(p => { var xy = p.Split(','); return new Vector2(F(xy[0]), F(xy[1])); }).ToArray();
        }
        return (nodes, routes, gates);
    }

    private static Sprite S(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(Art + name + ".png");

    private static Color Hex(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out var c);
        return c;
    }

    private static GameObject SpriteObject(string name, Sprite sprite, Vector3 pos, int order, Transform parent)
    {
        var go = new GameObject(name);
        if (parent) go.transform.SetParent(parent, false);
        go.transform.position = pos;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = order;
        sr.sharedMaterial = spriteMat;
        return go;
    }

    private static RectTransform Rect(string name, Transform parent, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = aMin;
        rt.anchorMax = aMax;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    private static void Img(RectTransform rt, Sprite sprite)
    {
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.type = Image.Type.Sliced;
        img.raycastTarget = false;
    }

    private static TMP_Text Text(string name, RectTransform parent, Vector2 aMin, Vector2 aMax, Vector2 offMin, Vector2 offMax, float size, Color colour, string text)
    {
        var rt = Rect(name, parent, aMin, aMax, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        rt.offsetMin = offMin;
        rt.offsetMax = offMax;
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.font = font;
        t.fontSize = size;
        t.color = colour;
        t.text = text;
        t.alignment = TextAlignmentOptions.Midline;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Overflow;
        t.raycastTarget = false;
        return t;
    }
}
