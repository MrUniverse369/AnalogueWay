using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MoveToObject : MonoBehaviour
{
    [SerializeField] GameObject pref; // Reference to the target object (e.g., player)
    [SerializeField] private float speed; // Movement speed
    Vector2 currentPosition;
    Vector2 targetPosition;
    public static bool coinIsMovingToPlayer = false;

    private GameObject[] coins; // Array to hold all the coins


}
