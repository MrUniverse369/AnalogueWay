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
        GameSession.NewGame(); // SAVE: Play starts fresh
        GameSession.SaveProgressForBuildIndex(1); // SAVE: and replaces the old save
        SceneManager.LoadScene(1);
        isGamePaused = false;
    }
    // SAVE: hook the main menu Continue button to this. Starts a new game if there is no usable save.
    public void ContinueGame()
    {
        Time.timeScale = 1f;
        isGamePaused = false;
        if (!GameSession.TryContinue()) LoadFirstLevel();
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
            }

            else
            {
                Pause();
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

