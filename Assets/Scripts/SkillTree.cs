using UnityEngine;

/// <summary>SKILLS: which half of the skill tree a skill is in.</summary>
public enum SkillBranch { Natural, Talisman }

/// <summary>SKILLS: one skill on the tree. Levels are bought one at a time; costs[i] is the price of level i + 1.</summary>
public sealed class SkillDef
{
    public readonly string id, name, icon;
    public readonly SkillBranch branch;
    public readonly int[] costs;
    public readonly string[] levelText; // what each level does, shown in the note
    public readonly string description;
    public readonly string requires;    // a skill that must be learned first (null = a root)
    public readonly int requiresLevel;

    public SkillDef(string id, string name, string icon, SkillBranch branch, int[] costs, string[] levelText, string description,
        string requires = null, int requiresLevel = 1)
    {
        this.id = id; this.name = name; this.icon = icon; this.branch = branch; this.costs = costs; this.levelText = levelText;
        this.description = description; this.requires = requires; this.requiresLevel = requiresLevel;
    }

    public int MaxLevel => costs.Length;
}

/// <summary>
/// SKILLS (2026-10-20): Harvey's skill tree. Two branches, four skills each, bought with crystals: the gems found in
/// treasure chests (Jade 1, Lapis 2, Topaz 3, Ruby 5 crystals each). Paying cashes gems in from the cheapest up; the
/// change stays as a crystal balance (Inventory.CrystalBalance), so nothing is ever lost. Levels are saved with the
/// Bag (Inventory), so buying one autosaves.
///   NATURAL ABILITIES                                TALISMAN SKILLS
///   Stomp Power I-III  stomps deal 2 / 3 / 4         Chi Focus I-II       talismans use 15% / 30% less chi
///   Dash Power I-III   Spirit Dash deals 3 / 4 / 5   Lasting Charms I-II  talismans last 15% / 30% longer
///                      and lasts 12% longer a level  Quick Recharge I-II  cooldowns 20% / 40% shorter
///   Jump Power I-III   the Super Jump (tap jump      Sky Leap I-II        Super Jumps launch 6% / 12% harder,
///                      twice): take-off x1.25 / 1.5                       with the Gale / Comet take-off (JUMPSKILLS)
///                      / 1.65 (56% / 125% / 172% higher). JUMPBOOST: II clears the Grass boss (fox). SUPERTAP: one
///                      press is always the normal jump; Jump Power and Sky Leap only power the double-tap Super Jump
///   Double Jump        a second jump in mid-air
///   Combo Mastery I-IV the stomp/dash combo can climb   Chi Flow I-III     during a 3+ hit combo chi comes back:
///                      to stage 2 / 3 / 4 / 5 (COMBOLOCK)                    a unit every 5 / 3 / 2 s (CHIFLOW)
/// Enemies take more stomps zone by zone (EnemyController.ZoneToughness, REBALANCE: 1 in the Grass, 2 in the Tomb and
/// the Dunes, 3 in Japan; the Japan guard 4), so with Stomp Power III every normal enemy falls to one stomp again. Bosses keep
/// their own health.
/// COMBOLOCK (2026-10-22): the combo's stages and effects are locked behind Combo Mastery. With no levels the combo
/// stays at stage 1 (yellow outline, two motes, small hit burst); each level opens the next stage and everything that
/// stage brings (StompCombo has the list), including its chi discount (Chi.StageCost), so level IV gives the rainbow
/// stage and free chi skills. The 5 s window is the same at every level. Stage 1 stays free so players see the combo
/// exists before they can grow it.
/// JUMPSKILLS (2026-10-25): the Super Jump talisman is gone; one jump button. SUPERTAP: one press is the normal jump at
/// every level; tapping jump twice quickly is the Super Jump, which Jump Power unlocks (I) and raises, and Sky Leap
/// raises further (PlayerController shows it with the super jump take-off: animation, dust ring, Gale / Comet trails by
/// Sky Leap level). Ledges built for the old Super Jump (about 2.6 tiles) need Jump Power II or III. The tutorial
/// ignores bought skills; it lends Jump Power III and Double Jump so its ledge step can be climbed (TutorialLevel).
/// </summary>
public static class SkillTree
{
    public const string StompPower = "StompPower", DashPower = "DashPower", JumpPower = "JumpPower", DoubleJump = "DoubleJump";
    public const string ComboMastery = "ComboMastery"; // COMBOLOCK:
    public const string ChiFocus = "ChiFocus", LastingCharms = "LastingCharms", QuickRecharge = "QuickRecharge", SkyLeap = "SkyLeap";
    public const string ChiFlow = "ChiFlow"; // CHIFLOW:

