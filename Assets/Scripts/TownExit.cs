using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;


public class TownExit : MonoBehaviour
{
    [SerializeField] private GameObject fadeOut;
    [SerializeField] private bool exitVillage;
    private bool exiting; // PERF: B1 fix - the exit sequence must only start once
    /// <summary>B14: true through the village-exit freeze, which the pause menu must not undo either.</summary>
    public static bool Exiting { get; private set; }
    // Start is called before the first frame update
    void Start()
    {
        exitVillage = false;
    }

    // Update is called once per frame
    void Update()
    {
        // PERF: B1 fix - this used to start a new coroutine every frame of the 5 s freeze
        if (exitVillage && !exiting)
        {
            exiting = true;
            Exiting = true; // B14:
            StartCoroutine(VillageExitPauseCo());
        }
    }
    public IEnumerator VillageExitPauseCo()
    {

        // VILLAGES: the newer villages have no fade of their own; the loading screen covers the change instead
        if (fadeOut != null) fadeOut.SetActive(true);
        Time.timeScale = 0.0f;
        yield return new WaitForSecondsRealtime(fadeOut != null ? 5f : 0.3f);
        GameSession.FinishLevel(); // FLOW: back to the Zone Select with the next level unlocked (was straight into it)
        Time.timeScale = 1.0f;
        if (fadeOut != null) fadeOut.SetActive(false);
        Exiting = false; // B14:

    }

    private void OnDestroy()
    {
        if (exiting) Exiting = false; // B14:
    }
    void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("PlayerDetectionArea")) exitVillage = true;
    }
}
