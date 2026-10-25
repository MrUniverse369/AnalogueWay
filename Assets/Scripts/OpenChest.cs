using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>CHEST: what a treasure chest holds. Random rolls for a trap first, then picks a reward by weight.</summary>
public enum ChestContents { Random, Coins, Health, Chi, Gems, Trap, Restore } // CHALLENGE: Restore appended (saved values keep their meaning)

/// <summary>
/// A treasure chest. CHEST: Harvey opens it himself: standing next to it on the ground shows "UP / E: OPEN CHEST"
/// (the same keys as villagers and the shop), and Up / E opens it. Each time the level loads a chest rolls what it
/// holds: a coin jackpot, a Spirit Peach (a life back, or into the bag when lives are full), Chi Crystals (chi, or
/// into the bag when chi is full) or gems for the bag. Rewards pop out of the lid and fly into Harvey.
/// Some chests are traps. A trapped chest gives itself away only by a faint one-pixel rattle now and then while
/// Harvey is near; opening it lights a fuse and it bursts like a firework, taking two lives. The same protection as
/// other hazards applies (the hit flicker, and talismans that make Harvey immune to enemies, like Stone Skin).
/// </summary>
public class OpenChest : MonoBehaviour
{
    [SerializeField] private Animator animatorRef;
    private bool isChestOpened = false;
    private AudioManager audioManager; // SFX:

    // CHEST: opening
    [Header("Opening")]
    [Tooltip("How far beside the chest Harvey can stand and still open it (world units from its edge).")]
    [SerializeField] private float openReach = 0.6f;
    [Tooltip("How far above the chest's base Harvey's feet can be.")]
    [SerializeField] private float openHeight = 1.2f;

    // CHEST: contents
    [Header("Contents")]
    [Tooltip("Random rolls for a trap, then picks a reward by the weights below, every time the level loads. Anything else always gives that (tutorials, set pieces).")]
    [SerializeField] private ChestContents contents = ChestContents.Random;
    [Tooltip("Chance (0-1) that a Random chest is a trap.")]
    [SerializeField, Range(0f, 1f)] private float trapChance = 0.15f;
    [SerializeField, Min(0f)] private float coinsWeight = 35f;
    [SerializeField, Min(0f)] private float healthWeight = 20f;
    [SerializeField, Min(0f)] private float chiWeight = 25f;
    [SerializeField, Min(0f)] private float gemsWeight = 20f;

    // COINS: coins burst out of the chest when it opens, arc through the air and land around it.
    [Header("Coin jackpot")]
    [Tooltip("Prefabs/Collectibles/Coin. If empty, a coin already in the scene is copied instead.")]
    [SerializeField] private GameObject coinPrefab;
    [Tooltip("CHEST: the fewest coins in a coin jackpot.")]
    [SerializeField, Min(0)] private int coinCount = 12;
    [Tooltip("CHEST: the most coins in a coin jackpot.")]
    [SerializeField, Min(0)] private int coinCountMax = 20;
    [Tooltip("How far left/right of the chest the coins land (world units).")]
    [SerializeField] private float burstSpread = 1.8f;
    [Tooltip("How high above the chest the coins fly (world units).")]
    [SerializeField] private float burstHeight = 2.2f;
    [SerializeField] private float flightTime = 0.6f;
    [Tooltip("Wait for the lid to open before the coins come out (seconds).")]
    [SerializeField] private float burstDelay = 0.15f;
    [Tooltip("Time between each coin leaving the chest (seconds).")]
    [SerializeField] private float coinInterval = 0.05f;

    // CHEST: the other rewards
    [Header("Other rewards")]
    [Tooltip("Chi Crystals that pop out of a chi chest. Each gives the Chi Crystal item's amount of chi.")]
    [SerializeField, Min(1)] private int chiCrystals = 3;
    [Tooltip("Gems that pop out of a gem chest (random between the two).")]
    [SerializeField, Min(1)] private int gemsMin = 2;
    [SerializeField, Min(1)] private int gemsMax = 3;

    // CHEST: traps
    [Header("Trap")]
    [Tooltip("Lives a trapped chest takes.")]
    [SerializeField, Min(1)] private int trapDamage = 2;
    [Tooltip("Harvey is hurt if he is this close (world units) when it goes off.")]
    [SerializeField] private float blastRadius = 3f;
    [Tooltip("Seconds of fizzing fuse between opening a trapped chest and the bang.")]
    [SerializeField] private float fuseTime = 0.45f;
    [Tooltip("A trapped chest rattles now and then while Harvey is this close (world units).")]
    [SerializeField] private float rattleRange = 3.5f;
    [Tooltip("Seconds between rattles (random between the two).")]
    [SerializeField] private Vector2 rattleGap = new Vector2(1.4f, 2.8f);

