using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// PAUSE (2026-10-21): menus answer the controller in every level. The scenes were set up at different times: some
/// EventSystems still had the old StandaloneInputModule (reads the legacy Input Manager, so a DualSense or Xbox pad
/// can't move the selection) and others had their own copies of the UI actions. On every scene load each EventSystem
/// gets an InputSystemUIInputModule with the Input System's default UI actions, the same set the working levels use,
/// so the stick, d-pad, Cross and the keyboard arrows/Enter behave the same in every menu. The scenes are not edited.
/// </summary>
public static class UIInputModuleFix
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Init()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        foreach (EventSystem es in Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            Fix(es);
    }

    private static void Fix(EventSystem es)
    {
        var legacy = es.GetComponent<StandaloneInputModule>();
        if (legacy != null)
        {
            legacy.enabled = false;
            Object.Destroy(legacy);
        }
        var module = es.GetComponent<InputSystemUIInputModule>();
        if (module == null) module = es.gameObject.AddComponent<InputSystemUIInputModule>();
        module.enabled = true;
        module.AssignDefaultActions();
    }
}
