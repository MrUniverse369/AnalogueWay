using System.Collections;
using System.Collections.Generic;
using AnalogueWay;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using System;
using UnityEngine.SceneManagement;

public class ActiveCardManager : MonoBehaviour
{
    PlayerControls pControls;
    bool pOneOn;
    bool pTwoOn;
    bool pThreeOn;
    [SerializeField] private Image cardImage;
    [SerializeField] private Image cardImage2;
    [SerializeField] private Image cardImage3;
    [SerializeField] private Sprite activeCard;
    [SerializeField] private Sprite inActiveCard;
    [SerializeField] private Sprite activeCardTwo;
    [SerializeField] private Sprite inActiveCardTwo;
    [SerializeField] private Sprite activeCardThree;
    [SerializeField] private Sprite inActiveCardThree;
    [SerializeField] private PlayerController pScriptRef;
    [SerializeField] private float speedBoost;
    [SerializeField] private float speedBoostMultiplier;
    [SerializeField] private float JspeedBoostMultiplier;
    [SerializeField] public float cardCoolDown;
    private CameraController camControllerScriptRef;
    public static bool jumpPowerUp = false;
    public static bool speedBoostPowerUp = false;
    private float airTime;
    private static float sCard;
    private bool buttonTwoIsActive;
    private bool buttonThreeIsActive;
    private bool activateCardOne;
    private int activeCardNumber;
    private AudioManager audioManager;
    private int index;

    private void Awake()
    {
        pOneOn = false;
        audioManager = GameObject.FindGameObjectWithTag("Audio").GetComponent<AudioManager>();
        pControls = new PlayerControls();
        pControls.Gameplay.powerUpOne.performed += ctx => powerUpOne();
        pControls.Gameplay.powerUpTwo.performed += ctx => this.powerUpTwo();
        pControls.Gameplay.powerUpThree.performed += ctx => powerUpThree();


    }

    void powerUpOne()
    {
        pOneOn = true;
        Debug.Log("Sqaure button registers Ps5");
    }
    void powerUpTwo()
    {
        pTwoOn = true;

    }
    void powerUpThree()
    {
        pThreeOn = true;

    }

    void OnEnable()
    {

        pControls.Gameplay.Enable();
    }

    void OnDisable()
    {


        pControls.Gameplay.Disable();
    }

    // Start is called before the first frame update
    void Start()
    {
        activeCardNumber = 0;
        airTime = 0;
        activateCardOne = false;
        camControllerScriptRef = GameObject.FindGameObjectWithTag("MainCamera").GetComponent<CameraController>();
    }

    // Update is called once per frame
    void Update()
    {
        ActivateCard(SelectCard());

        index = SceneManager.GetActiveScene().buildIndex;
    }

    private int SelectCard()
    {

        if (Input.GetKeyDown(KeyCode.Z) && cardCoolDown < 1 || pOneOn != false && cardCoolDown < 1)
        {
            activeCardNumber = 1;
            cardCoolDown = 8;
            audioManager.PlaySfx(audioManager.powerUpSound);
            pOneOn = false;
        }
        if (Input.GetKey(KeyCode.X) && cardCoolDown < 1 || pTwoOn != false && cardCoolDown < 1)
        {
            activeCardNumber = 2;
            cardCoolDown = 1.5f;
            audioManager.PlaySfx(audioManager.powerUpSound);
            pTwoOn = false;
        }



        if (Input.GetKey(KeyCode.C) && cardCoolDown < 1 || pThreeOn != false && cardCoolDown < 1)
        {
            Debug.Log("SUPER JUMPING");
            activeCardNumber = 3;
            cardCoolDown = 2;
            audioManager.PlaySfx(audioManager.powerUpSound);
            pThreeOn = false;

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

        //SPEEED BOOST POWER UP CODE 
        if (this.activeCardNumber == 2 && cardCoolDown >= 1 && index != 1 && index != 2 && index != 3)
        {
            if (pScriptRef.GetIsGrounded != false)
            {
                cardImage2.sprite = activeCardTwo;
                pScriptRef.GetSpeed = speedBoost * speedBoostMultiplier;
                speedBoostPowerUp = true;
                cardImage.color = Color.white;
            }
        }
        else
        {
            cardImage2.sprite = inActiveCardTwo;
            speedBoostPowerUp = false;
            pScriptRef.GetSpeed = pScriptRef.GetSpeedDefault;
        }

        if (this.activeCardNumber == 3 && cardCoolDown > 1)
        {
            Debug.Log("SuperJUMOPPGFF");
            cardImage3.sprite = activeCardThree;
            cardImage.color = Color.white;
            pScriptRef.GetJSpeed = pScriptRef.GetSpeedDefault * JspeedBoostMultiplier;
        }
        else
        {
            cardImage3.sprite = inActiveCardThree;
            pScriptRef.GetJSpeed = 5.0f;

        }

        cardCoolDown += -1 * Time.deltaTime;

    }

}
