using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// BUGSWEEP (2026-10-29): scripted checks of the tomb bug sweep's respawn bugs for the Playtest agent, started from the
/// Unity MCP in Play mode in an act: QARespawnBugsDriver.Run("&lt;log file&gt;"). Deaths go through the level's own
/// hazards (their HurtPlayer.OnTriggerEnter2D, handed Harvey's PlayerDetectionArea, as the physics would):
///   B3   a spike (Fspike, or a HurtPlayer-tagged trap where the act has none), then a kill plane, on 1 life: one respawn each (HurtPlayer.oopsCount, one per respawn); a spike and
///        a kill plane in the same frame: one respawn; a death after he has reappeared is a new one
///   B5   after a respawn every ResetOnRespawn object keeps the z scale it started with (was set to 0)
///   B14  touching the treasure: NextLevel.Ending holds through the freeze (the pause menu is shut meanwhile) with the
///        time scale at 0, then the level finishes. Esc itself can't be pressed from a script (legacy Input), so the
///        key press is not covered: only the flag MenueControl now checks
/// Lines go to the log file and the console as "[QABUG] ...", ending "[QABUG] END PASS n / FAIL m". The level
/// finishing writes the save, so back the slots up first (QARunner.BackupSaves) and restore them after.
/// </summary>
public class QARespawnBugsDriver : MonoBehaviour
{
    private string logPath;
    private readonly StringBuilder log = new StringBuilder();
    private int pass, fail;

    public static string Run(string logPath)
    {
        var go = new GameObject("QARespawnBugsDriver");
        DontDestroyOnLoad(go);
        go.AddComponent<QARespawnBugsDriver>().logPath = logPath;
        return "started";
    }

    private void Line(string s)
    {
        log.AppendLine("[QABUG] " + s);
        Debug.Log("[QABUG] " + s);
        if (!string.IsNullOrEmpty(logPath)) System.IO.File.WriteAllText(logPath, log.ToString());
    }

    private void Check(bool ok, string what)
    {
        if (ok) pass++; else fail++;
        Line((ok ? "PASS " : "FAIL ") + what);
    }

