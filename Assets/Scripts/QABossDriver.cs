using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

/// <summary>
/// QA (2026-10-27): scripted boss tests for the Playtest agent, run as QAProbe actions ("boss:&lt;suite&gt;") after its
/// sample, in the boss's own arena. Each check plays the real fight in Play mode and judges what happened from the
/// boss's public state, Harvey's lives and his position; it never edits the fight. Harvey is driven like a player who
/// reacts 0.3 s late at his run speed (4.5 u/s, moved along the floor) and jumps with his real jump; lives are topped
/// up after every counted hit so one check can't end the run. Boss hits in openings are a Spirit Dash's hit
/// (SendMessage TryHit false), the path the game itself takes.
/// Suites: sandfin (SANDFIN 2026-10-28: the Dunes mini-boss's intro, attacks, reset, fight, reward and place in the zone),
///         landshark-attacks (art + every attack against a standing Harvey and against the dodging bot),
///         landshark-fight (all three stages to defeat, Buried Regeneration: complete, interrupt, limit, death mid-heal,
///         no heal after defeat), samurai (five lives to defeat, Mokuso: complete, broken, zanshin counter, death mid-heal).
/// Editor only (QAProbe calls it from its editor-only body).
/// </summary>
public static class QABossDriver
{
#if UNITY_EDITOR
    private const float RunSpeed = 4.5f, Reaction = 0.3f;

    private class Check
    {
        public string name, detail;
        public bool ok;
    }

    private static readonly List<Check> checks = new List<Check>();
    private static PlayerController pc;
    private static Rigidbody2D rb;
    private static int hits;
    private static float floorY, leftX, rightX;
    private static readonly HashSet<string> spritesSeen = new HashSet<string>();

    public static IEnumerator Run(string suite, string[] result)
    {
        checks.Clear();
        spritesSeen.Clear();
        suiteName = suite;
        pc = Object.FindAnyObjectByType<PlayerController>();
        rb = pc != null ? pc.GetComponent<Rigidbody2D>() : null;
        if (pc == null || rb == null)
        {
            result[0] = Json(suite, false, "no PlayerController in the scene");
            yield break;
        }
        switch (suite)
        {
            case "landshark-attacks": yield return LandsharkAttacks(); break;
            case "landshark-fight": yield return LandsharkFight(); break;
            case "samurai": yield return Samurai(); break;
            case "sandfin": yield return Sandfin(); break; // SANDFIN (2026-10-28)
            case "next-level": yield return NextLevelCheck(); break; // SANDFIN: where finishing this level goes
            case "sandfin-diag": yield return SandfinDiag(); break;
            default: Add("suite", false, "unknown suite " + suite); break;
        }
        bool ok = checks.Count > 0 && checks.TrueForAll(c => c.ok);
        int passed = checks.FindAll(c => c.ok).Count;
        result[0] = Json(suite, ok, $"{passed}/{checks.Count} checks passed");
    }

    // ---------------------------------------------------------------- the Landshark: art and attacks

