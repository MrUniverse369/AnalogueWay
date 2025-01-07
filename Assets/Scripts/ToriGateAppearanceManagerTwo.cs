using System.Collections;
using System.Collections.Generic;
using AnalogueWay;
using UnityEngine;

public class ToriGateAppearanceManagerTwo : MonoBehaviour
{
    [SerializeField] private Animator animatorRef;
    [SerializeField] private GameObject[] ToriGateBack;
    public bool insideLivingWorld = false;
    bool iSWorld = true;
    // Start is called before the first frame update

    void Start()
    {
        ToriGateBack = GameObject.FindGameObjectsWithTag("ToriGateBack");
        // gameObject.GetComponent<Animator>().SetBool("LFA", true);
        //   animatorRef.SetBool("LBA", true);
    }


    // Update is called once per frame
    void Update()
    {
        Debug.Log("Inside lWorld is:" + changeWorld.lWorld);
        if (changeWorld.lWorld)
        {
            gameObject.GetComponent<Animator>().SetBool("LFA", false);
            animatorRef.SetBool("LBA", false);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        Debug.Log("inside the trigga of green gate");
        Debug.Log(other.gameObject.name);

        //LFA = Living active front LBA = living back active
        if (changeWorld.lWorld && other.CompareTag("PlayerDetectionArea"))
        {
            Debug.Log("Spirit world gate active");
            gameObject.GetComponent<Animator>().SetBool("LFA", true);
            animatorRef.SetBool("LBA", true);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {


    }
}
