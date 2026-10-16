using TMPro;
using UnityEngine;

public class CardCoolDownTimer : MonoBehaviour
{
    [SerializeField] private ActiveCardManager activeCardManagerScriptRef;
    [SerializeField] private TextMeshProUGUI timerText;

    // Update is called once per frame
    void Update()
    {
        if (activeCardManagerScriptRef.cardCoolDown > 0)
        {
            timerText.gameObject.SetActive(true);
            DisplayCoolDownTime();
            
        }
        else
        {
          timerText.gameObject.SetActive(false);
        }
    }

    private void DisplayCoolDownTime()
    {
        timerText.text = Mathf.RoundToInt(activeCardManagerScriptRef.cardCoolDown).ToString();
    }
}
