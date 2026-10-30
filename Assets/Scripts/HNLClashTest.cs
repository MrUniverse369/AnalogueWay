#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// TEST (2026-10-25): self-driving play-mode checks for the dash clash (DASHCLASH) and the Spirit Blast (SPIRITBLAST).
/// Editor only, safe to delete. Added to a new object in Play mode (from an MCP RunCommand) with Mode set:
///   "clash"  Z4A1_Japan: drives Harvey into a patrolling samurai three times with a Spirit Dash timed to meet its dash
///            (expects the battle card, no life lost, no knockback, no stun), then a dash into its wind-up (expects
///            the ordinary block: stun) and a slash taken standing still (expects a life lost)
///   "beam"   Z1Boss_GrassGreen: makes the fox fire its beam at Harvey from both sides and screenshots the charge, the shot
///            and the impact; checks the hazards and their dressing are gone afterwards
/// Results go to the Console as "[CLASHTEST] ..." lines and to Log (a text file next to the screenshots).
/// </summary>
public class HNLClashTest : MonoBehaviour
{
    public string Mode = "clash";
    public string OutDir = "/tmp";
    public float DashDelay = 0.24f; // seconds after the samurai's attack starts (wind-up 0.28) that Harvey dashes
    public float TargetX = float.NaN; // clash: prefer the samurai nearest this x (NaN: any order)
    public int Rounds = 5;            // clash: head-on clashes before the two controls
    public int ReleaseRound = -1;     // clash: in this round Harvey lets go of the stick at the clash (others keep pushing)

    private readonly List<string> log = new List<string>();
    private int shots;

    private void Start()
    {
        Directory.CreateDirectory(OutDir);
        HNLTrailerTake.UseMemorySaves();
        StartCoroutine(Mode == "beam" ? Beam() : Mode == "boss" ? Boss() : Clash());
    }

    private void Note(string s)
    {
        string line = Time.time.ToString("0.00") + " " + s;
        log.Add(line);
        Debug.Log("[CLASHTEST] " + line);
        File.WriteAllLines(Path.Combine(OutDir, "log.txt"), log);
    }

    private void Shot(string name)
    {
        string path = Path.Combine(OutDir, $"{shots++:00}_{name}.png");
        ScreenCapture.CaptureScreenshot(path);
        Note("shot " + path);
    }

    private static PlayerController Player => FindFirstObjectByType<PlayerController>();

    private static void Place(PlayerController pc, float x, float y)
    {
        var rb = pc.GetComponent<Rigidbody2D>();
        pc.transform.position = new Vector3(x, y, pc.transform.position.z);
        rb.position = new Vector2(x, y);
        rb.linearVelocity = Vector2.zero;
        PlayerController.chi = 90f;
        HurtPlayer.pLivesCount = GameSession.StartingLives;
        CameraController cc = FindFirstObjectByType<CameraController>();
        if (cc != null && cc.isActiveAndEnabled) cc.SnapToPlayer();
    }

    private static Talisman DashTalisman()
    {
        foreach (Talisman tal in Inventory.Catalog.talismans)
            if (tal != null && (tal.Traits & TalismanTrait.DefeatsEnemiesOnContact) != 0) return tal;
        return null;
    }

    private static float Facing(SamuraiSlash s) => Mathf.Sign(s.transform.lossyScale.x);