    private static IEnumerator LandsharkAttacks()
    {
        var boss = Object.FindAnyObjectByType<LandsharkBoss>();
        if (boss == null) { Add("boss", false, "no LandsharkBoss in the scene"); yield break; }
        Arena(boss.transform);
        var body = boss.GetComponentInChildren<SpriteRenderer>();
        var attacks = new[] { LandsharkBoss.Attack.Hunt, LandsharkBoss.Attack.Charge, LandsharkBoss.Attack.BiteCombo, LandsharkBoss.Attack.Devour };
        int[] livesFor = { 4, 4, 3, 2 }; // a stage the attack belongs to (breaches/charges/bites are per stage)

        // each attack against Harvey standing still, then against the dodging bot
        for (int a = 0; a < attacks.Length; a++)
        {
            for (int pass = 0; pass < 2; pass++)
            {
                bool dodge = pass == 1;
                Place(dodge ? 1f : 3f);
                boss.QAJump(livesFor[a], new[] { attacks[a] });
                hits = 0;
                bool opened = false, started = false;
                float t0 = Time.time, reactAt = -1f, escapeDir = 0f;
                bool jumped = false;
                while (Time.time - t0 < 16f)
                {
                    Watch(body);
                    if (boss.Doing == attacks[a].ToString()) started = true;
                    if (boss.Vulnerable) { opened = true; break; }
                    if (started && boss.Doing == "" && Time.time - t0 > 2f && !boss.Vulnerable && hits > 0 && !dodge) break; // it hit and moved on
                    if (dodge)
                    {
                        float hx = pc.transform.position.x;
                        switch (attacks[a])
                        {
                            case LandsharkBoss.Attack.Hunt:
                            {
                                GameObject mound = FindClone("MoundTemplate");
                                if (mound != null && reactAt < 0f) { reactAt = Time.time + Reaction; escapeDir = Room(hx, mound.transform.position.x); Shot("mound"); }
                                if (mound == null) reactAt = -1f;
                                if (reactAt > 0f && Time.time >= reactAt && Mathf.Abs(hx - mound.transform.position.x) < 3.2f) Step(escapeDir);
                                break;
                            }
                            case LandsharkBoss.Attack.Charge:
                            {
                                Bounds b = boss.GetComponent<BoxCollider2D>().bounds;
                                bool torpedo = boss.GetComponent<BoxCollider2D>().enabled && b.size.y < 0.8f && boss.transform.position.y < floorY - 1f;
                                float gap = hx < b.center.x ? b.min.x - hx : hx - b.max.x;
                                if (torpedo && gap < 2.2f && gap > -0.5f && pc.GetIsGrounded && !jumped) { Jump(); jumped = true; }
                                if (pc.GetIsGrounded && gap > 2.5f) jumped = false;
                                break;
                            }
                            case LandsharkBoss.Attack.BiteCombo:
                                if (boss.Doing == "BiteCombo" && boss.transform.position.y > floorY - 0.5f)
                                {
                                    if (reactAt < 0f) reactAt = Time.time + Reaction;
                                    if (Time.time >= reactAt && Mathf.Abs(hx - boss.transform.position.x) < 5.5f) Step(Mathf.Sign(hx - boss.transform.position.x + 0.01f));
                                }
                                break;
                            case LandsharkBoss.Attack.Devour:
                                if (boss.Doing == "Devour")
                                {
                                    float mid = (leftX + rightX) * 0.5f;
                                    if (reactAt < 0f) reactAt = Time.time + Reaction;
                                    if (Time.time >= reactAt && Mathf.Abs(hx - mid) < 3.5f) Step(Mathf.Sign(hx - mid + 0.01f));
                                }
                                break;
                        }
                    }
                    CountHits();
                    yield return null;
                }
                string who = dodge ? "dodging bot" : "standing Harvey";
                if (!dodge)
                    Add($"{attacks[a]} hits a standing Harvey", hits > 0, $"{hits} hit(s) on a Harvey standing still at x=3; attack started {started}");
                else
                {
                    Add($"{attacks[a]} can be dodged", hits == 0, $"{hits} hit(s) on the {who} (reacts {Reaction:0.0} s late, runs {RunSpeed} u/s, jumps the torpedo)");
                    Add($"{attacks[a]} miss leaves an opening", opened, opened ? $"on its back {Time.time - t0:0.0} s after the attack began" : "no opening within 16 s");
                }
            }
        }

        // every animation was shown at least once
        var missing = new List<string>();
        foreach (string anim in new[] { "idle", "dive", "swim", "torpedo", "breach", "gulp", "bite", "beached" }) // roar: stage changes, heals, defeat (landshark-fight)
            if (!spritesSeen.Contains(anim)) missing.Add(anim);
        Add("animations render", missing.Count == 0 && body.sprite != null, missing.Count == 0 ? $"seen: {string.Join(" ", spritesSeen)}" : "never shown: " + string.Join(" ", missing));
    }

    // ---------------------------------------------------------------- the Landshark: stages, heal, defeat

