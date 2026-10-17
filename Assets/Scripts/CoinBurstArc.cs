using UnityEngine;

/// <summary>
/// COINS: flies a spawned coin along an arc (used by OpenChest). The coin can't be collected or pulled by the
/// coin magnet while flying; when it lands its collider switches back on and it behaves like any other coin.
/// </summary>
public class CoinBurstArc : MonoBehaviour
{
    private Vector3 from;
    private Vector3 to;
    private float height;
    private float duration;
    private float progress;
    private Collider2D coinCollider;

    public void Launch(Vector3 start, Vector3 landing, float arcHeight, float flightTime)
    {
        from = start;
        to = landing;
        height = arcHeight;
        duration = Mathf.Max(0.01f, flightTime);
        progress = 0f;
        transform.position = start;
        coinCollider = GetComponent<Collider2D>();
        if (coinCollider != null) coinCollider.enabled = false;
    }

    private void Update()
    {
        progress += Time.deltaTime / duration;
        float k = Mathf.Clamp01(progress);
        Vector3 p = Vector3.Lerp(from, to, k);
        p.y += height * 4f * k * (1f - k);                     // parabola: 0 at both ends, peaks at k = 0.5
        transform.position = p;

        if (k >= 1f)
        {
            if (coinCollider != null) coinCollider.enabled = true;   // collectible from here on
            Destroy(this);
        }
    }
}
