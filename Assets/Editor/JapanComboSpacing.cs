using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// COMBOSPACE (2026-10-20): re-spaces the walking enemies in the Japan levels so stomp and dash combos chain.
/// A stomp bounce (9) keeps Harvey in the air about 1.7 s, roughly 7 tiles at his speed, and the combo window is 5 s.
/// Walkers that share a stretch of floor used to patrol long, overlapping ranges (up to 16 tiles), so they bunched up
/// and drifted apart at random: after a stomp the next enemy was as likely to be out of reach (combo lost) as right
/// under Harvey's landing spot (hurt). Here each group gets evenly spaced, shorter patrols inside the floor it
/// already covered, so the next enemy is always about one bounce away.
/// Only plain walkers are touched: same height, same world, overlapping patrols, ground under the whole patrol. Ghost
/// rows, ghost columns, the yokai over the spike pit and anything hovering over a pit keep their hand-tuned setup.
/// Enemy counts and speeds don't change, so the levels aren't made easier, only fairer to chain.
/// Menu: Tools/HNL/Re-space Japan Enemies (Dry Run) and Tools/HNL/Re-space Japan Enemies.
/// </summary>
public static class JapanComboSpacing
{
    private static readonly string[] Scenes = { "Z4A1_Japan", "Z4A2_Japan", "Z4A3_Japan", "Z4A4_Japan" };
    private const float MinSpacing = 3.5f;   // closer than this and a landing stomp clips the next enemy's side
    private const float MaxSpacing = 6f;     // further than this and a bounce no longer reaches the next one
    private const float MaxPatrol = 4f;      // patrol length per enemy: predictable, but still moving
    private const float MaxSamuraiPatrol = 4.5f; // the samurai monkey's patrol is also his dash range: keep it long
    private const float SameHeight = 0.6f;
    private const int GroundMask = 1 << 6;   // the Ground layer (PlayerController's feet check)

    [MenuItem("Tools/HNL/Re-space Japan Enemies (Dry Run)")]
    private static void DryRunMenu() => Debug.Log(Run(false));

