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
        if (pScriptRef == null || !pScriptRef.gameObject.activeInHierarchy || !pScriptRef.GetIsGrounded || !OnSolidGround(pScriptRef.transform))
        {
            groundedFor = 0f;
            return;
        }
        groundedFor += Time.fixedDeltaTime;
        if (groundedFor >= SafeGroundTime) arcadeSafePoint = pScriptRef.transform.position;
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

    public IEnumerator RespawnCo()
    {
        if (PowerUpManager.Instance != null) PowerUpManager.Instance.ResetAll(); // POWERUP: replaces speedBoostPowerUp = false below
        audioManager.PlaySfx(audioManager.gameOverSound);
        ScreenGrade.GreyPulse(); // FX: a brief greyscale pulse on game over
        PixelFX.Burst(pScriptRef.transform.position, 12, 1.5f, 3.5f, new[] { PixelFX.Cream, PixelFX.Hex("98b2b1") }, 0.4f, 0.7f, 1, PixelShape.Square, PixelMotion.Float, true); // FX:
        pScriptRef.gameObject.SetActive(false);
        if (gameOverScreen != null) gameOverScreen.SetActive(true); // RESPAWN: a level missing its screen (Z1A3 was) still respawns
        yield return new WaitForSeconds(respawnDelay);
        // ARCADE: back where he fell (the last safe ground before it); saved play goes back to the checkpoint
        pScriptRef.transform.position = GameSession.IsArcade && arcadeSafePoint.HasValue ? arcadeSafePoint.Value : ReSpawnPos.transform.position;
        groundedFor = 0f;
        pScriptRef.gameObject.SetActive(true);
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
    }

}
