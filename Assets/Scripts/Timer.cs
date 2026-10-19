using UnityEngine;
using TMPro;

public class Timer : MonoBehaviour
{
    public TextMeshProUGUI timerText;
    public TextMeshProUGUI oppsText;
    [SerializeField] private LevelComplete lcScriptRef;

    private float elapsedTime;

    /// <summary>BOSSCHEST: seconds on the level clock (what the HUD shows).</summary>
    public float Elapsed => elapsedTime;
    private int shownSeconds = -1; // PERF: last value written to timerText
    private int shownLives = int.MinValue; // PERF: last value written to oppsText

    // Update is called once per frame
    void Update()
    {
        elapsedTime += Time.deltaTime;
        int mins = Mathf.FloorToInt(elapsedTime / 60);
        int secs = Mathf.FloorToInt(elapsedTime % 60);
        // PERF: only format (and allocate) a new string when the displayed value changes
        if (mins * 60 + secs != shownSeconds)
        {
            shownSeconds = mins * 60 + secs;
            timerText.text = string.Format("Time:" + "{0:00}:{1:00}", mins, secs);
        }
        if (HurtPlayer.pLivesCount != shownLives)
        {
            shownLives = HurtPlayer.pLivesCount;
            oppsText.text = string.Format("Lives:" + "{0}", HurtPlayer.pLivesCount);
        }
        if (Input.GetKey(KeyCode.E))
        {
            lcScriptRef.GetSetFinalTimerText.text = timerText.text;
        }

    }
}
