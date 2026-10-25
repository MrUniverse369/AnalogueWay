using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// FOXMOUNT (2026-10-24): wires up Harvey's Spirit Fox mount and the Mount Talisman (Tools/HNL/Set Up Fox Ride; safe
/// to run again after re-running Tools/Art/fox_ride.py):
///   1. FoxRide.png (Harvey riding the Forest Spirit Fox, from Tools/Art/fox_ride.py) imported like the dragon ride
///      (16 PPU, point, no compression) and sliced into its 8 frames, all with the pivot from Tools/Art/fox_ride_layout.txt
///      (the middle of Harvey at the fox's feet)
///   2. FoxRide.anim: the 8 frames looping at 12 fps (FoxMountController sets its pace and holds a frame standing or leaping)
///   3. FoxRide.controller: one looping state
///   4. HarveyFoxMount in the Player prefab (every level's Harvey), next to HarveyDragonMount and drawn like it, off
///      until ridden, standing where Harvey's body collider stands (its feet on the collider's bottom), with the
///      Animator and FoxMountController; PlayerController.foxMount points at it
///   5. Talisman_FeatherFall becomes the Mount Talisman: the same asset (so saves, the Sandy boss's unlock and equipped
///      slots carry over) with the MountTalisman script, name, icon, text and timings
/// </summary>
public static class FoxRideSetup
{
    private const string Dir = "Assets/Sprites/MainChar/MainCharTwo/Mounts/";
    private const string Sheet = Dir + "FoxRide.png";
    private const string ClipPath = Dir + "FoxRide.anim";
    private const string ControllerPath = Dir + "FoxRide.controller";
    private const string PrefabPath = "Assets/Prefabs/Player.prefab";
    private const string LayoutPath = "Tools/Art/fox_ride_layout.txt";
    private const string TalismanPath = "Assets/Prefabs/Talismans/Talisman_FeatherFall.asset";
    private const string IconPath = "Assets/Sprites/UI/Menus/Talisman_SpiritFoxMount.png";
    private const string MountName = "HarveyFoxMount", DragonName = "HarveyDragonMount";
    private const float Fps = 12f;

    [MenuItem("Tools/HNL/Set Up Fox Ride")]
    public static void Run()
    {
        if (!File.Exists(Sheet) || !File.Exists(LayoutPath))
        {
            Debug.LogWarning("FoxRideSetup: run Tools/Art/fox_ride.py first (no sheet or layout).");
            return;
        }
        string[] lines = File.ReadAllLines(LayoutPath);
        float[] frame = Numbers(lines, "frame"), pivot = Numbers(lines, "pivot");
        int frameW = (int)frame[0], frameH = (int)frame[1];

        // 1. import and slice
        var ti = (TextureImporter)AssetImporter.GetAtPath(Sheet);
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
                name = "FoxRide_" + i,
                rect = new Rect(i * frameW, 0, frameW, frameH),
                alignment = (int)SpriteAlignment.Custom,
                pivot = new Vector2(pivot[0] / frameW, pivot[1] / frameH),
            };
#pragma warning disable 618 // still the simplest way to slice from code
        ti.spritesheet = metas;
#pragma warning restore 618
        ti.SaveAndReimport();
        Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(Sheet).OfType<Sprite>()
            .OrderBy(s => int.Parse(s.name.Substring(s.name.LastIndexOf('_') + 1))).ToArray();

