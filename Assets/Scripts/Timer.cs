using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class Timer : MonoBehaviour
{
   public TextMeshProUGUI timerText;
    [SerializeField] private LevelComplete lcScriptRef;

    private float elapsedTime;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        elapsedTime += Time.deltaTime;
        int mins = Mathf.FloorToInt(elapsedTime / 60);
        int secs = Mathf.FloorToInt(elapsedTime % 60);
        timerText.text = string.Format("Time:"+"{0:00}:{1:00}",mins,secs);
        if (Input.GetKey(KeyCode.E))
        {
            //Debug.Log(timerText.text);
            lcScriptRef.GetSetFinalTimerText.text = timerText.text;
        }

    }
}
