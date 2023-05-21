using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace AnalogueWay
{
    public class LevelManager : MonoBehaviour
    {
        [SerializeField] private MovingObstacles mORef;
        // Start is called before the first frame update
        void Start()
        {
           // mORef.GetComponent<MovingObstacles>();
            GameObject.Find("FallingOb1").GetComponent<MovingObstacles>();


        }

        // Update is called once per frame
        void FixedUpdate()
        {
           // mORef.moveOb();

        }
    }
}