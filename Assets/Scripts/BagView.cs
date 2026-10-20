using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// MENUS: Harvey's Bag. Three tabs (Collectibles, Power-ups and Misc, Village Goods) of pockets, and a note
/// describing the item under the cursor. Cross/Enter uses a usable item (chi tea, a spirit peach...).
/// L1/R1 or Q/E change tab. Talismans are not here: they live in the Talisman Holder.
/// </summary>
public class BagView : MenuView
{
    private const int Cols = 6, Rows = 3;
    private static readonly string[] TabNames = { "COLLECTIBLES", "POWER-UPS & MISC", "VILLAGE GOODS" };
    private static readonly ItemCategory[][] TabCategories =
    {
        new[] { ItemCategory.Collectible },
        new[] { ItemCategory.PowerUp, ItemCategory.Misc },
        new[] { ItemCategory.VillageGoods },
    };

    private readonly Button[] tabs = new Button[TabNames.Length];
    private readonly TMP_Text[] tabLabels = new TMP_Text[TabNames.Length];
    private readonly Button[] pockets = new Button[Cols * Rows];
    private readonly Image[] pocketIcons = new Image[Cols * Rows];
    private readonly TMP_Text[] pocketCounts = new TMP_Text[Cols * Rows];
    private readonly Image detailIcon;
    private readonly TMP_Text detailName, detailCount, detailText, detailAction, coinsText;

    private int tab;
    private int cursor;
    private List<ItemDefinition> shown = new List<ItemDefinition>();

    public BagView(RectTransform parent) : base("HarveysBag", parent, Inventory.Catalog.bagPanel)
    {
        var panel = Panel;
        MenuUI.Image("CoinPlate", panel, new Vector2(110, 71), new Vector2(46, 13), cat.frame, true);
        MenuUI.Image("CoinIcon", panel, new Vector2(95, 71), new Vector2(8, 8), cat.coin);
        coinsText = MenuUI.Text("Coins", panel, new Vector2(114, 71), new Vector2(26, 8), 5, MenuUI.Yellow, TextAlignmentOptions.Right, "");
        MenuUI.Title(panel, 71, 160, "HARVEY'S BAG"); // UI: the standard title, same height in every view

        for (int i = 0; i < tabs.Length; i++)
        {
            int index = i;
            var rt = MenuUI.Rect("Tab" + i, panel, new Vector2(-94 + 94 * i, 50), new Vector2(90, 14));
            tabs[i] = MenuUI.Button(rt, cat.tab, cat.tabSelected, true, () => SetTab(index, false), () => SetTab(index, true));
            tabLabels[i] = MenuUI.Text("Label", rt, Vector2.zero, new Vector2(88, 8), 5, MenuUI.Cream, TextAlignmentOptions.Center, TabNames[i]);
        }

        for (int i = 0; i < pockets.Length; i++)
        {
            int index = i;
            var pos = new Vector2(-128 + 26 * (i % Cols), 26 - 26 * (i / Cols));
            var rt = MenuUI.Rect("Pocket" + i, panel, pos, new Vector2(24, 24));
            pockets[i] = MenuUI.Button(rt, cat.pocket, cat.pocketSelected, false, () => ShowPocket(index), () => UsePocket(index));
            pocketIcons[i] = MenuUI.Image("Icon", rt, Vector2.zero, new Vector2(16, 16), null);
            pocketCounts[i] = MenuUI.Text("Count", rt, new Vector2(4, -8), new Vector2(16, 7), 5, MenuUI.Cream, TextAlignmentOptions.Right, "");
        }

        MenuUI.Image("Note", panel, new Vector2(82, 0), new Vector2(118, 82), cat.frame, true);
        detailIcon = MenuUI.Image("Icon", panel, new Vector2(36, 28), new Vector2(16, 16), null);
        detailName = MenuUI.Text("Name", panel, new Vector2(96, 31), new Vector2(84, 16), 5, MenuUI.Yellow, TextAlignmentOptions.Left, "", true);
        detailCount = MenuUI.Text("Count", panel, new Vector2(96, 20), new Vector2(84, 7), 5, MenuUI.Yellow, TextAlignmentOptions.Left, "");
        detailText = MenuUI.Text("Text", panel, new Vector2(82, -10), new Vector2(106, 44), 5, MenuUI.Cream, TextAlignmentOptions.TopLeft, "", true);
        detailAction = MenuUI.Text("Action", panel, new Vector2(82, -34), new Vector2(106, 7), 5, MenuUI.Yellow, TextAlignmentOptions.Center, "");
        Hint("L1/R1 OR Q/E: TABS   CROSS/ENTER: USE   SQUARE/R: QUICK ITEM   CIRCLE/ESC: BACK", new Vector2(0, -71)); // QUICKITEM:

        WireNavigation();
    }

