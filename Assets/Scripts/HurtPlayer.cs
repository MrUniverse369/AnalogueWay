using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace AnalogueWay
{
    public class HurtPlayer : MonoBehaviour
    {
        [SerializeField] private GameObject playerRef;
        // Start is called before the first frame update
        void Start()
        {
            playerRef = GameObject.Find("Player");
        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            if (collision.CompareTag("Player"))
            {
                Debug.Log("Player has hit death plane");
                playerRef.SetActive(false);
                playerRef.transform.position = new Vector3(0, 0, 0);
                playerRef.SetActive(true);

            }
        }
    }
}