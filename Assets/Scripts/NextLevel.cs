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
            if (pIsTouchingTreasure && Input.GetKey(KeyCode.E))
            {


                Debug.Log("Player is touching treasure and speed is 0");
            }
            NextLevelPause();

        }
    }

    public void NextLevelPause()
    {
        StartCoroutine(nameof(NextLevelCo));
    }
    public IEnumerator NextLevelCo()
    {
        if (Input.GetKeyDown(KeyCode.E))
        {
            Time.timeScale = 0;
            audioManager.PlaySfx(audioManager.winSound);
            levelComplete.gameObject.SetActive(true);
            levelComplete.GetComponent<LevelComplete>().GetSetFinalTimerText.text = timerScriptRef.timerText.text;
            levelComplete.GetComponent<LevelComplete>().GetCoinsCountText.text =
                "Coins:" + CollectableCollisiondetector.coinCollected.ToString();
            levelComplete.GetComponent<LevelComplete>().GetCompleteScoreText.text =
                "Score" + CollectableCollisiondetector.coinCollected * 1.5f;
            yield return new WaitForSeconds(5);
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex + 1);
            levelComplete.gameObject.SetActive(false);
            Time.timeScale = 1;
            Debug.Log("Time should be resuming ");

        }
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
