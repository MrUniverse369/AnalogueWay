using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OpenChest : MonoBehaviour
{
    [SerializeField] private Animator animatorRef;
    private bool isChestOpened = false; // Tracks if the chest has been opened

    // Update is called once per frame
    void Update()
    {
        // No need for continuous checks here; logic will be handled in the trigger methods
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (other.CompareTag("PlayerDetectionArea") && !isChestOpened)
        {
            if (Input.GetKeyDown(KeyCode.E)) // Check for E key press
            {
                isChestOpened = true; // Mark the chest as opened
                                      // animatorRef.SetBool("ChestOpen", true); // Trigger animation
                animatorRef.SetTrigger("ChestOpenTrig");
                Debug.Log("CHEST OPEN");
            }
        }
    }

}
