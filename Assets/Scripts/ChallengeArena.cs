using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// CHALLENGE (2026-10-25): a room that locks when Harvey walks in and lets him out after five rounds of enemies (the
/// first Grassy Green level's cave, Z4A6; set up by Tools/HNL/Set Up Grass Challenge Arena).
///   - Harvey inside 'area' (and in control) starts it. The ways out are closed with the ground tilemap's own rock
///     (gateCells, the tile at gateTileFrom), so the level's collision does the locking; the cells are emptied again
///     when it ends. The respawn point moves just outside the gate while it runs.
///   - Each round spawns its enemies (counts per prefab, more every round) one by one on the spawn points, never
///     within minSpawnDistance of Harvey, each marked by spirit wisps first. They patrol either side of their spot
///     (the prefab's MovingTiles points). The next round starts only when every enemy of this one is gone; one that
///     leaves the room counts as gone, so the challenge can't stall. A banner shows "ROUND 2/5" and how many are left.
///   - Win: the gate opens, a reward chest drops in with ChestContents.Restore (a Spirit Peach for health and Chi
///     Crystals for chi). Its contents are set, not rolled, so it is never a trap. It happens once.
///   - Lose (Harvey's lives run out): the enemies vanish at once, the gate opens, and the level's own death and respawn
///     take one life and bring him back just outside the gate; the challenge is ready to try again.
/// WAVEMUSHROOM (2026-10-28): a room can also be locked by taking its way out away (waysOut): Z4A7's side chamber
/// (Tools/HNL/Set Up Z4A7 Challenge Room) has no gate cells; its only exit is a shaft too high to jump, reached from
/// the Jump Mushroom under it. That mushroom is switched off whole (art, trigger, bounce) the moment the challenge
/// begins and switched on again where the gate would open: when every round is won, or when a fall resets the
/// challenge. Nothing else switches it, so it stays off between rounds and is never destroyed or duplicated.
/// </summary>
public class ChallengeArena : MonoBehaviour
{
    [Serializable]
    public class Round
    {
        [Tooltip("How many of each enemy, in the same order as Enemy Prefabs.")]
        public int[] counts;
    }

    [Header("Arena")]
    [Tooltip("The room: Harvey being inside it starts the challenge. Keep it clear of the gate cells.")]
    [SerializeField] private BoxCollider2D area;
    [Tooltip("The level's ground tilemap; the gate cells are filled with its rock while the challenge runs.")]
    [SerializeField] private Tilemap ground;
    [Tooltip("Empty cells of the ground tilemap that close the room's ways out.")]
    [SerializeField] private Vector3Int[] gateCells;
    [Tooltip("A solid cell of the ground tilemap whose tile closes the gates.")]
    [SerializeField] private Vector3Int gateTileFrom;
    [Tooltip("Where Harvey comes back if he falls during the challenge: just outside the gate.")]
    [SerializeField] private Transform respawnOutside;
    [Tooltip("WAVEMUSHROOM: objects that let Harvey leave the room (a bounce pad under its only exit). Off while the challenge runs; on again when it is won or reset.")]
    [SerializeField] private GameObject[] waysOut;

    [Header("Enemies")]
    [SerializeField] private GameObject[] enemyPrefabs;
    [Tooltip("Floor spots (feet height) the enemies appear on; they patrol either side.")]
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private float patrolHalfWidth = 2.5f;
    [Tooltip("Enemies never appear this close to Harvey.")]
    [SerializeField] private float minSpawnDistance = 3f;
    [SerializeField] private Round[] rounds;
    [SerializeField] private float startDelay = 1.2f;
    [SerializeField] private float spawnInterval = 0.45f;
    [Tooltip("Wisps mark the spot this long before the enemy appears.")]
    [SerializeField] private float spawnWarn = 0.5f;
    [SerializeField] private float roundPause = 1.5f;

    [Header("Reward")]
    [Tooltip("Where the reward chest lands (on the floor inside the room).")]
    [SerializeField] private Transform rewardSpot;
    [SerializeField] private float chestDropHeight = 4f;

    private enum State { Waiting, Running, Won }

