using System.Collections;
using System.Collections.Generic;
using AnalogueWay;
using UnityEngine;

public class DetectPlayer : MonoBehaviour
{
    [SerializeField] private GameObject pRef;
    [SerializeField] private GameObject fallingTileRef;
    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }

    private void OnTriggerEnter2D(Collider2D other)
    {


        if (other.CompareTag("Player"))
        {
            fallingTileRef.GetComponent<MovingObstacles>().GetPDetected = true;
        }

    }
}
