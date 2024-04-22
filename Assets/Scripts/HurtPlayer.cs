using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AnalogueWay
{
    public class HurtPlayer : MonoBehaviour
    {
        [SerializeField] private GameObject playerRef;

      [SerializeField]   private GameObject pRespawnPos;
      private LevelManager lmanagerScriptRef;
        // Start is called before the first frame update
        void Start()
        { 
            playerRef = GameObject.Find("Player");
            lmanagerScriptRef = FindObjectOfType<LevelManager>();
            
        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            if (collision.CompareTag("Player"))
            {
               lmanagerScriptRef.Respawn();
            }
        }
    }
}