using System;
using System.Collections;
using System.Collections.Generic;
using AnalogueWay;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;

public class NextLevel : MonoBehaviour
{
    [SerializeField] private GameObject levelComplete;
    [SerializeField] private Timer timerScriptRef;
    [SerializeField] private GameObject pScriptObjRef;
    private PlayerController pScriptRef;
    private bool pIsTouchingTreasure;
    private AudioManager audioManager;

    private void Awake()
    {
        audioManager = GameObject.FindGameObjectWithTag("Audio").GetComponent<AudioManager>();
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
        if (pIsTouchingTreasure)
        {
            if (pIsTouchingTreasure && Input.GetKeyDown(KeyCode.E))
            {

                StartCoroutine(nameof(NextLevelPauseCo));
                Debug.Log("Player is touching treasure and speed is 0");
            }


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

        // Wait for 5 real-time seconds while the game is frozen
        yield return new WaitForSecondsRealtime(5);

        // Load the next level
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex + 1);

        // Unfreeze the game (this happens after the scene is loaded)
        Time.timeScale = 1;

        // Optionally deactivate the levelComplete UI after scene transition
        levelComplete.gameObject.SetActive(false);

        Debug.Log("Time should be resuming and next level loaded");
    }



    //loads the next scene 
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            pIsTouchingTreasure = true;
        }
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            pIsTouchingTreasure = true;
        }

    }

    private void OnTriggerExit2D(Collider2D other)
    {
        pIsTouchingTreasure = false;
    }
}
