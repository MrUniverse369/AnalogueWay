using System.Collections;
using System.Reflection;
using System.Text;
using UnityEngine;

/// <summary>
/// ENCOUNTERS (2026-10-29): scripted Mushroom Spirit chest-game tests for the Playtest agent, on any level that has a
/// ChestShuffleGame (Z4A6, Z4A7, Z4A10): QAChestDriver.Run("&lt;mode&gt;", "&lt;log file&gt;"), one Play session a mode
/// (a finished game stays finished for the level run). Harvey is stood by the Spirit; the game is started with the
/// same Begin the Up/E press calls, and chests are opened with the same Pick the Up/E press at a chest calls. Every
/// line of the Spirit's dialogue is closed the way Esc closes it. Modes:
///   win      lock while it talks, reveals and shuffles (no pick taken, Harvey held); two reward chests: Won, both
///            rewards pay out once, the trap stays shut; no second game; Harvey has control
///   lose     the trap chest: Lost at once, the trap's hit lands, no reward; no second game; control back
///   respawn  a respawn mid-shuffle puts the chests back on their slots and the game back to waiting; it can be
///            started again and won
/// Lines go to the log file and the console as "[QACHEST] ...", ending "[QACHEST] END mode PASS n / FAIL m".
/// </summary>
public class QAChestDriver : MonoBehaviour
{
    private const BindingFlags Any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
    private string mode, logPath;
    private readonly StringBuilder log = new StringBuilder();
    private int pass, fail;
    private PlayerController pc;
    private ChestShuffleGame game;

    public static string Run(string mode, string logPath)
    {
        foreach (var old in FindObjectsByType<QAChestDriver>(FindObjectsSortMode.None)) Destroy(old.gameObject);
        var d = new GameObject("QAChestDriver").AddComponent<QAChestDriver>();
        d.mode = mode;
        d.logPath = logPath;
        return "started " + mode;
    }

    private void Line(string s)
    {
        log.AppendLine("[QACHEST] " + s);
        Debug.Log("[QACHEST] " + s);
        if (!string.IsNullOrEmpty(logPath)) System.IO.File.WriteAllText(logPath, log.ToString());
    }

    private void Check(bool ok, string what)
    {
        if (ok) pass++; else fail++;
        Line((ok ? "PASS " : "FAIL ") + what);
    }

    // closes any open dialogue the way Esc does (DialogueBox's own Close)
    private static void CloseDialogue()
    {
        if (!DialogueBox.IsOpen) return;
        object box = typeof(DialogueBox).GetField("instance", Any)?.GetValue(null);
        typeof(DialogueBox).GetMethod("Close", Any)?.Invoke(box, null);
    }

    private IEnumerator Start()
    {
        pc = FindFirstObjectByType<PlayerController>();
        game = FindFirstObjectByType<ChestShuffleGame>();
        string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (game == null) { Line("no ChestShuffleGame in " + scene); Line("END " + mode + " PASS 0 / FAIL 1"); yield break; }
        Line($"mode {mode} in {scene}; game at {game.transform.position}");
        foreach (EnemyController e in FindObjectsByType<EnemyController>(FindObjectsSortMode.None))
            if (Vector2.Distance(e.transform.position, game.transform.position) < 14f) { e.gameObject.SetActive(false); Line("switched off a nearby enemy " + e.name); }

        // stand Harvey by the Spirit, on the floor the chests stand on
        Vector3 at = game.transform.position + new Vector3(0.6f, 0.62f, 0f);
        pc.transform.position = at;
        pc.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
        yield return new WaitForSeconds(0.8f);
        Check(pc.GetIsGrounded, $"Harvey stands by the Spirit ({pc.transform.position.x:0.0},{pc.transform.position.y:0.0}), grounded");
        Check(game.Current == ChestShuffleGame.State.Idle, "the game waits for Harvey (" + game.Current + ")");

        switch (mode)
        {
            case "win": yield return Win(); break;
            case "lose": yield return Lose(); break;
            case "respawn": yield return Respawn(); break;
            default: Line("unknown mode " + mode); fail++; break;
        }
        Line($"END {mode} PASS {pass} / FAIL {fail}");
    }

    // runs the talk, the reveal and the shuffle, checking that nothing can be picked and Harvey is held meanwhile
    private IEnumerator ToChoosing(string what)
    {
        bool pickedEarly = false, freed = false;
        float end = Time.time + 40f;
        while (game.Current != ChestShuffleGame.State.Choosing && Time.time < end)
        {
            CloseDialogue();
            if (game.Current == ChestShuffleGame.State.Shuffling || game.Current == ChestShuffleGame.State.Reveal)
            {
                if (game.Pick(game.Holders[0])) pickedEarly = true;
                if (!pc.Cinematic) freed = true;
            }
            yield return null;
        }
        Check(game.Current == ChestShuffleGame.State.Choosing, $"{what}: the chests come to rest and wait for a pick ({game.Current})");
        Check(!pickedEarly, $"{what}: no pick is taken while the Spirit reveals or shuffles");
        Check(!freed, $"{what}: Harvey is held while it reveals and shuffles");
        Check(!pc.Cinematic, $"{what}: Harvey is free to walk to a chest");
        bool onSlots = true;
        foreach (Transform h in game.Holders)
            if (Mathf.Abs(h.position.y - game.transform.position.y) > 0.01f) onSlots = false;
        Check(onSlots, $"{what}: every chest is back on the floor after the shuffle");
    }

