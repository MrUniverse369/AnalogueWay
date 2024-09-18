using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using AnalogueWay;
public class EnemyController : MonoBehaviour
{
    [SerializeField] private float bounceSpeed;
    [SerializeField] private MovingTiles mTRef;
    public float BounceSpeed
    {
        get { return bounceSpeed;}
        set { bounceSpeed = value; }
    }
    // Start is called before the first frame update
    void Start()
    {
        
        
    }

    // Update is called once per frame
    void Update()
    {
       /* if(mTRef.enemyTurn == false)
        {
            transform.localScale = new Vector3(2, 1, 1);
            
        }
        else
        {
            transform.localScale = new Vector3(-2, 1, 1);
        }*/
    }
}
