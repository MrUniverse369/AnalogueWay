using System.Collections;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// NINELIVES (2026-10-29): scripted Nine Lives tests for the Playtest agent, started from the Unity MCP in Play mode on
/// the main menu: QANineLivesDriver.Run("&lt;mode&gt;", "&lt;log file&gt;", "&lt;screenshot prefix&gt;"). The run is
/// started by the menu's own Nine Lives button (its onClick: MenueControl.StartArcade); deaths go through the level's
/// own paths (a kill plane, an enemy's HurtPlayer, LevelManager.Respawn as a hazard calls it). Saves must already be in
/// memory; the save files are not written by a Nine Lives run anyway. Modes:
///   run      the button's label, nine lives and the counter at the start; an enemy hit takes one bar of health and no
///            life; the Healing Stone shields but heals nothing; a health item can't be eaten; a chest's peach goes to
///            the Bag; a checkpoint and a save attempt change nothing; two hazards in one moment cost one life; the
///            respawn starts the next life with a full bar and the count stays down; then every life lost, the last
///            one ends the run (the card, no respawn, the level stopped) and any key goes to the main menu
///   win      a new run starts at nine; one death; the last level finished: the Win scene's card with eight left,
///            recorded once (loading the Win scene again records nothing)
///   normal   a saved game (not a Nine Lives run): the Healing Stone and a health item heal, deaths don't touch the
///            count, no counter on screen; and the tutorial is not a Nine Lives run
/// Lines go to the log file and the console as "[QA9L] ...", ending "[QA9L] END mode PASS n / FAIL m".
/// The win mode writes the Nine Lives record to PlayerPrefs (HNL.NineLives.*); the caller restores it.
/// </summary>
public class QANineLivesDriver : MonoBehaviour
{
    private const BindingFlags Any = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    private string mode, logPath, shots;
    private readonly StringBuilder log = new StringBuilder();
    private int pass, fail;

    public static string Run(string mode, string logPath, string shots)
    {
        var go = new GameObject("QANineLivesDriver");
        DontDestroyOnLoad(go);
        var d = go.AddComponent<QANineLivesDriver>();
        d.mode = mode;
        d.logPath = logPath;
        d.shots = shots;
        return "started " + mode;
    }

    private void Line(string s)
    {
        log.AppendLine("[QA9L] " + s);
        Debug.Log("[QA9L] " + s);
        if (!string.IsNullOrEmpty(logPath)) System.IO.File.WriteAllText(logPath, log.ToString());
    }

    private void Check(bool ok, string what)
    {
        if (ok) pass++; else fail++;
        Line((ok ? "PASS " : "FAIL ") + what);
    }

    private IEnumerator Start()
    {
        StartCoroutine(WatchLives());
        if (mode == "run") yield return RunMode();
        else if (mode == "win") yield return WinMode();
        else if (mode == "normal") yield return NormalMode();
        else if (mode == "kill") yield return KillMode();
        Line($"END {mode} PASS {pass} / FAIL {fail}");
        Destroy(gameObject);
    }

    // every life lost, with where Harvey was and what hit him last (to account for each one)
    private IEnumerator WatchLives()
    {
        int last = NineLives.Left;
        Vector3 seen = Vector3.zero;
        while (true)
        {
            PlayerController pc = Harvey;
            if (pc != null && pc.gameObject.activeInHierarchy) seen = pc.transform.position;
            if (NineLives.Left != last)
            {
                if (NineLives.Left < last)
                    Line($"  (life lost: {last} -> {NineLives.Left}; last seen at ({seen.x:0.0},{seen.y:0.0}); last HurtPlayer hit frame {HurtPlayer.LastHitFrame} (now {Time.frameCount}) by {(HurtPlayer.LastHitByEnemy != null ? HurtPlayer.LastHitByEnemy.name : "no enemy")})");
                last = NineLives.Left;
            }
            yield return null;
        }
    }

    // ---------------------------------------------------------------- helpers
    private IEnumerator WaitForLevel(string name = null, float timeout = 20f)
    {
        float end = Time.realtimeSinceStartup + timeout;
        yield return null;
        while (Time.realtimeSinceStartup < end &&
               (LevelLoader.Busy || (name != null && SceneManager.GetActiveScene().name != name) || (name == null && FindFirstObjectByType<PlayerController>() == null)))
            yield return null;
        yield return new WaitForSecondsRealtime(1.5f);
    }

