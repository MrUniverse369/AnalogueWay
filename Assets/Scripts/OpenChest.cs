using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OpenChest : MonoBehaviour
{
    [SerializeField] private Animator animatorRef;
    private bool isChestOpened = false;
       
    private void OnTriggerStay2D(Collider2D other)
    {
        if (other.CompareTag("PlayerDetectionArea") && !isChestOpened)
        {
            
                isChestOpened = true;
                animatorRef.SetTrigger("ChestOpenTrig");
            
        }
    }

}