    private static IEnumerator LandsharkFight()
    {
        var boss = Object.FindAnyObjectByType<LandsharkBoss>();
        if (boss == null) { Add("boss", false, "no LandsharkBoss in the scene"); yield break; }
        Arena(boss.transform);
        var R = LandsharkBoss.Attack.Regenerate;

        // 1. heal left alone: one life back, the stage stays
        Place(1f);
        boss.QAJump(3, new[] { R });
        float t0 = Time.time, began = -1f;
        while (Time.time - t0 < 14f && !(began > 0f && !boss.Healing)) { if (boss.Healing && began < 0f) began = Time.time; CountHits(); yield return null; }
        float took = began > 0f ? Time.time - began : -1f;
        Add("regen completes", began > 0f && boss.Lives == 4 && boss.StageIndex == 1 && boss.Regen.Healed == 1 && boss.Regen.Attempts == 1 && !boss.Healing,
            $"lives 3 -> {boss.Lives}, stage {boss.StageIndex} (stays 1), healed {boss.Regen.Healed}, attempts {boss.Regen.Attempts}, took {took:0.00} s (set 4.0 s)");
        Add("regen never past max", boss.Lives <= boss.MaxLives, $"lives {boss.Lives} of {boss.MaxLives}");

        // 2. heal interrupted by a dash on the fin: nothing given back, an opening instead
        boss.QAJump(3, new[] { R });
        t0 = Time.time;
        while (Time.time - t0 < 8f && !boss.Healing) yield return null;
        yield return new WaitForSeconds(1f);
        Shot("regen");
        bool wasHealing = boss.Healing;
        boss.SendMessage("TryHit", false);
        float hitAt = Time.time;
        while (Time.time - hitAt < 1.5f && !boss.Vulnerable) yield return null;
        Add("regen interrupted", wasHealing && !boss.Healing && boss.Lives == 3 && boss.Vulnerable && !boss.Regen.Active && boss.Regen.Healed == 0,
            $"healing before the hit {wasHealing}; after: healing {boss.Healing}, lives {boss.Lives} (3), on its back {boss.Vulnerable} after {Time.time - hitAt:0.00} s");

        // 3. the limit: only two tries a fight, the cooldown between them
        boss.QAJump(3, new[] { R, LandsharkBoss.Attack.Hunt });
        t0 = Time.time;
        var starts = new List<float>();
        bool prev = false;
        while (Time.time - t0 < 48f)
        {
            if (boss.Healing && !prev) { starts.Add(Time.time - t0); }
            if (boss.Healing && Time.time - t0 - starts[starts.Count - 1] > 0.5f) boss.SendMessage("TryHit", false); // break every heal
            prev = boss.Healing;
            if (boss.Vulnerable) { /* let the opening run out: no hits here */ }
            CountHits();
            yield return null;
        }
        bool gapOk = starts.Count < 2 || starts[1] - starts[0] >= 18f;
        Add("regen limited", starts.Count == 2 && boss.Regen.Attempts == 2 && gapOk,
            $"heals started at {string.Join(", ", starts.ConvertAll(s => s.ToString("0.0", CultureInfo.InvariantCulture)))} s over 48 s (max 2, cooldown 18 s)");

        // 4. Harvey dies mid-heal: the fight resets, heal and all
        boss.QAJump(3, new[] { R });
        t0 = Time.time;
        while (Time.time - t0 < 8f && !boss.Healing) yield return null;
        yield return new WaitForSeconds(0.8f);
        bool healingAtDeath = boss.Healing;
        HurtPlayer.pLivesCount = 0;
        LevelManager.Instance.Respawn();
        yield return new WaitForSeconds(4f);
        Add("death mid-heal resets", healingAtDeath && !boss.Healing && boss.Lives == boss.MaxLives && boss.StageIndex == 0 && boss.Regen.Attempts == 0 && !boss.Regen.Active,
            $"healing at death {healingAtDeath}; after the respawn: lives {boss.Lives}, stage {boss.StageIndex}, attempts {boss.Regen.Attempts}, healing {boss.Healing}");
        HurtPlayer.pLivesCount = 3;

        // 5. the whole fight: every stage in order, its heals broken, to defeat
        boss.QAJump(boss.MaxLives, null);
        Place(-1f);
        t0 = Time.time;
        var stageSeq = new List<int> { boss.StageIndex };
        var seen = new List<string>();
        float vulnSince = -1f, healSince = -1f;
        int interrupts = 0;
        while (Time.time - t0 < 240f && !boss.Defeated)
        {
            if (boss.StageIndex != stageSeq[stageSeq.Count - 1]) stageSeq.Add(boss.StageIndex);
            string d = boss.StageIndex + ":" + boss.Doing;
            if (boss.Doing != "" && !seen.Contains(d)) seen.Add(d);
            if (boss.Vulnerable) { if (vulnSince < 0f) vulnSince = Time.time; if (Time.time - vulnSince > Reaction) { boss.SendMessage("TryHit", false); vulnSince = -1f; } }
            else vulnSince = -1f;
            if (boss.Healing) { if (healSince < 0f) healSince = Time.time; if (Time.time - healSince > 1.2f) { boss.SendMessage("TryHit", false); interrupts++; healSince = -1f; } }
            else healSince = -1f;
            Watch(boss.GetComponentInChildren<SpriteRenderer>());
            CountHits();
            yield return null;
        }
        float fightS = Time.time - t0;
        yield return new WaitForSeconds(4f);
        var treasure = GameObject.Find("BossTreasure");
        int chests = Object.FindObjectsByType<OpenChest>(FindObjectsSortMode.None).Length;
        bool orderOk = stageSeq.Count == 3 && stageSeq[0] == 0 && stageSeq[1] == 1 && stageSeq[2] == 2;
        Add("three stages in order", orderOk, "stages " + string.Join(" -> ", stageSeq) + "; attacks seen " + string.Join(", ", seen));
        Add("defeat + treasure", boss.Defeated && treasure != null && treasure.activeInHierarchy,
            $"defeated {boss.Defeated} after {fightS:0.0} s (heals broken {interrupts}, {hits} hits taken standing still), treasure shown {(treasure != null && treasure.activeInHierarchy)}, {chests} chests");

        Add("stage change, heal and defeat animations render", spritesSeen.Contains("roar") && spritesSeen.Contains("hurt") && spritesSeen.Contains("beached"),
            "seen in the fight: " + string.Join(" ", spritesSeen));

        // 6. no heal once it is beaten, and a respawn doesn't bring it back
        bool can = boss.Regen.CanStart(2, 1, boss.MaxLives, boss.Defeated);
        LevelManager.Instance.Respawn();
        yield return new WaitForSeconds(4f);
        Add("no heal after defeat", !can && !boss.Healing && !boss.Regen.Active && boss.Defeated,
            $"CanStart after defeat {can}; after a respawn: defeated {boss.Defeated}, healing {boss.Healing}");
    }

