using System;
using System.Collections;
using System.Collections.Generic;
using AnalogueWay;
using UnityEngine;

public class StompEnemy : MonoBehaviour
{

    [SerializeField] private PlayerController pref;
    [SerializeField] private float setactiveCountDown = 3;
    [SerializeField] private bool enemTouched;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Enemy"))
        {
            pref.GetComponent<Rigidbody2D>().velocity = new Vector2(pref.GetComponent<Rigidbody2D>().velocity.x, other.GetComponent<EnemyController>().BounceSpeed);
            //   other.gameObject.SetActive(false);
            CollectableCollisiondetector.coinCollected = CollectableCollisiondetector.coinCollected + 10;
        }
    }

}
