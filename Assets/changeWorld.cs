using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class changeWorld : MonoBehaviour
{
    [SerializeField] private GameObject worldOne;
    [SerializeField] private GameObject worldOneTree;

    [SerializeField] private GameObject worldOneSpikes;
    [SerializeField] private GameObject worldOneBackGround;
    [SerializeField] private GameObject worldTwo;
    [SerializeField] private GameObject worldTwoBackGround;
    [SerializeField] private bool switchWorld;
    // Start is called before the first frame update
    void Start()
    {
        // worldOne = GameObject.FindGameObjectWithTag("WorldOne");
        // worldTwo = GameObject.FindGameObjectWithTag("WorldTwo");
        switchWorld = false;
    }

    // Update is called once per frame
    void Update()
    {
        if (switchWorld != false)
        {
            worldOne.SetActive(false);
            worldOneBackGround.SetActive(false);
            worldTwo.SetActive(true);
            worldTwoBackGround.SetActive(true);
            Debug.Log("SwitchWorld implemented");
        }
        else
        {
            worldTwo.SetActive(false);
            worldTwoBackGround.SetActive(false);
            worldOne.SetActive(true);
            worldOneBackGround.SetActive(true);
        }
    }


    private void OnTriggerEnter2D(Collider2D other)
    {
        Debug.Log(other);
        if (other.gameObject.CompareTag("PlayerDetectionArea") && switchWorld != true)
        {
            Debug.Log("SwitchWorld asked");
            switchWorld = true;

        }

    }
}
