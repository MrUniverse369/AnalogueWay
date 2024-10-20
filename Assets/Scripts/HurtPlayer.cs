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
        [SerializeField] private int pLives;
        public static int oopsCount = 0;
        public static int pLivesCount = 5;
        [SerializeField] private GameObject pRespawnPos;
        private LevelManager lmanagerScriptRef;
        // Start is called before the first frame update
        void Start()
        {
            pLivesCount = pLives;
            playerRef = GameObject.Find("Player");
            lmanagerScriptRef = FindObjectOfType<LevelManager>();

        }

        private void OnTriggerEnter2D(Collider2D other)
        {

            if (other.CompareTag("Player"))
            {
                // playerRef.GetComponent<PlayerController>().PushBackPlayer();
                if (PlayerController.playerInviInvincible == false) pLivesCount -= 1;
                Debug.Log("PlayerLivesCount" + pLivesCount);
                if (pLivesCount <= 0) lmanagerScriptRef.Respawn();
            }
        }
    }
}