    // ---------------------------------------------------------------- Sumaria

    private static IEnumerator Samurai()
    {
        var boss = Object.FindAnyObjectByType<SamuraiBoss>();
        if (boss == null) { Add("boss", false, "no SamuraiBoss in the scene"); yield break; }
        Arena(boss.transform);
        var M = SamuraiBoss.Attack.Meditate;

        // 1. left alone: one life back, the stage stays, then a dash slash
        Place(1f);
        boss.QAJump(4, new[] { M });
        float t0 = Time.time, began = -1f;
        bool sawGuard = false;
        while (Time.time - t0 < 12f && !(began > 0f && !boss.Meditating))
        {
            if (boss.Meditating && began < 0f) began = Time.time;
            if (boss.Guarded) sawGuard = true;
            CountHits();
            yield return null;
        }
        float took = began > 0f ? Time.time - began : -1f;
        int livesAfter = boss.Lives;
        float t1 = Time.time;
        bool dashed = false;
        while (Time.time - t1 < 4f) { if (boss.Charging || boss.Dashing) dashed = true; CountHits(); yield return null; }
        Add("meditation completes", began > 0f && livesAfter == 5 && boss.StageIndex == 1 && boss.Regen.Healed == 1 && sawGuard,
            $"lives 4 -> {livesAfter}, stage {boss.StageIndex} (stays 1), took {took:0.00} s (set 3.4 s), eyes opened (zanshin) {sawGuard}");
        Add("dash slash follows", dashed, dashed ? "charged straight into a dash slash" : "no dash slash within 4 s");

        // 2. struck with his eyes closed: focus broken, the hit counts
        boss.QAJump(4, new[] { M });
        t0 = Time.time;
        while (Time.time - t0 < 8f && !boss.Meditating) yield return null;
        yield return new WaitForSeconds(0.6f);
        Shot("meditate");
        bool still = boss.Meditating && !boss.Guarded;
        boss.SendMessage("TryHit", false);
        yield return null;
        Add("meditation broken", still && !boss.Meditating && boss.Lives == 3 && boss.Regen.Healed == 0,
            $"eyes closed at the hit {still}; after: meditating {boss.Meditating}, lives {boss.Lives} (3), healed {boss.Regen.Healed}");
        yield return new WaitForSeconds(3f);

        // 3. struck in zanshin: countered (Harvey hurt), he keeps meditating
        boss.QAJump(4, new[] { M });
        t0 = Time.time;
        while (Time.time - t0 < 10f && !boss.Guarded) yield return null;
        yield return null;
        Shot("zanshin");
        int harveyBefore = HurtPlayer.pLivesCount;
        boss.SendMessage("TryHit", false);
        yield return null;
        bool countered = HurtPlayer.pLivesCount < harveyBefore;
        bool kept = boss.Meditating && boss.Lives == 4;
        HurtPlayer.pLivesCount = 3;
        t1 = Time.time;
        while (Time.time - t1 < 4f && boss.Meditating) yield return null;
        Add("zanshin counters", countered && kept && boss.Lives == 5,
            $"Harvey {harveyBefore} -> {(countered ? harveyBefore - 1 : harveyBefore)} lives; still meditating {kept}; finished with {boss.Lives} lives");

        // 4. Harvey dies mid-meditation: the fight resets
        boss.QAJump(4, new[] { M });
        t0 = Time.time;
        while (Time.time - t0 < 8f && !boss.Meditating) yield return null;
        bool medAtDeath = boss.Meditating;
        HurtPlayer.pLivesCount = 0;
        LevelManager.Instance.Respawn();
        yield return new WaitForSeconds(4f);
        Add("death mid-meditation resets", medAtDeath && !boss.Meditating && boss.Lives == 5 && boss.StageIndex == 0 && boss.Regen.Attempts == 0,
            $"meditating at death {medAtDeath}; after: lives {boss.Lives}, stage {boss.StageIndex}, attempts {boss.Regen.Attempts}");
        HurtPlayer.pLivesCount = 3;

        // 5. the whole fight: five lives over three stages, meditations broken while his eyes are shut
        boss.QAJump(5, null);
        Place(1f);
        t0 = Time.time;
        var stageSeq = new List<int> { boss.StageIndex };
        float vulnSince = -1f;
        int broken = 0;
        while (Time.time - t0 < 240f && !boss.Defeated)
        {
            if (boss.StageIndex != stageSeq[stageSeq.Count - 1]) stageSeq.Add(boss.StageIndex);
            if (boss.Vulnerable) { if (vulnSince < 0f) vulnSince = Time.time; if (Time.time - vulnSince > Reaction) { boss.SendMessage("TryHit", false); vulnSince = -1f; } }
            else vulnSince = -1f;
            if (boss.Meditating && !boss.Guarded && boss.Regen.Progress > 0.2f) { boss.SendMessage("TryHit", false); broken++; }
            // stay clear of his dashes: jump the blade line when he dashes
            if (boss.Dashing && pc.GetIsGrounded) Jump();
            CountHits();
            yield return null;
        }
        float fightS = Time.time - t0;
        yield return new WaitForSeconds(4f);
        var treasure = GameObject.Find("BossTreasure");
        bool orderOk = stageSeq.Count == 3 && stageSeq[0] == 0 && stageSeq[1] == 1 && stageSeq[2] == 2;
        Add("three stages over five lives", orderOk, "stages " + string.Join(" -> ", stageSeq));
        Add("defeat + treasure", boss.Defeated && treasure != null && treasure.activeInHierarchy,
            $"defeated {boss.Defeated} after {fightS:0.0} s (meditations broken {broken}, {hits} hits taken), treasure shown {(treasure != null && treasure.activeInHierarchy)}");
        bool can = boss.Regen.CanStart(2, 1, boss.MaxLives, boss.Defeated);
        Add("no meditation after defeat", !can && !boss.Meditating, $"CanStart after defeat {can}");
    }

