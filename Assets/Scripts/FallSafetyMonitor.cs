using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// FALLSAFETY (2026-10-30): a global second layer under the KillPlane prefabs. Kill planes only protect where
/// someone remembered to place one; this watches every level without any scene editing and kills Harvey if he is
/// ever genuinely falling below the ground he last stood on and stays there, whether or not a kill plane is there
/// to catch him. It never needs a hardcoded world-space Y: the "floor" is wherever Harvey himself last had solid
/// footing (PlayerController.GetIsGrounded, the same signal the jump and landing code already trusts), so it
/// adapts automatically to every zone's height and to legitimate descents (a tomb shaft, a staircase) as he goes.
///
/// Bootstraps itself the same way ZoneAtmosphere does: no prefab, no per-scene placement. One
/// FallSafetyMonitor is spawned into every scene that has a player, and it is gone again the moment that scene
/// unloads, so there is nothing to reset by hand between levels.
///
/// Reuses the existing systems end to end: PlayerController.GetIsGrounded and NoControl decide when it's safe to
/// judge, and LevelManager.Respawn() is the same call a KillPlane makes, so the normal death animation, sound,
/// checkpoint and Nine Lives bookkeeping all run unchanged, and LevelManager's own "down" guard already stops a
/// kill plane and this monitor from ever respawning Harvey twice for the same fall.
/// </summary>
public class FallSafetyMonitor : MonoBehaviour
{
    /// <summary>How far below the last ground Harvey stood on counts as "fell out of the level", in world units
    /// (tiles). Must comfortably clear the deepest legitimate single jump-down in the game (about 5-7 units per
    /// calibrate.py) and the deepest kill plane found in the full-game audit (about 14 units in Z2 Tomb), so a
    /// normal platform drop or an already-covered pit never starts the timer.</summary>
    public static float FallTriggerDepth = 20f;

    /// <summary>Seconds spent continuously that far below ground, ungrounded, before the normal death sequence
    /// fires. ~2 seconds per the brief; tune here, nowhere else.</summary>
    public static float FallDeathDelay = 2f;

    /// <summary>Logs every trigger to the Console with the scene, depth and elapsed time, for QA and debugging.</summary>
    public static bool DebugLogging = false;

    private PlayerController player;
    private Transform playerT;
    private float? lastGroundedY;
    private float? belowSince;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single) return;
        PlayerController pc = FindFirstObjectByType<PlayerController>();
        if (pc == null) return; // menus, ZoneSelect, Win: nothing to watch
        GameObject go = new GameObject("FallSafetyMonitor");
        SceneManager.MoveGameObjectToScene(go, scene);
        FallSafetyMonitor mon = go.AddComponent<FallSafetyMonitor>();
        mon.player = pc;
        mon.playerT = pc.transform;
    }

    private void Start()
    {
        if (playerT != null) lastGroundedY = playerT.position.y; // seed from the spawn point; never fires on load
        LevelManager.Respawned += OnRespawned;
    }

    private void OnDestroy()
    {
        LevelManager.Respawned -= OnRespawned;
    }

    // NINELIVES-style reset: a fresh fall gets a fresh timer and a fresh floor reference every time Harvey
    // reappears, whatever put him down (a kill plane, an enemy, or this monitor itself).
    private void OnRespawned()
    {
        belowSince = null;
        if (playerT != null) lastGroundedY = playerT.position.y;
    }

    private void FixedUpdate()
    {
        if (player == null || playerT == null || !player.gameObject.activeSelf) { belowSince = null; return; }

        // CUTSCENE/RESPAWNHOLD: a scripted sequence or the camera-return hold decides Harvey's position on
        // purpose; don't second-guess it. IsStunned is covered by NoControl too (a stomp/clash daze is brief).
        if (player.NoControl) { belowSince = null; return; }

        if (player.GetIsGrounded)
        {
            lastGroundedY = playerT.position.y; // ratchets with him as he legitimately descends (stairs, a shaft)
            belowSince = null;
            return;
        }

        if (lastGroundedY == null) return; // no reference yet this frame (shouldn't happen after Start, but safe)

        float depth = lastGroundedY.Value - playerT.position.y;
        if (depth < FallTriggerDepth) { belowSince = null; return; }

        if (belowSince == null) belowSince = Time.time;
        else if (Time.time - belowSince.Value >= FallDeathDelay)
        {
            belowSince = null;
            if (DebugLogging)
                Debug.Log($"[FallSafetyMonitor] {SceneManager.GetActiveScene().name}: Harvey was {depth:F1} units " +
                           $"below his last safe ground for {FallDeathDelay:F1}s with no kill plane catching him; " +
                           "triggering the normal death sequence.");
            LevelManager.Instance?.Respawn(); // same entry point a KillPlane uses; its own "down" guard covers overlap
        }
    }
}
