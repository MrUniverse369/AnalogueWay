using System.Collections;
using System.Collections.Generic;
using AnalogueWay;
using UnityEngine;

public class ResetOnRespawn : MonoBehaviour
{
    [SerializeField] private Vector2 startPos;
    [SerializeField] private Quaternion startRot;
    [SerializeField] private Vector2 startScale;
    [SerializeField] private Rigidbody2D startRb;
    [SerializeField] private MovingObstacles fallingObj;
    [SerializeField] private GameObject activeCardManScriptRef;
    
    // Start is called before the first frame update
    void Start()
    {
        startPos = transform.position;
        startRot = transform.rotation;
        startScale = transform.localScale;
//        activeCardManScriptRef = GameObject.FindGameObjectWithTag("activeCardManager");

        if (GetComponent<Rigidbody2D>()!= null)
        {
            startRb = GetComponent<Rigidbody2D>();
        }

        if (GetComponent<MovingObstacles>()!= null)
        {
            fallingObj = GetComponent<MovingObstacles>();
        }

    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void ResetObjs()
    {
        transform.position = startPos;
        transform.rotation = startRot;
        transform.localScale = startScale;
        if (GetComponent<Rigidbody2D>() != null) startRb.velocity = Vector2.zero;
        if (GetComponent<MovingObstacles>() != null) fallingObj.pDetected = false;
        
    }
}
