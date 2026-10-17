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

        fadeOut.SetActive(true);
        Time.timeScale = 0.0f;
        yield return new WaitForSecondsRealtime(5);
        GameSession.SaveProgressForBuildIndex(SceneManager.GetActiveScene().buildIndex + 1); // SAVE: autosave so Continue starts the next level
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex + 1);
        Time.timeScale = 1.0f;
        fadeOut.SetActive(false);

    }
    void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("PlayerDetectionArea")) exitVillage = true;
    }
}
