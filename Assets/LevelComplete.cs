using System.Collections;
using System.Collections.Generic;
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

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        levelCompleteTimerText.text = GetSetFinalTimerText.text;
        if (Input.GetKeyDown(KeyCode.R))
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
