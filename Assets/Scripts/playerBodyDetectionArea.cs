using System.Collections;
using System.Collections.Generic;
using AnalogueWay;
using UnityEngine;

public class playerBodyDetectionArea : MonoBehaviour
{
    [SerializeField] private float pFeetRadius;
    [SerializeField] private LayerMask isPFeetOnGround;
    [SerializeField] private Animator animatorRef;
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
        if (other.CompareTag("HurtPlayer"))
        {
            animatorRef.SetTrigger("IsHurtStanding");
        }
        if (other.CompareTag("ChiStone") && PlayerController.mana < 100)
        {
            PlayerController.mana += 34;
        }

        isGrounded = Physics2D.OverlapCircle(pFeet.position, pFeetRadius, isPFeetOnGround);
        if (other.gameObject.CompareTag("CheckPoint"))
        {
            lManager.ReSpawnPos.transform.position = other.transform.position;
        }
        //if the player is touching the ground when the enemy has
        // made contact we push the player in the direction opposite of the enemy 
        if (other.gameObject.CompareTag("Enemy") && isGrounded != false)
        {
            animatorRef.SetTrigger("isHurt");
            pushBackCounter = pushBackLenght;
            enemyScale = other.gameObject.transform.localScale;
        }

        if (other.gameObject.CompareTag("WorldOneSwitch"))
        {
            changeWorld.lWorld = true;


        }
        if (other.gameObject.CompareTag("WorldTwoSwitch"))
        {
            changeWorld.lWorld = false;

        }
    }

}
