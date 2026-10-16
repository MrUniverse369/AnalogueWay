using UnityEngine;


    abstract public class CharecterBehaviour : MonoBehaviour
    {
        public int attackPower { get; set; }
        public int lives { get; set; }

        abstract public void CharMovement();
        abstract public void CharJump();


    }
