using System.Collections;
using AnalogueWay;
using UnityEngine;
using UnityEngine.UI;
public class LevelManager : MonoBehaviour
{
    [SerializeField] private float respawnDelay;
    [SerializeField] private PlayerController pScriptRef;
    [SerializeField] private ResetOnRespawn[] resetObjsArr;
    [SerializeField] private Transform respawnPos;
    [SerializeField] private Image cardImage;
    [SerializeField] private Sprite activeCard;
    [SerializeField] private Sprite inActiveCard;
    [SerializeField] private GameObject gameOverScreen;
    private AudioManager audioManager;

    // PERF: one shared reference instead of every hazard scanning the whole scene for it.
    private static LevelManager instance;
    public static LevelManager Instance
    {
        get
        {
            if (instance == null) instance = FindObjectOfType<LevelManager>();
            return instance;
        }
    }

    public Transform ReSpawnPos
    {
        get { return respawnPos; }
        set { respawnPos = value; }
    }

    private void Awake()
    {
        instance = this; // PERF:
        audioManager = AudioManager.Instance; // PERF:
    }

    // Start is called before the first frame update
    void Start()
    {
        pScriptRef = FindObjectOfType<PlayerController>();
        resetObjsArr = FindObjectsOfType<ResetOnRespawn>();

    }

    public void Respawn()
    {
        StartCoroutine("RespawnCo");

    }

    public IEnumerator RespawnCo()
    {
        if (PowerUpManager.Instance != null) PowerUpManager.Instance.ResetAll(); // POWERUP: replaces speedBoostPowerUp = false below
        audioManager.PlaySfx(audioManager.gameOverSound);
        pScriptRef.gameObject.SetActive(false);
        gameOverScreen.SetActive(true);
        yield return new WaitForSeconds(respawnDelay);
        pScriptRef.transform.position = ReSpawnPos.transform.position;
        pScriptRef.gameObject.SetActive(true);
        gameOverScreen.SetActive(false);
        HurtPlayer.pLivesCount = 3;
        PlayerController.chi = 90;
        HurtPlayer.oopsCount = HurtPlayer.oopsCount + 1;
        for (int i = 0; i < resetObjsArr.Length; i++)
        {
            resetObjsArr[i].ResetObjs();
            resetObjsArr[i].gameObject.SetActive(true);

        }
        changeWorld.RequestRefresh(); // PERF: the loop above re-enables objects from both worlds; let changeWorld correct them
    }

}
