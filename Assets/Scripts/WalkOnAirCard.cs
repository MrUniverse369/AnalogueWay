using UnityEngine;

public class WalkOnAirCard : MonoBehaviour
{

    private float airTime;
    [SerializeField] private ActiveCardManager activeCardManScriptRef;
    public float GetAirTime
    {
        get { return airTime; }
        set { airTime = value; }
    }
}