    // ---------------------------------------------------------------- the samurai
    private IEnumerator Clash()
    {
        float until = Time.time + 8f;
        PlayerController pc = null;
        SamuraiSlash samurai = null;
        while (Time.time < until && (pc == null || samurai == null))
        {
            pc = Player;
            foreach (SamuraiSlash s in FindObjectsByType<SamuraiSlash>(FindObjectsSortMode.None))
                if (s.isActiveAndEnabled) { samurai = s; break; }
            if (samurai == null && Time.time > until - 6f && !AnalogueWay.changeWorld.lWorld) { AnalogueWay.changeWorld.lWorld = true; Note("switched to the living world"); }
            yield return null;
        }
        if (pc == null || samurai == null) { Note("FAIL: no player or samurai"); Finish(); yield break; }
        Talisman dash = DashTalisman();
        Inventory.Unlock(dash);
        PowerUpManager.Instance.Equip(0, dash);
        Note($"samurai {samurai.transform.parent?.name}/{samurai.name} at {samurai.transform.position:0.0}, dash {dash}");

        float floorDelta = pc.transform.position.y - pc.FeetPosition.y; // transform above the feet
        var all = new List<SamuraiSlash>();
        foreach (SamuraiSlash s in FindObjectsByType<SamuraiSlash>(FindObjectsSortMode.None)) if (s.isActiveAndEnabled) all.Add(s);
        if (!float.IsNaN(TargetX)) all.Sort((a, b) => Mathf.Abs(a.transform.position.x - TargetX).CompareTo(Mathf.Abs(b.transform.position.x - TargetX)));
        Note($"{all.Count} samurai in the level");
        for (int round = 0; round < Rounds; round++)
        {
            if (round < all.Count) samurai = all[round]; // a fresh one each round
            if (!samurai.isActiveAndEnabled) { Note($"round {round}: no samurai left"); break; }
            yield return Approach(pc, samurai, floorDelta);
            if (!samurai.IsAttacking) { Note($"round {round}: FAIL the samurai never attacked"); continue; }
            float t0 = Time.time;
            HurtPlayer.pLivesCount = GameSession.StartingLives; // whatever the walk in cost
            int lives = HurtPlayer.pLivesCount;
            yield return new WaitForSeconds(DashDelay);
            PowerUpManager.Instance.SendMessage("TryActivate", (object)0);
            PlayerController.BotX = Facing(samurai) * -1f; // at its face
            bool card = false, knock = false, stun = false, dashSeen = false, died = false, protectionEnded = false;
            float cardAt = -1f;
            EnemyController ec = samurai.GetComponent<EnemyController>();
            Collider2D monkeyBox = samurai.GetComponent<Collider2D>();
            Collider2D stompBox = null;
            foreach (Collider2D c in pc.GetComponentsInChildren<Collider2D>(true)) if (c.CompareTag("pStomp")) stompBox = c;
            for (float t = 0f; t < 2.6f; t += Time.deltaTime)
            {
                if (PowerUpManager.Has(TalismanTrait.DefeatsEnemiesOnContact)) dashSeen = true;
                if (ClashCard.Showing && !card)
                {
                    card = true; cardAt = Time.time - t0;
                    Note($"round {round}: CLASH harvey={pc.transform.position:0.00} monkey={samurai.transform.position:0.00} monkeyBox={monkeyBox.bounds:0.00} facing={Facing(samurai)}");
                    if (round == ReleaseRound) { PlayerController.BotX = 0f; Note($"round {round}: stick released"); }
                }
                if (card && !protectionEnded && !SamuraiSlash.ClashProtected)
                {
                    protectionEnded = true;
                    Note($"round {round}: PROTECTION ENDED at +{Time.time - t0:0.00} (card at {cardAt:0.00}, so {Time.time - t0 - cardAt:0.00} s of grace) monkey={samurai.transform.position:0.00} harvey={pc.transform.position:0.00} timeScale={Time.timeScale}");
                }
                if (HurtPlayer.pLivesCount < lives)
                {
                    Note($"round {round}: LIFE LOST at +{Time.time - t0:0.00} harvey={pc.transform.position:0.00} v={pc.GetComponent<Rigidbody2D>().linearVelocity:0.0} monkey={samurai.transform.position:0.00} attacking={samurai.IsAttacking} protected={SamuraiSlash.ClashProtected} push={pc.GetComponentInChildren<playerBodyDetectionArea>().pushBackCounter:0.00} state={(samurai.GetComponent<Animator>().GetCurrentAnimatorStateInfo(0).IsName("Block") ? "Block" : samurai.GetComponent<Animator>().GetCurrentAnimatorStateInfo(0).IsName("Slash") ? "Slash" : "Walk?")}");
                    lives = HurtPlayer.pLivesCount;
                }
                if (ec != null && ec.IsDying && !died)
                {
                    died = true;
                    Note($"round {round}: SAMURAI DYING at +{Time.time - t0:0.00} (card at {cardAt:0.00}) dashTrait={PowerUpManager.Has(TalismanTrait.DefeatsEnemiesOnContact)} " +
                         $"harvey={pc.transform.position:0.00} v={pc.GetComponent<Rigidbody2D>().linearVelocity:0.0} grounded? stompBox={(stompBox != null ? stompBox.bounds.ToString("0.00") : "none")} " +
                         $"monkeyBox={(monkeyBox != null ? monkeyBox.bounds.ToString("0.00") : "none")} landsOnTop={StompEnemy.LandsOnTop(stompBox, monkeyBox)} protected={SamuraiSlash.ClashProtected} invincible={PlayerController.playerInviInvincible}");
                }
                if (card && !knock && Time.time - t0 < cardAt + 0.4f && pc.GetComponentInChildren<playerBodyDetectionArea>().pushBackCounter > 0f) knock = true;
                if (pc.IsStunned) stun = true;
                if (card && shots < 3 && Time.unscaledTime > 0f && Time.time - t0 > cardAt + 0.02f * (shots + 1)) Shot($"clash{round}");
                yield return null;
            }
            PlayerController.BotX = 0f;
            int livesAtStart = GameSession.StartingLives;
            Note($"round {round}: dash={dashSeen} card={card} at {cardAt:0.00} lives {livesAtStart}->{HurtPlayer.pLivesCount} knockback={knock} stun={stun} died={died} samuraiAttacking={samurai.IsAttacking} monkeyActive={samurai.isActiveAndEnabled} " +
                 $"rootActive={samurai.transform.root.gameObject.activeSelf} lWorld={AnalogueWay.changeWorld.lWorld} harvey={pc.transform.position:0.0}");
            string verdict = card && HurtPlayer.pLivesCount == livesAtStart && !knock && !stun && !died ? "PASS" : "FAIL";
            Note($"round {round}: {verdict}");
            yield return new WaitForSeconds(1.5f);
            if (!samurai.isActiveAndEnabled) { Note(died ? "samurai defeated; stopping" : "samurai switched off by the level (not a defeat); stopping"); break; }
        }

        // control 1: a dash into the wind-up (not its dash) blocks as before: Harvey stunned, no card, no life lost
        if (samurai.isActiveAndEnabled)
        {
            yield return Approach(pc, samurai, floorDelta);
            HurtPlayer.pLivesCount = GameSession.StartingLives;
            int lives = HurtPlayer.pLivesCount;
            PowerUpManager.Instance.SendMessage("TryActivate", (object)0);
            PlayerController.BotX = Facing(samurai) * -1f;
            bool card = false, stun = false;
            for (float t = 0f; t < 1.2f; t += Time.deltaTime)
            {
                if (ClashCard.Showing) card = true;
                if (pc.IsStunned) stun = true;
                yield return null;
            }
            PlayerController.BotX = 0f;
            Note($"control block: card={card} stun={stun} lives {lives}->{HurtPlayer.pLivesCount} -> {(!card && stun && lives == HurtPlayer.pLivesCount ? "PASS" : "FAIL")}");
            yield return new WaitForSeconds(1.5f);
        }

        // control 2: standing in the slash loses a life as before
        if (samurai.isActiveAndEnabled)
        {
            yield return Approach(pc, samurai, floorDelta);
            HurtPlayer.pLivesCount = GameSession.StartingLives;
            int lives = HurtPlayer.pLivesCount;
            bool card = false;
            for (float t = 0f; t < 1.5f; t += Time.deltaTime)
            {
                if (ClashCard.Showing) card = true;
                yield return null;
            }
            Note($"control hit: card={card} lives {lives}->{HurtPlayer.pLivesCount} -> {(!card && HurtPlayer.pLivesCount == lives - 1 ? "PASS" : "FAIL")}");
        }
        Note($"timeScale={Time.timeScale} invincible={PlayerController.playerInviInvincible}");
        Finish();
    }

