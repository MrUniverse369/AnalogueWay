using System.Collections;
using System.Collections.Generic;
using AnalogueWay;
using UnityEngine;

public class playerBodyDetectionArea : MonoBehaviour
{
    [SerializeField] private float pFeetRadius;
    [SerializeField] private LayerMask isPFeetOnGround;
    [SerializeField] private Transform pFeet;
    [SerializeField] private LevelManager lManager;
    public float pushBackCounter;  // Knockback force
    [SerializeField] private float pushBackLenght;
    private bool isGrounded;
    Vector2 enemyScale;

    // Start is called before the first frame update
    void Start()
    {
        lManager = GameObject.FindGameObjectWithTag("LevelManager").GetComponent<LevelManager>();
    }

    // Handle player pushback when hitting the enemy trigger
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Enemy")) Debug.Log("EnemyDetected In playbdArea");

        isGrounded = Physics2D.OverlapCircle(pFeet.position, pFeetRadius, isPFeetOnGround);
        if (other.gameObject.CompareTag("CheckPoint"))
        {
            lManager.ReSpawnPos.transform.position = other.transform.position;
        }
        //if the player is touching the ground when the enemy has
        // made contact we push the player in the direction opposite of the enemy 
        if (other.gameObject.CompareTag("Enemy") && isGrounded != false)
        {
            pushBackCounter = pushBackLenght;
            enemyScale = other.gameObject.transform.localScale;
        }
    }

}