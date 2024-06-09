using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices.WindowsRuntime;
using UnityEngine;

public class WalkOnAirCard : MonoBehaviour
{

    private bool isWallVisible;
    private GameObject walkOnAirRef;
    private Collider2D colRef;
    private float airTime;
    private bool cardIsInPlay;
    [SerializeField] private ActiveCardManager activeCardManScriptRef;
public float GetAirTime
{
    get { return airTime; }
    set { airTime = value; }
}
    // Start is called before the first frame update
    void Start()
    {
      
    }

    // Update is called once per frame
    void Update()
    { 
    }
}