    // Harvey outside the trigger range in front of the monkey, then walks in until it attacks (or gives up)
    private IEnumerator Approach(PlayerController pc, SamuraiSlash samurai, float floorDelta)
    {
        PlayerController.BotX = 0f;
        for (int tries = 0; tries < 2; tries++)
        {
            float face = Facing(samurai);
            var sr = samurai.GetComponent<SpriteRenderer>();
            float floor = sr.bounds.min.y;
            Place(pc, sr.bounds.center.x + face * 4.6f, floor + floorDelta + 0.05f);
            // face the monkey
            Vector3 sc = pc.transform.localScale;
            sc.x = Mathf.Abs(sc.x) * -face;
            pc.transform.localScale = sc;
            yield return new WaitForSeconds(0.6f);
            float until = Time.time + 6f;
            while (Time.time < until && !samurai.IsAttacking)
            {
                float dx = (sr.bounds.center.x - pc.transform.position.x) * Mathf.Sign(sr.bounds.center.x - pc.transform.position.x);
                PlayerController.BotX = dx > 2.9f ? Mathf.Sign(sr.bounds.center.x - pc.transform.position.x) : 0f;
                yield return null;
            }
            PlayerController.BotX = 0f;
            if (samurai.IsAttacking) yield break;
            Note("the samurai did not attack; trying its other side");
        }
    }

