using System.Collections;
using UnityEngine;

/// <summary>
/// SHUFFLE (2026-10-28): the Mushroom Spirit's treasure-chest guessing game, one component shared by every level
/// that has it (Z4A6, where it replaced the Challenge Arena, and Z4A7's eastern expansion). It lives on the root of
/// Prefabs/LevelMechanics/GrassyGreen/MushroomSpiritChestGame (built by Tools/HNL/Chest Shuffle/Set Up).
///
/// The rules:
///   - Three chests. On starting, one is picked at random as the trap; the other two hold rewards. What a chest
///     holds belongs to the chest itself (trapChest is a reference to one holder object, never a position), so no
///     amount of moving chests about can move the trap to another chest.
///   - Reveal: the reward chests show a gem over them and the trap chest rattles with a red glow, long enough to
///     see (revealTime), then the Mushroom Spirit shuffles them: neighbouring chests swap places, one sliding along the
///     floor while the other hops over it, so they never overlap and always end exactly on a slot.
///   - Harvey can't move and no chest can be picked while the Spirit talks, reveals or shuffles. Then, and only
///     then, a chest in reach shows "UP / E: OPEN THIS CHEST". A pick is taken once: the state leaves Choosing on
///     the same press, so held or repeated presses do nothing.
///   - A reward chest is one correct guess (correct, an explicit counter). Two correct guesses win: the two reward
///     chests open and pay out through OpenChest's own reward flow (a gem chest and a chi chest). Fewer than two,
///     and the chests go round again from where they stand (a fresh look, then a fresh shuffle).
///   - The trap chest, picked at any point, ends the game at once as a loss: it goes off exactly like any trapped
///     chest (OpenChest's fuse, blast and two-health hit) and the rewards are never paid. No retry, no second look.
///   - Won or lost, Harvey gets control back and the game never starts again in this visit to the level (the trap
///     chest stays shut and dark, the reward chests stay as they were). Reloading the level starts it afresh.
///     A death or respawn in the middle (LevelManager.Respawned) puts the chests back on their slots and the game
///     back to the start, so it can be tried again; a finished game stays finished.
/// The chests are real Treasure_Chest_One instances, each the child of a holder that does the moving (OpenChest
/// puts a chest back at its own local start whenever it opens, so it must never be moved itself). Their OpenChest
/// is switched off after its first frame so it shows no prompt and never rattles; the game switches it back on only
/// to open it. Nothing is saved: the game is a per-visit encounter, like a chest roll.
/// </summary>
public class ChestShuffleGame : MonoBehaviour
{
    public enum State { Idle, Intro, Reveal, Shuffling, Choosing, Resolving, Won, Lost }

    [Header("Cast")]
    [SerializeField] private SpriteRenderer spirit;
    [Tooltip("The Spirit's frames: idle, idle bobbed, shuffling, cheering (Sprites/Characters/MushroomSpirit).")]
    [SerializeField] private Sprite[] spiritFrames = new Sprite[4];
    [Tooltip("The three chest holders. Their positions when the level starts are the three slots.")]
    [SerializeField] private Transform[] holders = new Transform[3];
    [Tooltip("Shown over a reward chest during the reveal.")]
    [SerializeField] private Sprite rewardIcon;

    [Header("Talk")]
    [SerializeField] private TextAsset dialogue;
    [SerializeField] private string introNode = "intro", againNode = "again", winNode = "win", lossNode = "loss";
    [SerializeField] private float talkRange = 1.8f, heightRange = 2f;

    [Header("Rules")]
    [SerializeField, Min(1)] private int winsNeeded = 2;
    [Tooltip("Swaps in each round (the last entry repeats).")]
    [SerializeField] private int[] swapsPerRound = { 4, 6 };
    [Tooltip("Seconds per swap in each round (the last entry repeats).")]
    [SerializeField] private float[] swapTimes = { 0.6f, 0.48f };
    [SerializeField] private float revealTime = 1.8f;
    [Tooltip("How far sideways from a chest's centre Harvey's feet can be to pick it.")]
    [SerializeField] private float pickReach = 0.75f;
    [SerializeField] private float hopHeight = 1.3f;
    [SerializeField] private ChestContents firstPrize = ChestContents.Gems, secondPrize = ChestContents.Chi;

    private State state = State.Idle;
    private int correct, round;
    private Transform trapChest;      // the trap's chest, picked once per game
    private Vector3[] slots;          // the holders' starting positions
    private OpenChest[] chests;
    private PlayerController player;
    private DialogueScript script;
    private bool upWasHeld = true, locked;
    private Coroutine running;
    private SpriteRenderer[] icons;

    public State Current => state;
    public int Correct => correct;
    public int TrapIndex => trapChest == null ? -1 : System.Array.IndexOf(holders, trapChest);
    public Transform[] Holders => holders;
    public bool CanPick => state == State.Choosing;

