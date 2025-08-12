using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using AnalogueWay;
public class EnemyController : MonoBehaviour
{
    [SerializeField] private float bounceSpeed;
    [SerializeField] private MovingTiles mTRef;
    [SerializeField] private Animator animator;
    [SerializeField] private bool deActivate;
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

        if (deActivate)
        {
            animCounter -= 1 * Time.deltaTime;
            animator.SetBool("DeathAnim", deActivate);
        }
        if (animCounter < 0 && deActivate == true)
        {
            gameObject.SetActive(false);
            deActivate = false;
            //animCounter = 0.6f;
        }

    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("pStomp") && gameObject.CompareTag("japanBoss") != true)
        {
            animCounter = 0.7f;
            deActivate = true;
        }

        if (other.CompareTag("PlayerDetectionArea") && ActiveCardManager.speedBoostPowerUp != false && gameObject.CompareTag("japanBoss") != true)
        {
            animCounter = 0.5f;
            BounceSpeed = 0;
            deActivate = true;
        }
    }
}
