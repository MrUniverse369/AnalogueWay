using System.Collections;
using System.Collections.Generic;
using AnalogueWay;
using UnityEngine;
using UnityEngine.UI;

public class HealthUIManager : MonoBehaviour
{
    [SerializeField] private Image playerHealthUI;
    [SerializeField] private Sprite healthOne;
    [SerializeField] private Sprite healthTwo;
    [SerializeField] private Sprite healthThree;
    [SerializeField] private Sprite healthFour;


    [SerializeField] private Image playerManaUI;
    [SerializeField] private Sprite manaOne;
    [SerializeField] private Sprite manaTwo;
    [SerializeField] private Sprite manaThree;
    [SerializeField] private Sprite manaFour;
    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        updatePlayerHealthUI();
        updatePlayerManaUI();
    }

    private void updatePlayerHealthUI()
    {
        if (HurtPlayer.pLivesCount >= 3) { playerHealthUI.sprite = healthOne; }
        if (HurtPlayer.pLivesCount == 2) { playerHealthUI.sprite = healthTwo; }
        if (HurtPlayer.pLivesCount == 1) { playerHealthUI.sprite = healthThree; }
        if (HurtPlayer.pLivesCount <= 0) { playerHealthUI.sprite = healthFour; }
    }

    private void updatePlayerManaUI()
    {
        if (PlayerController.mana > 70 && PlayerController.mana < 100) { playerManaUI.sprite = manaOne; }
        if (PlayerController.mana > 40 && PlayerController.mana < 69) { playerManaUI.sprite = manaTwo; }
        if (PlayerController.mana > 10 && PlayerController.mana < 39) { playerManaUI.sprite = manaThree; }
        if (PlayerController.mana < 10) { playerManaUI.sprite = manaFour; }
    }
}
