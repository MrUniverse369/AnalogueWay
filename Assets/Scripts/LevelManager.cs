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
       
        // Start is called before the first frame update
        void Start()
        {
            pScriptRef = FindObjectOfType<PlayerController>();
            resetObjsArr = FindObjectsOfType<ResetOnRespawn>();

        }

        void Update()
        {
           /* if (Input.GetKey(KeyCode.Z))
            {
                wOnAirCardScriptRef.gameObject.SetActive(true);
                wOnAirCardScriptRef.GetAirTime = 5;
                cardImage.sprite = activeCard;
            }
    
            if (wOnAirCardScriptRef.GetAirTime < 0)
            {
                wOnAirCardScriptRef.gameObject.SetActive(false);  
                cardImage.sprite = inActiveCard;
            }*/
        }

        public void Respawn()
        {
            StartCoroutine("RespawnCo");
         
        }

        public IEnumerator RespawnCo()
        {
            pScriptRef.gameObject.SetActive(false);
            yield return new WaitForSeconds(respawnDelay);
            pScriptRef.transform.position = respawnPos.transform.position;
            pScriptRef.gameObject.SetActive(true);
            for (int i = 0; i < resetObjsArr.Length; i++)
            {
                resetObjsArr[i].ResetObjs();
                resetObjsArr[i].gameObject.SetActive(true);
                
            }
        }

    }
}