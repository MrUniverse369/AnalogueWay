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
    public readonly bool needsSuperJump; // the Super Jump talisman must be owned

    public SkillDef(string id, string name, string icon, SkillBranch branch, int[] costs, string[] levelText, string description,
        string requires = null, int requiresLevel = 1, bool needsSuperJump = false)
    {
        this.id = id; this.name = name; this.icon = icon; this.branch = branch; this.costs = costs; this.levelText = levelText;
        this.description = description; this.requires = requires; this.requiresLevel = requiresLevel; this.needsSuperJump = needsSuperJump;
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
///   Jump Power I-III   jumps 5% faster a level       Sky Leap I-II        Super Jump launches 6% / 12% harder
///                      (10% / 21% / 32% higher)
///   Double Jump        a second jump in mid-air
/// Enemies take more stomps zone by zone (EnemyController.ZoneToughness: 1 in the Tomb, 2 in the Dunes, 3 on the
/// Shrine Road, 4 in the Meadow), so with Stomp Power III every normal enemy falls to one stomp again. Bosses keep
/// their own health. Jump Power stops below the Super Jump's launch speed (PlayerController treats a jump over 6 as
/// a super jump), so ledges built for the Super Jump still need it. The tutorial ignores skills (it teaches the basics).
/// </summary>
public static class SkillTree
{
    public const string StompPower = "StompPower", DashPower = "DashPower", JumpPower = "JumpPower", DoubleJump = "DoubleJump";
    public const string ChiFocus = "ChiFocus", LastingCharms = "LastingCharms", QuickRecharge = "QuickRecharge", SkyLeap = "SkyLeap";

    // the costs climb with each level's power; the whole tree is 83 crystals (a gem chest holds about 5-6)
    public static readonly SkillDef[] All =
    {
        new SkillDef(StompPower, "STOMP POWER", "Skill_Stomp", SkillBranch.Natural, new[] { 2, 4, 7 },
            new[] { "STOMPS DEAL 2 DAMAGE", "STOMPS DEAL 3 DAMAGE", "STOMPS DEAL 4 DAMAGE" },
            "HARVEY LANDS HARDER. TOUGH ENEMIES FALL TO FEWER STOMPS."),
        new SkillDef(DashPower, "DASH POWER", "Skill_Dash", SkillBranch.Natural, new[] { 2, 4, 7 },
            new[] { "DASH DEALS 3, LASTS 12% LONGER", "DASH DEALS 4, LASTS 24% LONGER", "DASH DEALS 5, LASTS 36% LONGER" },
            "A STRONGER, LONGER SPIRIT DASH.", StompPower),
        new SkillDef(JumpPower, "JUMP POWER", "Skill_Jump", SkillBranch.Natural, new[] { 2, 4, 6 },
            new[] { "JUMPS 10% HIGHER", "JUMPS 21% HIGHER", "JUMPS 32% HIGHER" },
            "STRONGER LEGS FOR TALL STEPS AND HIGH LEDGES.", StompPower),
        new SkillDef(DoubleJump, "DOUBLE JUMP", "Skill_DoubleJump", SkillBranch.Natural, new[] { 8 },
            new[] { "JUMP AGAIN IN MID-AIR" },
            "PRESS JUMP IN THE AIR FOR A SECOND JUMP. NOT WHILE SWIMMING.", JumpPower, 2, true),

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
            new[] { "SUPER JUMP 6% STRONGER", "SUPER JUMP 12% STRONGER" },
            "THE SUPER JUMP TALISMAN LAUNCHES HARVEY EVEN HIGHER.", LastingCharms, 1, true),
    };

    // ---------------------------------------------------------------- levels
    public static SkillDef Def(string id)
    {
        foreach (SkillDef d in All) if (d.id == id) return d;
        return null;
    }

    /// <summary>The level of a skill in play (0 in the tutorial, which teaches the basics).</summary>
    public static int Level(string id) => GameSession.IsTutorial ? 0 : Inventory.SkillLevel(id);

    // ---------------------------------------------------------------- effects (read by the game)
    public static int StompDamage => 1 + Level(StompPower);
    public static int DashDamage => 2 + Level(DashPower);
    public static float DashDurationScale => 1f + 0.12f * Level(DashPower);
    /// <summary>On jump speed. Level III gives 5 x 1.15 = 5.75, under the 6 that marks a super jump.</summary>
    public static float JumpScale => 1f + 0.05f * Level(JumpPower);
    public static bool HasDoubleJump => Level(DoubleJump) > 0;
    public static float TalismanChiScale => 1f - 0.15f * Level(ChiFocus);
    public static float TalismanDurationScale => 1f + 0.15f * Level(LastingCharms);
    public static float CooldownScale => 1f - 0.2f * Level(QuickRecharge);
    public static float SuperJumpScale => 1f + 0.06f * Level(SkyLeap);

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
        (d.requires == null || Inventory.SkillLevel(d.requires) >= d.requiresLevel) && (!d.needsSuperJump || OwnsSuperJump);

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
        if (d.needsSuperJump && !OwnsSuperJump) parts.Add("THE SUPER JUMP TALISMAN");
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

    private static bool OwnsSuperJump
    {
        get
        {
            HNLCatalog cat = Inventory.Catalog;
            if (cat == null || cat.talismans == null) return false;
            foreach (Talisman t in cat.talismans)
                if (t is SuperJumpTalisman && Inventory.Owns(t)) return true;
            return false;
        }
    }

    public static string Numeral(int level) => level <= 1 ? "I" : level == 2 ? "II" : level == 3 ? "III" : level.ToString();
}