    // the costs climb with each level's power; the whole tree is 123 crystals (a gem chest holds about 5-6)
    public static readonly SkillDef[] All =
    {
        new SkillDef(StompPower, "STOMP POWER", "Skill_Stomp", SkillBranch.Natural, new[] { 2, 4, 7 },
            new[] { "STOMPS DEAL 2 DAMAGE", "STOMPS DEAL 3 DAMAGE", "STOMPS DEAL 4 DAMAGE" },
            "HARVEY LANDS HARDER. TOUGH ENEMIES FALL TO FEWER STOMPS."),
        new SkillDef(DashPower, "DASH POWER", "Skill_Dash", SkillBranch.Natural, new[] { 2, 4, 7 },
            new[] { "DASH DEALS 3, LASTS 12% LONGER", "DASH DEALS 4, LASTS 24% LONGER", "DASH DEALS 5, LASTS 36% LONGER" },
            "A STRONGER, LONGER SPIRIT DASH.", StompPower),
        new SkillDef(JumpPower, "JUMP POWER", "Skill_Jump", SkillBranch.Natural, new[] { 2, 4, 6 },
            new[] { "SUPER JUMP: 56% HIGHER", "SUPER JUMP: 125% HIGHER", "SUPER JUMP: 172% HIGHER" }, // JUMPBOOST: height goes with the square of JumpPowerScales
            "TAP JUMP TWICE, QUICKLY, FOR A SUPER JUMP. EACH LEVEL LAUNCHES HIGHER.", StompPower), // SUPERTAP:
        new SkillDef(DoubleJump, "DOUBLE JUMP", "Skill_DoubleJump", SkillBranch.Natural, new[] { 8 },
            new[] { "JUMP AGAIN IN MID-AIR" },
            "PRESS JUMP IN THE AIR FOR A SECOND JUMP. NOT WHILE SWIMMING.", JumpPower, 2), // JUMPSKILLS: no talisman needed
        new SkillDef(ComboMastery, "COMBO MASTERY", "Skill_Combo", SkillBranch.Natural, new[] { 3, 5, 7, 9 }, // COMBOLOCK:
            new[] { "COMBOS REACH STAGE 2 (ORANGE)", "COMBOS REACH STAGE 3 (PINK)", "COMBOS REACH STAGE 4 (VIOLET)",
                    "COMBOS REACH STAGE 5 (RAINBOW): FREE CHI" },
            "CHAIN STOMPS AND DASH DEFEATS INTO BIGGER COMBOS. EACH LEVEL OPENS THE NEXT STAGE.", DashPower),

        new SkillDef(ChiFocus, "CHI FOCUS", "Skill_ChiFocus", SkillBranch.Talisman, new[] { 3, 6 },
            new[] { "TALISMANS USE 15% LESS CHI", "TALISMANS USE 30% LESS CHI" },
            "A CALM MIND MAKES CHI LAST."),
        new SkillDef(LastingCharms, "LASTING CHARMS", "Skill_Lasting", SkillBranch.Talisman, new[] { 3, 6 },
            new[] { "TALISMANS LAST 15% LONGER", "TALISMANS LAST 30% LONGER" },
            "TALISMAN EFFECTS HOLD ON FOR LONGER.", ChiFocus),
        new SkillDef(QuickRecharge, "QUICK RECHARGE", "Skill_Recharge", SkillBranch.Talisman, new[] { 2, 5 },
            new[] { "COOLDOWNS 20% SHORTER", "COOLDOWNS 40% SHORTER" },
            "TALISMANS ARE READY AGAIN SOONER.", ChiFocus),
        new SkillDef(SkyLeap, "SKY LEAP", "Skill_SkyLeap", SkillBranch.Talisman, new[] { 4, 8 },
            new[] { "SUPER JUMPS 6% HIGHER (GALE)", "SUPER JUMPS 12% HIGHER (COMET)" }, // JUMPSKILLS: not a talisman // SUPERTAP: the double-tap jump
            "SPIRIT WIND UNDER HARVEY'S FEET. SUPER JUMPS LAUNCH EVEN HIGHER.", LastingCharms),
        new SkillDef(ChiFlow, "CHI FLOW", "Skill_ChiFlow", SkillBranch.Talisman, new[] { 3, 5, 8 }, // CHIFLOW:
            new[] { "A CHI UNIT EVERY 5 S IN A 3+ COMBO", "A CHI UNIT EVERY 3 S IN A 3+ COMBO",
                    "A CHI UNIT EVERY 2 S IN A 3+ COMBO" },
            "WHILE A COMBO OF 3 OR MORE HITS LASTS, CHI FLOWS BACK INTO THE METER.", QuickRecharge),
    };

