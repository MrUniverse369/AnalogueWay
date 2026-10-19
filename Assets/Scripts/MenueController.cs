using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenue : MonoBehaviour
{

    public void loadLevel()
    {
        LevelLoader.Load(0); // LOADING:
    }
    public void QuitGame()
    {
        Debug.Log("Game ShutDown");
        Application.Quit();
    }
}
