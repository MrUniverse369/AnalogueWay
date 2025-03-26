using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AnalogueWay
{
    public class HurtPlayer : MonoBehaviour
    {
        [SerializeField] private GameObject playerRef;
        [SerializeField] private const int PLIVES = 3;
        public static int oopsCount = 0;
        public static int pLivesCount = 3;
        [SerializeField] private GameObject pRespawnPos;
        private LevelManager lmanagerScriptRef;
        // Start is called before the first frame update
        void Start()
        {
            pLivesCount = PLIVES;
            playerRef = GameObject.Find("Player");
            lmanagerScriptRef = FindObjectOfType<LevelManager>();

        }

        private void OnTriggerEnter2D(Collider2D other)
        {

            if (other.CompareTag("PlayerDetectionArea"))
            {
                // playerRef.GetComponent<PlayerController>().PushBackPlayer();
                if (PlayerController.playerInviInvincible == false) pLivesCount -= 1;
                if (pLivesCount <= 0) lmanagerScriptRef.Respawn();
            }

            if (other.CompareTag("PlayerDetectionArea") && gameObject.CompareTag("KillPlane"))
            {
                // playerRef.GetComponent<PlayerController>().PushBackPlayer();
                if (PlayerController.playerInviInvincible == false) pLivesCount = 0;
                if (pLivesCount <= 0) lmanagerScriptRef.Respawn();
            }
        }
    }
}