    // ---------------------------------------------------------------- SANDFIN (2026-10-28): the Dunes mini-boss

    private static IEnumerator Sandfin()
    {
        var boss = Object.FindAnyObjectByType<SandfinMiniBoss>();
        if (boss == null) { Add("boss", false, "no SandfinMiniBoss in the scene"); yield break; }
        Arena(boss.transform);
        var body = boss.GetComponentInChildren<SpriteRenderer>();
        var pips = GameObject.Find("SandfinPips");

        // 1. the intro: the fin crosses, it breaches at the far wall, the pips come up; it never hurts Harvey
        Place(-2.5f);
        bool pipsBefore = pips != null && pips.GetComponentsInChildren<SpriteRenderer>().Length > 0 && pips.GetComponentsInChildren<SpriteRenderer>()[0].enabled;
        float t0 = Time.time;
        while (Time.time - t0 < 12f && !boss.IntroDone) { Watch(body); CountHits(); yield return null; }
        int lit = 0;
        if (pips != null) foreach (SpriteRenderer p in pips.GetComponentsInChildren<SpriteRenderer>()) if (p.enabled) lit++;
        Add("intro plays and starts the fight", boss.IntroDone && !pipsBefore && lit == 3 && hits == 0,
            $"intro done after {Time.time - t0:0.0} s; pips hidden before {!pipsBefore}, lit after {lit}/3; {hits} hits on Harvey during it");

        // 2. each attack against Harvey standing still, then against the dodging bot
        var attacks = new[] { SandfinMiniBoss.Attack.Leap, SandfinMiniBoss.Attack.Lunge, SandfinMiniBoss.Attack.LungeLeap };
        int[] livesFor = { 3, 3, 1 };
        for (int a = 0; a < attacks.Length; a++)
        {
            for (int pass = 0; pass < 2; pass++)
            {
                bool dodge = pass == 1;
                Place(3f);
                boss.QAJump(livesFor[a], new[] { attacks[a] });
                hits = 0;
                bool opened = false, started = false;
                dodgeAt = -1f;
                t0 = Time.time;
                while (Time.time - t0 < 16f)
                {
                    Watch(body);
                    if (boss.Doing == attacks[a].ToString()) started = true;
                    if (boss.Vulnerable) { opened = true; break; }
                    if (started && boss.Doing == "" && hits > 0 && !dodge) break;
                    if (dodge) SandfinDodge(boss);
                    CountHits();
                    yield return null;
                }
                if (!dodge)
                    Add($"{attacks[a]} hits a standing Harvey", hits > 0, $"{hits} hit(s) on a Harvey standing still at x=3; attack started {started}; touches {boss.Touches}, damage tries {boss.DamageTries}");
                else
                {
                    Add($"{attacks[a]} can be dodged", hits == 0, $"{hits} hit(s) on the dodging bot (reacts {Reaction:0.0} s late, runs {RunSpeed} u/s)");
                    Add($"{attacks[a]} miss leaves an opening", opened, opened ? $"belly-up {Time.time - t0:0.0} s after the attack began" : "no opening within 16 s");
                }
            }
        }
        var missing = new List<string>();
        foreach (string anim in new[] { "swim", "attack" }) if (!spritesSeen.Contains(anim)) missing.Add(anim);
        Add("animations render", missing.Count == 0, missing.Count == 0 ? "seen: " + string.Join(" ", spritesSeen) : "never shown: " + string.Join(" ", missing));

        // 3. Harvey dies mid-fight: it resets to three lives, stage 1
        boss.QAJump(1, new[] { SandfinMiniBoss.Attack.Leap });
        yield return new WaitForSeconds(1f);
        HurtPlayer.pLivesCount = 0;
        LevelManager.Instance.Respawn();
        yield return new WaitForSeconds(4f);
        Add("death resets the fight", boss.Lives == 3 && boss.StageIndex == 0 && !boss.Defeated, $"after the respawn: lives {boss.Lives}, stage {boss.StageIndex}");
        HurtPlayer.pLivesCount = 3;

        // 4. the whole fight against the dodging bot (a Harvey who stands still is hit every time, so it never misses):
        // both stages, to defeat, the reward and the exit
        boss.QAJump(3, null);
        Place(-1f);
        hits = 0;
        dodgeAt = -1f;
        t0 = Time.time;
        var stageSeq = new List<int> { boss.StageIndex };
        var seen = new List<string>();
        float vulnSince = -1f;
        while (Time.time - t0 < 150f && !boss.Defeated)
        {
            if (boss.StageIndex != stageSeq[stageSeq.Count - 1]) stageSeq.Add(boss.StageIndex);
            string d = boss.StageIndex + ":" + boss.Doing;
            if (boss.Doing != "" && !seen.Contains(d)) seen.Add(d);
            if (boss.Vulnerable) { if (vulnSince < 0f) { vulnSince = Time.time; Shot("sandfin-opening"); } if (Time.time - vulnSince > Reaction) { boss.SendMessage("TryHit", false); vulnSince = -1f; } }
            else vulnSince = -1f;
            SandfinDodge(boss);
            CountHits();
            yield return null;
        }
        float fightS = Time.time - t0;
        int fightHits = hits;
        yield return new WaitForSeconds(4f);
        Shot("sandfin-defeat");
        yield return null; // the shot is taken at the end of the frame: nothing else may change this one (the respawn below)
        var treasure = GameObject.Find("BossTreasure");
        int chests = Object.FindObjectsByType<OpenChest>(FindObjectsSortMode.None).Length;
        bool talisman = treasure != null && treasure.GetComponent<TalismanUnlock>() != null;
        int here = UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex;
        string next = GameSession.SceneNameAt(here + 1), before = GameSession.SceneNameAt(here - 1);
        Add("two stages in order", stageSeq.Count == 2 && stageSeq[0] == 0 && stageSeq[1] == 1, "stages " + string.Join(" -> ", stageSeq) + "; attacks seen " + string.Join(", ", seen));
        Add("defeat, treasure and reward", boss.Defeated && treasure != null && treasure.activeInHierarchy && !talisman && chests >= 1 && chests <= 2,
            $"defeated {boss.Defeated} after {fightS:0.0} s ({fightHits} hits on the dodging bot); treasure shown {(treasure != null && treasure.activeInHierarchy)}; talisman on it {talisman}; {chests} chest(s)");
        Add("sits between Act 3 and Act 4", before == "Z3A3_Egypt" && next == "Z3A4_Egypt",
            $"build index {here}: after {before}, before {next}");
        LevelManager.Instance.Respawn();
        yield return new WaitForSeconds(4f);
        Add("stays beaten after a respawn", boss.Defeated, $"defeated {boss.Defeated}");
    }

