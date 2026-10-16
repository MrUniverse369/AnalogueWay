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
    private Rigidbody2D ownRb; // PERF: cached in Start
    private MovingObstacles ownFallingObj; // PERF: cached in Start

    // Start is called before the first frame update
    void Start()
    {
        startPos = transform.position;
        startRot = transform.rotation;
        startScale = transform.localScale;

        ownRb = GetComponent<Rigidbody2D>(); // PERF:
        ownFallingObj = GetComponent<MovingObstacles>(); // PERF:
        if (ownRb != null)
        {
            startRb = ownRb;
        }

        if (ownFallingObj != null)
        {
            fallingObj = ownFallingObj;
        }

    }

    public void ResetObjs()
    {
        transform.position = startPos;
        transform.rotation = startRot;
        transform.localScale = startScale;
        if (ownRb != null) ownRb.velocity = Vector2.zero; // PERF: was two GetComponent calls per object per respawn
        if (ownFallingObj != null) ownFallingObj.GetPDetected = false; // PERF:
    }
}
