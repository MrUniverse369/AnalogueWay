using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// SPIKES (2026-10-21): puts every Tomb spike trap on the Z1A6 pop/retract rhythm (Tools/HNL/Set Up Tomb Spike
/// Rhythms; safe to run again, it only adds spikes no SpikeRhythm drives yet):
///   1. finds the spike traps (HurtPlayer on a "Spikes" sprite) in each Tomb level
///   2. groups them into runs: spikes facing the same way, on the same surface, side by side (a SpikeGroup is one run)
///   3. one SpikeRhythm per level ("SpikeRhythm", or the existing one in Z1A6) drives them all with the Z1A6 timing
///      and the HNL_SpikeExtend / HNL_SpikeRetract sounds. Each spike sinks by its own height into the stone behind it
///   4. phases: a run pops together; along a surface the runs alternate (0 / half a beat), and a ceiling or wall run
///      facing another across a corridor or shaft takes the opposite beat, so facing traps never close at the same
///      time and there is always a way through. Damage only happens while a spike is at least half out (SpikeRhythm)
/// </summary>
public static class TombSpikeRhythmSetup
{
    private static readonly string[] Scenes =
    {
        "Z2A1_Tomb", "Z2A2_Tomb", "Z2A3_Tomb", "Z2A4_Tomb", "Z2A5_Tomb", "Z2A6_Tomb", "Z2Boss_Tomb", "Z2Village_Tomb"
    };
    private const string ExtendSound = "Assets/Sound/ElevenLabs/World/HNL_SpikeExtend.wav";
    private const string RetractSound = "Assets/Sound/ElevenLabs/World/HNL_SpikeRetract.wav";
    private const float RunGap = 1.3f; // spikes closer than this along a surface are one run

    [MenuItem("Tools/HNL/Set Up Tomb Spike Rhythms")]
    public static void Run()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string start = EditorSceneManager.GetActiveScene().path;
        foreach (string name in Scenes)
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/" + name + ".unity", OpenSceneMode.Single);
            int added = SetUp();
            if (added > 0) EditorSceneManager.SaveScene(scene);
            Debug.Log("TombSpikeRhythmSetup: " + name + " +" + added + " spikes");
        }
        if (!string.IsNullOrEmpty(start)) EditorSceneManager.OpenScene(start, OpenSceneMode.Single);
    }

    /// <summary>Adds the open scene's free spike traps to its SpikeRhythm. Returns how many were added.</summary>
    public static int SetUp()
    {
        var driven = new HashSet<Transform>();
        SpikeRhythm rhythm = Object.FindFirstObjectByType<SpikeRhythm>(FindObjectsInactive.Include);
        if (rhythm != null)
        {
            var arr = new SerializedObject(rhythm).FindProperty("spikes");
            for (int i = 0; i < arr.arraySize; i++)
            {
                var t = arr.GetArrayElementAtIndex(i).FindPropertyRelative("spike").objectReferenceValue as Transform;
                if (t != null) driven.Add(t);
            }
        }

        var free = new List<SpriteRenderer>();
        foreach (var sr in Object.FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (sr.sprite != null && sr.sprite.name == "Spikes" && sr.GetComponent<HurtPlayer>() != null && !driven.Contains(sr.transform))
                free.Add(sr);
        if (free.Count == 0) return 0;

        if (rhythm == null)
        {
            var go = new GameObject("SpikeRhythm");
            Undo.RegisterCreatedObjectUndo(go, "Spike rhythm");
            rhythm = go.AddComponent<SpikeRhythm>();
        }
        var so = new SerializedObject(rhythm);
        so.FindProperty("extendSound").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(ExtendSound);
        so.FindProperty("retractSound").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(RetractSound);

        // runs: same facing, same surface line, neighbours within RunGap
        var runs = new List<List<SpriteRenderer>>();
        foreach (var sr in free.OrderBy(s => Along(s.transform)))
        {
            List<SpriteRenderer> run = runs.FirstOrDefault(r => SameRun(r[r.Count - 1].transform, sr.transform));
            if (run == null) runs.Add(run = new List<SpriteRenderer>());
            run.Add(sr);
        }

        // phases: floors and left walls alternate along the surface; a ceiling or right-wall run facing one of those
        // (across a corridor or shaft) takes the opposite beat, so the two sides never close together
        var phases = new Dictionary<List<SpriteRenderer>, float>();
        foreach (var facing in runs.GroupBy(r => Facing(r[0].transform)).OrderBy(g => g.Key))
        {
            int i = 0;
            foreach (var run in facing.OrderBy(r => Along(r[0].transform)))
            {
                float phase = Mathf.Repeat((i++ % 2) * 0.5f + (facing.Key % 2) * 0.5f, 1f);
                if (facing.Key % 2 == 1)
                {
                    var across = runs.Where(r => Facing(r[0].transform) == facing.Key - 1 && phases.ContainsKey(r))
                        .OrderBy(r => Vector2.Distance(Centre(r), Centre(run))).FirstOrDefault();
                    if (across != null && Vector2.Distance(Centre(across), Centre(run)) < 5f)
                        phase = Mathf.Repeat(phases[across] + 0.5f, 1f);
                }
                phases[run] = phase;
            }
        }
        var spikes = so.FindProperty("spikes");
        foreach (var run in runs)
        {
            foreach (var sr in run)
            {
                int n = spikes.arraySize;
                spikes.InsertArrayElementAtIndex(n);
                var e = spikes.GetArrayElementAtIndex(n);
                e.FindPropertyRelative("spike").objectReferenceValue = sr.transform;
                e.FindPropertyRelative("phase").floatValue = phases[run];
                e.FindPropertyRelative("depth").floatValue = Mathf.Round(sr.sprite.bounds.size.y * sr.transform.lossyScale.y * 32f) / 32f;
            }
        }
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(rhythm);
        return free.Count;
    }

    // 0 floor (points up), 1 ceiling, 2 left wall (points right), 3 right wall
    private static int Facing(Transform t)
    {
        Vector2 up = t.up;
        if (Mathf.Abs(up.y) >= Mathf.Abs(up.x)) return up.y > 0f ? 0 : 1;
        return up.x > 0f ? 2 : 3;
    }

    private static Vector2 Centre(List<SpriteRenderer> run)
    {
        Vector2 sum = Vector2.zero;
        foreach (var sr in run) sum += (Vector2)sr.transform.position;
        return sum / run.Count;
    }

    // position along the surface (x for floors and ceilings, y for walls)
    private static float Along(Transform t) => Facing(t) < 2 ? t.position.x : t.position.y;
    private static float Across(Transform t) => Facing(t) < 2 ? t.position.y : t.position.x;

    private static bool SameRun(Transform a, Transform b) =>
        Facing(a) == Facing(b) && Mathf.Abs(Across(a) - Across(b)) < 0.3f && Mathf.Abs(Along(a) - Along(b)) < RunGap;
}