    // SANDFIN: a player who reacts Reaction late at his run speed: off the sand mound (away from it, toward the roomier
    // side when it is under him), away from a surfaced lunge
    private static float dodgeAt = -1f, dodgeDir;

    private static void SandfinDodge(SandfinMiniBoss boss)
    {
        float hx = pc.transform.position.x;
        GameObject mound = FindClone("MoundTemplate");
        if (mound != null)
        {
            if (dodgeAt < 0f) { dodgeAt = Time.time + Reaction; dodgeDir = Room(hx, mound.transform.position.x); Shot("sandfin-mound"); }
            if (Time.time >= dodgeAt && Mathf.Abs(hx - mound.transform.position.x) < 3.2f) Step(dodgeDir);
        }
        else if (boss.Doing.StartsWith("Lunge") && boss.transform.position.y > floorY)
        {
            if (dodgeAt < 0f) dodgeAt = Time.time + Reaction;
            if (Time.time >= dodgeAt && Mathf.Abs(hx - boss.transform.position.x) < 6f) Step(Mathf.Sign(hx - boss.transform.position.x + 0.01f));
        }
        else dodgeAt = -1f;
    }

    // SANDFIN: per-frame contact numbers for one lunge at a Harvey standing still (hurtbox on, bounds overlap, what a
    // physics query finds there, whether Harvey's body is awake)
    private static IEnumerator SandfinDiag()
    {
        var boss = Object.FindAnyObjectByType<SandfinMiniBoss>();
        Arena(boss.transform);
        var box = boss.GetComponent<BoxCollider2D>();
        Collider2D harvey = null;
        foreach (Collider2D c in pc.GetComponentsInChildren<Collider2D>()) if (c.name == "PlayerBodyDetectionArea") harvey = c;
        Place(3f);
        boss.QAJump(3, new[] { SandfinMiniBoss.Attack.Lunge });
        int on = 0, overlap = 0, query = 0, awake = 0, frames = 0, queryAny = 0;
        string sample = "";
        float t0 = Time.time;
        while (Time.time - t0 < 9f && !boss.Vulnerable)
        {
            frames++;
            if (rb.IsAwake()) awake++;
            if (box.enabled)
            {
                on++;
                if (box.bounds.Intersects(harvey.bounds)) overlap++;
                var found = Physics2D.OverlapBoxAll(box.bounds.center, box.bounds.size, 0f);
                if (found.Length > 0) queryAny++;
                foreach (var f in found) if (f == harvey) { query++; break; }
                if (on % 40 == 1)
                    sample += $"box {box.bounds.min}-{box.bounds.max} harvey {harvey.bounds.min}-{harvey.bounds.max} found [{string.Join(",", System.Array.ConvertAll(found, f => f.name))}] bossRb {boss.GetComponent<Rigidbody2D>().bodyType} awake {boss.GetComponent<Rigidbody2D>().IsAwake()} simulated {boss.GetComponent<Rigidbody2D>().simulated} doing {boss.Doing} bossPos {boss.transform.position} offset {box.offset} size {box.size} | ";
            }
            CountHits();
            yield return null;
        }
        Add("diag", true, $"frames {frames}, harvey awake {awake}, box on {on}, overlap {overlap}, query finds harvey {query}, query finds anything {queryAny}, touches {boss.Touches}, hits {hits}; {sample}");
    }

