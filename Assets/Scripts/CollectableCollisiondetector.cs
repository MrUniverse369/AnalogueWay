using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class CollectableCollisiondetector : MonoBehaviour
{

    public static int coinCollected = 0;
    private AudioManager audioManager;
    private void Awake()
    {
        audioManager = GameObject.FindGameObjectWithTag("Audio").GetComponent<AudioManager>();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {

        if (other.gameObject.CompareTag("PlayerDetectionArea"))
        {
            coinCollected = coinCollected + 1;
            audioManager.PlaySfx(audioManager.coinSound);
            gameObject.SetActive(false);
        }

    }
}
