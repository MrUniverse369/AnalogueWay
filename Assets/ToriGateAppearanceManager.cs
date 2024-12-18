using System.Collections;
using System.Collections.Generic;
using AnalogueWay;
using UnityEngine;

public class ToriGateAppearanceManager : MonoBehaviour
{
    [SerializeField] private Animator animatorRef;

    [SerializeField] private GameObject[] ToriGate;

    public static bool SpiritGatehasExited = false;
    public static bool insideSpiritWorld = true;
    // Start is called before the first frame update
    void Start()
    {
        ToriGate = GameObject.FindGameObjectsWithTag("ToriGate");

    }

    void Update()
    {
        if (insideSpiritWorld && changeWorld.sWorld)
        {
            gameObject.GetComponent<Animator>().SetBool("SToridle", true);
            animatorRef.SetBool("SBTidle", true);
        }

        Debug.Log("InsideSWorldIS:" + insideSpiritWorld);
    }
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (insideSpiritWorld && other.CompareTag("PlayerDetectionArea") && gameObject.CompareTag("ToriGate"))
        {
            Debug.Log("Inside OnTriggerEnter2D");
            gameObject.GetComponent<Animator>().SetBool("SToriActive", true);
            animatorRef.SetBool("SBTactive", true);
        }

        /* if (!SpiritGatehasExited && other.CompareTag("PlayerDetectionArea") && gameObject.CompareTag("ToriGateBack"))
         {
             Debug.Log("Inside OnTriggerEnter2D");
             gameObject.GetComponent<Animator>().SetBool("SToriActive", true);
             animatorRef.SetBool("SBTactive", true);
         }*/
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("PlayerDetectionArea"))
        {
            Debug.Log("Inside OnTriggerExit2D");
            gameObject.GetComponent<Animator>().SetBool("SToriActive", false);
            animatorRef.SetBool("SBTactive", false);

            SpiritGatehasExited = true;
            insideSpiritWorld = false;
        }
    }
}
