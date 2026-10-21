using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// MENUS: what Harvey carries: the talismans he owns and the three he has equipped (Talisman Holder), and the
/// items in his Bag. Saved through GameSession.Capture/Apply, reset by GameSession.NewGame.
/// Talismans and items are looked up by id in Resources/HNLCatalog.
/// </summary>
public static class Inventory
{
    public const int SlotCount = 3;

    /// <summary>The equipped talismans changed (PowerUpManager re-equips its slots).</summary>
    public static event Action LoadoutChanged;
    /// <summary>Anything changed: talismans owned or equipped, or items.</summary>
    public static event Action Changed;

    private static HNLCatalog catalog;
    private static bool catalogLoaded;
    private static bool initialised;
    private static readonly HashSet<string> owned = new HashSet<string>();
    private static readonly string[] equipped = new string[SlotCount];
    private static readonly Dictionary<string, int> counts = new Dictionary<string, int>();
    private static readonly List<string> itemOrder = new List<string>(); // pick-up order, so new things go last
    private static readonly HashSet<string> claimed = new HashSet<string>();
    private static string quickItem; // QUICKITEM:
    private static readonly Dictionary<string, int> skillLevels = new Dictionary<string, int>(); // SKILLS:
    private static int crystalBalance; // SKILLS:

    public static HNLCatalog Catalog
    {
        get
        {
            if (!catalogLoaded)
            {
                catalogLoaded = true;
                catalog = Resources.Load<HNLCatalog>(HNLCatalog.ResourcePath);
            }
            return catalog;
        }
    }

    public static bool HasCatalog => Catalog != null;

    private static void EnsureInit()
    {
        if (initialised) return;
        initialised = true;
        ResetToNewGame();
    }

    /// <summary>A new game: the starting loadout, owned and equipped, and an empty bag.</summary>
    public static void ResetToNewGame()
    {
        initialised = true;
        owned.Clear();
        counts.Clear();
        itemOrder.Clear();
        claimed.Clear();
        quickItem = null; // QUICKITEM:
        skillLevels.Clear(); // SKILLS:
        crystalBalance = 0;
        for (int i = 0; i < SlotCount; i++) equipped[i] = null;
        if (Catalog != null && Catalog.startingLoadout != null)
        {
            for (int i = 0; i < SlotCount && i < Catalog.startingLoadout.Length; i++)
            {
                Talisman t = Catalog.startingLoadout[i];
                if (t == null) continue;
                owned.Add(t.name);
                equipped[i] = t.name;
            }
        }
        RaiseAll();
    }

    // ---------------------------------------------------------------- talismans
    public static bool Owns(Talisman talisman)
    {
        EnsureInit();
        return talisman != null && owned.Contains(talisman.name);
    }

    /// <summary>Gives Harvey a talisman. Returns true if it is new. It goes into a free slot if there is one.</summary>
    public static bool Unlock(Talisman talisman)
    {
        EnsureInit();
        if (talisman == null || !owned.Add(talisman.name)) return false;
        for (int i = 0; i < SlotCount; i++)
        {
            if (equipped[i] != null) continue;
            equipped[i] = talisman.name;
            LoadoutChanged?.Invoke();
            break;
        }
        Changed?.Invoke();
        return true;
    }

    public static Talisman Equipped(int slot)
    {
        EnsureInit();
        return (uint)slot < SlotCount && Catalog != null ? Catalog.TalismanById(equipped[slot]) : null;
    }

    /// <summary>The slot a talisman is equipped in, or -1.</summary>
    public static int SlotOf(Talisman talisman)
    {
        EnsureInit();
        if (talisman == null) return -1;
        for (int i = 0; i < SlotCount; i++)
            if (equipped[i] == talisman.name) return i;
        return -1;
    }

    public static int EquippedCount
    {
        get
        {
            EnsureInit();
            int n = 0;
            foreach (string id in equipped) if (id != null) n++;
            return n;
        }
    }