    // ---------------------------------------------------------------- levels
    public static SkillDef Def(string id)
    {
        foreach (SkillDef d in All) if (d.id == id) return d;
        return null;
    }

    /// <summary>The level of a skill in play (0 in the tutorial, which teaches the basics).</summary>
    public static int Level(string id) => GameSession.IsTutorial ? TutorialLevel(id) : Inventory.SkillLevel(id);

    // JUMPSKILLS: the tutorial's ledge step was built for the Super Jump; it lends Jump Power III and Double Jump instead
    private static int TutorialLevel(string id) => id == JumpPower ? 3 : id == DoubleJump ? 1 : 0;

    // ---------------------------------------------------------------- effects (read by the game)
    public static int StompDamage => 1 + Level(StompPower);
    public static int DashDamage => 2 + Level(DashPower);
    public static float DashDurationScale => 1f + 0.12f * Level(DashPower);
    /// <summary>The Super Jump's take-off speed, times the normal jump's: Jump Power times Sky Leap (SUPERTAP).</summary>
    public static float JumpScale => JumpPowerScales[Mathf.Clamp(Level(JumpPower), 0, JumpPowerScales.Length - 1)] * SuperJumpScale;

    /// <summary>
    /// JUMPBOOST (2026-10-25): the Super Jump's take-off speed per Jump Power level (0 = no Super Jump). Height goes
    /// with the square: x1.56, x2.25, x2.72 of the normal jump (1.16 units at speed 5, gravity 1.1). Level II peaks at
    /// 2.6, over the whole Grass boss (fox: hurtbox 1.75 high and 2.5 wide, antler tips 2.19), with about 4 units of
    /// run under it; level III is about where the old Super Jump talisman went (8.1). SUPERTAP: the normal jump stays
    /// at speed 5 whatever is learned.
    /// </summary>
    public static readonly float[] JumpPowerScales = { 1f, 1.25f, 1.5f, 1.65f };
    public static bool HasDoubleJump => Level(DoubleJump) > 0;
    /// <summary>SUPERTAP: tapping jump twice gives the Super Jump (Jump Power I and up).</summary>
    public static bool HasSuperJump => Level(JumpPower) > 0;
    public static float TalismanChiScale => 1f - 0.15f * Level(ChiFocus);
    public static float TalismanDurationScale => 1f + 0.15f * Level(LastingCharms);
    public static float CooldownScale => 1f - 0.2f * Level(QuickRecharge);
    /// <summary>JUMPSKILLS: Sky Leap's part of JumpScale (the name is from the Super Jump talisman it used to boost).</summary>
    public static float SuperJumpScale => 1f + 0.06f * Level(SkyLeap);
    /// <summary>COMBOLOCK: the highest combo stage that can be reached (1 with no Combo Mastery, MaxStage at level IV).</summary>
    public static int ComboStageCap => 1 + Level(ComboMastery);
    /// <summary>CHIFLOW: chi restored per second during a 3+ hit combo (Chi.Flow): a unit (10) every 5 / 3 / 2 s.</summary>
    public static float ChiFlowPerSecond => ChiFlowRates[Mathf.Clamp(Level(ChiFlow), 0, ChiFlowRates.Length - 1)];
    private static readonly float[] ChiFlowRates = { 0f, Chi.PerUnit / 5f, Chi.PerUnit / 3f, Chi.PerUnit / 2f };