    private void WireNavigation()
    {
        for (int i = 0; i < tabs.Length; i++)
            MenuUI.Link(tabs[i], pockets[(Rows - 1) * Cols + Mathf.Min(i * 2, Cols - 1)], pockets[Mathf.Min(i * 2, Cols - 1)],
                tabs[(i + tabs.Length - 1) % tabs.Length], tabs[(i + 1) % tabs.Length]);
        for (int i = 0; i < pockets.Length; i++)
        {
            int row = i / Cols, col = i % Cols;
            Selectable up = row == 0 ? tabs[Mathf.Min(col / 2, tabs.Length - 1)] : (Selectable)pockets[i - Cols];
            Selectable down = row == Rows - 1 ? tabs[Mathf.Min(col / 2, tabs.Length - 1)] : (Selectable)pockets[i + Cols];
            MenuUI.Link(pockets[i], up, down, pockets[row * Cols + (col + Cols - 1) % Cols], pockets[row * Cols + (col + 1) % Cols]);
        }
    }

    protected override void OnShow()
    {
        SetTab(tab, false);
        MenuUI.Select(pockets[0]);
        ShowPocket(0);
    }

    protected override void OnTick()
    {
        if (coinsText != null) coinsText.text = CollectableCollisiondetector.coinCollected.ToString();
        Keyboard kb = Keyboard.current;
        Gamepad pad = Gamepad.current;
        int step = 0;
        if ((kb != null && kb.qKey.wasPressedThisFrame) || (pad != null && pad.leftShoulder.wasPressedThisFrame)) step = -1;
        if ((kb != null && kb.eKey.wasPressedThisFrame) || (pad != null && pad.rightShoulder.wasPressedThisFrame)) step = 1;
        if ((kb != null && kb.rKey.wasPressedThisFrame) || (pad != null && pad.buttonWest.wasPressedThisFrame)) SetQuick(cursor); // QUICKITEM:
        if (step != 0)
        {
            SetTab((tab + step + tabs.Length) % tabs.Length, false);
            Sound(cat.moveSound);
            MenuUI.Select(pockets[0]);
            ShowPocket(0);
        }
    }

    private void SetTab(int index, bool jumpToPockets)
    {
        tab = index;
        for (int i = 0; i < tabs.Length; i++)
        {
            tabs[i].image.sprite = i == tab ? cat.tabSelected : cat.tab;
            tabLabels[i].color = i == tab ? MenuUI.Yellow : MenuUI.Muted;
        }
        Refresh();
        if (jumpToPockets)
        {
            Sound(cat.selectSound);
            MenuUI.Select(pockets[0]);
            ShowPocket(0);
        }
    }