    private void Awake()
    {
        slots = new Vector3[holders.Length];
        chests = new OpenChest[holders.Length];
        icons = new SpriteRenderer[holders.Length];
        for (int i = 0; i < holders.Length; i++)
        {
            slots[i] = holders[i].position;
            chests[i] = holders[i].GetComponentInChildren<OpenChest>(true);
            if (chests[i] != null) chests[i].SetContents(ChestContents.Coins); // never a trap roll of its own
            var icon = new GameObject("RevealIcon");
            icon.transform.SetParent(holders[i], false);
            icon.transform.localPosition = new Vector3(0f, 1.45f, 0f);
            icons[i] = icon.AddComponent<SpriteRenderer>();
            icons[i].sprite = rewardIcon;
            icons[i].sortingLayerName = "Player";
            icons[i].sortingOrder = 20;
            icons[i].enabled = false;
        }
        if (dialogue != null) script = DialogueScript.Parse(dialogue.text, dialogue.name);
    }

    private IEnumerator Start()
    {
        yield return null; // each OpenChest runs its own Start first, then stays switched off until the game opens it
        foreach (OpenChest c in chests) if (c != null) c.enabled = false;
    }

    private void OnEnable() => LevelManager.Respawned += OnRespawned;

    private void OnDisable()
    {
        LevelManager.Respawned -= OnRespawned;
        Lock(false);
    }

    private void Update()
    {
        AnimateSpirit();
        bool up = ActionButton.Held();
        bool pressed = up && !upWasHeld;
        upWasHeld = up;
        if (DialogueBox.IsOpen || HarveyMenus.IsOpen || MenueControl.isGamePaused) return;
        if (player == null) player = FindFirstObjectByType<PlayerController>();
        if (player == null || !player.isActiveAndEnabled) return;

        if (state == State.Idle)
        {
            if (!NearSpirit()) return;
            HarveyMenus.Prompt(this, ActionButton.Prompt + "TALK TO THE MUSHROOM SPIRIT");
            if (pressed) Begin();
        }
        else if (state == State.Choosing)
        {
            int i = ChestInReach();
            if (i < 0) return;
            HarveyMenus.Prompt(this, ActionButton.Prompt + "OPEN THIS CHEST");
            if (pressed) Pick(holders[i]);
        }
    }

    // ---------------------------------------------------------------- the game

    /// <summary>Starts a game (also used by the QA driver). Does nothing unless the game is waiting to start.</summary>
    public bool Begin()
    {
        if (state != State.Idle) return false;
        trapChest = holders[Random.Range(0, holders.Length)];
        correct = 0;
        round = 0;
        state = State.Intro;
        Lock(true);
        running = StartCoroutine(Play());
        return true;
    }

    private IEnumerator Play()
    {
        yield return Say(introNode);
        yield return RevealAndShuffle();
    }

    private IEnumerator RevealAndShuffle()
    {
        state = State.Reveal;
        Lock(true);
        HarveyMenus.Banner("FIND THE TREASURE", $"{correct} / {winsNeeded} FOUND - AVOID THE TRAP");
        for (int i = 0; i < holders.Length; i++) icons[i].enabled = holders[i] != trapChest;
        for (float t = 0f; t < revealTime; t += Time.deltaTime)
        {
            Rattle(trapChest, t);
            for (int i = 0; i < holders.Length; i++)
                if (holders[i] != trapChest) icons[i].transform.localPosition = new Vector3(0f, 1.45f + Mathf.Round(Mathf.Sin((t + i) * 6f) * 1.5f) / 16f, 0f);
            yield return null;
        }
        trapChest.GetChild(0).localPosition = new Vector3(0f, trapChest.GetChild(0).localPosition.y, 0f);
        SpriteRenderer trapArt = trapChest.GetChild(0).GetComponent<SpriteRenderer>();
        if (trapArt != null) trapArt.color = Color.white;
        foreach (SpriteRenderer icon in icons) icon.enabled = false;

        state = State.Shuffling;
        int swaps = swapsPerRound[Mathf.Min(round, swapsPerRound.Length - 1)];
        float swapTime = swapTimes[Mathf.Min(round, swapTimes.Length - 1)];
        // neighbours only (slots 0-1 or 1-2): swapping the outer pair would slide one chest straight through the middle one
        for (int s = 0; s < swaps; s++)
        {
            int a = Random.Range(0, holders.Length - 1);
            yield return Swap(SlotHolder(a), SlotHolder(a + 1), swapTime);
        }
        round++;
        state = State.Choosing;
        Lock(false);
        HarveyMenus.Banner("PICK A CHEST", $"{correct} / {winsNeeded} FOUND - UP / E AT A CHEST");
    }