    private State state;
    private int round;
    private bool locked;
    private bool restoreRespawn;
    private Vector3 savedRespawn;
    private TileBase gateTile;
    private PlayerController player;
    private Coroutine running;
    private readonly List<GameObject> spawned = new List<GameObject>();  // the patrol holders
    private readonly List<GameObject> enemies = new List<GameObject>();  // their Enemy children (alive while active)

    private static readonly Color[] Wisp = { new Color(0.72f, 0.96f, 1f), new Color(0.6f, 0.9f, 0.5f), new Color(1f, 1f, 0.92f) };

    public int RoundCount => rounds != null ? rounds.Length : 0;
    public bool IsRunning => state == State.Running; // WAVEMUSHROOM: read by QAArenaDriver
    public bool IsCleared => state == State.Won;
    public int CurrentRound => round;

    private void Start()
    {
        player = FindFirstObjectByType<PlayerController>();
        if (ground != null) gateTile = ground.GetTile(gateTileFrom);
        LevelManager.Respawned += OnRespawned;
    }

    private void OnDestroy()
    {
        LevelManager.Respawned -= OnRespawned;
        HarveyMenus.Banner(null);
    }

    private void Update()
    {
        if (player == null || area == null) return;
        if (state == State.Waiting)
        {
            if (HurtPlayer.pLivesCount > 0 && !player.NoControl && area.bounds.Contains((Vector2)player.transform.position)) Begin();
            return;
        }
        if (state == State.Running && HurtPlayer.pLivesCount <= 0) Fail(); // the enemies go the moment he falls
    }

    // ---------------------------------------------------------------- flow

    private void Begin()
    {
        if (rounds == null || rounds.Length == 0 || enemyPrefabs == null || enemyPrefabs.Length == 0) return;
        state = State.Running;
        round = 0;
        Lock(true);
        CloseWaysOut(true); // WAVEMUSHROOM:
        LevelManager lm = LevelManager.Instance;
        if (lm != null && respawnOutside != null)
        {
            savedRespawn = lm.ReSpawnPos.position;
            lm.ReSpawnPos.position = respawnOutside.position;
            restoreRespawn = true;
        }
        running = StartCoroutine(Run());
    }

    private IEnumerator Run()
    {
        HarveyMenus.Toast(null, "CHALLENGE!", "BEAT " + rounds.Length + " ROUNDS OF ENEMIES", 2.5f);
        yield return new WaitForSeconds(startDelay);
        for (round = 0; round < rounds.Length; round++)
        {
            HarveyMenus.Banner("ROUND " + (round + 1) + "/" + rounds.Length, "GET READY");
            yield return SpawnRound(rounds[round]);
            while (true)
            {
                int left = Alive();
                HarveyMenus.Banner("ROUND " + (round + 1) + "/" + rounds.Length, "ENEMIES LEFT " + left);
                if (left == 0) break;
                yield return null;
            }
            ClearSpawned();
            if (round + 1 < rounds.Length) yield return new WaitForSeconds(roundPause);
        }
        Win();
    }

    private IEnumerator SpawnRound(Round r)
    {
        var order = new List<int>();
        int most = 0;
        foreach (int n in r.counts) most = Mathf.Max(most, n);
        for (int k = 0; k < most; k++) // one of each kind in turn, so a round mixes them
            for (int i = 0; i < r.counts.Length && i < enemyPrefabs.Length; i++)
                if (k < r.counts[i]) order.Add(i);

        var used = new HashSet<Transform>();
        foreach (int kind in order)
        {
            Transform spot = PickSpot(used);
            used.Add(spot);
            for (float t = 0f; t < spawnWarn; t += Time.deltaTime)
            {
                if (UnityEngine.Random.value < 0.5f)
                    PixelFX.Emit(spot.position + new Vector3(UnityEngine.Random.Range(-0.5f, 0.5f), UnityEngine.Random.Range(0f, 1.2f), 0f),
                        new Vector2(0f, UnityEngine.Random.Range(0.5f, 1.2f)), PixelFX.Pick(Wisp), UnityEngine.Random.Range(0.4f, 0.7f), 1,
                        PixelShape.Square, PixelMotion.Drift, true);
                yield return null;
            }
            Spawn(enemyPrefabs[kind], spot.position);
            yield return new WaitForSeconds(spawnInterval);
        }
    }

