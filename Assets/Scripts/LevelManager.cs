using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
namespace AnalogueWay
{
    public class LevelManager : MonoBehaviour
    {
        [SerializeField] private float respawnDelay;
        [SerializeField] private PlayerController pScriptRef;
        [SerializeField] private WalkOnAirCard wOnAirCardScriptRef;
        [SerializeField] private ResetOnRespawn[] resetObjsArr;
        [SerializeField] private Transform respawnPos;
        [SerializeField] private Image cardImage;
        [SerializeField] private Sprite activeCard;
        [SerializeField] private Sprite inActiveCard;
        private AudioManager audioManager;

        public Transform ReSpawnPos
        {
            get { return respawnPos; }
            set { respawnPos = value; }
        }

        private void Awake()
        {
            audioManager = GameObject.FindGameObjectWithTag("Audio").GetComponent<AudioManager>();
        }

        // Start is called before the first frame update
        void Start()
        {
            pScriptRef = FindObjectOfType<PlayerController>();
            resetObjsArr = FindObjectsOfType<ResetOnRespawn>();
            ReSpawnPos.transform.position = respawnPos.transform.position;

        }

        void Update()
        {

        }

        public void Respawn()
        {
            StartCoroutine("RespawnCo");

        }

        public IEnumerator RespawnCo()
        {
            audioManager.PlaySfx(audioManager.gameOverSound);
            pScriptRef.gameObject.SetActive(false);
            yield return new WaitForSeconds(respawnDelay);
            pScriptRef.transform.position = ReSpawnPos.transform.position;
            pScriptRef.gameObject.SetActive(true);
            HurtPlayer.oopsCount = HurtPlayer.oopsCount + 1;
            for (int i = 0; i < resetObjsArr.Length; i++)
            {
                resetObjsArr[i].ResetObjs();
                resetObjsArr[i].gameObject.SetActive(true);

            }
        }

    }
}