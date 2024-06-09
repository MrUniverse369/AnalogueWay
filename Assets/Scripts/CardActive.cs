using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CardActive : MonoBehaviour
{   [SerializeField] private Image cardImage; 
    [SerializeField] private Sprite activeCard;
    [SerializeField] private Sprite inActiveCard;
    // Start is called before the first frame update
    void Start()
    {
       
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKey(KeyCode.Z))
        {
            cardImage.sprite = activeCard;
         
        }
    }
}