    // a spot at least minSpawnDistance from Harvey, preferring ones not used yet this round; if none is far enough,
    // the farthest one
    private Transform PickSpot(HashSet<Transform> used)
    {
        Vector3 at = player != null ? player.transform.position : transform.position;
        var far = new List<Transform>();
        Transform farthest = spawnPoints[0];
        foreach (Transform s in spawnPoints)
        {
            float d = Vector2.Distance(s.position, at);
            if (d >= minSpawnDistance) far.Add(s);
            if (d > Vector2.Distance(farthest.position, at)) farthest = s;
        }
        var fresh = far.FindAll(s => !used.Contains(s));
        if (fresh.Count > 0) return fresh[UnityEngine.Random.Range(0, fresh.Count)];
        if (far.Count > 0) return far[UnityEngine.Random.Range(0, far.Count)];
        return farthest;
    }

    private void Spawn(GameObject prefab, Vector3 feet)
    {
        GameObject holder = Instantiate(prefab, feet, Quaternion.identity, transform);
        Transform a = holder.transform.Find("PointA"), b = holder.transform.Find("PointB"), enemy = holder.transform.Find("Enemy");
        SpriteRenderer sr = enemy != null ? enemy.GetComponent<SpriteRenderer>() : null;
        // the patrol turns before the sprite's edge reaches the room's walls (wide foxes need more room than walkers)
        float edge = Mathf.Max(0.8f, (sr != null ? sr.bounds.extents.x : 0f) + 0.3f);
        float left = area.bounds.min.x + edge, right = area.bounds.max.x - edge;
        if (a != null) a.position = new Vector3(Mathf.Max(left, feet.x - patrolHalfWidth), a.position.y, a.position.z);
        if (b != null) b.position = new Vector3(Mathf.Min(right, feet.x + patrolHalfWidth), b.position.y, b.position.z);
        // stand the sprite on the floor (the patrol keeps the holder's height)
        if (sr != null) holder.transform.position += Vector3.up * (feet.y - sr.bounds.min.y);
        spawned.Add(holder);
        enemies.Add(enemy != null ? enemy.gameObject : holder);
        if (sr != null)
            PixelFX.Burst(sr.bounds.center, 10, 1f, 2.5f, Wisp, 0.4f, 0.7f, 1, PixelShape.Square, PixelMotion.Drift, true, PixelLayer.Front, 90f, 360f, 0.4f);
    }

    // enemies still in the fight; one that has left the room (fell, was knocked away) is taken out and counts as gone
    private int Alive()
    {
        Bounds room = area.bounds;
        room.Expand(2f);
        int n = 0;
        for (int i = 0; i < enemies.Count; i++)
        {
            GameObject e = enemies[i];
            if (e == null || !e.activeInHierarchy) continue;
            if (!room.Contains((Vector2)e.transform.position))
            {
                e.SetActive(false);
                continue;
            }
            n++;
        }
        return n;
    }

    private void ClearSpawned()
    {
        foreach (GameObject h in spawned)
            if (h != null) Destroy(h);
        spawned.Clear();
        enemies.Clear();
    }

    private void Win()
    {
        state = State.Won; // for good: no second reward
        running = null;
        HarveyMenus.Banner(null);
        Lock(false);
        CloseWaysOut(false); // WAVEMUSHROOM: only after the last round
        RestoreRespawn();
        AudioManager audio = AudioManager.Instance;
        if (audio != null) audio.PlaySfxOneShot(audio.BossDefeatSound != null ? audio.BossDefeatSound : audio.powerUpSound);
        HarveyMenus.Toast(null, "CHALLENGE CLEARED!", "A REWARD CHEST HAS APPEARED", 3f);
        GameObject prefab = Inventory.Catalog != null ? Inventory.Catalog.treasureChest : null;
        if (prefab == null || rewardSpot == null) return;
        GameObject chest = Instantiate(prefab, rewardSpot.position, Quaternion.identity);
        // rewardSpot is floor height and the chest's pivot is its middle: stand its base on the floor (as BossChests does)
        SpriteRenderer chestSprite = chest.GetComponent<SpriteRenderer>();
        if (chestSprite != null) chest.transform.position += Vector3.up * (rewardSpot.position.y - chestSprite.bounds.min.y);
        OpenChest open = chest.GetComponentInChildren<OpenChest>();
        if (open != null)
        {
            open.SetContents(ChestContents.Restore); // set, not rolled: never a trap
            open.DropIn(chestDropHeight, 0.3f);
        }
    }

