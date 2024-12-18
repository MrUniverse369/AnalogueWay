using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace AnalogueWay
{
    public class changeWorld : MonoBehaviour
    {
        [SerializeField] private GameObject worldOne;
        [SerializeField] private GameObject[] worldOneSwitches;
        [SerializeField] private GameObject[] worldTwoSwitches;
        [SerializeField] private GameObject[] Enemy;
        [SerializeField] private GameObject[] EnemyTwo;

        [SerializeField] private Sprite switchSprite;
        [SerializeField] private Sprite originSprite;
        [SerializeField] private GameObject worldOneTree;
        [SerializeField] private GameObject worldOneSpikes;
        [SerializeField] private GameObject worldOneBackGround;
        [SerializeField] private GameObject worldTwo;
        [SerializeField] private GameObject worldTwoBackGround;
        [SerializeField] private bool switchWorld;
        [SerializeField] public static bool sWorld = false;



        // Start is called before the first frame update
        void Start()
        {
            // Find all switches with the respective tags
            worldOneSwitches = GameObject.FindGameObjectsWithTag("WorldOneSwitch");
            worldTwoSwitches = GameObject.FindGameObjectsWithTag("WorldTwoSwitch");
            Enemy = GameObject.FindGameObjectsWithTag("SpiritEnemy");
            EnemyTwo = GameObject.FindGameObjectsWithTag("LivingEnemy");
            switchWorld = false;
        }

        // Update is called once per frame
        void Update()
        {


            if (sWorld != false)
            {
                // Spirit world is inactive
                worldOne.SetActive(false);
                worldOneBackGround.SetActive(false);
                ToriGateAppearanceManager.insideSpiritWorld = false;
                SetSwitchesActive(worldOneSwitches, false);


                // livingWorld world is active
                worldTwo.SetActive(true);
                worldTwoBackGround.SetActive(true);
                SetSwitchesActive(worldTwoSwitches, true);

                //spirit world Enemies 
                SetSwitchesActive(Enemy, false);
                SetSwitchesActive(EnemyTwo, true);
                //   Debug.Log("SwitchWorld One implemented");


                //ToriGateSWitch

                gameObject.GetComponent<Animator>().SetBool("TF", true);

                // animatorRef.SetTrigger("ToriBack");

            }
            else
            {


                //living world is inactive
                worldTwo.SetActive(false);
                worldTwoBackGround.SetActive(false);
                SetSwitchesActive(worldTwoSwitches, false);


                // Spirit world is active
                worldOne.SetActive(true);
                worldOneBackGround.SetActive(true);
                ToriGateAppearanceManager.insideSpiritWorld = true;
                SetSwitchesActive(worldOneSwitches, true);
                SetSwitchesActive(Enemy, true);
                SetSwitchesActive(EnemyTwo, false);

                Debug.Log("SwitchWorld Two implemented");


                //ToriGateSWitch

                //   gameObject.GetComponent<Animator>().SetBool("TF", false);


            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            Debug.Log(other);
            if (other.gameObject.CompareTag("PlayerDetectionArea"))
            {
                Debug.Log("SwitchWorld toggled");
                switchWorld = !switchWorld;
            }
        }

        // Helper function to activate or deactivate switches
        private void SetSwitchesActive(GameObject[] switches, bool isActive)
        {
            foreach (GameObject switchObject in switches)
            {
                if (switchObject != null)
                {
                    switchObject.SetActive(isActive);
                }
            }
        }


    }
}