    [MenuItem("Tools/HNL/Re-space Japan Enemies")]
    private static void ApplyMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Debug.Log(Run(true));
    }

    private class Walker
    {
        public MovingTiles patrol;
        public Transform a, b, body;
        public float min, max, y;
        public int world; // 0 both, 1 world one, 2 world two
        public bool samurai;
    }

    public static string Run(bool apply)
    {
        var report = new StringBuilder();
        foreach (string name in Scenes)
        {
            var scene = EditorSceneManager.OpenScene($"Assets/Scenes/{name}.unity", OpenSceneMode.Single);
            Physics2D.SyncTransforms();
            Dictionary<GameObject, int> worlds = WorldsOf();
            var walkers = new List<Walker>();
            int skipped = 0;
            foreach (MovingTiles mt in Object.FindObjectsByType<MovingTiles>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                EnemyController ec = mt.ObjToMove != null ? mt.ObjToMove.GetComponent<EnemyController>() : null;
                if (ec == null || ec.CompareTag("japanBoss")) continue;
                if (!IsWalker(mt, ec)) { skipped++; continue; }
                var so = new SerializedObject(mt);
                walkers.Add(new Walker
                {
                    patrol = mt,
                    a = (Transform)so.FindProperty("pointA").objectReferenceValue,
                    b = (Transform)so.FindProperty("pointB").objectReferenceValue,
                    body = mt.ObjToMove,
                    min = mt.MinX, max = mt.MaxX, y = mt.ObjToMove.position.y,
                    world = WorldOf(mt.transform, worlds),
                    samurai = ec.GetComponent<SamuraiSlash>() != null,
                });
            }

            int changed = 0;
            foreach (List<Walker> group in Groups(walkers))
            {
                if (group.Count < 2) continue;
                float from = group.Min(w => w.min), to = group.Max(w => w.max);
                int n = group.Count;
                float spacing = Mathf.Clamp((to - from) / n, MinSpacing, MaxSpacing);
                if (spacing * (n - 1) > to - from) continue; // too crowded to space out on this floor: leave it
                float patrolLength = Mathf.Min(MaxPatrol, spacing * 0.6f);
                float samuraiLength = Mathf.Min(MaxSamuraiPatrol, spacing * 0.75f);
                float chain = spacing * (n - 1) + (group.Any(w => w.samurai) ? samuraiLength : patrolLength);
                if (chain > to - from) continue;
                float start = Mathf.Clamp((from + to) / 2f - chain / 2f, from, to - chain); // centred on the old stretch
                report.AppendLine($"{name}: {n} walkers on y {group[0].y:F1}, x {from:F1}-{to:F1} -> {spacing:F1} apart, {patrolLength:F1}-tile patrols");
                group.Sort((p, q) => (p.min + p.max).CompareTo(q.min + q.max));
                for (int i = 0; i < n; i++)
                {
                    Walker w = group[i];
                    float lo = start + spacing * i, hi = lo + (w.samurai ? samuraiLength : patrolLength);
                    report.AppendLine($"    {w.patrol.name}: [{w.min:F1},{w.max:F1}] -> [{lo:F1},{hi:F1}]");
                    if (!apply) continue;
                    bool aIsLow = w.a.position.x <= w.b.position.x;
                    Undo.RecordObjects(new Object[] { w.a, w.b, w.body }, "Re-space enemies");
                    (aIsLow ? w.a : w.b).position = new Vector3(lo, (aIsLow ? w.a : w.b).position.y, 0f);
                    (aIsLow ? w.b : w.a).position = new Vector3(hi, (aIsLow ? w.b : w.a).position.y, 0f);
                    // every enemy starts at its pointA and heads for pointB (MovingTiles' first target), so a group's
                    // patrols run in step and the spacing between neighbours stays the same
                    w.body.position = new Vector3(w.a.position.x, w.body.position.y, w.body.position.z);
                    changed++;
                }
            }
            report.AppendLine($"{name}: {walkers.Count} walkers checked, {skipped} hand-tuned/hovering left alone, {changed} re-spaced");
            if (apply && changed > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
        }
        return (apply ? "APPLIED\n" : "DRY RUN (nothing saved)\n") + report;
    }

    // walkers only: a plain ground enemy with floor under both ends and the middle of its patrol
    private static bool IsWalker(MovingTiles mt, EnemyController ec)
    {
        string n = mt.name;
        if (n.StartsWith("SGhost") || mt.transform.parent != null && mt.transform.parent.name.StartsWith("SGB")) return false; // ghost rows/columns
        var so = new SerializedObject(mt);
        if (so.FindProperty("followPatrol").objectReferenceValue != null) return false; // GHOSTSYNC pairs
        if (n == "YokaiWarriorTwo (10)" || n == "YokaiWarriorTwo (11)") return false;    // the pair over the Z3A2 spike pit
        Collider2D col = ec.GetComponent<Collider2D>();
        float bottom = col != null ? col.bounds.min.y : ec.transform.position.y - 0.5f;
        float y = ec.transform.position.y;
        foreach (float x in new[] { mt.MinX, (mt.MinX + mt.MaxX) / 2f, mt.MaxX })
        {
            RaycastHit2D hit = Physics2D.Raycast(new Vector2(x, y), Vector2.down, 6f, GroundMask);
            if (hit.collider == null || bottom - hit.point.y > 0.6f) return false; // nothing close underneath: it hovers
        }
        return true;
    }

    // walkers on one floor: same world, same height, and patrols that overlap or touch (so the floor between is walkable)
    private static IEnumerable<List<Walker>> Groups(List<Walker> walkers)
    {
        var left = walkers.OrderBy(w => w.min).ToList();
        while (left.Count > 0)
        {
            var group = new List<Walker> { left[0] };
            left.RemoveAt(0);
            bool grew = true;
            while (grew)
            {
                grew = false;
                for (int i = left.Count - 1; i >= 0; i--)
                {
                    Walker w = left[i];
                    if (group.Any(g => Compatible(g, w) && w.min <= g.max + 0.5f && w.max >= g.min - 0.5f))
                    {
                        group.Add(w);
                        left.RemoveAt(i);
                        grew = true;
                    }
                }
            }
            yield return group;
        }
    }

    private static bool Compatible(Walker p, Walker q) =>
        Mathf.Abs(p.y - q.y) < SameHeight && (p.world == 0 || q.world == 0 || p.world == q.world);

    // changeWorld lists which objects belong to which world (worldOne... / worldTwo... fields)
    private static Dictionary<GameObject, int> WorldsOf()
    {
        var map = new Dictionary<GameObject, int>();
        foreach (var cw in Object.FindObjectsByType<AnalogueWay.changeWorld>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var it = new SerializedObject(cw).GetIterator();
            while (it.Next(true))
            {
                if (it.propertyType != SerializedPropertyType.ObjectReference || !(it.objectReferenceValue is GameObject g)) continue;
                if (it.propertyPath.StartsWith("worldOne")) map[g] = 1;
                else if (it.propertyPath.StartsWith("worldTwo")) map[g] = 2;
            }
        }
        return map;
    }

    private static int WorldOf(Transform t, Dictionary<GameObject, int> worlds)
    {
        for (; t != null; t = t.parent)
            if (worlds.TryGetValue(t.gameObject, out int w)) return w;
        return 0;
    }
}
