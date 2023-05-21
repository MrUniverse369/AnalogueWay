using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MenueControl : MonoBehaviour
{
    public static bool isGamePaused = false;
    [SerializeField] private GameObject pauseMenueUI;

    public void LoadMainMenue()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(0);
        isGamePaused = false;

    }
    public void LoadFirstLevel()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(1);
        isGamePaused = false;
    }
    public void QuitGame()
    {

        Debug.Log("Game ShutDown");
        Application.Quit();
    }
    private void Update()
    {
        PauseMenue();
    }

    public void PauseMenue()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (isGamePaused)
            {
                Resume();
                Debug.Log("ESCAPE PRESSED RESUME");
            }

            else
            {
                Pause();
                Debug.Log("ESCAPE PRESSED PASUE");
            }
        }
    }

    public void Resume()
    {
        pauseMenueUI.SetActive(false);
        Time.timeScale = 1f;
        isGamePaused = false;
    }

    public void Pause()
    {
        pauseMenueUI.SetActive(true);
        Time.timeScale = 0f;
        isGamePaused = true;
    }
}

