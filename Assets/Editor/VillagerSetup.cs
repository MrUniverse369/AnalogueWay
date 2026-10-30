using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// DIALOGUE: editor tools for the dialogue system.
///   Tools/HNL/Set Up Village Villagers   gives each villager in Z4Village_Japan a Villager component with a name,
///                                         the village dialogue file and their start node (safe to run again; it
///                                         only sets these fields, so later inspector tweaks to range are kept)
///   Tools/HNL/Check Dialogue Files       parses every .txt in Assets/Dialogue and lists mistakes with line numbers
/// </summary>
public static class VillagerSetup
{
    private const string ScenePath = "Assets/Scenes/Z4Village_Japan.unity";
    private const string DialogueDir = "Assets/Dialogue";
    private const string VillageFile = DialogueDir + "/JapanVillage.txt";

    // scene object -> (name, start node)
    private static readonly (string obj, string name, string node)[] Villagers =
    {
        ("Chartwojapanidle_0", "Hana", "hana"),
        ("Sidecharacterone 1_0 (2)", "Genji", "genji"),
        ("JVCharfive_0", "Kuro", "kuro"),
        ("Chartwojapanidle_0 (1)", "Sakura", "villager"),
        ("Walkjapancharthree_0", "Yuki", "villager"),
        ("JVCharfour_0", "Tetsu", "villager"),
        ("Walkjapancharthree_0 (1)", "Mochi", "villager"),
        ("JVCharfour_0 (1)", "Ren", "villager"),
        ("Sidecharacterone 1_0 (3)", "Old Ume", "villager"),
    };

    [MenuItem("Tools/HNL/Set Up Village Villagers")]
    public static void SetUp()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("Stop Play mode first."); return; }
        var file = AssetDatabase.LoadAssetAtPath<TextAsset>(VillageFile);
        if (file == null) { Debug.LogWarning("No " + VillageFile); return; }
        var scene = EditorSceneManager.GetSceneByPath(ScenePath);
        bool openedHere = !scene.isLoaded;
        if (openedHere) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

        int done = 0;
        foreach (var (obj, name, node) in Villagers)
        {
            GameObject go = Find(scene, obj);
            if (go == null) { Debug.LogWarning("VillagerSetup: no " + obj + " in the village."); continue; }
            var v = go.GetComponent<Villager>();
            if (v == null) v = Undo.AddComponent<Villager>(go);
            var so = new SerializedObject(v);
            so.FindProperty("displayName").stringValue = name;
            so.FindProperty("dialogue").objectReferenceValue = file;
            so.FindProperty("startNode").stringValue = node;
            so.ApplyModifiedProperties();
            done++;
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        if (openedHere) EditorSceneManager.CloseScene(scene, true);
        Debug.Log($"VillagerSetup: {done} villagers can talk.");
        CheckFiles();
    }

    [MenuItem("Tools/HNL/Check Dialogue Files")]
    public static void CheckFiles()
    {
        int files = 0, problems = 0;
        foreach (string path in Directory.GetFiles(DialogueDir, "*.txt", SearchOption.AllDirectories))
        {
            files++;
            var script = DialogueScript.Parse(File.ReadAllText(path), Path.GetFileNameWithoutExtension(path));
            foreach (string e in script.errors) { Debug.LogWarning("Dialogue: " + e); problems++; }
        }
        Debug.Log($"Dialogue: checked {files} file(s), {problems} problem(s).");
    }

    private static GameObject Find(UnityEngine.SceneManagement.Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t.gameObject;
        return null;
    }
}