    /// <summary>
    /// Puts an owned talisman (or nothing) in a slot. If it was already in another slot the two slots swap, so a
    /// talisman is never equipped twice.
    /// </summary>
    public static void SetEquipped(int slot, Talisman talisman)
    {
        EnsureInit();
        if ((uint)slot >= SlotCount) return;
        if (talisman != null && !owned.Contains(talisman.name)) return;
        string id = talisman != null ? talisman.name : null;
        int from = SlotOf(talisman);
        if (from == slot) return;
        if (from >= 0) equipped[from] = equipped[slot];
        equipped[slot] = id;
        RaiseAll();
    }

    /// <summary>Swaps two slots (either may be empty).</summary>
    public static void SwapSlots(int a, int b)
    {
        EnsureInit();
        if ((uint)a >= SlotCount || (uint)b >= SlotCount || a == b) return;
        (equipped[a], equipped[b]) = (equipped[b], equipped[a]);
        RaiseAll();
    }

    // ---------------------------------------------------------------- items
    public static int Count(ItemDefinition item)
    {
        EnsureInit();
        return item != null && counts.TryGetValue(item.Id, out int n) ? n : 0;
    }

    /// <summary>Adds items. Unique items stop at one. Returns how many were actually added.</summary>
    public static int Add(ItemDefinition item, int amount = 1)
    {
        EnsureInit();
        if (item == null || amount <= 0) return 0;
        int have = Count(item);
        int target = item.Unique ? Mathf.Min(1, have + amount) : have + amount;
        if (target == have) return 0;
        counts[item.Id] = target;
        if (!itemOrder.Contains(item.Id)) itemOrder.Add(item.Id);
        Changed?.Invoke();
        return target - have;
    }

    public static bool Remove(ItemDefinition item, int amount = 1)
    {
        EnsureInit();
        int have = Count(item);
        if (item == null || have < amount) return false;
        if (have == amount)
        {
            counts.Remove(item.Id);
            itemOrder.Remove(item.Id);
        }
        else counts[item.Id] = have - amount;
        Changed?.Invoke();
        return true;
    }

    /// <summary>Owned items in a set of categories, in the order they were picked up.</summary>
    public static List<ItemDefinition> Items(params ItemCategory[] categories)
    {
        EnsureInit();
        var list = new List<ItemDefinition>();
        if (Catalog == null) return list;
        foreach (string id in itemOrder)
        {
            ItemDefinition item = Catalog.ItemById(id);
            if (item != null && Array.IndexOf(categories, item.Category) >= 0) list.Add(item);
        }
        return list;
    }

    // ---------------------------------------------------------------- QUICKITEM: the quick-use item
    /// <summary>The item L1 / Q uses (it stays set when the last one is used, shown greyed until more are found).</summary>
    public static ItemDefinition QuickItem
    {
        get
        {
            EnsureInit();
            return Catalog != null ? Catalog.ItemById(quickItem) : null;
        }
    }

    /// <summary>Sets the quick item (a usable item; null clears it).</summary>
    public static void SetQuick(ItemDefinition item)
    {
        EnsureInit();
        if (item != null && !item.Usable) return;
        string id = item != null ? item.Id : null;
        if (id == quickItem) return;
        quickItem = id;
        Changed?.Invoke();
    }

    // ---------------------------------------------------------------- SKILLS: the skill tree (rules in SkillTree)
    /// <summary>The level learned of a skill (0 = not learned).</summary>
    public static int SkillLevel(string id)
    {
        EnsureInit();
        return !string.IsNullOrEmpty(id) && skillLevels.TryGetValue(id, out int n) ? n : 0;
    }

    /// <summary>Sets a skill's level (SkillTree.TryLearn calls this after paying).</summary>
    public static void SetSkillLevel(string id, int level)
    {
        EnsureInit();
        if (string.IsNullOrEmpty(id) || SkillLevel(id) == level) return;
        if (level <= 0) skillLevels.Remove(id);
        else skillLevels[id] = level;
        Changed?.Invoke();
    }

