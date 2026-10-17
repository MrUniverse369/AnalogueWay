using UnityEngine;

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

        [SerializeField] private GameObject worldOneTreasureChest;
        [SerializeField] private GameObject worldOneBackGround;
        [SerializeField] private GameObject[] worldOneFloatingTrap;
        [SerializeField] private GameObject[] worldOneFallingTile;
        [SerializeField] private GameObject worldOneCover;
        [SerializeField] private GameObject worldTwo;
        [SerializeField] private GameObject worldTwoCoins;
        [SerializeField] private GameObject worldTwoBackGround;
        [SerializeField] private GameObject worldTwoCover;
        [SerializeField] private GameObject worldOneBounceBox;
        [SerializeField] private GameObject worldTwoBounceBox;
        public static bool lWorld = false;
        public static bool lworldGateActive = false;
        public static bool SworldGateActive = false;

        // PERF: world objects are only switched when something could have changed them, not every frame.
        private static int refreshGeneration;   // bumped by RequestRefresh()
        private bool hasApplied;                // false until this instance has applied the world (reset on enable)
        private bool appliedWorld;
        private int appliedGeneration;

        /// <summary>Call after code re-enables world objects (e.g. respawn) so every active instance reapplies once.</summary>
        public static void RequestRefresh() => refreshGeneration++;

        private void OnEnable()
        {
            hasApplied = false; // PERF: several instances switch each other on/off; reapply whenever this one wakes up
        }



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

        void Update()
        {
            // PERF: skip when nothing changed (used to set every world object active/inactive every frame)
            if (hasApplied && appliedWorld == lWorld && appliedGeneration == refreshGeneration) return;
            bool world = lWorld;
            int generation = refreshGeneration;
            ApplyWorld();
            hasApplied = true;
            appliedWorld = world;
            appliedGeneration = generation;
        }

        // PERF: unchanged body of the old Update
        private void ApplyWorld()
        {
            if (lWorld)
            {
                // Spirit world is inactive
                worldOne.SetActive(false);
                worldOneTreasureChest.SetActive(false);
                worldOneBackGround.SetActive(false);
                worldOneCoins.SetActive(false);
                worldOneBounceBox.SetActive(false);
                worldOneCover.SetActive(false);
                SetSwitchesActive(worldOneFloatingTrap, false);
                SetSwitchesActive(worldOneFallingTile, false);
                SetSwitchesActive(worldOneSwitches, false);
                SworldGateActive = false;



                // livingWorld world is active
                lworldGateActive = true;
                worldTwo.SetActive(true);
                worldTwoBackGround.SetActive(true);
                worldTwoCoins.SetActive(true);
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
                worldTwoCover.SetActive(false);
                worldTwoBounceBox.SetActive(false);
                SetSwitchesActive(worldTwoSwitches, false);
                SetSwitchesActive(EnemyTwo, false);
                lworldGateActive = false;


                // Spirit world is active
                SworldGateActive = true;
                worldOne.SetActive(true);
                worldOneBackGround.SetActive(true);
                worldOneCoins.SetActive(true);
                worldOneTreasureChest.SetActive(true);
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
