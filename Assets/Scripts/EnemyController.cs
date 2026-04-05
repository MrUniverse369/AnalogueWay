using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using AnalogueWay;
public class EnemyController : MonoBehaviour
{
    [SerializeField] private float bounceSpeed;
    [SerializeField] private MovingTiles mTRef;
    [SerializeField] private Animator animator;
    [SerializeField] private bool enemyDeathAnimActivate;
    [SerializeField] private float animCounter;
    public float BounceSpeed
    {
        get { return bounceSpeed; }
        set { bounceSpeed = value; }
    }
    // Start is called before the first frame update
    void Start()
    {
        animator = GetComponent<Animator>();
    }

    // Update is called once per frame
    void Update()
    {

        if (enemyDeathAnimActivate)
        {
            animCounter -= 1 * Time.deltaTime;
            animator.SetBool("DeathAnim", enemyDeathAnimActivate);
            Debug.Log("Death Anim played ");
        }
        if (animCounter < 0 && enemyDeathAnimActivate == true)
        {
            gameObject.SetActive(false);
            enemyDeathAnimActivate = false;
            //animCounter = 0.6f;
        }

    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("pStomp") && gameObject.CompareTag("japanBoss") != true)
        {
            animCounter = 0.7f;
            enemyDeathAnimActivate = true;
            Debug.Log("deathanime activated from stomp");
        }

        if (other.CompareTag("PlayerDetectionArea") && ActiveCardManager.speedBoostPowerUp != false && gameObject.CompareTag("japanBoss") != true)
        {
            animCounter = 0.5f;
            BounceSpeed = 0;
            enemyDeathAnimActivate = true;
            Debug.Log("deathanime activated from dash");
        }
    }
}
