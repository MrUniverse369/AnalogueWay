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
        GameSession.EndArcade(); // ARCADE: quitting a run keeps nothing
        SceneManager.LoadScene(0);
        isGamePaused = false;

    }
    // SLOTS: main menu Play button. Opens the save files: pick an empty one for a new game, or a used one to
    // continue it or start over in it (SaveSlotMenu asks before anything is overwritten).
    public void LoadFirstLevel()
    {
        Time.timeScale = 1f;
        isGamePaused = false;
        SaveSlotMenu.Open(SaveSlotMenu.Mode.Play);
    }
    // SLOTS: main menu Continue button. Opens the save files to pick one to continue.
    public void ContinueGame()
    {
        Time.timeScale = 1f;
        isGamePaused = false;
        SaveSlotMenu.Open(SaveSlotMenu.Mode.Continue);
    }
    // ARCADE: main menu Arcade button. An unsaved run from the first level.
    public void StartArcade()
    {
        Time.timeScale = 1f;
        isGamePaused = false;
        GameSession.StartArcade();
    }
    // ZONEMAP: main menu Zone Select button (plays the save file used last)
    public void LoadZoneSelect()
    {
        Time.timeScale = 1f;
        isGamePaused = false;
        GameSession.EndArcade(); // ARCADE:
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