    private Transform RewardHolder()
    {
        int trap = game.TrapIndex;
        for (int i = 0; i < game.Holders.Length; i++) if (i != trap && game.Holders[i].GetComponentInChildren<OpenChest>(true) != null && !picked[i]) return game.Holders[i];
        return null;
    }

    private readonly bool[] picked = new bool[3];

    private IEnumerator Win()
    {
        int chi0 = Mathf.RoundToInt(PlayerController.chi);
        Check(game.Begin(), "talking to the Spirit starts the game");
        Check(!game.Begin(), "a second press while it runs does nothing");
        yield return ToChoosing("round 1");
        Transform first = RewardHolder();
        picked[System.Array.IndexOf(game.Holders, first)] = true;
        Check(game.Pick(first), "a reward chest can be picked");
        Check(!game.Pick(first) && !game.Pick(game.Holders[game.TrapIndex]), "the same press can't pick again");
        Check(game.Correct == 1, $"one found ({game.Correct})");
        yield return ToChoosing("round 2");
        Transform second = RewardHolder();
        picked[System.Array.IndexOf(game.Holders, second)] = true;
        Check(game.Pick(second), "the second reward chest can be picked");
        float end = Time.time + 15f;
        while (Time.time < end && (DialogueBox.IsOpen || pc.Cinematic || game.Current == ChestShuffleGame.State.Resolving)) { CloseDialogue(); yield return null; }
        yield return new WaitForSeconds(2.5f);
        CloseDialogue();
        Check(game.Current == ChestShuffleGame.State.Won, $"two found: the game is won ({game.Current}, {game.Correct} found)");
        Line($"chi {chi0} -> {Mathf.RoundToInt(PlayerController.chi)}");
        Check(!pc.Cinematic && !DialogueBox.IsOpen, "Harvey has control afterwards");
        Check(!game.Begin(), "the Spirit starts no second game this level run");
        yield return new WaitForSeconds(0.5f);
        Check(game.Current == ChestShuffleGame.State.Won, "it stays won");
    }

    private IEnumerator Lose()
    {
        int lives0 = HurtPlayer.pLivesCount;
        Check(game.Begin(), "talking to the Spirit starts the game");
        yield return ToChoosing("round 1");
        Transform trap = game.Holders[game.TrapIndex];
        pc.transform.position = new Vector3(trap.position.x, game.transform.position.y + 0.62f, 0f); // at the chest, as a player picking it stands
        yield return new WaitForSeconds(0.3f);
        Check(game.Pick(trap), "the trap chest can be picked");
        Check(game.Current == ChestShuffleGame.State.Lost, $"the trap ends the game at once ({game.Current})");
        float end = Time.time + 15f;
        while (Time.time < end && (DialogueBox.IsOpen || pc.Cinematic)) { CloseDialogue(); yield return null; }
        yield return new WaitForSeconds(1.5f);
        CloseDialogue();
        Line($"lives {lives0} -> {HurtPlayer.pLivesCount}");
        Check(HurtPlayer.pLivesCount < lives0 || pc.RespawnHeld, "the trap's blast hits Harvey");
        Check(game.Correct == 0, "no reward counted");
        yield return new WaitForSeconds(2f);
        Check(!game.Begin(), "the Spirit starts no second game this level run");
        Check(game.Current == ChestShuffleGame.State.Lost, "it stays lost (" + game.Current + ")");
    }

    private IEnumerator Respawn()
    {
        Vector3[] slots = new Vector3[game.Holders.Length];
        for (int i = 0; i < slots.Length; i++) slots[i] = game.Holders[i].position;
        Check(game.Begin(), "talking to the Spirit starts the game");
        float end = Time.time + 30f;
        while (game.Current != ChestShuffleGame.State.Shuffling && Time.time < end) { CloseDialogue(); yield return null; }
        yield return new WaitForSeconds(0.3f);
        Check(game.Current == ChestShuffleGame.State.Shuffling, "the chests are being shuffled");
        HurtPlayer.pLivesCount = 0;
        LevelManager.Instance.Respawn(); // as a fatal hit or a fall does
        end = Time.time + 8f;
        while (game.Current != ChestShuffleGame.State.Idle && Time.time < end) yield return null; // the game hears LevelManager.Respawned once Harvey is back
        Check(game.Current == ChestShuffleGame.State.Idle, $"a respawn mid-game puts it back to waiting ({game.Current})");
        bool back = true;
        for (int i = 0; i < slots.Length; i++) if ((game.Holders[i].position - slots[i]).sqrMagnitude > 0.0001f) back = false;
        Check(back, "every chest is back on its own slot");
        end = Time.time + 8f;
        while (pc.RespawnHeld && Time.time < end) yield return null;
        yield return new WaitForSeconds(1.5f);
        pc.transform.position = game.transform.position + new Vector3(0.6f, 0.62f, 0f);
        yield return new WaitForSeconds(0.8f);
        Check(game.Begin(), "after the respawn the game can be started again");
        yield return ToChoosing("after the respawn");
        Transform first = RewardHolder();
        picked[System.Array.IndexOf(game.Holders, first)] = true;
        game.Pick(first);
        yield return ToChoosing("after the respawn, round 2");
        Transform second = RewardHolder();
        game.Pick(second);
        end = Time.time + 15f;
        while (Time.time < end && (DialogueBox.IsOpen || pc.Cinematic || game.Current == ChestShuffleGame.State.Resolving)) { CloseDialogue(); yield return null; }
        Check(game.Current == ChestShuffleGame.State.Won, $"and won ({game.Current})");
        Check(!game.Begin(), "then no further game");
    }
}
