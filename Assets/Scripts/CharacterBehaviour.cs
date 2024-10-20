using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AnalogueWay
{

    abstract public class CharecterBehaviour : MonoBehaviour
    {
        public int attackPower { get; set; }
        public int lives { get; set; }

        // Start is called before the first frame update
        void Start()
        {

        }

        abstract public void CharMovement();
        abstract public void CharJump();


    }
}