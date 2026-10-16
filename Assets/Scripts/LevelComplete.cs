using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;

public class LevelComplete : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI levelCompleteTimerText;
    [SerializeField] private TextMeshProUGUI levelCompleteCoinCountText;
    [SerializeField] private TextMeshProUGUI levelCompleteScoreText;
    [SerializeField] private int finalCoinCount;
    [SerializeField] private int finalScore;


    public TextMeshProUGUI GetSetFinalTimerText
    {
        get { return levelCompleteTimerText; }
        set { levelCompleteTimerText = value; }
    }
    
    public TextMeshProUGUI GetCoinsCountText
    {
        get { return levelCompleteCoinCountText; }
        set { levelCompleteCoinCountText = value; }
    }

    public TextMeshProUGUI GetCompleteScoreText 
    {
        get { return levelCompleteScoreText; }
        set { levelCompleteScoreText = value; }
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