        // 2. the looping clip
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
        if (clip == null)
        {
            clip = new AnimationClip { frameRate = Fps };
            AssetDatabase.CreateAsset(clip, ClipPath);
        }
        clip.frameRate = Fps;
        var keys = new ObjectReferenceKeyframe[sprites.Length]; // Unity holds the last key for one frame, so no closing key
        for (int i = 0; i < sprites.Length; i++) keys[i] = new ObjectReferenceKeyframe { time = i / Fps, value = sprites[i] };
        AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), keys);
        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = true;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        EditorUtility.SetDirty(clip);

        // 3. the controller
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath)
                   ?? AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        AnimatorStateMachine sm = ctrl.layers[0].stateMachine;
        AnimatorState state = sm.states.Select(s => s.state).FirstOrDefault(s => s.name == "FoxRide")
                              ?? sm.AddState("FoxRide", new Vector3(300, 100, 0));
        state.motion = clip;
        sm.defaultState = state;
        EditorUtility.SetDirty(ctrl);
        AssetDatabase.SaveAssets();

        // 4. the mount in the Player prefab
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            Transform dragon = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == DragonName);
            var bodyBox = root.GetComponentsInChildren<BoxCollider2D>(true).FirstOrDefault(b => b.GetComponent<playerBodyDetectionArea>() != null);
            Transform mount = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == MountName);
            if (mount == null)
            {
                mount = new GameObject(MountName).transform;
                mount.SetParent(root.transform, false);
                if (dragon != null) mount.SetSiblingIndex(dragon.GetSiblingIndex() + 1);
            }
            // the fox's feet on the bottom of Harvey's body collider, the rider over its middle
            Vector3 at = Vector3.zero;
            if (bodyBox != null)
            {
                Vector3 bottom = root.transform.InverseTransformPoint(bodyBox.transform.TransformPoint(
                    bodyBox.offset + new Vector2(0f, -bodyBox.size.y / 2f)));
                at = new Vector3(bottom.x, bottom.y, 0f);
            }
            mount.localPosition = at;
            mount.localRotation = Quaternion.identity;
            mount.localScale = Vector3.one; // one art pixel = one of Harvey's (the fox is doubled in the art)

            var sr = mount.GetComponent<SpriteRenderer>();
            if (sr == null) sr = mount.gameObject.AddComponent<SpriteRenderer>();
            if (sprites.Length > 0) sr.sprite = sprites[0];
            SpriteRenderer dragonSr = dragon != null ? dragon.GetComponent<SpriteRenderer>() : null;
            if (dragonSr != null) // drawn exactly like the dragon
            {
                sr.sharedMaterial = dragonSr.sharedMaterial;
                sr.sortingLayerID = dragonSr.sortingLayerID;
                sr.sortingOrder = dragonSr.sortingOrder;
                sr.maskInteraction = dragonSr.maskInteraction;
            }
            Animator anim = mount.GetComponent<Animator>();
            if (anim == null) anim = mount.gameObject.AddComponent<Animator>(); // not ??: a missing component is a "fake null" in the editor
            anim.runtimeAnimatorController = ctrl;
            anim.updateMode = AnimatorUpdateMode.Normal;
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            if (mount.GetComponent<FoxMountController>() == null) mount.gameObject.AddComponent<FoxMountController>();
            mount.gameObject.SetActive(false);

            var so = new SerializedObject(root.GetComponent<PlayerController>());
            so.FindProperty("foxMount").objectReferenceValue = mount.gameObject;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Debug.Log($"FoxRideSetup: {sprites.Length} frames of {frameW}x{frameH}, pivot ({pivot[0]}, {pivot[1]}) px, {MountName} at {at}.");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }

        // 5. Feather Fall's asset becomes the Mount Talisman
        ConvertTalisman();
        AssetDatabase.SaveAssets();
    }

    private static void ConvertTalisman()
    {
        var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(TalismanPath);
        if (asset == null)
        {
            Debug.LogWarning("FoxRideSetup: no " + TalismanPath + ".");
            return;
        }
        if (!(asset is MountTalisman))
        {
            var temp = ScriptableObject.CreateInstance<MountTalisman>();
            MonoScript script = MonoScript.FromScriptableObject(temp);
            Object.DestroyImmediate(temp);
            var swap = new SerializedObject(asset);
            swap.FindProperty("m_Script").objectReferenceValue = script; // the fields it shares (Talisman's) are kept
            swap.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(TalismanPath, ImportAssetOptions.ForceUpdate);
            asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(TalismanPath);
        }
        var iti = (TextureImporter)AssetImporter.GetAtPath(IconPath);
        if (iti != null) // like the other talisman gems (Tools/UI/menus_art.py mount): a single point-filtered 16 PPU sprite
        {
            iti.textureType = TextureImporterType.Sprite;
            iti.spriteImportMode = SpriteImportMode.Single;
            iti.spritePixelsPerUnit = 16f;
            iti.filterMode = FilterMode.Point;
            iti.mipmapEnabled = false;
            iti.textureCompression = TextureImporterCompression.Uncompressed;
            iti.alphaIsTransparency = true;
            iti.SaveAndReimport();
        }
        var icon = AssetDatabase.LoadAssetAtPath<Sprite>(IconPath);
        var so = new SerializedObject(asset);
        so.FindProperty("displayName").stringValue = "Mount Talisman";
        if (icon != null) so.FindProperty("icon").objectReferenceValue = icon;
        so.FindProperty("description").stringValue = HarveyMenusContentBuilder.MountDescription;
        so.FindProperty("duration").floatValue = HarveyMenusContentBuilder.MountDuration;
        so.FindProperty("cooldown").floatValue = HarveyMenusContentBuilder.MountCooldown;
        so.FindProperty("chiPerSecond").floatValue = HarveyMenusContentBuilder.MountChiPerSecond;
        so.FindProperty("retriggerWhileHeld").boolValue = false;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(asset);
        Debug.Log("FoxRideSetup: " + TalismanPath + " is the Mount Talisman (" + asset.GetType().Name + ").");
    }

    private static float[] Numbers(string[] lines, string key) =>
        lines.First(l => l.StartsWith(key + " ")).Split(' ').Skip(1).Select(v => float.Parse(v, CultureInfo.InvariantCulture)).ToArray();
}
