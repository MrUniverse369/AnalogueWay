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
        [SerializeField] private GameObject worldOneCoins;
        [SerializeField] private GameObject walkOnAirWorldOne;
        [SerializeField] private GameObject worldOneTreasureChest;
        [SerializeField] private GameObject worldOneBackGround;
        [SerializeField] private GameObject[] worldOneFloatingTrap;
        [SerializeField] private GameObject[] worldOneFallingTile;
        [SerializeField] private GameObject worldOneCover;
        [SerializeField] private GameObject worldTwo;
        [SerializeField] private GameObject worldTwoCoins;
        [SerializeField] private GameObject walkOnAirWorldTwo;
        [SerializeField] private GameObject worldTwoBackGround;
        [SerializeField] private GameObject worldTwoCover;
        [SerializeField] private GameObject worldOneBounceBox;
        [SerializeField] private GameObject worldTwoBounceBox;
        [SerializeField] public static bool lWorld = false;



        // Start is called before the first frame update
        void Start()
        {
            // Find all switches with the respective tags
            worldOneSwitches = GameObject.FindGameObjectsWithTag("WorldOneSwitch");
            worldTwoSwitches = GameObject.FindGameObjectsWithTag("WorldTwoSwitch");
            worldOneFloatingTrap = GameObject.FindGameObjectsWithTag("worldOneFloatingTrap");
            worldOneFallingTile = GameObject.FindGameObjectsWithTag("FTile");
            Enemy = GameObject.FindGameObjectsWithTag("SpiritEnemy");
            EnemyTwo = GameObject.FindGameObjectsWithTag("LivingEnemy");

        }

        // Update is called once per frame
        void Update()
        {

            if (lWorld)
            {
                // Spirit world is inactive
                worldOne.SetActive(false);
                worldOneTreasureChest.SetActive(false);
                worldOneBackGround.SetActive(false);
                worldOneCoins.SetActive(false);
                walkOnAirWorldOne.SetActive(false);
                worldOneBounceBox.SetActive(false);
                worldOneCover.SetActive(false);
                SetSwitchesActive(worldOneFloatingTrap, false);
                SetSwitchesActive(worldOneFallingTile, false);
                SetSwitchesActive(worldOneSwitches, false);



                // livingWorld world is active
                worldTwo.SetActive(true);
                worldTwoBackGround.SetActive(true);
                worldTwoCoins.SetActive(true);
                walkOnAirWorldTwo.SetActive(true);
                worldTwoCover.SetActive(true);
                worldTwoBounceBox.SetActive(true);
                SetSwitchesActive(worldTwoSwitches, true);

                //spirit world Enemies 
                SetSwitchesActive(Enemy, false);
                SetSwitchesActive(EnemyTwo, true);


            }
            else
            {
                //living world is inactive
                worldTwo.SetActive(false);
                worldTwoBackGround.SetActive(false);
                worldTwoCoins.SetActive(false);
                walkOnAirWorldTwo.SetActive(false);
                worldTwoCover.SetActive(false);
                worldTwoBounceBox.SetActive(false);
                SetSwitchesActive(worldTwoSwitches, false);
                SetSwitchesActive(EnemyTwo, false);


                // Spirit world is active
                worldOne.SetActive(true);
                worldOneBackGround.SetActive(true);
                worldOneCoins.SetActive(true);
                worldOneTreasureChest.SetActive(true);
                walkOnAirWorldOne.SetActive(true);
                worldOneBounceBox.SetActive(true);
                worldOneCover.SetActive(true);
                SetSwitchesActive(worldOneSwitches, true);
                SetSwitchesActive(Enemy, true);
                SetSwitchesActive(worldOneFloatingTrap, true);
                SetSwitchesActive(worldOneFallingTile, true);

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
