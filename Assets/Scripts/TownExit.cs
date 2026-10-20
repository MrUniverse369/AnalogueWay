using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;


public class TownExit : MonoBehaviour
{
    [SerializeField] private GameObject fadeOut;
    [SerializeField] private bool exitVillage;
    private bool exiting; // PERF: B1 fix - the exit sequence must only start once
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

    }
    void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("PlayerDetectionArea")) exitVillage = true;
    }
}
