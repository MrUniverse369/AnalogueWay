using AnalogueWay;
using UnityEngine;

public class DetectPlayer : MonoBehaviour
{
    [SerializeField] private GameObject pRef;
    [SerializeField] private GameObject fallingTileRef;

    private void OnTriggerEnter2D(Collider2D other)
    {


        if (other.CompareTag("PlayerDetectionArea"))
        {
            fallingTileRef.GetComponent<MovingObstacles>().GetPDetected = true;
        }

    }
}
