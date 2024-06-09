using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class CollectableCollisiondetector : MonoBehaviour
{
    public static int coinCollected = 0;
    private void OnTriggerEnter2D(Collider2D other)
    {  

        if (other.gameObject.CompareTag("Player"))
        {
          
            coinCollected = coinCollected + 1;
            gameObject.SetActive(false);
        }
      
    }
}