    // ---------------------------------------------------------------- the fox
    private IEnumerator Beam()
    {
        float until = Time.time + 8f;
        PlayerController pc = null;
        YokaiBoss boss = null;
        while (Time.time < until && (pc == null || boss == null))
        {
            pc = Player;
            boss = FindFirstObjectByType<YokaiBoss>();
            yield return null;
        }
        if (pc == null || boss == null) { Note("FAIL: no player or boss"); Finish(); yield break; }
        Animator anim = boss.GetComponentInChildren<Animator>();
        const BindingFlags Any = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        FieldInfo stageField = typeof(YokaiBoss).GetField("stage", Any);
        MethodInfo fight = typeof(YokaiBoss).GetMethod("Fight", Any);
        // the third stage opens with the beam; one beam per pass, so the counts below are one beam's
        var stages = (System.Array)typeof(YokaiBoss).GetField("stages", Any).GetValue(boss);
        object stage2 = stages.GetValue(2);
        stage2.GetType().GetField("beams").SetValue(stage2, 1);
        float floorDelta = pc.transform.position.y - pc.FeetPosition.y;
        float floor = boss.transform.position.y; // the boss stands on the arena floor
        Note($"boss {boss.name} at {boss.transform.position:0.0}");
        yield return new WaitForSeconds(0.5f);

        Transform leftLimit = (Transform)typeof(YokaiBoss).GetField("leftLimit", Any).GetValue(boss);
        Transform rightLimit = (Transform)typeof(YokaiBoss).GetField("rightLimit", Any).GetValue(boss);
        Note($"arena {leftLimit.position.x:0.0} .. {rightLimit.position.x:0.0}");
        for (int side = 0; side < 2; side++)
        {
            // side 0: the boss by the left wall firing right; side 1: the boss moved to the right wall, firing left
            float dir = side == 0 ? 1f : -1f;
            boss.StopAllCoroutines();
            yield return new WaitForSeconds(0.8f); // the last beam dies
            float bossX = side == 0 ? leftLimit.position.x + 2.5f : rightLimit.position.x - 2.5f;
            boss.transform.position = new Vector3(bossX, floor, boss.transform.position.z);
            Place(pc, bossX + dir * 5.5f, floor + floorDelta + 0.05f); // standing in the beam's path
            PlayerController.BotX = 0f;
            yield return new WaitForSeconds(0.3f);
            Note($"side {side}: boss at {boss.transform.position:0.00}, harvey at {pc.transform.position:0.00}, feet {pc.FeetPosition.y:0.00}, floor {floor:0.00}");
            stageField.SetValue(boss, 2);
            boss.StartCoroutine((IEnumerator)fight.Invoke(boss, new object[] { 0.2f }));
            int lives = HurtPlayer.pLivesCount;
            bool charged = false, fired = false, impacted = false;
            float chargeAt = -1f, fireAt = -1f, hitAt = -1f;
            int maxFx = 0;
            float t0 = Time.time;
            while (Time.time - t0 < 6f)
            {
                var st = anim.GetCurrentAnimatorStateInfo(0);
                if (st.IsName("BeamCharge") && !charged) { charged = true; chargeAt = Time.time; }
                if (charged && st.IsName("BeamFire") && !fired) { fired = true; fireAt = Time.time; }
                if (fired && !impacted && HurtPlayer.pLivesCount < lives) { impacted = true; hitAt = Time.time - fireAt; }
                int fx = FindObjectsByType<SpiritBlastFX>(FindObjectsSortMode.None).Length;
                maxFx = Mathf.Max(maxFx, fx);
                if (fx > 0 && !dumped[side])
                {
                    dumped[side] = true;
                    SpiritBlastFX b = FindFirstObjectByType<SpiritBlastFX>();
                    Note($"side {side}: beam at {b.transform.position:0.00} scale {b.transform.localScale} life {b.GetComponent<BossHazard>().LifeLeft:0.00}/{b.GetComponent<BossHazard>().Lifetime:0.00}");
                    foreach (SpriteRenderer sr in b.GetComponentsInChildren<SpriteRenderer>())
                        Note($"   {sr.name}: enabled={sr.enabled} visible={sr.isVisible} sprite={(sr.sprite != null ? sr.sprite.name : "none")} color={sr.color} mat={(sr.sharedMaterial != null ? sr.sharedMaterial.shader.name : "none")} layer={sr.sortingLayerName}/{sr.sortingOrder} bounds={sr.bounds:0.00} drawMode={sr.drawMode} size={sr.size}");
                }
                if (charged && !fired && Time.time - chargeAt > 0.25f && !shotTaken[side, 0]) { shotTaken[side, 0] = true; Shot($"charge_side{side}"); }
                if (charged && !fired && Time.time - chargeAt > 0.62f && !shotTaken[side, 3]) { shotTaken[side, 3] = true; Shot($"orb_side{side}"); }
                if (fired && Time.time - fireAt > 0.08f && !shotTaken[side, 1]) { shotTaken[side, 1] = true; Shot($"fire_side{side}"); }
                if (fired && Time.time - fireAt > 0.3f && !shotTaken[side, 2]) { shotTaken[side, 2] = true; Shot($"beam_side{side}"); }
                if (fired && Time.time - fireAt > 1.0f) break;
                yield return null;
            }
            boss.StopAllCoroutines();
            yield return new WaitForSeconds(1.0f);
            int left = FindObjectsByType<SpiritBlastFX>(FindObjectsSortMode.None).Length;
            int hazards = FindObjectsByType<BossHazard>(FindObjectsSortMode.None).Length;
            Note($"side {side} (dir {dir}): charged={charged} fired={fired} chargeLen={(fireAt - chargeAt):0.00} hit={impacted} at +{hitAt:0.00} blastFx max={maxFx} left={left} hazardsLeft={hazards} lives {lives}->{HurtPlayer.pLivesCount}");
            Note($"side {side}: {(charged && fired && impacted && maxFx >= 1 && left == 0 && hazards == 0 ? "PASS" : "FAIL")}");
        }
        Finish();
    }

