using System.Collections;
using System.Collections.Generic;
using AnalogueWay;
using UnityEngine;

public class ToriGateAppearanceManager : MonoBehaviour
{
    [SerializeField] private Animator animatorRef;
    void Start()
    {

    }

    void Update()
    {
        if (changeWorld.SworldGateActive != true)
        {
            gameObject.GetComponent<Animator>().SetBool("LBA", false);
            animatorRef.SetBool("LBA", false);
        }

        if (changeWorld.SworldGateActive && !changeWorld.lworldGateActive)
        {
            Debug.Log("green tori being turned on");
            gameObject.GetComponent<Animator>().SetBool("LBA", true);
            animatorRef.SetBool("LBA", true);
            changeWorld.SworldGateActive = true;
            changeWorld.lworldGateActive = false;
        }
    }
}

