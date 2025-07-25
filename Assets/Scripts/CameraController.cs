using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraController : MonoBehaviour
{
    [SerializeField] private Transform pObj;

    [SerializeField] private float followAheadDefault;
    [SerializeField] private float followAhead;
    [SerializeField] private float smoothing;
    [SerializeField] private GameObject pRef;
    private Vector3 targetPos;
    // Start is called before the first frame update
    void Start()
    {

    }
    public float Smoothing
    {
        get { return smoothing; }
        set { smoothing = value; }
    }
    public float FollowAhead
    {
        get { return followAhead; }
        set { followAhead = value; }
    }

    public float FollowAheadDefault
    {
        get { return followAheadDefault; }
    }
    // Update is called once per frame
    void Update()
    {
        FollowPlayer();
    }
    private void FollowPlayer()
    {
        targetPos = new Vector3(pObj.position.x - FollowAhead, pObj.transform.position.y + 1.5f, transform.position.z);
        if (pObj.localScale.x > 0) targetPos = new Vector3(pObj.position.x + FollowAhead, pObj.transform.position.y - 0.25f, transform.position.z);
        if (pObj.localScale.x < 0) targetPos = new Vector3(pObj.position.x - FollowAhead, pObj.transform.position.y - 0.25f, transform.position.z);

        transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * Smoothing);
        // if (pObj.transform.position.y < -8);


    }
}
