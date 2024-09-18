using System.Collections;
using System.Collections.Generic;
using AnalogueWay;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;


public class ActiveCardManager : MonoBehaviour
{
    [SerializeField] private Image cardImage; 
    [SerializeField] private Image cardImage2;
    [SerializeField] private Image cardImage3;
    [SerializeField] private Sprite activeCard;
    [SerializeField] private Sprite inActiveCard;
    [SerializeField] private Sprite activeCardTwo;
    [SerializeField] private Sprite inActiveCardTwo;
    [SerializeField] private Sprite activeCardThree;
    [SerializeField] private Sprite inActiveCardThree;
    [SerializeField] private WalkOnAirCard wOnAirCardScriptRef;
    [SerializeField] private PlayerController pScriptRef;
    [SerializeField] private float speedBoost;
    [SerializeField] public float cardCoolDown;
    private float airTime;
    private static bool  buttonOneIsActive;
    private static float sCard; 
    private bool buttonTwoIsActive;
    private bool buttonThreeIsActive;
    private bool activateCardOne;
    private int activeCardNumber;
    private AudioManager audioManager;
    private void Awake()
    {
        audioManager = GameObject.FindGameObjectWithTag("Audio").GetComponent<AudioManager>();
    }

    
    // Start is called before the first frame update
    void Start()
    {
        activeCardNumber = 0;
        airTime = 0;
        buttonOneIsActive = false;
        activateCardOne = false;
    }

    // Update is called once per frame
    void Update()
    {
        ActivateCard(SelectCard());
        ActivateWalkOnAir();
    }

    private int SelectCard()
    {

        if (Input.GetKeyDown(KeyCode.Z) && cardCoolDown < 1)
        {
            activeCardNumber = 1;
            cardCoolDown = 8;
            audioManager.PlaySfx(audioManager.powerUpSound);
        }
        if (Input.GetKey(KeyCode.X)&& cardCoolDown < 1)
        {
            activeCardNumber = 2;
            cardCoolDown = 3;
            audioManager.PlaySfx(audioManager.powerUpSound);
        }
        if (Input.GetKey(KeyCode.C)&& cardCoolDown < 1) 
        {
            activeCardNumber = 3;
            cardCoolDown = 2;
            audioManager.PlaySfx(audioManager.powerUpSound);
        }
        return activeCardNumber;
    }


    // ReSharper disable Unity.PerformanceAnalysis
    private void ActivateCard(int selectCard)
    {//note to future self have this function be for activating
     //the card only sprite wise and have a seprate component for activating
        if (selectCard == 1) SwitchCardSprite(selectCard);
        if (selectCard == 2) SwitchCardSprite(selectCard);
        if (selectCard == 3) SwitchCardSprite(selectCard);
    }
    
    private void ActivateWalkOnAir()
    { 
        if (activateCardOne)
        {
            wOnAirCardScriptRef.gameObject.SetActive(true);
            cardImage.sprite = activeCard;
            
        }
        if (!activateCardOne)
        {   
            wOnAirCardScriptRef.gameObject.SetActive(false);  
            cardImage.sprite = inActiveCard;
            
        }
      
    }

    private void SwitchCardSprite(int activeCardNumber)
    {
        if (this.activeCardNumber == 1 && cardCoolDown > 1)
        {
            cardImage.sprite = activeCard;
            cardImage.color = Color.white;
            activateCardOne = true;
        }
        else
        {
            cardImage.sprite = inActiveCard;
            activateCardOne = false;
        }

        if (this.activeCardNumber == 2 && cardCoolDown > 1)
        {
            cardImage2.sprite = activeCardTwo;
            pScriptRef.GetSpeed = speedBoost;
            cardImage.color = Color.white;
        }
        else
        {
            cardImage2.sprite = inActiveCardTwo;
            pScriptRef.GetSpeed = 4.0f;
        }

        if (this.activeCardNumber == 3 && cardCoolDown > 1)
        {
            cardImage3.sprite = activeCardThree;
            cardImage.color = Color.white;
            pScriptRef.GetJSpeed = speedBoost*1.4f;
        }
        else
        {
            cardImage3.sprite = inActiveCardThree;
            pScriptRef.GetJSpeed = 5.0f;
        }
        
        cardCoolDown += -1 * Time.deltaTime;

    }

}
