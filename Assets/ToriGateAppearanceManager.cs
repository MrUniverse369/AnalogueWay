using System.Collections;
using System.Collections.Generic;
using AnalogueWay;
using UnityEngine;

public class ToriGateAppearanceManager : MonoBehaviour
{
    [SerializeField] private Animator animatorRef;

    [SerializeField] private GameObject[] ToriGate;
    public bool insideSpiritWorld = true;
    public bool insideLivingWorld = false;
    bool iSWorld = true;
    // Start is called before the first frame update
    void Start()
    {
        ToriGate = GameObject.FindGameObjectsWithTag("ToriGate");

    }

    void Update()
    {
        if (!changeWorld.lWorld)
        {
            gameObject.GetComponent<Animator>().SetBool("SFA", false);
            animatorRef.SetBool("SBA", false);
        }

    }
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!changeWorld.lWorld && other.CompareTag("PlayerDetectionArea"))
        {
            Debug.Log("Red tori being turned on");
            gameObject.GetComponent<Animator>().SetBool("SFA", true);
            animatorRef.SetBool("SBA", true);
        }
    }
    private void OnTriggerExit2D(Collider2D other)
    {

    }
}
