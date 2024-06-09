using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MovingTiles : MonoBehaviour
{   [SerializeField] private Transform pointA;
    [SerializeField] private Transform pointB;
    [SerializeField] private GameObject objToMove;
    [SerializeField] private float speed;

    private Vector3 CurTargetPos;
    // Start is called before the first frame update
    void Start()
    {
        CurTargetPos = pointB.position;
      

    }

    // Update is called once per frame
    void Update()
    {
        MoveTile();
    }

    private void MoveTile()
    {
        objToMove.transform.position = Vector3.MoveTowards(objToMove.transform.position, CurTargetPos, Time.deltaTime * speed);
        if (objToMove.transform.position == pointB.position) CurTargetPos = pointA.transform.position;
        if (objToMove.transform.position == pointA.position) CurTargetPos = pointB.transform.position;
        
    }
}
