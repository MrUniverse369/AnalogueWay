using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;


public class TownExit : MonoBehaviour
{
    [SerializeField] private GameObject fadeOut;
    [SerializeField] private bool exitVillage;
    // Start is called before the first frame update
    void Start()
    {
        exitVillage = false;
    }

    // Update is called once per frame
    void Update()
    {
        if (exitVillage) StartCoroutine(nameof(VillageExitPauseCo));
    }
    public IEnumerator VillageExitPauseCo()
    {

        fadeOut.SetActive(true);
        Time.timeScale = 0.0f;
        yield return new WaitForSecondsRealtime(5);
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex + 1);
        Time.timeScale = 1.0f;
        fadeOut.SetActive(false);

    }
    void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("PlayerDetectionArea")) exitVillage = true;
    }
}