    // CHEST: gems a chest can hold (item id, how common, sparkle colours). The items are in Resources/HNLCatalog.
    private static readonly (string id, float weight, string light, string mid)[] GemTable =
    {
        ("JadeGem", 35f, "88ce6e", "52ab61"), ("LapisGem", 30f, "8fd3ff", "4da6ff"),
        ("TopazGem", 22f, "ffe478", "f2a65e"), ("RubyGem", 13f, "ff6b97", "eb564b"),
    };

    private static readonly Color[] PeachSparkle = { PixelFX.Hex("ffd2dc"), PixelFX.Hex("ff6b97"), PixelFX.Cream };
    private static readonly Color[] ChiSparkle = { PixelFX.Hex("e7fbfe"), PixelFX.Hex("66ffe3"), PixelFX.Hex("4da6ff") };
    private static readonly Color[] Smoke = { PixelFX.Hex("5a697d"), PixelFX.Hex("769099"), PixelFX.Hex("333941") };
    private static readonly Color[] Splinters = { PixelFX.Hex("663931"), PixelFX.Hex("a35a3c"), PixelFX.Hex("45283c") };
    private static readonly Color[] ShellColours = { PixelFX.Yellow, PixelFX.Hex("ff6b97"), PixelFX.Hex("66ffe3"), PixelFX.Hex("99e550"), PixelFX.Hex("c9b8ff") };
    private static readonly Color Charred = new Color(0.45f, 0.38f, 0.42f, 1f);

    private static readonly List<OpenChest> all = new List<OpenChest>();
    private static PlayerController player;

    private ChestContents rolled;     // what this chest holds this time
    private SpriteRenderer sprite;
    private Vector3 restLocal;        // where the chest sits; the rattles always come back here
    private bool started;
    private float nextRattle;
    private bool upWasHeld = true;    // don't open on the frame Harvey walks up holding Up
    private bool dropping;            // BOSSCHEST: still falling into a boss arena; it can't be opened yet

