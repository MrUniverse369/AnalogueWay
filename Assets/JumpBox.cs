using System;
using System.Collections;
using System.Collections.Generic;
using AnalogueWay;
using Unity.VisualScripting;
using UnityEngine;

public class JumpBox : MonoBehaviour
{
    [SerializeField] private PlayerController pref;
    [SerializeField] private float jForce;
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
           Debug.Log("JumpBox tocuhed player");
           pref.GetComponent<Rigidbody2D>().velocity = new Vector2(pref.GetComponent<Rigidbody2D>().velocity.x, jForce);
        }
    }
}
