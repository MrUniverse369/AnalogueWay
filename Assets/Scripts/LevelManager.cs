using System.Collections;
using AnalogueWay;
using UnityEngine;
public class LevelManager : MonoBehaviour
{
    [SerializeField] private float respawnDelay;
    [SerializeField] private PlayerController pScriptRef;
    [SerializeField] private ResetOnRespawn[] resetObjsArr;
    [SerializeField] private Transform respawnPos;
    [SerializeField] private GameObject gameOverScreen;
    public GameObject GameOverScreen => gameOverScreen; // UI: LevelUIStyle restyles it
    private AudioManager audioManager;

    /// <summary>BOSS: raised after the player has respawned, so things like the boss fight can reset.</summary>
    public static event System.Action Respawned;

    // PERF: one shared reference instead of every hazard scanning the whole scene for it.
    private static LevelManager instance;
    public static LevelManager Instance
    {
        get
        {
            if (instance == null) instance = FindObjectOfType<LevelManager>();
            return instance;
        }
    }

    public Transform ReSpawnPos
    {
        get { return respawnPos; }
        set { respawnPos = value; }
    }

    private void Awake()
    {
        instance = this; // PERF:
        audioManager = AudioManager.Instance; // PERF:
    }

    // Start is called before the first frame update
    void Start()
    {
        if (pScriptRef == null) pScriptRef = FindObjectOfType<PlayerController>(); // ARCADE: keep the scene's reference; the search misses a player hidden by a game over
        resetObjsArr = FindObjectsOfType<ResetOnRespawn>();

    }

    // ARCADE: where Harvey last stood still on solid ground, used instead of the checkpoint on game over
    private const float SafeGroundTime = 0.25f; // on the same ground this long before it counts
    private Vector3? arcadeSafePoint;
    private float groundedFor;
    private readonly RaycastHit2D[] groundHits = new RaycastHit2D[8];
    private Collider2D lastGround; // PERF: the ground checked last time, so standing on it costs one raycast
    private bool lastGroundSolid;

    private void FixedUpdate()
    {
        TrackArcadeSafePoint(); // SHIELD: tracked in every mode now (the Healing Stone's pit rescue uses it); only Arcade respawns there
    }

    /// <summary>SHIELD (2026-10-21): the Healing Stone caught Harvey falling into a pit: put him back on the last safe
    /// ground (or the checkpoint) with nothing lost and no game over.</summary>
    public void RescueToSafeGround()
    {
        if (pScriptRef == null) return;
        Vector3 from = pScriptRef.transform.position;
        Vector3 to = arcadeSafePoint.HasValue ? arcadeSafePoint.Value : ReSpawnPos.transform.position;
        PixelFX.Burst(from, 10, 0.8f, 2.2f, new[] { PixelFX.Cream, PixelFX.Hex("ff9db1") }, 0.3f, 0.6f, 1, PixelShape.Cross, PixelMotion.Float, true); // FX: the heal colours
        pScriptRef.transform.position = to;
        Rigidbody2D rb = pScriptRef.GetComponent<Rigidbody2D>();
        if (rb != null) rb.linearVelocity = Vector2.zero;
        PixelFX.Burst(to, 12, 0.8f, 2.2f, new[] { PixelFX.Cream, PixelFX.Hex("ff9db1") }, 0.3f, 0.6f, 1, PixelShape.Cross, PixelMotion.Float, true);
        groundedFor = 0f;
    }

    private void TrackArcadeSafePoint()
    {
        // NINELIVES (2026-10-29): never while he can't be hurt (the flicker after a hit or a respawn) or while he touches
        // a hazard. A spike walked into during the flicker doesn't fire again once it ends, so he could stand in it
        // unharmed and it became the safe point; every arcade respawn then put him back in it, and it killed him again
        // straight away: a death loop (in a Nine Lives run it took six lives in a few seconds).
        if (pScriptRef == null || !pScriptRef.gameObject.activeInHierarchy || !pScriptRef.GetIsGrounded || !OnSolidGround(pScriptRef.transform)
            || PlayerController.playerInviInvincible || InHazard(pScriptRef.transform))
        {
            groundedFor = 0f;
            return;
        }
        groundedFor += Time.fixedDeltaTime;
        if (groundedFor >= SafeGroundTime) arcadeSafePoint = pScriptRef.transform.position;
    }

    // NINELIVES: anything that hurts on touch (spikes, kill planes, enemies: HurtPlayer) overlapping Harvey's body
    private readonly Collider2D[] hazardHits = new Collider2D[8];
    private static readonly ContactFilter2D HazardFilter = new ContactFilter2D { useTriggers = true };

    private bool InHazard(Transform player)
    {
        int count = Physics2D.OverlapBox(player.position, new Vector2(0.5f, 1f), 0f, HazardFilter, hazardHits);
        for (int i = 0; i < count; i++)
            if (hazardHits[i] != null && hazardHits[i].GetComponentInParent<HurtPlayer>() != null) return true;
        return false;
    }

    // ground that stays put: not a moving or falling tile, an enemy or a hazard, so respawning there is safe
    private bool OnSolidGround(Transform player)
    {
        int count = Physics2D.RaycastNonAlloc(player.position, Vector2.down, groundHits, 2f);
        for (int i = 0; i < count; i++)
        {
            Collider2D c = groundHits[i].collider;
            if (c == null || c.isTrigger || c.transform.IsChildOf(player)) continue;
            if (c == lastGround) return lastGroundSolid; // PERF:
            lastGround = c;
            lastGroundSolid = !(c.attachedRigidbody != null && c.attachedRigidbody.bodyType != RigidbodyType2D.Static)
                && c.GetComponentInParent<ResetOnRespawn>() == null && c.GetComponentInParent<MovingTiles>() == null
                && c.GetComponentInParent<MovingObstacles>() == null && c.GetComponentInParent<HurtPlayer>() == null;
            return lastGroundSolid; // the first solid thing under his feet
        }
        return false;
    }

