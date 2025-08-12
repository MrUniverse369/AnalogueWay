using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class JapanBossAttacks : MonoBehaviour
{
    //public static bool pDetected = false;
    // Start is called before the first frame update
    [SerializeField] GameObject bossRef;
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        /* if (!pDetected)
         {
             transform.position = new Vector2(5 * Time.deltaTime, 0);
         }*/
    }

    void OnTriggerEnter2D(Collider2D collision)
    {
        Debug.Log(collision);
        if (collision.CompareTag("PlayerDetectionArea"))
        {
            Debug.Log("PLAYER HIT BY BOSS");
            bossRef.transform.position = new Vector2(5 * Time.deltaTime, 0);
            // pDetected = true;
        }

    }

    void OnTriggerExit2D(Collider2D collision)
    {
        // pDetected = false;
    }
}
