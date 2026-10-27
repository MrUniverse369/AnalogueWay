using System.Linq;
using UnityEngine;

/// <summary>
/// BOSSREGEN (2026-10-27): boss life pips for bosses with more than three lives. Copies the last pip until the row
/// has 'count', removes extras, and lays them out centred at the row's spacing (0.5 units in every arena so far).
/// Returns them left to right, the order the bosses light them in.
/// </summary>
public static class BossPips
{
    public static SpriteRenderer[] Ensure(Transform row, int count, float spacing = 0.5f)
    {
        if (row == null) return new SpriteRenderer[0];
        var pips = row.GetComponentsInChildren<SpriteRenderer>(true).OrderBy(p => p.transform.localPosition.x).ToList();
        if (pips.Count == 0) return new SpriteRenderer[0];
        while (pips.Count < count)
        {
            SpriteRenderer copy = Object.Instantiate(pips[pips.Count - 1], row);
            copy.name = "Pip" + (pips.Count + 1);
            pips.Add(copy);
        }
        while (pips.Count > count)
        {
            Object.DestroyImmediate(pips[pips.Count - 1].gameObject);
            pips.RemoveAt(pips.Count - 1);
        }
        for (int i = 0; i < pips.Count; i++)
        {
            pips[i].name = "Pip" + (i + 1);
            Vector3 p = pips[i].transform.localPosition;
            pips[i].transform.localPosition = new Vector3((i - (pips.Count - 1) * 0.5f) * spacing, p.y, p.z);
        }
        return pips.ToArray();
    }
}