    private static PlayerController Harvey => FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);

    private IEnumerator StartFromMenu()
    {
        Button button = null;
        foreach (Button b in FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (b.name == "ArcadeButton") button = b;
        string label = button != null ? button.GetComponentInChildren<TMPro.TMP_Text>(true).text : "(none)";
        Check(button != null && label == "NINE LIVES", $"the main menu's button reads '{label}'");
        if (!string.IsNullOrEmpty(shots)) { ScreenCapture.CaptureScreenshot(shots + "_menu.png"); yield return new WaitForSecondsRealtime(0.4f); }
        button.onClick.Invoke(); // the button's own call: MenueControl.StartArcade
        yield return WaitForLevel();
        Line($"started: scene {SceneManager.GetActiveScene().name}, IsArcade {GameSession.IsArcade}, IsNineLives {GameSession.IsNineLives}, lives {NineLives.Left}, health {HurtPlayer.pLivesCount}");
    }

    private int LitPips()
    {
        var hud = FindFirstObjectByType<NineLivesHUD>();
        if (hud == null) return -1;
        var pips = (Image[])typeof(NineLivesHUD).GetField("pips", Any).GetValue(hud);
        int lit = 0;
        foreach (Image p in pips) if (p.color == MenuUI.Cream || p.color == MenuUI.Red) lit++;
        return lit;
    }

    // a death as a hazard causes it (HurtPlayer: lives to 0, then the level's respawn); twice = two hazards at once
    private IEnumerator Die(bool twice = false)
    {
        HurtPlayer.pLivesCount = 0;
        LevelManager.Instance.Respawn();
        if (twice) LevelManager.Instance.Respawn();
        float end = Time.realtimeSinceStartup + 6f;
        while (Time.realtimeSinceStartup < end && !(Harvey != null && Harvey.gameObject.activeInHierarchy && !Harvey.NoControl) && !NineLives.Over) yield return null;
        yield return new WaitForSecondsRealtime(0.3f);
    }

    private static Talisman Stone()
    {
        foreach (Talisman t in Inventory.Catalog.talismans) if (t is HealingTalisman) return t;
        return null;
    }

    private IEnumerator UseStone()
    {
        Talisman stone = Stone();
        Inventory.Unlock(stone);
        Inventory.SetEquipped(1, stone);
        yield return null;
        PowerUpManager m = PowerUpManager.Instance;
        int slot = -1;
        for (int i = 0; i < m.SlotCount; i++) if (m.TalismanIn(i) == stone) slot = i;
        PlayerController.chi = 90f;
        var slots = (System.Array)typeof(PowerUpManager).GetField("slots", Any).GetValue(m);
        object s = slots.GetValue(slot);
        s.GetType().GetField("pressQueued", Any).SetValue(s, true);
        yield return null;
        yield return null;
        Line($"  (Healing Stone in slot {slot}: running {m.IsSlotActive(slot)}, Harvey in control {!Harvey.NoControl}, chi {PlayerController.chi:0})");
        yield return new WaitForSeconds(2.5f); // past the stone's 1.2 s fill and a few regen steps
    }

    private static ItemDefinition Item(string id) => Inventory.Catalog.ItemById(id);

    // ---------------------------------------------------------------- modes
    private IEnumerator RunMode()
    {
        yield return StartFromMenu();
        Check(GameSession.IsNineLives && NineLives.Left == 9 && !NineLives.Over, $"a new run: Nine Lives on, {NineLives.Left} lives");
        Check(LitPips() == 9, $"the counter shows nine lit pips ({LitPips()})");
        if (!string.IsNullOrEmpty(shots)) { ScreenCapture.CaptureScreenshot(shots + "_hud9.png"); yield return new WaitForSecondsRealtime(0.4f); }

        // an enemy's hit: one bar of health, no life
        PlayerController pc = Harvey;
        EnemyController foe = null;
        foreach (EnemyController e in FindObjectsByType<EnemyController>(FindObjectsSortMode.None))
            if (e.GetComponent<HurtPlayer>() != null && (foe == null || Vector2.Distance(e.transform.position, pc.transform.position) < Vector2.Distance(foe.transform.position, pc.transform.position))) foe = e;
        int health = HurtPlayer.pLivesCount;
        if (foe != null)
        {
            pc.transform.position = foe.transform.position + Vector3.up * 0.1f;
            float end = Time.realtimeSinceStartup + 2f;
            while (HurtPlayer.pLivesCount == health && Time.realtimeSinceStartup < end) yield return null;
        }
        Line($"enemy hit from {(foe != null ? foe.transform.parent.name : "none")}: health {health} -> {HurtPlayer.pLivesCount}");
        Check(HurtPlayer.pLivesCount == health - 1 && NineLives.Left == 9, $"an enemy's hit takes one bar of health ({health} -> {HurtPlayer.pLivesCount}) and no life ({NineLives.Left})");
        yield return new WaitForSeconds(1.6f); // the hurt flicker

        // the Healing Stone: shield yes, heal no
        HurtPlayer.pLivesCount = 1;
        yield return UseStone();
        Check(HurtPlayer.pLivesCount == 1, $"the Healing Stone heals nothing (health 1 -> {HurtPlayer.pLivesCount})");
        Check(HurtPlayer.Shielded, $"its shield still runs (shielded {HurtPlayer.Shielded}): a shield is not healing");
        yield return new WaitForSeconds(5f); // let the stone run out

        // a health item can't be eaten
        ItemDefinition peach = Item("SpiritPeach");
        Inventory.Add(peach, 1);
        bool used = peach.TryUse(out string msg);
        Check(!used && HurtPlayer.pLivesCount == 1, $"a Spirit Peach can't be eaten ('{msg}', health {HurtPlayer.pLivesCount})");

        // a chest's peach goes to the Bag
        int inBag = Inventory.Count(peach);
        OpenChest chest = FindFirstObjectByType<OpenChest>(FindObjectsInactive.Include);
        if (chest != null) chest.SendMessage("CollectPeach", peach);
        Check(chest != null && HurtPlayer.pLivesCount == 1 && Inventory.Count(peach) == inBag + 1, $"a chest's peach heals nothing and goes to the Bag (health {HurtPlayer.pLivesCount}, Bag {inBag} -> {Inventory.Count(peach)})");

        // a checkpoint and a save attempt change nothing
        GameSession.SaveProgress();
        CheckPoint cp = FindFirstObjectByType<CheckPoint>();
        if (cp != null)
        {
            pc.transform.position = cp.transform.position;
            yield return new WaitForSeconds(0.5f);
        }
        Check(NineLives.Left == 9 && HurtPlayer.pLivesCount == 1, $"a checkpoint and a save attempt restore nothing (lives {NineLives.Left}, health {HurtPlayer.pLivesCount})");

        // two hazards at once: one life
        yield return Die(true);
        Check(NineLives.Left == 8, $"two hazards in one moment cost one life ({NineLives.Left} left)");
        Check(HurtPlayer.pLivesCount == GameSession.StartingLives, $"the next life starts with a full bar ({HurtPlayer.pLivesCount})");
        Check(LitPips() == 8, $"the counter follows ({LitPips()} lit)");

        // the spike trap: Harvey standing in a spike that didn't fire (as when a knockback, which makes him
        // invincible, pushes him into one: a spike only hurts on entry) must not become the place a respawn puts him.
        // For the step in, the spike is untagged: its entry then costs one bar of health instead of a life (as any
        // HurtPlayer that isn't a kill plane does), and he is left standing inside a spike that has already fired,
        // the state a knockback leaves him in. The tag goes back straight after.
        yield return new WaitForSeconds(1f);
        GameObject spike = null;
        foreach (GameObject g in GameObject.FindGameObjectsWithTag("KillPlane"))
            if (g.activeInHierarchy && g.GetComponent<HurtPlayer>() != null && g.GetComponent<Renderer>() != null) { spike = g; break; }
        if (spike != null)
        {
            Bounds sb = spike.GetComponent<Collider2D>().bounds;
            string tag = spike.tag;
            spike.tag = "Untagged";
            Harvey.transform.position = new Vector3(sb.center.x, sb.max.y + 0.4f, 0f);
            yield return new WaitForSeconds(0.4f);
            spike.tag = tag;                                   // a kill plane again, but he is already inside it: no entry fires
            yield return new WaitForSeconds(0.8f);             // standing in it, grounded, far longer than the 0.25 s a safe point needs
            Vector3 stood = Harvey.transform.position;
            string safe = SafePoint();
            bool stillAlive = Harvey.gameObject.activeInHierarchy;
            Line($"stood in {spike.name} at ({stood.x:0.00},{stood.y:0.00}) (spike {sb.min}..{sb.max}), alive {stillAlive}, grounded {Harvey.GetIsGrounded}; safe point {safe}");
            object sp = typeof(LevelManager).GetField("arcadeSafePoint", Any).GetValue(LevelManager.Instance);
            bool safeInSpike = sp is Vector3 v && Mathf.Abs(v.x - sb.center.x) < 0.8f && Mathf.Abs(v.y - stood.y) < 0.6f;
            Check(stillAlive && !safeInSpike, $"standing in a spike that didn't fire doesn't make it the safe point (safe point {safe})");
            if (sp is Vector3 off) Harvey.transform.position = off; // off it again, onto ground known to be safe (spikes come in rows)
            yield return new WaitForSeconds(1f);
            int before = NineLives.Left;
            yield return Die();
            yield return new WaitForSeconds(3f); // long enough for a death loop to show
            Vector3 p = Harvey.transform.position;
            Check(NineLives.Left == before - 1 && Harvey.gameObject.activeInHierarchy, $"no death loop: the next death costs one life ({before} -> {NineLives.Left}) and he respawns clear ({p.x:0.0},{p.y:0.0})");
        }
        else Line("no spike found for the trap check");

        // the level's kill plane (the pit under the level): one more
        GameObject kill = GameObject.Find("KillPlane");
        int beforeKill = NineLives.Left;
        if (kill != null && kill.GetComponent<Collider2D>() != null)
        {
            Harvey.transform.position = kill.GetComponent<Collider2D>().bounds.center;
            float end = Time.realtimeSinceStartup + 6f;
            while (NineLives.Left == beforeKill && Time.realtimeSinceStartup < end) yield return null;
            end = Time.realtimeSinceStartup + 6f;
            while (Time.realtimeSinceStartup < end && !(Harvey.gameObject.activeInHierarchy && !Harvey.NoControl)) yield return null;
            yield return new WaitForSeconds(2f);
        }
        Check(kill != null && NineLives.Left == beforeKill - 1, $"a fall into the level's kill plane costs one life ({beforeKill} -> {NineLives.Left})");
        if (!string.IsNullOrEmpty(shots)) { ScreenCapture.CaptureScreenshot(shots + "_hud.png"); yield return new WaitForSecondsRealtime(0.4f); }

        // down to one, then the last
        while (NineLives.Left > 1) yield return Die();
        Check(NineLives.Left == 1 && !NineLives.Over && Harvey.gameObject.activeInHierarchy, $"at one life left Harvey is still playing ({NineLives.Left})");
        yield return Die();
        yield return new WaitForSecondsRealtime(1f);
        Check(NineLives.Over && !NineLives.Won && NineLives.Left == 0, $"the ninth death ends the run (Over {NineLives.Over}, lives {NineLives.Left})");
        Check(!Harvey.gameObject.activeInHierarchy && Time.timeScale == 0f, $"no respawn after it, the level stopped (Harvey active {Harvey.gameObject.activeInHierarchy}, timeScale {Time.timeScale})");
        Check(FindFirstObjectByType<NineLivesCard>() != null, "the game-over card is up");
        if (!string.IsNullOrEmpty(shots)) { ScreenCapture.CaptureScreenshot(shots + "_gameover.png"); yield return new WaitForSecondsRealtime(0.5f); }
        yield return new WaitForSecondsRealtime(1.2f);
        FindFirstObjectByType<NineLivesCard>().Leave(); // as any key does
        yield return WaitForLevel("MainMenue");
        Check(SceneManager.GetActiveScene().name == "MainMenue" && !GameSession.IsArcade && Time.timeScale == 1f, $"then the main menu, the run ended ({SceneManager.GetActiveScene().name}, IsArcade {GameSession.IsArcade})");
    }

    // diagnostic: one fall onto the level's kill plane, entered only once the post-hit flicker is over; logs where
    // each respawn puts him (the arcade safe point) and the count, for 8 seconds
    private IEnumerator KillMode()
    {
        yield return StartFromMenu();
        PlayerController pc = Harvey;
        float end = Time.realtimeSinceStartup + 4f;
        while (PlayerController.playerInviInvincible && Time.realtimeSinceStartup < end) yield return null;
        yield return new WaitForSeconds(1f);
        GameObject kill = null;
        foreach (GameObject g in GameObject.FindGameObjectsWithTag("KillPlane")) if (g.activeInHierarchy && g.GetComponent<Collider2D>() != null) { kill = g; break; }
        Bounds kb = kill.GetComponent<Collider2D>().bounds;
        Line($"kill plane {kill.name} bounds {kb.min}..{kb.max}; Harvey at {pc.transform.position}, invincible {PlayerController.playerInviInvincible}, safe point {SafePoint()}");
        pc.transform.position = new Vector3(pc.transform.position.x, kb.center.y, 0f); // straight down onto it, under where he stood
        end = Time.realtimeSinceStartup + 8f;
        int last = NineLives.Left;
        while (Time.realtimeSinceStartup < end)
        {
            if (NineLives.Left != last) { last = NineLives.Left; Line($"life lost: {last} left; Harvey {pc.transform.position} active {pc.gameObject.activeInHierarchy}, invincible {PlayerController.playerInviInvincible}, safe point {SafePoint()}"); }
            yield return null;
        }
        Check(NineLives.Left == 8, $"one fall onto the kill plane costs one life ({NineLives.Left} left)");
        Line($"after: Harvey {pc.transform.position} active {pc.gameObject.activeInHierarchy} grounded {pc.GetIsGrounded}");
    }

    private static string SafePoint()
    {
        object v = typeof(LevelManager).GetField("arcadeSafePoint", Any).GetValue(LevelManager.Instance);
        return v == null ? "none" : v.ToString();
    }

    private IEnumerator WinMode()
    {
        int wins0 = NineLives.Wins;
        yield return StartFromMenu();
        Check(NineLives.Left == 9 && !NineLives.Over, $"a new run starts at nine ({NineLives.Left})");
        yield return Die();
        Check(NineLives.Left == 8, $"one death: {NineLives.Left} left");
        // the last level, finished as its treasure does it
        LevelLoader.Load("Z4Boss_Japan");
        yield return WaitForLevel("Z4Boss_Japan");
        Check(GameSession.IsNineLives && NineLives.Left == 8, $"a level change keeps the run and the count ({NineLives.Left})");
        GameSession.FinishLevel();
        yield return WaitForLevel("Win");
        yield return new WaitForSecondsRealtime(0.5f);
        Check(SceneManager.GetActiveScene().name == "Win", "finishing the last level reaches the end (Win)");
        Check(NineLives.Over && NineLives.Won && NineLives.Wins == wins0 + 1, $"the run is won and recorded once (wins {wins0} -> {NineLives.Wins}, best {NineLives.BestLivesLeft})");
        Check(FindFirstObjectByType<NineLivesCard>() != null, "the completion card is up");
        if (!string.IsNullOrEmpty(shots)) { ScreenCapture.CaptureScreenshot(shots + "_won.png"); yield return new WaitForSecondsRealtime(0.5f); }
        LevelLoader.Load("Win");
        yield return WaitForLevel("Win");
        Check(NineLives.Wins == wins0 + 1, $"loading the end again records nothing more ({NineLives.Wins})");
    }

    private IEnumerator NormalMode()
    {
        // the tutorial is an unsaved run too, but not Nine Lives
        GameSession.BeginTutorialInPlace();
        Check(!GameSession.IsNineLives && GameSession.HealingAllowed, $"the tutorial is not a Nine Lives run (IsNineLives {GameSession.IsNineLives}, healing allowed {GameSession.HealingAllowed})");
        GameSession.EndArcade();
        // a saved game: the level played from the save file (Continue / the map); here, a level loaded with no run on
        // (Z4A7: Z4A6 would play its opening cinematic first, with Harvey out of control)
        LevelLoader.Load("Z1A2_GrassGreen");
        yield return WaitForLevel("Z1A2_GrassGreen");
        float wait = Time.realtimeSinceStartup + 10f;
        while (Harvey != null && Harvey.NoControl && Time.realtimeSinceStartup < wait) yield return null;
        Check(!GameSession.IsNineLives && FindFirstObjectByType<NineLivesHUD>() == null, $"normal play: no Nine Lives run, no counter (IsNineLives {GameSession.IsNineLives})");
        int left = NineLives.Left;
        HurtPlayer.pLivesCount = 1;
        yield return UseStone();
        Check(HurtPlayer.pLivesCount == GameSession.StartingLives, $"normal play: the Healing Stone heals (health 1 -> {HurtPlayer.pLivesCount})");
        yield return new WaitForSeconds(5f);
        HurtPlayer.pLivesCount = 1;
        ItemDefinition peach = Item("SpiritPeach");
        Inventory.Add(peach, 1);
        bool used = peach.TryUse(out string msg);
        Check(used && HurtPlayer.pLivesCount == 2, $"normal play: a Spirit Peach heals ('{msg}', health {HurtPlayer.pLivesCount})");
        yield return Die();
        Check(NineLives.Left == left && HurtPlayer.pLivesCount == GameSession.StartingLives, $"normal play: a death respawns as before and touches no Nine Lives count ({left} -> {NineLives.Left}, health {HurtPlayer.pLivesCount})");
    }
}
