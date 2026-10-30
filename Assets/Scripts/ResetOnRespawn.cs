using AnalogueWay;
using UnityEngine;

public class ResetOnRespawn : MonoBehaviour
{
    [SerializeField] private Vector2 startPos;
    [SerializeField] private Quaternion startRot;
    [SerializeField] private Vector2 startScale;
    private float startScaleZ = 1f; // B5: startScale is a Vector2 (serialized, so kept), which reset the z scale to 0
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
        startScaleZ = transform.localScale.z; // B5:

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
        transform.localScale = new Vector3(startScale.x, startScale.y, startScaleZ); // B5: was = startScale (z became 0)
        if (ownRb != null) ownRb.linearVelocity = Vector2.zero; // PERF: was two GetComponent calls per object per respawn
        if (ownFallingObj != null) ownFallingObj.GetPDetected = false; // PERF:
    }
}
