using UnityEngine;
using UnityEngine.EventSystems; // PAUSE:
using UnityEngine.InputSystem; // PAUSE:
using UnityEngine.SceneManagement;
using UnityEngine.UI; // PAUSE:

public class MenueControl : MonoBehaviour
{
    public static bool isGamePaused = false;
    [SerializeField] private GameObject pauseMenueUI;

    /// <summary>MENUS: HarveyMenus adds its Talismans and Bag buttons to this panel.</summary>
    public GameObject PausePanel => pauseMenueUI;

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
    // ZONEMAP: main menu Zone Select button
    public void LoadZoneSelect()
    {
        Time.timeScale = 1f;
        isGamePaused = false;
        SceneManager.LoadScene(ZoneSelectMap.SceneName);
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
        if (HarveyMenus.BlocksPause) return; // MENUS: Esc/Options belong to the open Talisman Holder, Bag or shop
        // PAUSE: only a real pause panel toggles. In the main menu pauseMenueUI is the menu itself, so pausing
        // there used to freeze time and then hide the whole menu (with this script on it), leaving no way back.
        if (pauseMenueUI != null && pauseMenueUI != gameObject && PausePressed())
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

    // PAUSE: Esc on the keyboard, or the gamepad Start button (Options on a PS5 DualSense, Menu on Xbox)
    private static bool PausePressed()
    {
        if (Input.GetKeyDown(KeyCode.Escape)) return true;
        Gamepad pad = Gamepad.current;
        return pad != null && pad.startButton.wasPressedThisFrame;
    }

    public void Resume()
    {
        pauseMenueUI.SetActive(false);
        Time.timeScale = 1f;
        isGamePaused = false;
        if (PowerUpManager.Instance != null) PowerUpManager.Instance.CancelQueuedPresses(); // PAUSE: the Cross that clicked Resume isn't a dash
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null); // PAUSE:
    }

    public void Pause()
    {
        pauseMenueUI.SetActive(true);
        Time.timeScale = 0f;
        isGamePaused = true;
        SelectFirstButton(); // PAUSE: gives a controller something to navigate from
    }

    // PAUSE: highlight the first button in the pause panel (Resume) so the stick/d-pad and Cross work straight away
    private void SelectFirstButton()
    {
        if (EventSystem.current == null) return;
        Button first = pauseMenueUI.GetComponentInChildren<Button>();
        EventSystem.current.SetSelectedGameObject(first != null ? first.gameObject : null);
    }
}