    private static PlayerController Harvey => FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);

    private static Collider2D DetectionArea()
    {
        PlayerController pc = Harvey;
        if (pc == null) return null;
        foreach (Collider2D c in pc.GetComponentsInChildren<Collider2D>(true))
            if (c.CompareTag("PlayerDetectionArea")) return c;
        return null;
    }

    private static HurtPlayer Hazard(string tag)
    {
        foreach (HurtPlayer h in FindObjectsByType<HurtPlayer>(FindObjectsSortMode.None))
            if (h.isActiveAndEnabled && h.CompareTag(tag)) return h;
        return null;
    }

    // until Harvey is back at the checkpoint and has control again
    private IEnumerator Settle()
    {
        float until = Time.realtimeSinceStartup + 8f;
        while (Time.realtimeSinceStartup < until && (!Harvey.gameObject.activeInHierarchy || Harvey.RespawnHeld)) yield return null;
        yield return new WaitForSeconds(0.5f);
    }

    private IEnumerator Start()
    {
        yield return new WaitForSeconds(2f);
        Line($"scene {SceneManager.GetActiveScene().name}");
        Collider2D area = DetectionArea();
        // the tomb tags its spikes and traps HurtPlayer; Fspike (instant death) is only in Snow Temple acts
        HurtPlayer spike = Hazard("Fspike"), kill = Hazard("KillPlane");
        if (spike == null)
            foreach (HurtPlayer h in FindObjectsByType<HurtPlayer>(FindObjectsSortMode.None))
                if (h.isActiveAndEnabled && h.CompareTag("HurtPlayer") && h.GetComponent<EnemyController>() == null) { spike = h; break; }
        Check(area != null && spike != null && kill != null, $"found Harvey's detection area ({area != null}), a spike ({(spike != null ? spike.name : "none")}) and a kill plane ({(kill != null ? kill.name : "none")})");
        if (area == null || spike == null || kill == null) { Finish(); yield break; }
        PlayerController.playerInviInvincible = false;

        // B5: the z scale every reset object starts with
        ResetOnRespawn[] resets = FindObjectsByType<ResetOnRespawn>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var startZ = new float[resets.Length];
        for (int i = 0; i < resets.Length; i++) startZ[i] = resets[i].transform.localScale.z;

        // B3: a spike on 1 life
        HurtPlayer.pLivesCount = 1;
        int oops = HurtPlayer.oopsCount;
        spike.SendMessage("OnTriggerEnter2D", area);
        yield return Settle();
        Check(HurtPlayer.oopsCount == oops + 1, $"B3: a {spike.tag} hazard ({spike.name}) on 1 life respawns once (respawns {HurtPlayer.oopsCount - oops})");

        // B3: a kill plane on 1 life
        HurtPlayer.pLivesCount = 1;
        oops = HurtPlayer.oopsCount;
        kill.SendMessage("OnTriggerEnter2D", area);
        yield return Settle();
        Check(HurtPlayer.oopsCount == oops + 1, $"B3: a kill plane on 1 life respawns once (respawns {HurtPlayer.oopsCount - oops})");

        // B3: two hazards in the same frame
        oops = HurtPlayer.oopsCount;
        spike.SendMessage("OnTriggerEnter2D", area);
        kill.SendMessage("OnTriggerEnter2D", area);
        yield return Settle();
        Check(HurtPlayer.oopsCount == oops + 1, $"B3: a spike and a kill plane in one frame respawn once (respawns {HurtPlayer.oopsCount - oops})");
        Check(Harvey.gameObject.activeInHierarchy && !Harvey.RespawnHeld && HurtPlayer.pLivesCount == 3, $"he is back with control and full health ({HurtPlayer.pLivesCount})");

        // B3: the guard doesn't swallow a new death once he is back (during the camera's return)
        oops = HurtPlayer.oopsCount;
        kill.SendMessage("OnTriggerEnter2D", area);
        float back = Time.realtimeSinceStartup + 8f;
        while (Time.realtimeSinceStartup < back && !Harvey.gameObject.activeInHierarchy) yield return null;
        yield return null;
        kill.SendMessage("OnTriggerEnter2D", area); // he has reappeared but the camera is still coming back
        yield return Settle();
        Check(HurtPlayer.oopsCount == oops + 2, $"B3: a death just after reappearing is a new respawn (respawns {HurtPlayer.oopsCount - oops}, want 2)");

        // B5
        int bad = 0, checkedCount = 0;
        string first = "";
        for (int i = 0; i < resets.Length; i++)
        {
            if (resets[i] == null) continue;
            checkedCount++;
            if (!Mathf.Approximately(resets[i].transform.localScale.z, startZ[i]))
            {
                bad++;
                if (first == "") first = $" e.g. {resets[i].name} z {startZ[i]} -> {resets[i].transform.localScale.z}";
            }
        }
        Check(checkedCount > 0 && bad == 0, $"B5: after the respawns all {checkedCount} ResetOnRespawn objects keep their start z scale ({bad} changed{first})");

        // B14: the level-complete freeze
        NextLevel end = FindFirstObjectByType<NextLevel>();
        Check(end != null, $"found the treasure ({(end != null ? end.name : "none")})");
        if (end == null) { Finish(); yield break; }
        if (end.RequireAction) { Line("the treasure needs the action button here; B14 not run"); Finish(); yield break; }
        end.SendMessage("OnTriggerStay2D", area); // the treasure takes his detection area through OnTriggerStay2D
        yield return new WaitForSecondsRealtime(1f);
        bool frozen = NextLevel.Ending && Time.timeScale == 0f;
        Check(frozen, $"B14: in the freeze NextLevel.Ending is {NextLevel.Ending} and the time scale {Time.timeScale}");
        yield return new WaitForSecondsRealtime(2.5f);
        Check(NextLevel.Ending && Time.timeScale == 0f && !MenueControl.isGamePaused, $"B14: still frozen 3.5 s in (Ending {NextLevel.Ending}, timeScale {Time.timeScale}, paused {MenueControl.isGamePaused})");
        float done = Time.realtimeSinceStartup + 15f;
        while (Time.realtimeSinceStartup < done && SceneManager.GetActiveScene().name != "ZoneSelect") yield return null;
        yield return new WaitForSecondsRealtime(0.5f);
        Check(!NextLevel.Ending && SceneManager.GetActiveScene().name == "ZoneSelect", $"B14: then the level finishes to the map ({SceneManager.GetActiveScene().name}) and the flag clears (Ending {NextLevel.Ending})");
        Finish();
    }

    private void Finish()
    {
        Line($"END PASS {pass} / FAIL {fail}");
        Destroy(gameObject);
    }
}