    private void Refresh()
    {
        shown = Inventory.Items(TabCategories[tab]);
        for (int i = 0; i < pockets.Length; i++)
        {
            ItemDefinition item = i < shown.Count ? shown[i] : null;
            pocketIcons[i].sprite = item != null ? item.Icon : null;
            pocketIcons[i].color = item != null && item.Icon != null ? Color.white : new Color(1, 1, 1, 0);
            int n = item != null ? Inventory.Count(item) : 0;
            pocketCounts[i].text = n > 1 ? "x" + n : "";
            pocketCounts[i].color = item != null && item == Inventory.QuickItem ? MenuUI.Yellow : MenuUI.Cream; // QUICKITEM: the quick item's count is yellow
        }
    }

    private void ShowPocket(int index)
    {
        cursor = index;
        ItemDefinition item = index < shown.Count ? shown[index] : null;
        detailIcon.sprite = item != null ? item.Icon : null;
        detailIcon.color = item != null && item.Icon != null ? Color.white : new Color(1, 1, 1, 0);
        if (item == null)
        {
            detailName.text = shown.Count == 0 ? "NOTHING HERE YET" : "EMPTY POCKET";
            detailCount.text = "";
            detailText.text = shown.Count == 0 ? EmptyTabText() : "";
            detailAction.text = "";
            return;
        }
        detailName.text = item.DisplayName.ToUpperInvariant();
        int n = Inventory.Count(item);
        detailCount.text = item.Unique ? "" : "CARRYING " + n;
        detailText.text = (item.Description ?? "").ToUpperInvariant();
        detailAction.text = item == Inventory.QuickItem ? "QUICK ITEM: R1 / Q IN A LEVEL" // QUICKITEM:
                          : item.Usable && CanUseHere ? "CROSS/ENTER: USE" : item.Usable ? "SQUARE/R: SET AS QUICK ITEM" : "";
    }

    private string EmptyTabText()
    {
        switch (tab)
        {
            case 0: return "BOOKS, MAPS AND RELICS ARE HIDDEN AROUND THE LEVELS.";
            case 1: return "POWER-UPS TURN UP IN TREASURE CHESTS.";
            default: return "VILLAGE SHOPS SELL GOODS FOR COINS.";
        }
    }

    // QUICKITEM: Square / R puts the item under the cursor on R1 / Q
    private void SetQuick(int index)
    {
        ItemDefinition item = index < shown.Count ? shown[index] : null;
        if (item == null || !item.Usable)
        {
            Sound(cat.blockedSound);
            Message(item == null ? "PICK AN ITEM FIRST" : "ONLY USABLE ITEMS CAN GO ON R1", MenuUI.Muted);
            return;
        }
        Inventory.SetQuick(item);
        Sound(cat.equipSound);
        Message("QUICK ITEM: " + item.DisplayName.ToUpperInvariant() + " (R1 / Q)", MenuUI.Yellow);
        Refresh();
        ShowPocket(index);
    }

    // items can only be used where Harvey is actually playing
    private static bool CanUseHere => Object.FindFirstObjectByType<PlayerController>() != null;

    private void UsePocket(int index)
    {
        ItemDefinition item = index < shown.Count ? shown[index] : null;
        if (item == null) return;
        if (!item.Usable)
        {
            Sound(cat.blockedSound);
            Message("A KEEPSAKE: IT STAYS IN THE BAG", MenuUI.Muted);
            return;
        }
        if (!CanUseHere || !item.TryUse(out string result))
        {
            Sound(cat.blockedSound);
            Message(CanUseHere ? (item.Effect == ItemEffect.RestoreChi ? "CHI IS ALREADY FULL" : "LIVES ARE ALREADY FULL") : "USE IT DURING A LEVEL", MenuUI.Red);
            return;
        }
        Inventory.Remove(item);
        Sound(cat.equipSound);
        Message(item.DisplayName.ToUpperInvariant() + ": " + result, MenuUI.Yellow);
        Refresh();
        ShowPocket(Mathf.Min(cursor, Mathf.Max(0, shown.Count - 1)));
        MenuUI.Select(pockets[Mathf.Min(cursor, Mathf.Max(0, shown.Count - 1))]);
    }
}