    /// <summary>How long a talisman runs with the skills learned (Spirit Dash also gets Dash Power).</summary>
    public static float DurationOf(Talisman t)
    {
        float scale = TalismanDurationScale;
        if ((t.Traits & TalismanTrait.DefeatsEnemiesOnContact) != 0) scale *= DashDurationScale;
        return t.Duration * scale;
    }

    // ---------------------------------------------------------------- crystals
    private static readonly (string id, int value)[] Gems = { ("JadeGem", 1), ("LapisGem", 2), ("TopazGem", 3), ("RubyGem", 5) };

    /// <summary>Crystals one of this item is worth (0 = not a gem).</summary>
    public static int CrystalValue(ItemDefinition item)
    {
        if (item == null) return 0;
        foreach (var g in Gems) if (g.id == item.Id) return g.value;
        return 0;
    }

    /// <summary>All crystals Harvey has: the balance plus every gem in the Bag.</summary>
    public static int Crystals
    {
        get
        {
            int total = Inventory.CrystalBalance;
            if (Inventory.Catalog == null) return total;
            foreach (var g in Gems) total += Inventory.Count(Inventory.Catalog.ItemById(g.id)) * g.value;
            return total;
        }
    }

    /// <summary>A gem icon for the crystal counter (the first gem in the catalog).</summary>
    public static Sprite CrystalIcon
    {
        get
        {
            if (Inventory.Catalog == null) return null;
            ItemDefinition topaz = Inventory.Catalog.ItemById("TopazGem");
            return topaz != null ? topaz.Icon : null;
        }
    }

    // ---------------------------------------------------------------- buying
    public enum State { Learnable, TooExpensive, Locked, Maxed }

    public static int NextCost(SkillDef d)
    {
        int level = Inventory.SkillLevel(d.id);
        return level < d.MaxLevel ? d.costs[level] : 0;
    }

    public static bool RequirementsMet(SkillDef d) =>
        d.requires == null || Inventory.SkillLevel(d.requires) >= d.requiresLevel;

    public static State StateOf(SkillDef d)
    {
        if (Inventory.SkillLevel(d.id) >= d.MaxLevel) return State.Maxed;
        if (!RequirementsMet(d)) return State.Locked;
        return Crystals >= NextCost(d) ? State.Learnable : State.TooExpensive;
    }

    /// <summary>What is still needed before a locked skill can be learned.</summary>
    public static string RequirementText(SkillDef d)
    {
        var parts = new System.Collections.Generic.List<string>();
        if (d.requires != null && Inventory.SkillLevel(d.requires) < d.requiresLevel)
            parts.Add(Def(d.requires).name + " " + Numeral(d.requiresLevel));
        return parts.Count == 0 ? "" : "NEEDS " + string.Join(" + ", parts);
    }

    /// <summary>Buys the next level: pays (cashing gems in as needed) and learns it.</summary>
    public static bool TryLearn(SkillDef d, out string message)
    {
        switch (StateOf(d))
        {
            case State.Maxed: message = d.name + " IS FULLY LEARNED"; return false;
            case State.Locked: message = RequirementText(d); return false;
            case State.TooExpensive: message = "NOT ENOUGH CRYSTALS (" + NextCost(d) + " NEEDED)"; return false;
        }
        Pay(NextCost(d));
        int level = Inventory.SkillLevel(d.id) + 1;
        Inventory.SetSkillLevel(d.id, level); // raises Inventory.Changed: GameSession autosaves
        message = d.name + (d.MaxLevel > 1 ? " " + Numeral(level) : "") + " LEARNED";
        return true;
    }

    // the balance first, then gems from the cheapest up; the change goes back on the balance
    private static void Pay(int cost)
    {
        int balance = Inventory.CrystalBalance;
        HNLCatalog cat = Inventory.Catalog;
        foreach (var g in Gems)
        {
            if (balance >= cost || cat == null) break;
            ItemDefinition gem = cat.ItemById(g.id);
            while (balance < cost && Inventory.Count(gem) > 0 && Inventory.Remove(gem)) balance += g.value;
        }
        Inventory.CrystalBalance = balance - cost;
    }

    public static string Numeral(int level) => level <= 1 ? "I" : level == 2 ? "II" : level == 3 ? "III" : level.ToString();
}
