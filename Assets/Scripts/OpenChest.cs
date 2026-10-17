using System.Collections;
using UnityEngine;

public class OpenChest : MonoBehaviour
{
    [SerializeField] private Animator animatorRef;
    private bool isChestOpened = false;
    private AudioManager audioManager; // SFX:

    // COINS: coins burst out of the chest when it opens, arc through the air and land around it.
    [Header("Coin burst")]
    [Tooltip("Prefabs/Collectibles/Coin. If empty, a coin already in the scene is copied instead.")]
    [SerializeField] private GameObject coinPrefab;
    [SerializeField, Min(0)] private int coinCount = 5;
    [Tooltip("How far left/right of the chest the coins land (world units).")]
    [SerializeField] private float burstSpread = 1.8f;
    [Tooltip("How high above the chest the coins fly (world units).")]
    [SerializeField] private float burstHeight = 2.2f;
    [SerializeField] private float flightTime = 0.6f;
    [Tooltip("Wait for the lid to open before the coins come out (seconds).")]
    [SerializeField] private float burstDelay = 0.15f;
    [Tooltip("Time between each coin leaving the chest (seconds).")]
    [SerializeField] private float coinInterval = 0.05f;

    private void Start()
    {
        audioManager = AudioManager.Instance; // SFX: PERF:
        // ANIM: chests sit inside world containers that are switched off/on by changeWorld; without this the
        // Animator resets on re-enable and an opened chest shows as closed again (and can never be reopened)
        if (animatorRef != null) animatorRef.keepAnimatorControllerStateOnDisable = true;
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (other.CompareTag("PlayerDetectionArea") && !isChestOpened)
        {

                isChestOpened = true;
                animatorRef.SetTrigger("ChestOpenTrig");
                if (audioManager != null) audioManager.PlaySfxOneShot(audioManager.chestOpenSound); // SFX:
                if (coinCount > 0) StartCoroutine(BurstCoins()); // COINS:

        }
    }

    // COINS:
    private IEnumerator BurstCoins()
    {
        yield return new WaitForSeconds(burstDelay);

        GameObject template = coinPrefab != null ? coinPrefab : FindCoinToCopy();
        if (template == null) yield break;                       // no prefab assigned and no coin left to copy

        Transform parent = transform.parent;                      // same world container as the chest, so coins hide with its world
        Vector3 scale = template.transform.lossyScale;
        Vector3 start = transform.position + Vector3.up * 0.3f;

        for (int i = 0; i < coinCount; i++)
        {
            float side = coinCount == 1 ? 0f : Mathf.Lerp(-1f, 1f, i / (float)(coinCount - 1));
            Vector3 landing = new Vector3(transform.position.x + side * burstSpread + Random.Range(-0.15f, 0.15f),
                                          transform.position.y, transform.position.z);

            GameObject coin = Instantiate(template, start, Quaternion.identity, parent);
            coin.name = "ChestCoin";
            Vector3 parentScale = parent != null ? parent.lossyScale : Vector3.one;
            coin.transform.localScale = new Vector3(scale.x / parentScale.x, scale.y / parentScale.y, scale.z / parentScale.z);
            coin.SetActive(true);
            coin.AddComponent<CoinBurstArc>().Launch(start, landing, burstHeight + Random.Range(-0.3f, 0.3f), flightTime);

            if (coinInterval > 0f) yield return new WaitForSeconds(coinInterval);
        }
    }

    // COINS: fallback when no prefab is assigned - copy a resting coin from the scene (never one still in flight)
    private static GameObject FindCoinToCopy()
    {
        foreach (GameObject coin in GameObject.FindGameObjectsWithTag("Coin"))
            if (coin.GetComponent<CoinBurstArc>() == null) return coin;
        return null;
    }

}
