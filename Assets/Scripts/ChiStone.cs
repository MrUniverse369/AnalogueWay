using System.Collections;
using System.Collections.Generic;
using AnalogueWay;
using UnityEngine;

public class ChiStone : MonoBehaviour
{

    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }

    void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.gameObject.CompareTag("PlayerDetectionArea") && PlayerController.chi < 90) gameObject.SetActive(false);
    }
}
