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
        if (pIsTouchingTreasure && !levelEnding)
        {
            levelEnding = true;
            StartCoroutine(NextLevelPauseCo());
        }
    }


    public IEnumerator NextLevelPauseCo()
    {
        // Freeze game
        Time.timeScale = 0;

        // Play audio and activate the level complete UI

        audioManager.PlaySfx(audioManager.winSound);
        levelComplete.gameObject.SetActive(true);
        levelComplete.GetComponent<LevelComplete>().GetSetFinalTimerText.text = timerScriptRef.timerText.text;
        levelComplete.GetComponent<LevelComplete>().GetCoinsCountText.text =
            "Coins: " + CollectableCollisiondetector.coinCollected.ToString();
        levelComplete.GetComponent<LevelComplete>().GetCompleteScoreText.text =
            "Score: " + (CollectableCollisiondetector.coinCollected * 1.5f);

        yield return new WaitForSecondsRealtime(5);
        GameSession.SaveProgressForBuildIndex(SceneManager.GetActiveScene().buildIndex + 1); // SAVE: autosave so Continue starts the next level
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex + 1);


        Time.timeScale = 1;

        levelComplete.gameObject.SetActive(false);

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