    // the holder standing on slot i right now
    private Transform SlotHolder(int slot)
    {
        foreach (Transform h in holders) if ((h.position - slots[slot]).sqrMagnitude < 0.01f) return h;
        return holders[slot];
    }

    // one swap: 'a' hops over, 'b' slides under, and both land exactly on each other's slot
    private IEnumerator Swap(Transform a, Transform b, float time)
    {
        Vector3 pa = a.position, pb = b.position;
        if (Inventory.HasCatalog && Inventory.Catalog.moveSound != null) HarveyMenus.PlayUiSound(Inventory.Catalog.moveSound);
        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / time);
            a.position = Pixel(Vector3.Lerp(pa, pb, k) + Vector3.up * hopHeight * Mathf.Sin(k * Mathf.PI));
            b.position = Pixel(Vector3.Lerp(pb, pa, k));
            yield return null;
        }
        a.position = pb;
        b.position = pa;
        PixelFX.Burst(pa, 4, 0.4f, 1.2f, PixelFX.GrassDust, 0.25f, 0.45f, 1, PixelShape.Square, PixelMotion.Fall, false, PixelLayer.Front, 90f, 120f);
        PixelFX.Burst(pb, 4, 0.4f, 1.2f, PixelFX.GrassDust, 0.25f, 0.45f, 1, PixelShape.Square, PixelMotion.Fall, false, PixelLayer.Front, 90f, 120f);
    }

    /// <summary>Harvey opens this chest (or the QA driver does, as he would). Ignored unless a pick is allowed now.</summary>
    public bool Pick(Transform holder)
    {
        if (state != State.Choosing || System.Array.IndexOf(holders, holder) < 0) return false;
        state = State.Resolving; // one press, one pick: nothing else can be chosen from here on
        Lock(true);
        running = StartCoroutine(Resolve(holder));
        return true;
    }

    private IEnumerator Resolve(Transform holder)
    {
        HarveyMenus.Banner("");
        if (holder == trapChest)
        {
            state = State.Lost;
            OpenChest c = holder.GetComponentInChildren<OpenChest>(true);
            c.SetContents(ChestContents.Trap);
            c.enabled = true;
            c.SendMessage("Open");             // OpenChest's own trap: fuse, blast, a two-health hit
            yield return new WaitForSeconds(1.2f);
            Darken(c);
            yield return Say(lossNode);
            Finish();
            yield break;
        }

        correct++;
        Cheer(holder);
        if (correct >= winsNeeded)
        {
            state = State.Won;
            yield return new WaitForSeconds(0.6f);
            yield return Say(winNode);
            ContentsFor(holders, out OpenChest first, out OpenChest second);
            Pay(first, firstPrize);
            yield return new WaitForSeconds(0.35f);
            Pay(second, secondPrize);
            OpenChest trap = trapChest.GetComponentInChildren<OpenChest>(true);
            Darken(trap);
            Finish();
            yield break;
        }
        yield return new WaitForSeconds(0.7f);
        yield return Say(againNode);
        yield return RevealAndShuffle();
    }

    private void ContentsFor(Transform[] all, out OpenChest first, out OpenChest second)
    {
        first = second = null;
        foreach (Transform h in all)
        {
            if (h == trapChest) continue;
            OpenChest c = h.GetComponentInChildren<OpenChest>(true);
            if (first == null) first = c; else second = c;
        }
    }

    private static void Pay(OpenChest c, ChestContents what)
    {
        if (c == null) return;
        c.SetContents(what);
        c.enabled = true;
        c.SendMessage("Open");                 // the chest's own reward flow: the prize pops out and flies to Harvey
    }

    private void Finish()
    {
        HarveyMenus.Banner("");
        Lock(false);
        running = null;
    }

    // a death or respawn in the middle of a game: the chests go back on their slots and the game can start again
    private void OnRespawned()
    {
        if (state == State.Idle || state == State.Won || state == State.Lost) return;
        if (running != null) StopCoroutine(running);
        running = null;
        for (int i = 0; i < holders.Length; i++)
        {
            holders[i].position = slots[i];
            holders[i].GetChild(0).localPosition = new Vector3(0f, holders[i].GetChild(0).localPosition.y, 0f);
            icons[i].enabled = false;
        }
        trapChest = null;
        correct = 0;
        state = State.Idle;
        HarveyMenus.Banner("");
        Lock(false);
        upWasHeld = true;
    }

    // ---------------------------------------------------------------- pieces

    private IEnumerator Say(string node)
    {
        if (script == null || string.IsNullOrEmpty(node) || !script.HasNode(node)) yield break;
        while (!DialogueBox.Open(script, node, null)) yield return null; // waits out a menu or a hit-stop
        while (DialogueBox.IsOpen) yield return null;
        upWasHeld = true; // the key that closed the box mustn't also pick a chest
    }

    private void Lock(bool on)
    {
        if (locked == on) return;
        locked = on;
        if (player == null) player = FindFirstObjectByType<PlayerController>();
        if (player != null) player.Cinematic = on; // no running, jumping, talismans or mounts while the Spirit works
    }

    private bool NearSpirit()
    {
        if (!player.GetIsGrounded || spirit == null) return false;
        Vector3 feet = player.FeetPosition;
        return Mathf.Abs(feet.x - spirit.transform.position.x) <= talkRange && Mathf.Abs(feet.y - holders[0].position.y) <= heightRange;
    }

    private int ChestInReach()
    {
        if (!player.GetIsGrounded) return -1;
        Vector3 feet = player.FeetPosition;
        int best = -1;
        float bestD = pickReach;
        for (int i = 0; i < holders.Length; i++)
        {
            float d = Mathf.Abs(feet.x - holders[i].position.x);
            if (d <= bestD && Mathf.Abs(feet.y - holders[i].position.y) <= 1f) { best = i; bestD = d; }
        }
        return best;
    }

    // the trap chest's tell: a shake in whole pixels and a red glow
    private void Rattle(Transform holder, float t)
    {
        Transform chest = holder.GetChild(0);
        float dx = (Mathf.Repeat(t, 0.5f) < 0.2f) ? Mathf.Round(Mathf.Sin(t * 70f)) / 32f : 0f;
        chest.localPosition = new Vector3(dx, chest.localPosition.y, 0f);
        // a pulsing red tint over the chest's own art (a full flash turns the sprite into a solid red block)
        SpriteRenderer sr = chest.GetComponent<SpriteRenderer>();
        if (sr != null) sr.color = Color.Lerp(Color.white, new Color(1f, 0.45f, 0.5f, 1f), 0.5f + 0.5f * Mathf.Sin(t * 9f));
        if (Random.value < 0.15f) PixelFX.Emit(chest.position + new Vector3(Random.Range(-0.3f, 0.3f), 0.35f, 0f), new Vector2(0f, 0.8f), PixelFX.Pick(PixelFX.Sparks), 0.35f);
    }

    private void Cheer(Transform holder)
    {
        Vector3 at = holder.position + Vector3.up * 0.9f;
        PixelFX.Ring(at, 14, 2.4f, new[] { PixelFX.Cream, PixelFX.Yellow, PixelFX.Hex("99e550") }, 0.45f, 1, PixelShape.Star, true);
        int i = System.Array.IndexOf(holders, holder);
        if (i >= 0) StartCoroutine(PopIcon(icons[i]));
        AudioManager am = AudioManager.Instance;
        if (am != null) am.PlaySfxOneShot(am.coinSound);
        cheerUntil = Time.time + 1.2f;
        HarveyMenus.Toast(rewardIcon, "TREASURE!", $"{correct} OF {winsNeeded} FOUND");
    }

    private IEnumerator PopIcon(SpriteRenderer icon)
    {
        icon.enabled = true;
        for (float t = 0f; t < 0.9f; t += Time.deltaTime)
        {
            icon.transform.localPosition = new Vector3(0f, 1f + Mathf.Round(t * 16f * 1.2f) / 16f, 0f);
            yield return null;
        }
        icon.enabled = false;
        icon.transform.localPosition = new Vector3(0f, 1.45f, 0f);
    }

    private static void Darken(OpenChest c)
    {
        SpriteRenderer sr = c != null ? c.GetComponent<SpriteRenderer>() : null;
        if (sr != null) sr.color = new Color(0.55f, 0.5f, 0.55f, 1f);
    }

    private float cheerUntil;

    private void AnimateSpirit()
    {
        if (spirit == null || spiritFrames == null || spiritFrames.Length < 4) return;
        int f = state == State.Shuffling ? 2 : Time.time < cheerUntil || state == State.Won ? 3 : (Mathf.FloorToInt(Time.time * 2f) % 2);
        if (spiritFrames[f] != null && spirit.sprite != spiritFrames[f]) spirit.sprite = spiritFrames[f];
        if (Random.value < 0.02f) PixelFX.Emit(spirit.bounds.center + new Vector3(Random.Range(-0.8f, 0.8f), Random.Range(-0.6f, 0.6f), 0f), new Vector2(0f, 0.25f), PixelFX.Hex("66ffe3"), 0.9f, 1, PixelShape.Square, PixelMotion.Float, true);
    }

    private static Vector3 Pixel(Vector3 p) => new Vector3(Mathf.Round(p.x * 16f) / 16f, Mathf.Round(p.y * 16f) / 16f, p.z);
}