    // SANDFIN: the scene GameSession.FinishLevel opens next (it saves and loads build index + 1), and the one before
    private static IEnumerator NextLevelCheck()
    {
        yield return null;
        var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        string next = GameSession.SceneNameAt(active.buildIndex + 1), before = GameSession.SceneNameAt(active.buildIndex - 1);
        Add("finishing leads to", next != null, $"{active.name} (build index {active.buildIndex}): finishing opens {next}; the level before is {before}");
    }

    // ---------------------------------------------------------------- Harvey

    private static void Arena(Transform boss)
    {
        floorY = boss.position.y;
        leftX = -4.2f;
        rightX = 10.2f;
        foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
        {
            if (t.name == "LeftLimit")
            {
                leftX = t.position.x;
                floorY = t.position.y; // SANDFIN: the limits sit on the floor; a boss's own transform may not (Sandfin's is its middle, under the sand)
            }
            if (t.name == "RightLimit") rightX = t.position.x;
        }
        HurtPlayer.pLivesCount = 3;
    }

    private static void Place(float x)
    {
        rb.position = new Vector2(x, floorY + 0.6f);
        rb.linearVelocity = Vector2.zero;
        HurtPlayer.pLivesCount = 3;
    }

    // along the floor at his run speed, never past the arena's walls
    private static void Step(float dir)
    {
        Vector2 p = rb.position;
        rb.position = new Vector2(Mathf.Clamp(p.x + dir * RunSpeed * Time.deltaTime, leftX + 0.3f, rightX - 0.3f), p.y);
    }