    private readonly bool[,] shotTaken = new bool[2, 4];
    private readonly bool[] dumped = new bool[2];

    // ---------------------------------------------------------------- the samurai boss (DASHCLASH)
    // Z4Boss_Japan: Harvey stands mid-arena; each round waits for the boss's dash slash and dashes into it when he is
    // within reach (expects the card, no life lost, no knockback, the boss attacking again afterwards); then two
    // controls: standing in the dash's path (a life), and dashing into him while he charges (the counter, a life).
    private IEnumerator Boss()
    {
        float until = Time.time + 8f;
        PlayerController pc = null;
        SamuraiBoss boss = null;
        while (Time.time < until && (pc == null || boss == null))
        {
            pc = Player;
            boss = FindFirstObjectByType<SamuraiBoss>();
            yield return null;
        }
        if (pc == null || boss == null) { Note("FAIL: no player or boss"); Finish(); yield break; }
        Talisman dash = DashTalisman();
        Inventory.Unlock(dash);
        PowerUpManager.Instance.Equip(0, dash);
        const BindingFlags Any = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        Transform leftLimit = (Transform)typeof(SamuraiBoss).GetField("leftLimit", Any).GetValue(boss);
        Transform rightLimit = (Transform)typeof(SamuraiBoss).GetField("rightLimit", Any).GetValue(boss);
        float mid = (leftLimit.position.x + rightLimit.position.x) * 0.5f;
        float floor = boss.transform.position.y;
        float floorDelta = pc.transform.position.y - pc.FeetPosition.y;
        Note($"boss at {boss.transform.position:0.0}, arena {leftLimit.position.x:0.0}..{rightLimit.position.x:0.0}, stage {boss.StageIndex}");

        for (int round = 0; round < 3; round++)
        {
            Place(pc, mid, floor + floorDelta + 0.05f);
            PlayerController.BotX = 0f;
            float t0 = Time.time;
            while (Time.time - t0 < 14f && !boss.Dashing) yield return null;
            if (!boss.Dashing) { Note($"round {round}: FAIL he never dashed"); continue; }
            HurtPlayer.pLivesCount = GameSession.StartingLives;
            int lives = HurtPlayer.pLivesCount;
            float dashAt = Time.time;
            while (boss.Dashing && Mathf.Abs(boss.transform.position.x - pc.transform.position.x) > 4.5f) yield return null;
            float toward = Mathf.Sign(boss.transform.position.x - pc.transform.position.x);
            PowerUpManager.Instance.SendMessage("TryActivate", (object)0);
            PlayerController.BotX = toward;
            bool card = false, knock = false, dashSeen = false;
            float cardAt = -1f;
            for (float t = 0f; t < 1.6f; t += Time.deltaTime)
            {
                if (PowerUpManager.Has(TalismanTrait.DefeatsEnemiesOnContact)) dashSeen = true;
                if (ClashCard.Showing && !card)
                {
                    card = true; cardAt = Time.time - dashAt;
                    Note($"round {round}: CLASH at +{cardAt:0.00} after his dash began, harvey={pc.transform.position:0.00} boss={boss.transform.position:0.00}");
                }
                if (card && !knock && Time.time - dashAt < cardAt + 0.4f && pc.GetComponentInChildren<playerBodyDetectionArea>().pushBackCounter > 0f) knock = true;
                if (card && shots < 3 && Time.time - dashAt > cardAt + 0.02f * (shots + 1)) Shot($"bossclash{round}");
                yield return null;
            }
            PlayerController.BotX = 0f;
            Place(pc, mid, floor + floorDelta + 0.05f); // out of his way while we wait for his next attack
            bool resumed = false;
            float waitFrom = Time.time;
            while (Time.time - waitFrom < 10f && !resumed)
            {
                if (boss.Charging || boss.Dashing) resumed = true;
                yield return null;
            }
            Note($"round {round}: dash={dashSeen} card={card} lives {lives}->{HurtPlayer.pLivesCount} knockback={knock} stun={pc.IsStunned} bossVulnerable={boss.Vulnerable} resumedIn={(resumed ? (Time.time - waitFrom).ToString("0.0") : "never")} lives(boss)={boss.Lives}");
            Note($"round {round}: {(card && HurtPlayer.pLivesCount == lives && !knock && resumed ? "PASS" : "FAIL")}");
            HurtPlayer.pLivesCount = GameSession.StartingLives;
        }

        // control 1: standing in the dash's path, no dash of his own: the blade cuts as before
        {
            Place(pc, mid, floor + floorDelta + 0.05f);
            PlayerController.BotX = 0f;
            float t0 = Time.time;
            while (Time.time - t0 < 14f && !boss.Dashing) yield return null;
            HurtPlayer.pLivesCount = GameSession.StartingLives;
            int lives = HurtPlayer.pLivesCount;
            bool card = false;
            for (float t = 0f; t < 1.5f; t += Time.deltaTime) { if (ClashCard.Showing) card = true; yield return null; }
            Note($"control blade: card={card} lives {lives}->{HurtPlayer.pLivesCount} -> {(!card && HurtPlayer.pLivesCount == lives - 1 ? "PASS" : "FAIL")}");
        }
        // control 2: a Spirit Dash into him while he charges: the counter, as before (no card)
        {
            Place(pc, mid, floor + floorDelta + 0.05f);
            PlayerController.BotX = 0f;
            float t0 = Time.time;
            while (Time.time - t0 < 14f && !boss.Charging) yield return null;
            HurtPlayer.pLivesCount = GameSession.StartingLives;
            int lives = HurtPlayer.pLivesCount;
            PowerUpManager.Instance.SendMessage("TryActivate", (object)0);
            PlayerController.BotX = Mathf.Sign(boss.transform.position.x - pc.transform.position.x);
            bool card = false, knock = false;
            for (float t = 0f; t < 1.5f; t += Time.deltaTime)
            {
                if (ClashCard.Showing) card = true;
                if (pc.GetComponentInChildren<playerBodyDetectionArea>().pushBackCounter > 0f) knock = true;
                yield return null;
            }
            PlayerController.BotX = 0f;
            Note($"control counter: card={card} knockback={knock} lives {lives}->{HurtPlayer.pLivesCount} -> {(!card && knock && HurtPlayer.pLivesCount == lives - 1 ? "PASS" : "FAIL")}");
        }
        Note($"timeScale={Time.timeScale} invincible={PlayerController.playerInviInvincible}");
        Finish();
    }

    private void Finish()
    {
        PlayerController.BotX = float.NaN;
        Note("done");
        EditorApplication.isPlaying = false;
    }
}
#endif
