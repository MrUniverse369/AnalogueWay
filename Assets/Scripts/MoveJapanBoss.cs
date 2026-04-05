using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MoveJapanBoss : MonoBehaviour
{

    [SerializeField] private GameObject Pref;
    [SerializeField] private Transform returnSpot;
    [SerializeField] private GameObject objToMove;
    [SerializeField] private float speed;
    public bool enemyTurn;
    private Vector3 CurTargetPos;
    private Vector3 originalScale;


    void Start()
    {
        CurTargetPos = returnSpot.position;
        originalScale = objToMove.transform.localScale;
        Pref = GameObject.FindGameObjectWithTag("PlayerDetectionArea");
    }


    void Update()
    {
        MoveTile();
    }

    private void MoveTile()
    {

        objToMove.transform.position = Vector3.MoveTowards(objToMove.transform.position, CurTargetPos, Time.deltaTime * speed);


        if (objToMove.transform.position == returnSpot.position)
        {
            enemyTurn = true;
            CurTargetPos = Pref.transform.position;
        }

        /* if (objToMove.transform.position == Pref.transform.position)
           {
               enemyTurn = false;
               CurTargetPos = pointB.position;
           }  */
        FlipObject();

    }

    private void FlipObject()
    {

        if (CurTargetPos.x < objToMove.transform.position.x)
        {
            objToMove.transform.localScale = new Vector3(-Mathf.Abs(originalScale.x), originalScale.y, originalScale.z);
        }

        else if (CurTargetPos.x > objToMove.transform.position.x)
        {
            objToMove.transform.localScale = new Vector3(Mathf.Abs(originalScale.x), originalScale.y, originalScale.z);
        }
    }
}