    private static void Jump() => rb.linearVelocity = new Vector2(rb.linearVelocity.x, pc.GetJSpeedDefault);

    // which way has more room, away from x
    private static float Room(float hx, float x)
    {
        float dir = Mathf.Sign(hx - x + 0.01f);
        if (dir > 0 && rightX - hx < 3f) dir = -1f;
        if (dir < 0 && hx - leftX < 3f) dir = 1f;
        return dir;
    }

    private static void CountHits()
    {
        if (HurtPlayer.pLivesCount < 3)
        {
            hits += 3 - HurtPlayer.pLivesCount;
            HurtPlayer.pLivesCount = 3;
        }
    }

    private static void Watch(SpriteRenderer body)
    {
        if (body == null || body.sprite == null) return;
        string n = body.sprite.name; // Landshark_<anim>_<i>
        int a = n.IndexOf('_'), b = n.LastIndexOf('_');
        if (a > 0 && b > a && spritesSeen.Add(n.Substring(a + 1, b - a - 1))) Shot(n.Substring(a + 1, b - a - 1));
    }

    // a screenshot of the Game view, the first time something shows (Logs/QA/shots/<suite>-<name>.png)
    private static string suiteName = "", shotDir;

    private static void Shot(string name)
    {
        if (shotDir == null)
        {
            shotDir = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../Logs/QA/shots"));
            System.IO.Directory.CreateDirectory(shotDir); // before the first capture: a failed save is an error
        }
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(shotDir, suiteName + "-" + name + ".png"));
    }

    private static GameObject FindClone(string prefix)
    {
        foreach (SpriteRenderer sr in Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
            if (sr.gameObject.activeInHierarchy && sr.name.StartsWith(prefix) && sr.name.EndsWith("(Clone)")) return sr.gameObject;
        return null;
    }

    // ---------------------------------------------------------------- the result

    private static void Add(string name, bool ok, string detail)
    {
        checks.Add(new Check { name = name, ok = ok, detail = detail });
        Debug.Log($"[QABoss] {(ok ? "PASS" : "FAIL")} {name}: {detail}");
    }

    private static string Json(string suite, bool ok, string detail)
    {
        var sb = new StringBuilder();
        sb.Append("{\"name\":").Append(Str("boss:" + suite)).Append(",\"ok\":").Append(ok ? "true" : "false")
          .Append(",\"detail\":").Append(Str(detail)).Append(",\"checks\":[");
        for (int i = 0; i < checks.Count; i++)
            sb.Append(i > 0 ? "," : "").Append("{\"name\":").Append(Str(checks[i].name)).Append(",\"ok\":").Append(checks[i].ok ? "true" : "false")
              .Append(",\"detail\":").Append(Str(checks[i].detail)).Append('}');
        return sb.Append("]}").ToString();
    }

    private static string Str(string s)
    {
        var sb = new StringBuilder("\"");
        foreach (char c in s ?? "")
        {
            if (c == '"' || c == '\\') sb.Append('\\').Append(c);
            else if (c < ' ') sb.Append("\\u").Append(((int)c).ToString("x4"));
            else sb.Append(c);
        }
        return sb.Append('"').ToString();
    }
#endif
}