    private void Fail()
    {
        if (state != State.Running) return;
        state = State.Waiting; // ready for another try once he walks back in
        if (running != null) StopCoroutine(running);
        running = null;
        foreach (GameObject e in enemies)
            if (e != null && e.activeInHierarchy)
                PixelFX.Burst(e.transform.position, 8, 1f, 2.2f, Wisp, 0.4f, 0.7f, 1, PixelShape.Square, PixelMotion.Drift, true, PixelLayer.Front, 90f, 360f, 0.4f);
        ClearSpawned();
        HarveyMenus.Banner(null);
        Lock(false);
        CloseWaysOut(false); // WAVEMUSHROOM: reset with the challenge (he is already gone: the respawn has hidden him)
        // the respawn point stays outside the gate until the level's respawn has used it (OnRespawned)
        HarveyMenus.Toast(null, "CHALLENGE FAILED", "STEP BACK IN TO TRY AGAIN", 3f);
    }

    private void OnRespawned()
    {
        if (state == State.Running) Fail();
        RestoreRespawn();
    }

    private void RestoreRespawn()
    {
        if (!restoreRespawn) return;
        restoreRespawn = false;
        LevelManager lm = LevelManager.Instance;
        if (lm != null) lm.ReSpawnPos.position = savedRespawn;
    }

    // ---------------------------------------------------------------- the gate

    private void Lock(bool on)
    {
        if (ground == null || gateCells == null || locked == on || (on && gateTile == null)) return;
        locked = on;
        foreach (Vector3Int c in gateCells)
        {
            if (on && ground.HasTile(c)) continue; // never overwrite the level
            ground.SetTile(c, on ? gateTile : null);
            Vector3 centre = ground.GetCellCenterWorld(c);
            PixelFX.Burst(centre, 6, 1f, 2.2f, PixelFX.ZoneDust(), 0.3f, 0.5f, 1, PixelShape.Square, PixelMotion.Fall, false, PixelLayer.Front, 90f, 360f, 0.3f);
        }
        AudioManager audio = AudioManager.Instance;
        if (audio != null) audio.PlaySfxOneShot(audio.BossSlamSound);
    }

    // WAVEMUSHROOM: the ways out go and come back whole, in a puff of the spawn wisps
    private void CloseWaysOut(bool closed)
    {
        if (waysOut == null) return;
        foreach (GameObject w in waysOut)
        {
            if (w == null || w.activeSelf != closed) continue; // already as wanted
            if (!closed) w.SetActive(true);
            Renderer art = w.GetComponentInChildren<Renderer>();
            PixelFX.Burst(art != null ? art.bounds.center : w.transform.position, 10, 1f, 2.5f, Wisp, 0.4f, 0.7f, 1,
                PixelShape.Square, PixelMotion.Drift, true, PixelLayer.Front, 90f, 360f, 0.4f);
            if (closed) w.SetActive(false);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (area != null)
        {
            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.8f);
            Gizmos.DrawWireCube(area.bounds.center, area.bounds.size);
        }
        if (ground != null && gateCells != null)
        {
            Gizmos.color = Color.red;
            foreach (Vector3Int c in gateCells) Gizmos.DrawWireCube(ground.GetCellCenterWorld(c), ground.cellSize);
        }
        if (spawnPoints != null)
        {
            Gizmos.color = Color.cyan;
            foreach (Transform s in spawnPoints)
                if (s != null) Gizmos.DrawLine(s.position + Vector3.left * patrolHalfWidth, s.position + Vector3.right * patrolHalfWidth);
        }
    }
}
