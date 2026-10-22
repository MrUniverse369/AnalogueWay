using UnityEngine;

/// <summary>
/// BOULDER (2026-10-21): Z1A3's rolling boulder trap. Its trigger box covers the corridor on both sides of a shaft in
/// the ceiling. When Harvey is in it and at least minLead to one side of the shaft (ahead of where the boulder will
/// land, not under it), the trap goes off once: a stone-grinding rumble, grit trickling from the shaft mouth and a
/// small shake for warnTime, then the boulder (a RollingBoulder child, hidden until now) drops from the top of the
/// shaft and rolls after him. Nothing stops: no cutscene, no camera change, Harvey keeps control throughout.
/// One boulder only: the trap stays spent until Harvey respawns (LevelManager.Respawned), which also clears a
/// boulder still rolling; reloading the level starts it fresh.
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class BoulderTrap : MonoBehaviour
{
    [SerializeField] private RollingBoulder boulder;
    [Tooltip("Where the boulder starts: above the playable passage, inside the shaft.")]
    [SerializeField] private Transform spawnPoint;
    [Tooltip("The shaft's opening in the corridor ceiling: grit trickles from here during the warning.")]
    [SerializeField] private Transform shaftMouth;
    [SerializeField] private float shaftWidth = 2f;
    [Tooltip("Seconds of rumble before the boulder drops, so Harvey can react.")]
    [SerializeField] private float warnTime = 0.6f;
    [Tooltip("Harvey has to be at least this far (x) to the side of the shaft for the trap to go off.")]
    [SerializeField] private float minLead = 1.2f;
    [SerializeField] private AudioClip warnSound;
    [SerializeField, Range(0f, 1f)] private float warnVolume = 1f;

    private bool armed = true;
    private float warnLeft;

    private void OnEnable() => LevelManager.Respawned += Rearm;
    private void OnDisable() => LevelManager.Respawned -= Rearm;

    private void Start()
    {
        if (boulder != null) boulder.Hide();
    }

    private void OnTriggerEnter2D(Collider2D other) => Check(other);
    private void OnTriggerStay2D(Collider2D other) => Check(other);

    private void Check(Collider2D other)
    {
        if (!armed || boulder == null || !other.CompareTag("PlayerDetectionArea")) return;
        if (Mathf.Abs(other.transform.position.x - spawnPoint.position.x) < minLead) return; // under the shaft: not yet
        armed = false;
        warnLeft = warnTime;
        AudioManager audio = AudioManager.Instance;
        if (audio != null) audio.PlaySfxOneShot(warnSound, warnVolume);
        CameraController.Shake(warnTime, 0.04f);
        if (warnTime <= 0f) boulder.Drop(spawnPoint.position);
    }

    private void Update()
    {
        if (warnLeft <= 0f) return;
        warnLeft -= Time.deltaTime;
        // grit trickling out of the shaft, more of it as the drop gets close
        if (shaftMouth != null && Random.value < Mathf.Lerp(0.25f, 0.8f, 1f - warnLeft / warnTime))
        {
            Vector3 at = shaftMouth.position + new Vector3(Random.Range(-0.5f, 0.5f) * shaftWidth, 0f, 0f);
            PixelFX.Emit(at, new Vector2(Random.Range(-0.15f, 0.15f), Random.Range(-1.5f, -0.5f)), PixelFX.Pick(PixelFX.StoneDust), Random.Range(0.4f, 0.7f));
        }
        if (warnLeft <= 0f) boulder.Drop(spawnPoint.position);
    }

    // Harvey respawned: clear whatever is left of the boulder and set the trap again
    private void Rearm()
    {
        warnLeft = 0f;
        armed = true;
        if (boulder != null) boulder.Hide();
    }

    private void OnDrawGizmos()
    {
        if (spawnPoint == null) return;
        Gizmos.color = new Color(0.95f, 0.65f, 0.37f, 0.8f);
        Gizmos.DrawWireSphere(spawnPoint.position, 0.72f);
        BoxCollider2D box = GetComponent<BoxCollider2D>();
        Vector3 c = box.bounds.center;
        Gizmos.color = new Color(0.95f, 0.65f, 0.37f, 0.3f);
        Gizmos.DrawLine(new Vector3(spawnPoint.position.x - minLead, c.y - box.bounds.extents.y), new Vector3(spawnPoint.position.x - minLead, c.y + box.bounds.extents.y));
        Gizmos.DrawLine(new Vector3(spawnPoint.position.x + minLead, c.y - box.bounds.extents.y), new Vector3(spawnPoint.position.x + minLead, c.y + box.bounds.extents.y));
    }
}