    /// <summary>Crystals left over after cashing in gems (a Ruby is worth 5: paying 3 with it leaves 2 here).</summary>
    public static int CrystalBalance
    {
        get { EnsureInit(); return crystalBalance; }
        set { EnsureInit(); crystalBalance = Mathf.Max(0, value); }
    }

    // ---------------------------------------------------------------- one-time rewards
    /// <summary>Marks a one-time reward (a chest's bonus item, say) as taken. Returns false if it already was.</summary>
    public static bool Claim(string key)
    {
        EnsureInit();
        return !string.IsNullOrEmpty(key) && claimed.Add(key);
    }

    public static bool IsClaimed(string key)
    {
        EnsureInit();
        return !string.IsNullOrEmpty(key) && claimed.Contains(key);
    }

    // ---------------------------------------------------------------- saving
    public static void Capture(SaveData data)
    {
        EnsureInit();
        data.ownedTalismans = new List<string>(owned).ToArray();
        data.equippedTalismans = new string[SlotCount];
        for (int i = 0; i < SlotCount; i++) data.equippedTalismans[i] = equipped[i] ?? "";
        var stacks = new List<ItemStack>();
        foreach (string id in itemOrder) stacks.Add(new ItemStack { id = id, count = counts[id] });
        data.items = stacks.ToArray();
        data.claimed = new List<string>(claimed).ToArray();
        data.quickItem = quickItem ?? ""; // QUICKITEM:
        var skills = new List<ItemStack>(); // SKILLS:
        foreach (KeyValuePair<string, int> pair in skillLevels) skills.Add(new ItemStack { id = pair.Key, count = pair.Value });
        data.skills = skills.ToArray();
        data.crystalBalance = crystalBalance;
    }

    /// <summary>Restores a save. Saves from before the Talisman Holder (no talisman data) get the new-game loadout.</summary>
    public static void Apply(SaveData data)
    {
        initialised = true;
        if (data.ownedTalismans == null || data.ownedTalismans.Length == 0)
        {
            ResetToNewGame();
        }
        else
        {
            owned.Clear();
            foreach (string id in data.ownedTalismans) if (Catalog == null || Catalog.TalismanById(id) != null) owned.Add(id);
            for (int i = 0; i < SlotCount; i++)
            {
                string id = data.equippedTalismans != null && i < data.equippedTalismans.Length ? data.equippedTalismans[i] : null;
                equipped[i] = !string.IsNullOrEmpty(id) && owned.Contains(id) ? id : null;
            }
            counts.Clear();
            itemOrder.Clear();
            claimed.Clear();
        }
        if (data.items != null)
            foreach (ItemStack s in data.items)
                if (s != null && !string.IsNullOrEmpty(s.id) && s.count > 0 && (Catalog == null || Catalog.ItemById(s.id) != null))
                {
                    counts[s.id] = s.count;
                    if (!itemOrder.Contains(s.id)) itemOrder.Add(s.id);
                }
        if (data.claimed != null) foreach (string key in data.claimed) if (!string.IsNullOrEmpty(key)) claimed.Add(key);
        skillLevels.Clear(); // SKILLS: older saves have none
        if (data.skills != null)
            foreach (ItemStack s in data.skills)
                if (s != null && !string.IsNullOrEmpty(s.id) && s.count > 0) skillLevels[s.id] = s.count;
        crystalBalance = Mathf.Max(0, data.crystalBalance);
        quickItem = !string.IsNullOrEmpty(data.quickItem) && (Catalog == null || Catalog.ItemById(data.quickItem) != null) ? data.quickItem : null; // QUICKITEM: older saves have none
        RaiseAll();
    }

    private static void RaiseAll()
    {
        LoadoutChanged?.Invoke();
        Changed?.Invoke();
    }
}
