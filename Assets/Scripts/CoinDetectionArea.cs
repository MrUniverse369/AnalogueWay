using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CoinDetectionArea : MonoBehaviour
{
    [SerializeField] private GameObject coinRef;
    [SerializeField] private float coinSpeed;
    // Start is called before the first frame update
    void Start()
    {
        coinRef = GameObject.FindGameObjectWithTag("Coin");
    }

    // Update is called once per frame
    void Update()
    {

    }


    private void OnTriggerStay2D(Collider2D other)
    {
        if (other.CompareTag("Coin"))
        {
            other.gameObject.transform.position = Vector2.MoveTowards(other.gameObject.transform.position, transform.position, coinSpeed * Time.deltaTime);

        }
    }
}