    // a reward that comes out in several pieces is summed up in one message once the last piece arrives
    private int piecesLeft, piecesUsed, piecesBagged;
    private readonly Dictionary<ItemDefinition, int> found = new Dictionary<ItemDefinition, int>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        all.Clear();
        player = null;
    }

    private void Start()
    {
        audioManager = AudioManager.Instance; // SFX: PERF:
        // ANIM: chests sit inside world containers that are switched off/on by changeWorld; without this the
        // Animator resets on re-enable and an opened chest shows as closed again (and can never be reopened)
        if (animatorRef != null) animatorRef.keepAnimatorStateOnDisable = true;
        sprite = GetComponent<SpriteRenderer>();
        restLocal = transform.localPosition;
        started = true;
        rolled = Roll();
        nextRattle = Time.time + Random.Range(rattleGap.x, rattleGap.y);
    }

    private void OnEnable() => all.Add(this);

    private void OnDisable()
    {
        all.Remove(this);
        if (started) transform.localPosition = restLocal; // a rattle cut short by a world switch
    }

    // ---------------------------------------------------------------- opening
    private void Update()
    {
        bool up = ActionButton.Held();
        bool pressed = up && !upWasHeld;
        upWasHeld = up;
        if (isChestOpened || dropping) return;
        if (rolled == ChestContents.Trap) UpdateRattle();
        if (DialogueBox.IsOpen || HarveyMenus.IsOpen || MenueControl.isGamePaused || VillageShop.PlayerAtCounter || LevelLoader.Busy) return;
        if (!InReach(out _) || Nearest() != this) return;

        HarveyMenus.Prompt(this, ActionButton.Prompt + "OPEN CHEST");
        if (pressed) Open();
    }

    private void Open()
    {
        isChestOpened = true;
        StopAllCoroutines(); // a rattle in progress
        transform.localPosition = restLocal;
        if (rolled == ChestContents.Trap)
        {
            Run(TrapFuse());
            return;
        }

        if (animatorRef != null) animatorRef.SetTrigger("ChestOpenTrig");
        if (audioManager != null) audioManager.PlaySfxOneShot(audioManager.chestOpenSound); // SFX:
        Run(SparkleBurst()); // FX:
        switch (rolled)
        {
            case ChestContents.Health: Run(PopPeach()); break;
            case ChestContents.Chi: Run(PopChiCrystals()); break;
            case ChestContents.Gems: Run(PopGems()); break;
            case ChestContents.Restore: Run(PopPeach()); Run(PopChiCrystals()); break; // CHALLENGE: health and chi, never a trap (only Random rolls traps)
            default: Run(BurstCoins(Random.Range(Mathf.Min(coinCount, coinCountMax), Mathf.Max(coinCount, coinCountMax) + 1))); break; // COINS:
        }
    }

    private ChestContents Roll()
    {
        if (contents != ChestContents.Random) return contents;
        if (Random.value < trapChance) return ChestContents.Trap;
        float total = coinsWeight + healthWeight + chiWeight + gemsWeight;
        float r = Random.value * total;
        if (total <= 0f || (r -= coinsWeight) < 0f) return ChestContents.Coins;
        if ((r -= healthWeight) < 0f) return ChestContents.Health;
        if ((r -= chiWeight) < 0f) return ChestContents.Chi;
        return ChestContents.Gems;
    }

    // Sequences run on the LevelManager, which is never switched off: a coroutine stops when its own object is
    // disabled, so a chest hidden by a world switch half way through would otherwise never finish what it started.
    private void Run(IEnumerator routine)
    {
        LevelManager host = LevelManager.Instance;
        if (host != null && host.isActiveAndEnabled) host.StartCoroutine(routine);
        else StartCoroutine(routine);
    }

    private bool InReach(out float distance)
    {
        distance = float.MaxValue;
        if (dropping) return false;
        if (player == null) player = FindFirstObjectByType<PlayerController>();
        if (player == null || !player.isActiveAndEnabled || !player.GetIsGrounded) return false;
        Bounds b = ChestBounds();
        Vector3 feet = player.FeetPosition;
        distance = Mathf.Max(0f, Mathf.Abs(feet.x - b.center.x) - b.extents.x);
        return distance <= openReach && feet.y >= b.min.y - 0.6f && feet.y <= b.min.y + openHeight;
    }

    // only the nearest closed chest in reach answers, so two chests side by side don't both open
    private static OpenChest Nearest()
    {
        OpenChest best = null;
        float bestDistance = float.MaxValue;
        foreach (OpenChest c in all)
            if (!c.isChestOpened && c.InReach(out float d) && d < bestDistance)
            {
                best = c;
                bestDistance = d;
            }
        return best;
    }

    /// <summary>BOSSCHEST: true while Harvey stands at a closed chest he can open (the boss treasure waits its turn).</summary>
    public static bool PlayerAtChest => Nearest() != null;

    // ---------------------------------------------------------------- chests made at runtime (BOSSCHEST:)
    /// <summary>Sets what this chest holds, overriding its Contents setting. Call it right after Instantiate.</summary>
    public void SetContents(ChestContents what)
    {
        contents = what;
        if (started) rolled = Roll();
    }

    /// <summary>
    /// Drops the chest into place from 'height' above where it stands now, after 'delay' seconds: a hard landing with
    /// a dust puff, a squash and a few sparkles. It can't be opened until it has landed.
    /// </summary>
    public void DropIn(float height, float delay)
    {
        dropping = true;
        StartCoroutine(DropInRoutine(transform.localPosition, height, delay));
    }

    private IEnumerator DropInRoutine(Vector3 landLocal, float height, float delay)
    {
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null) sr.enabled = false;
        if (delay > 0f) yield return new WaitForSeconds(delay);
        if (sr != null) sr.enabled = true;
        Vector3 scale = transform.localScale;
        const float FallTime = 0.42f;
        for (float t = 0f; t < FallTime; t += Time.deltaTime)
        {
            float k = t / FallTime;
            transform.localPosition = landLocal + Vector3.up * height * (1f - k * k); // falling faster and faster
            transform.localScale = new Vector3(scale.x * 0.92f, scale.y * 1.1f, scale.z); // stretched by the fall
            yield return null;
        }
        transform.localPosition = landLocal;
        restLocal = landLocal;
        Bounds b = ChestBounds();
        Vector3 feet = new Vector3(b.center.x, b.min.y, 0f);
        Color[] dust = PixelFX.ZoneDust();
        for (int side = -1; side <= 1; side += 2)
            PixelFX.Burst(feet + new Vector3(side * b.extents.x * 0.8f, 0.03f, 0f), 6, 0.8f, 2.2f, dust, 0.25f, 0.45f, 1, PixelShape.Square, PixelMotion.Float, false, PixelLayer.Front, side < 0 ? 160f : 20f, 40f);
        PixelFX.Burst(Lid, 5, 1f, 2f, new[] { PixelFX.Cream, PixelFX.Yellow }, 0.3f, 0.5f, 1, PixelShape.Star, PixelMotion.Float, true, PixelLayer.Front, 90f, 120f);
        if (audioManager == null) audioManager = AudioManager.Instance;
        if (audioManager != null) audioManager.PlaySfxOneShot(audioManager.bounceSound, 0.8f);
        for (float t = 0f; t < 0.16f; t += Time.deltaTime)
        {
            float squash = Mathf.Sin(t / 0.16f * Mathf.PI) * 0.18f; // a quick squash on landing, then back to shape
            transform.localScale = new Vector3(scale.x * (1f + squash), scale.y * (1f - squash), scale.z);
            transform.localPosition = landLocal + Vector3.down * (b.extents.y * squash); // keep its base on the floor
            yield return null;
        }
        transform.localScale = scale;
        transform.localPosition = landLocal;
        dropping = false;
    }

    private Bounds ChestBounds() => sprite != null ? sprite.bounds : new Bounds(transform.position, new Vector3(0.9f, 0.75f, 0f));
    private Vector3 Lid => new Vector3(ChestBounds().center.x, ChestBounds().max.y - 0.15f, 0f);

    // rewards draw just in front of Harvey, so they read clearly as they fly into him
    private void RewardSorting(out int layer, out int order)
    {
        SpriteRenderer harvey = PlayerFX.Instance != null ? PlayerFX.Instance.Sprite : null;
        SpriteRenderer basis = harvey != null ? harvey : sprite;
        layer = basis != null ? basis.sortingLayerID : 0;
        order = (basis != null ? basis.sortingOrder : 0) + 5;
    }

    // ---------------------------------------------------------------- rewards
    // FX: sparkles fountain out of the opening lid
    private IEnumerator SparkleBurst()
    {
        yield return new WaitForSeconds(burstDelay);
        Vector3 lid = Lid;
        PixelFX.Burst(lid, 10, 2f, 4.2f, PixelFX.CoinGold, 0.35f, 0.6f, 1, PixelShape.Square, PixelMotion.Fall, true, PixelLayer.Front, 90f, 100f);
        PixelFX.Burst(lid, 5, 1.2f, 2.6f, new[] { PixelFX.Cream, PixelFX.Yellow }, 0.4f, 0.6f, 1, PixelShape.Star, PixelMotion.Float, true, PixelLayer.Front, 90f, 140f);
        yield return new WaitForSeconds(0.2f);
        PixelFX.Burst(lid, 4, 0.8f, 1.6f, new[] { PixelFX.Cream }, 0.4f, 0.7f, 1, PixelShape.Cross, PixelMotion.Float, true, PixelLayer.Front, 90f, 160f);
    }

    // COINS: a jackpot. Coins land in rows around the chest; big jackpots stack a row or two higher up.
    private IEnumerator BurstCoins(int count)
    {
        yield return new WaitForSeconds(burstDelay);

        GameObject template = coinPrefab != null ? coinPrefab : FindCoinToCopy();
        if (template == null) yield break;                       // no prefab assigned and no coin left to copy

        Transform parent = transform.parent;                      // same world container as the chest, so coins hide with its world
        Vector3 scale = template.transform.lossyScale;
        Vector3 start = Lid + Vector3.up * 0.1f;
        const int PerRow = 7;
        PixelFX.Burst(start, 16, 2.5f, 5f, PixelFX.CoinGold, 0.4f, 0.8f, 1, PixelShape.Square, PixelMotion.Fall, true, PixelLayer.Front, 90f, 70f); // CHEST: a gold gush
        HarveyMenus.Toast(Inventory.Catalog != null ? Inventory.Catalog.coin : null, "COIN JACKPOT!", count + " COINS SPILL OUT"); // CHEST:

        for (int i = 0; i < count; i++)
        {
            int row = i / PerRow, col = i % PerRow;
            int inRow = Mathf.Min(PerRow, count - row * PerRow);
            float side = inRow == 1 ? 0f : Mathf.Lerp(-1f, 1f, col / (float)(inRow - 1));
            Vector3 landing = new Vector3(transform.position.x + side * burstSpread + Random.Range(-0.15f, 0.15f),
                                          transform.position.y + row * 0.55f, transform.position.z);

            GameObject coin = Instantiate(template, start, Quaternion.identity, parent);
            coin.name = "ChestCoin";
            Vector3 parentScale = parent != null ? parent.lossyScale : Vector3.one;
            coin.transform.localScale = new Vector3(scale.x / parentScale.x, scale.y / parentScale.y, scale.z / parentScale.z);
            coin.SetActive(true);
            coin.AddComponent<CoinBurstArc>().Launch(start, landing, burstHeight + row * 0.3f + Random.Range(-0.3f, 0.3f), flightTime);

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

    private static ItemDefinition CatalogItem(string id) => Inventory.Catalog != null ? Inventory.Catalog.ItemById(id) : null;
    private static bool HarveyActive => PlayerFX.Instance != null && PlayerFX.Instance.isActiveAndEnabled;

    // a Spirit Peach rises out of the chest and flies into Harvey: a life back, or into the bag when lives are full
    private IEnumerator PopPeach()
    {
        ItemDefinition peach = CatalogItem("SpiritPeach");
        if (peach == null) { yield return BurstCoins(coinCount); yield break; } // no catalog: fall back to coins
        yield return new WaitForSeconds(burstDelay);
        RewardSorting(out int layer, out int order);
        Vector3 lid = Lid;
        PixelFX.Burst(lid, 8, 1.5f, 3f, PeachSparkle, 0.35f, 0.6f, 1, PixelShape.Star, PixelMotion.Float, true, PixelLayer.Front, 90f, 120f);
        ChestReward.Spawn(peach.Icon, lid, lid + new Vector3(0f, 1.45f, 0f), 0.55f, new Color(1f, 0.42f, 0.6f, 0.9f), PeachSparkle, layer, order,
            at => CollectPeach(peach));
    }

    private void CollectPeach(ItemDefinition peach)
    {
        int lives = Mathf.Max(1, (int)peach.Amount);
        if (HarveyActive && HurtPlayer.pLivesCount > 0 && HurtPlayer.pLivesCount < GameSession.StartingLives)
        {
            HurtPlayer.pLivesCount = Mathf.Min(GameSession.StartingLives, HurtPlayer.pLivesCount + lives);
            HarveyGlow(new Color(1f, 0.42f, 0.6f, 0.9f), PeachSparkle);
            HarveyMenus.Toast(peach.Icon, peach.DisplayName.ToUpperInvariant(), lives == 1 ? "A LIFE RESTORED" : lives + " LIVES RESTORED");
        }
        else
        {
            Inventory.Add(peach);
            HarveyMenus.Toast(peach.Icon, "FOUND " + peach.DisplayName.ToUpperInvariant(), "LIVES ARE FULL: IT'S IN YOUR BAG");
        }
        if (audioManager != null) audioManager.PlaySfxOneShot(audioManager.powerUpSound);
    }

    // Chi Crystals fan out of the chest and fly into Harvey one after another, each refilling some chi; any that
    // arrive when chi is already full go in the bag instead
    private IEnumerator PopChiCrystals()
    {
        ItemDefinition crystal = CatalogItem("ChiCrystal");
        if (crystal == null) { yield return BurstCoins(coinCount); yield break; }
        yield return new WaitForSeconds(burstDelay);
        RewardSorting(out int layer, out int order);
        Vector3 lid = Lid;
        float chi = crystal.Amount > 0f ? crystal.Amount : 30f;
        piecesLeft = chiCrystals;
        PixelFX.Burst(lid, 10, 1.5f, 3.2f, ChiSparkle, 0.35f, 0.6f, 1, PixelShape.Star, PixelMotion.Float, true, PixelLayer.Front, 90f, 130f);
        for (int i = 0; i < chiCrystals; i++)
        {
            Vector3 peak = lid + FanOffset(i, chiCrystals, 1.35f);
            ChestReward.Spawn(crystal.Icon, lid, peak, 0.45f + i * 0.14f, new Color(0.4f, 1f, 0.89f, 0.9f), ChiSparkle, layer, order,
                at => CollectChiCrystal(crystal, chi));
            yield return new WaitForSeconds(0.07f);
        }
    }

    private void CollectChiCrystal(ItemDefinition crystal, float chi)
    {
        if (HarveyActive && PlayerController.chi < GameSession.StartingChi - 0.5f)
        {
            PlayerController.chi = Mathf.Min(GameSession.StartingChi, PlayerController.chi + chi);
            HarveyGlow(new Color(0.4f, 1f, 0.89f, 0.9f), ChiSparkle);
            piecesUsed++;
        }
        else
        {
            Inventory.Add(crystal);
            piecesBagged++;
        }
        if (audioManager != null) audioManager.PlayCoin(); // rising chime, one note per crystal
        if (--piecesLeft > 0) return;
        string line = piecesBagged == 0 ? "CHI RESTORED"
                    : piecesUsed == 0 ? "CHI IS FULL: " + piecesBagged + " IN YOUR BAG"
                    : "CHI RESTORED, " + piecesBagged + " IN YOUR BAG";
        int total = piecesUsed + piecesBagged;
        HarveyMenus.Toast(crystal.Icon, total + " " + crystal.DisplayName.ToUpperInvariant() + (total == 1 ? "" : "S"), line);
    }

    // gems spring out in a fan, twinkle, and go into the bag (Collectibles)
    private IEnumerator PopGems()
    {
        int count = Random.Range(Mathf.Min(gemsMin, gemsMax), Mathf.Max(gemsMin, gemsMax) + 1);
        var gems = new List<(ItemDefinition item, Color[] sparkle, Color glow)>();
        for (int i = 0; i < count; i++)
        {
            var g = GemTable[PickGem()];
            ItemDefinition item = CatalogItem(g.id);
            if (item == null) continue;
            Color light = PixelFX.Hex(g.light), mid = PixelFX.Hex(g.mid);
            gems.Add((item, new[] { light, mid, PixelFX.Cream }, new Color(mid.r, mid.g, mid.b, 0.9f)));
        }
        if (gems.Count == 0) { yield return BurstCoins(coinCount); yield break; } // gems not in the catalog yet
        yield return new WaitForSeconds(burstDelay);
        RewardSorting(out int layer, out int order);
        Vector3 lid = Lid;
        piecesLeft = gems.Count;
        found.Clear();
        PixelFX.Burst(lid, 12, 1.5f, 3.4f, PixelFX.Confetti, 0.35f, 0.6f, 1, PixelShape.Star, PixelMotion.Float, true, PixelLayer.Front, 90f, 140f);
        for (int i = 0; i < gems.Count; i++)
        {
            var g = gems[i];
            ChestReward.Spawn(g.item.Icon, lid, lid + FanOffset(i, gems.Count, 1.4f), 0.5f + i * 0.14f, g.glow, g.sparkle, layer, order,
                at => CollectGem(g.item));
            yield return new WaitForSeconds(0.07f);
        }
    }

    private static int PickGem()
    {
        float total = 0f;
        foreach (var g in GemTable) total += g.weight;
        float r = Random.value * total;
        for (int i = 0; i < GemTable.Length; i++)
            if ((r -= GemTable[i].weight) < 0f) return i;
        return 0;
    }

    private void CollectGem(ItemDefinition gem)
    {
        Inventory.Add(gem);
        found[gem] = found.TryGetValue(gem, out int n) ? n + 1 : 1;
        if (audioManager != null) audioManager.PlayCoin();
        if (--piecesLeft > 0) return;

        int total = 0;
        var line = new StringBuilder();
        Sprite icon = null;
        foreach (var pair in found)
        {
            total += pair.Value;
            if (icon == null) icon = pair.Key.Icon;
            if (line.Length > 0) line.Append(", ");
            line.Append(pair.Key.DisplayName.ToUpperInvariant());
            if (pair.Value > 1) line.Append(" x").Append(pair.Value);
        }
        HarveyMenus.Toast(icon, total == 1 ? "FOUND A GEM: IN YOUR BAG" : "FOUND " + total + " GEMS: IN YOUR BAG", line.ToString());
    }

    // spread pieces in a fan above the lid: the middle one highest
    private static Vector3 FanOffset(int i, int count, float height)
    {
        float side = count == 1 ? 0f : Mathf.Lerp(-1f, 1f, i / (float)(count - 1));
        return new Vector3(side * 0.75f, height - Mathf.Abs(side) * 0.3f, 0f);
    }

    // a soft glow and a ring of sparkles round Harvey as a reward goes into him
    private static void HarveyGlow(Color colour, Color[] sparkle)
    {
        PlayerFX harvey = PlayerFX.Instance;
        if (harvey == null || harvey.Sprite == null) return;
        SpriteFX.On(harvey.Sprite).Flash(new Color(colour.r, colour.g, colour.b, 0.55f), 0.18f);
        SpriteAura.On(harvey.Sprite)?.Set("chestReward", colour, 2, 0f, 0, 0.6f, 0.4f);
        PixelFX.Burst(harvey.transform.position, 8, 0.8f, 1.8f, sparkle, 0.4f, 0.7f, 1, PixelShape.Star, PixelMotion.Float, true, PixelLayer.Front, 90f, 180f);
    }

    // ---------------------------------------------------------------- traps
    // the only hint: now and then, while Harvey is close, the chest knocks a pixel side to side (and very rarely
    // breathes out a thread of smoke); a safe chest never moves
    private void UpdateRattle()
    {
        if (Time.time < nextRattle) return;
        nextRattle = Time.time + Random.Range(rattleGap.x, rattleGap.y);
        if (player == null || !player.isActiveAndEnabled || MenueControl.isGamePaused) return;
        if ((player.transform.position - transform.position).sqrMagnitude > rattleRange * rattleRange) return;
        StartCoroutine(Rattle());
    }

    private IEnumerator Rattle()
    {
        if (audioManager != null) audioManager.PlaySfxOneShot(audioManager.chestRattleSound);
        int knocks = Random.Range(2, 4);
        for (int i = 0; i < knocks; i++)
        {
            Nudge(new Vector2(i % 2 == 0 ? PixelFX.Pixel : -PixelFX.Pixel, i == 0 ? PixelFX.Pixel : 0f));
            yield return new WaitForSeconds(0.05f);
            Nudge(Vector2.zero);
            yield return new WaitForSeconds(0.06f);
        }
        if (Random.value < 0.3f)
            PixelFX.Emit(Lid + new Vector3(Random.Range(-0.25f, 0.25f), 0f, 0f), new Vector2(0f, 0.45f), PixelFX.Pick(Smoke), 0.7f, 1, PixelShape.Square, PixelMotion.Drift);
    }

    // moves the chest by a world offset from where it rests
    private void Nudge(Vector2 offset)
    {
        Vector3 rest = transform.parent != null ? transform.parent.TransformPoint(restLocal) : restLocal;
        transform.position = rest + (Vector3)offset;
    }

    // the lid jumps, a fuse fizzes and spits sparks, the chest shakes harder and flashes hot... then it goes off
    private IEnumerator TrapFuse()
    {
        if (audioManager != null) audioManager.PlaySfxOneShot(audioManager.chestFuseSound);
        SpriteFX fx = SpriteFX.On(sprite);
        for (float t = 0f; t < fuseTime; t += Time.deltaTime)
        {
            float k = t / fuseTime;
            float px = PixelFX.Pixel * (k > 0.5f ? 2f : 1f);
            Nudge(new Vector2(Time.frameCount % 2 == 0 ? px : -px, Random.value < 0.25f ? PixelFX.Pixel : 0f));
            if (fx != null && Time.frameCount % 4 == 0) fx.Flash(new Color(1f, 0.65f, 0.37f, 0.35f + 0.4f * k), 0.06f);
            Vector3 lid = Lid;
            PixelFX.Emit(lid + new Vector3(Random.Range(-0.2f, 0.2f), 0f, 0f), new Vector2(Random.Range(-1.2f, 1.2f), Random.Range(1.5f, 3f)),
                PixelFX.Pick(PixelFX.Sparks), Random.Range(0.15f, 0.3f), 1, PixelShape.Square, PixelMotion.Fall, true);
            yield return null;
        }
        Nudge(Vector2.zero);
        Explode();
    }

    private void Explode()
    {
        Vector3 at = Lid;
        if (animatorRef != null) animatorRef.SetTrigger("ChestOpenTrig"); // the lid is blown open
        if (sprite != null) sprite.color = Charred;                         // and the wood scorched
        SpriteFX.On(sprite)?.Flash(new Color(1f, 1f, 0.92f, 1f), 0.12f);
        if (audioManager != null) audioManager.PlaySfxOneShot(audioManager.ChestTrapSound);
        CameraController.Shake(0.4f, 0.2f);
        ScreenGrade.Ripple(at);

        // the burst: a white-hot core, two rings of stars and sparks, glitter raining down, splinters and smoke
        PixelFX.Burst(at, 14, 3f, 6.5f, new[] { PixelFX.Cream, PixelFX.Yellow }, 0.1f, 0.2f, 2, PixelShape.Square, PixelMotion.Float, true);
        PixelFX.Ring(at, 24, 5.5f, ShellColours, 0.45f, 1, PixelShape.Star, true);
        PixelFX.Ring(at, 16, 3.2f, PixelFX.Sparks, 0.5f, 2, PixelShape.Square, true);
        PixelFX.Burst(at, 30, 2f, 6f, PixelFX.Confetti, 0.6f, 1.1f, 1, PixelShape.Square, PixelMotion.Fall, true, PixelLayer.Front, 90f, 160f);
        PixelFX.Burst(at, 10, 2.5f, 5f, Splinters, 0.5f, 0.9f, 2, PixelShape.Square, PixelMotion.Fall, false, PixelLayer.Front, 90f, 140f);
        PixelFX.Burst(at, 10, 0.4f, 1.2f, Smoke, 0.7f, 1.2f, 2, PixelShape.Square, PixelMotion.Drift, false, PixelLayer.Front, 90f, 360f, 0.2f);
        Run(FireworkShells(at));

        HurtHarvey(at);
    }

    // three rockets streak up out of the wreck and pop in coloured star bursts, then the chest smoulders
    private IEnumerator FireworkShells(Vector3 at)
    {
        const int Shells = 3;
        const float Climb = 0.38f;
        var targets = new Vector3[Shells];
        var colours = new Color[Shells];
        for (int i = 0; i < Shells; i++)
        {
            targets[i] = at + new Vector3((i - 1) * Random.Range(0.8f, 1.2f), Random.Range(1.8f, 2.6f), 0f);
            colours[i] = ShellColours[(i + Random.Range(0, ShellColours.Length)) % ShellColours.Length];
        }
        var popped = new bool[Shells];
        for (float t = 0f; t < Climb + 0.2f; t += Time.deltaTime)
        {
            for (int i = 0; i < Shells; i++)
            {
                float k = (t - i * 0.08f) / Climb;
                if (k < 0f || popped[i]) continue;
                if (k >= 1f)
                {
                    popped[i] = true;
                    Color[] shell = { colours[i], PixelFX.Cream };
                    PixelFX.Ring(targets[i], 14, 2.6f, shell, 0.5f, 1, PixelShape.Star, true);
                    PixelFX.Burst(targets[i], 10, 0.6f, 2f, shell, 0.5f, 0.9f, 1, PixelShape.Square, PixelMotion.Fall, true);
                    continue;
                }
                Vector3 p = Vector3.Lerp(at, targets[i], 1f - (1f - k) * (1f - k));
                PixelFX.Emit(p, new Vector2(Random.Range(-0.2f, 0.2f), -0.6f), PixelFX.Pick(PixelFX.Sparks), 0.25f, 1, PixelShape.Square, PixelMotion.Fall, true);
            }
            yield return null;
        }
        for (int i = 0; i < 6; i++)
        {
            yield return new WaitForSeconds(0.25f);
            PixelFX.Emit(at + new Vector3(Random.Range(-0.3f, 0.3f), 0f, 0f), new Vector2(Random.Range(-0.1f, 0.1f), 0.5f), PixelFX.Pick(Smoke), 0.9f, 2, PixelShape.Square, PixelMotion.Drift);
        }
    }

    // two lives, a knockback and the usual hurt feedback, unless Harvey is protected the way he is from enemies
    // and hazards (HurtPlayer): the hit flicker, or a talisman that makes him immune (Stone Skin, Spirit Dash)
    private void HurtHarvey(Vector3 at)
    {
        if (player == null) player = FindFirstObjectByType<PlayerController>();
        if (player == null || !player.isActiveAndEnabled) return;
        if ((player.transform.position - at).sqrMagnitude > blastRadius * blastRadius)
        {
            HarveyMenus.Toast(null, "IT WAS A TRAP!", "YOU GOT CLEAR JUST IN TIME");
            return;
        }
        if (PowerUpManager.Has(TalismanTrait.EnemyImmunity) || HurtPlayer.Shielded) // SHIELD:
        {
            HarveyMenus.Toast(null, "IT WAS A TRAP!", "YOUR TALISMAN SHIELDED YOU");
            return;
        }
        if (PlayerController.playerInviInvincible)
        {
            HarveyMenus.Toast(null, "IT WAS A TRAP!", "IT MISSED YOU");
            return;
        }

        HurtPlayer.pLivesCount = Mathf.Max(0, HurtPlayer.pLivesCount - trapDamage);
        PlayerFX.Hurt(at); // flash, sparks, flicker; also ends the combo
        if (HurtPlayer.pLivesCount <= 0)
        {
            if (LevelManager.Instance != null) LevelManager.Instance.Respawn(); // plays the game-over sound
            return;
        }
        // the knockback always throws Harvey backwards from where he faces, so face him into the blast first:
        // it then carries him away from the chest, never over it
        Vector3 scale = player.transform.localScale;
        float toward = at.x >= player.transform.position.x ? 1f : -1f;
        player.transform.localScale = new Vector3(Mathf.Abs(scale.x) * toward, scale.y, scale.z);
        GameObject area = GameObject.FindGameObjectWithTag("PlayerDetectionArea");
        playerBodyDetectionArea body = area != null ? area.GetComponent<playerBodyDetectionArea>() : null;
        if (body != null) body.Knockback();
        if (audioManager != null) audioManager.PlaySfxOneShot(audioManager.playerHurtSound);
        HarveyMenus.Toast(null, "IT WAS A TRAP!", trapDamage == 1 ? "YOU LOST A LIFE" : "YOU LOST " + trapDamage + " LIVES");
    }

    private void OnDrawGizmosSelected()
    {
        Bounds b = ChestBounds();
        Gizmos.color = new Color(1f, 0.9f, 0.45f, 0.6f);
        Gizmos.DrawWireCube(new Vector3(b.center.x, b.min.y + (openHeight - 0.6f) * 0.5f, 0f), new Vector3(b.size.x + openReach * 2f, openHeight + 0.6f, 0f));
        Gizmos.color = new Color(0.92f, 0.34f, 0.29f, 0.35f);
        Gizmos.DrawWireSphere(b.center, blastRadius);
    }
}
