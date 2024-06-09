using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class CardCoolDownTimer : MonoBehaviour
{
    [SerializeField] private ActiveCardManager activeCardManagerScriptRef;
    [SerializeField] private TextMeshProUGUI timerText;
    // Start is called before the first frame update
    void Start()
    {
        
    }

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