    public void Respawn()
    {
        StartCoroutine("RespawnCo");

    }

    // RESPAWNHOLD (2026-10-23): Harvey gets his controls back only once the follow camera is back on him: its timed
    // glide (CameraController.ReturnToPlayer) has finished and CameraController.IsOnPlayer (the check LevelLoader
    // uses) holds for SettledFrames frames, within SettledDistance. The glide is timed so the whole wait, from the
    // death to control back, stays inside MaxRespawnHold; at that point the camera is snapped onto him if it still
    // isn't there. A switched-off follow camera (the boss arenas' fixed view) has nothing to wait for. A death during
    // the wait starts a new respawn, and only the newest one hands control back. PlayerFX shows the wait.
    private const float SettledDistance = 0.25f; // four art pixels
    private const int SettledFrames = 2;
    private const float MaxRespawnHold = 3f;     // death to control back, at most
    private const float SettleSlack = 0.2f;      // kept for the settle check after the glide
    private const float MinGlide = 0.4f, MaxGlide = 1.2f;
    private int respawnRun;
    private CameraController followCamera;

    public IEnumerator RespawnCo()
    {
        int run = ++respawnRun; // RESPAWNHOLD:
        bool alive = pScriptRef != null && pScriptRef.gameObject.activeSelf; // NINELIVES: a second call while he's already down is the same death
        float deadline = Time.time + MaxRespawnHold;
        if (pScriptRef != null) pScriptRef.HoldForRespawn(true); // RESPAWNHOLD: no input from the moment he dies
        if (PowerUpManager.Instance != null) PowerUpManager.Instance.ResetAll(); // POWERUP: replaces speedBoostPowerUp = false below
        audioManager.PlaySfx(audioManager.gameOverSound);
        ScreenGrade.GreyPulse(); // FX: a brief greyscale pulse on game over
        PixelFX.Burst(pScriptRef.transform.position, 12, 1.5f, 3.5f, new[] { PixelFX.Cream, PixelFX.Hex("98b2b1") }, 0.4f, 0.7f, 1, PixelShape.Square, PixelMotion.Float, true); // FX:
        pScriptRef.gameObject.SetActive(false);
        // NINELIVES: a Nine Lives run takes one of its nine lives per death; with the last one gone there is no respawn
        // (NineLives shows the game-over card and goes to the main menu)
        if (alive && NineLives.LoseLife()) yield break;
        if (gameOverScreen != null) gameOverScreen.SetActive(true); // RESPAWN: a level missing its screen (Z1A3 was) still respawns
        yield return new WaitForSeconds(respawnDelay);
        // ARCADE: back where he fell (the last safe ground before it); saved play goes back to the checkpoint
        pScriptRef.transform.position = GameSession.IsArcade && arcadeSafePoint.HasValue ? arcadeSafePoint.Value : ReSpawnPos.transform.position;
        groundedFor = 0f;
        pScriptRef.gameObject.SetActive(true);
        // RESPAWNHOLD: the camera glides back in whatever is left of the wait
        float glide = Mathf.Clamp(deadline - Time.time - SettleSlack, MinGlide, MaxGlide);
        if (FollowCamera != null && FollowCamera.isActiveAndEnabled) FollowCamera.ReturnToPlayer(glide);
        pScriptRef.RespawnArrived(glide);
        PlayerFX.RespawnArrive();
        if (gameOverScreen != null) gameOverScreen.SetActive(false);
        HurtPlayer.pLivesCount = 3;
        PlayerController.chi = 90;
        HurtPlayer.oopsCount = HurtPlayer.oopsCount + 1;
        for (int i = 0; i < resetObjsArr.Length; i++)
        {
            resetObjsArr[i].ResetObjs();
            resetObjsArr[i].gameObject.SetActive(true);

        }
        changeWorld.RequestRefresh(); // PERF: the loop above re-enables objects from both worlds; let changeWorld correct them
        Respawned?.Invoke(); // BOSS:
        yield return CameraSettled(deadline, run); // RESPAWNHOLD:
        if (run == respawnRun) pScriptRef.HoldForRespawn(false);
    }

    // RESPAWNHOLD: until the follow camera is back on Harvey (or there is no following camera, or time is up)
    private IEnumerator CameraSettled(float deadline, int run)
    {
        int steady = 0;
        while (Time.time < deadline) // game time: a pause doesn't use the wait up
        {
            if (run != respawnRun) yield break; // a newer respawn has taken over
            CameraController cam = FollowCamera;
            if (cam == null || !cam.isActiveAndEnabled) yield break;
            steady = !cam.Returning && cam.IsOnPlayer(SettledDistance) ? steady + 1 : 0;
            if (steady >= SettledFrames) yield break;
            yield return null;
        }
        CameraController late = FollowCamera;
        if (run == respawnRun && late != null && late.isActiveAndEnabled) late.SnapToPlayer(); // never hand control back with him off screen
    }

    private CameraController FollowCamera
    {
        get
        {
            if (followCamera == null && Camera.main != null) followCamera = Camera.main.GetComponent<CameraController>();
            return followCamera;
        }
    }

}
