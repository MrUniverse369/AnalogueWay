using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class NextLevel : MonoBehaviour
{
    [SerializeField] private GameObject levelComplete;
    [SerializeField] private Timer timerScriptRef;
    [SerializeField] private GameObject pScriptObjRef;
    private PlayerController pScriptRef;
    private bool pIsTouchingTreasure;
    private bool levelEnding; // PERF: B1 fix - the end-of-level sequence must only start once
    private AudioManager audioManager;

    // BOSSCHEST: in a boss arena Harvey takes the treasure with the action button, so he can open the chests the boss
    // dropped first; elsewhere touching it ends the level, as before. The bosses switch this on for their treasure.
    [Tooltip("Harvey must press Up / E at the treasure to finish (boss arenas). Off: touching it finishes the level.")]
    [SerializeField] private bool requireAction;
    private bool upWasHeld = true;
    public bool RequireAction
    {
        get => requireAction;
        set => requireAction = value;
    }

    private void Awake()
    {
        audioManager = AudioManager.Instance; // PERF:
        pScriptRef = pScriptObjRef.GetComponent<PlayerController>();
    }

    // Start is called before the first frame update
    void Start()
    {
        pIsTouchingTreasure = false;
    }

    // Update is called once per frame
    void Update()
    {
        // PERF: B1 fix - this used to start a new coroutine every frame of the 5 s freeze (~300 of them),
        // each restarting the win sound and redoing the level-complete setup.
        bool up = ActionButton.Held(); // BOSSCHEST:
        bool pressed = up && !upWasHeld;
        upWasHeld = up;
        if (!pIsTouchingTreasure || levelEnding) return;
        if (requireAction) // BOSSCHEST:
        {
            if (MenueControl.isGamePaused || HarveyMenus.IsOpen || DialogueBox.IsOpen || OpenChest.PlayerAtChest) return; // a chest in reach answers first
            HarveyMenus.Prompt(this, ActionButton.Prompt + "TAKE THE TREASURE AND LEAVE");
            if (!pressed) return;
        }
        levelEnding = true;
        StartCoroutine(NextLevelPauseCo());
    }


    public IEnumerator NextLevelPauseCo()
    {
        // Freeze game
        Time.timeScale = 0;

        // Play audio and activate the level complete UI

        audioManager.PlaySfx(audioManager.winSound);
        StartCoroutine(Celebrate()); // FX: confetti and a coin shower (unscaled: the game is frozen)
        levelComplete.gameObject.SetActive(true);
        levelComplete.GetComponent<LevelComplete>().GetSetFinalTimerText.text = timerScriptRef.timerText.text;
        levelComplete.GetComponent<LevelComplete>().GetCoinsCountText.text =
            "Coins: " + CollectableCollisiondetector.coinCollected.ToString();
        levelComplete.GetComponent<LevelComplete>().GetCompleteScoreText.text =
            "Score: " + (CollectableCollisiondetector.coinCollected * 1.5f);

        yield return new WaitForSecondsRealtime(5);
        GameSession.FinishLevel(); // FLOW: back to the Zone Select with the next level unlocked (was straight into it)


        Time.timeScale = 1;

        levelComplete.gameObject.SetActive(false);

    }




    // FX: confetti bursts from the treasure and coins shower down across the screen for the first few seconds
    private IEnumerator Celebrate()
    {
        Vector3 at = transform.position + Vector3.up * 0.4f;
        Camera cam = Camera.main;
        PixelFX.Burst(at, 30, 3f, 7f, PixelFX.Confetti, 1.2f, 1.8f, 1, PixelShape.Square, PixelMotion.Fall, false, PixelLayer.Front, 90f, 100f, 0f, true);
        for (int wave = 0; wave < 14; wave++)
        {
            if (cam != null)
            {
                float h = cam.orthographicSize, w = h * cam.aspect;
                Vector3 c = cam.transform.position;
                for (int i = 0; i < 5; i++)
                {
                    var p = new Vector3(c.x + Random.Range(-w, w), c.y + h + 0.2f, 0f);
                    bool coin = i < 2;
                    PixelFX.Emit(p, new Vector2(Random.Range(-0.5f, 0.5f), Random.Range(-2f, -0.5f)), coin ? PixelFX.Pick(PixelFX.CoinGold) : PixelFX.Pick(PixelFX.Confetti),
                        2.2f, coin ? 2 : 1, PixelShape.Square, PixelMotion.Fall, coin, PixelLayer.Front, true);
                }
            }
            yield return new WaitForSecondsRealtime(0.15f);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            pIsTouchingTreasure = true;
        }

    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (other.CompareTag("PlayerDetectionArea"))
        {
            pIsTouchingTreasure = true;
        }

    }

    private void OnTriggerExit2D(Collider2D other)
    {
        pIsTouchingTreasure = false;
    }
}
