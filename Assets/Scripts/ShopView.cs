using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// MENUS: a village shop. A shelf of goods with prices on the left and a note about the selected one on the
/// right. Cross/Enter buys with coins; what's bought goes in the Village Goods tab of Harvey's Bag.
/// SHOPFOOD (2026-10-20): the stock is sorted onto three shelves, HEALTH (fish and lobster), CHI (flakes, tea, sushi)
/// and GOODS (keepsakes), switched with L1/R1, Q/E or left/right, so the shop can sell more than one screen of rows.
/// </summary>
public class ShopView : MenuView
{
    private const int MaxRows = 5;
    private static readonly string[] ShelfNames = { "HEALTH", "CHI", "GOODS" };

    private readonly TMP_Text title, coinsText;
    private readonly Button[] shelfTabs = new Button[ShelfNames.Length];
    private readonly TMP_Text[] shelfLabels = new TMP_Text[ShelfNames.Length];
    private readonly Button[] rows = new Button[MaxRows];
    private readonly Image[] rowIcons = new Image[MaxRows];
    private readonly TMP_Text[] rowNames = new TMP_Text[MaxRows];
    private readonly TMP_Text[] rowPrices = new TMP_Text[MaxRows];
    private readonly Image detailIcon;
    private readonly TMP_Text detailName, detailOwned, detailText, detailAction;

    private ItemDefinition[] allStock = new ItemDefinition[0];
    private readonly List<ItemDefinition>[] shelves = { new List<ItemDefinition>(), new List<ItemDefinition>(), new List<ItemDefinition>() };
    private ItemDefinition[] stock = new ItemDefinition[0]; // the shelf on show
    private int shelf;
    private int cursor;

    public ShopView(RectTransform parent) : base("VillageShop", parent, Inventory.Catalog.shopPanel)
    {
        var panel = Panel;
        MenuUI.Image("CoinPlate", panel, new Vector2(110, 71), new Vector2(46, 13), cat.frame, true); // UI: on the title row, like the Bag
        MenuUI.Image("CoinIcon", panel, new Vector2(95, 71), new Vector2(8, 8), cat.coin);
        coinsText = MenuUI.Text("Coins", panel, new Vector2(114, 71), new Vector2(26, 8), 5, MenuUI.Yellow, TextAlignmentOptions.Right, "");
        title = MenuUI.Title(panel, 71, 160, "VILLAGE SHOP"); // UI: the standard title, same height in every view

        for (int i = 0; i < shelfTabs.Length; i++) // SHOPFOOD: the shelves, over the rows like the Bag's tabs
        {
            int index = i;
            var rt = MenuUI.Rect("Shelf" + i, panel, new Vector2(-114 + 50 * i, 51), new Vector2(48, 14));
            shelfTabs[i] = MenuUI.Button(rt, cat.tab, cat.tabSelected, true, () => SetShelf(index, false), () => SetShelf(index, true));
            shelfLabels[i] = MenuUI.Text("Label", rt, Vector2.zero, new Vector2(46, 8), 5, MenuUI.Cream, TextAlignmentOptions.Center, ShelfNames[i]);
        }

        for (int i = 0; i < MaxRows; i++)
        {
            int index = i;
            var rt = MenuUI.Rect("Row" + i, panel, new Vector2(-64, 32 - 20 * i), new Vector2(150, 19));
            rows[i] = MenuUI.Button(rt, cat.card, cat.cardSelected, true, () => ShowRow(index), () => Buy(index));
            rowIcons[i] = MenuUI.Image("Icon", rt, new Vector2(-62, 0), new Vector2(16, 16), null);
            rowNames[i] = MenuUI.Text("Name", rt, new Vector2(-10, 0), new Vector2(86, 8), 5, MenuUI.Cream, TextAlignmentOptions.Left, "");
            rowPrices[i] = MenuUI.Text("Price", rt, new Vector2(56, 0), new Vector2(30, 8), 5, MenuUI.Yellow, TextAlignmentOptions.Right, "");
        }

        MenuUI.Image("Note", panel, new Vector2(82, -6), new Vector2(118, 96), cat.frame, true);
        detailIcon = MenuUI.Image("Icon", panel, new Vector2(36, 28), new Vector2(16, 16), null);
        detailName = MenuUI.Text("Name", panel, new Vector2(96, 31), new Vector2(84, 16), 5, MenuUI.Yellow, TextAlignmentOptions.Left, "", true);
        detailOwned = MenuUI.Text("Owned", panel, new Vector2(96, 20), new Vector2(84, 7), 5, MenuUI.Yellow, TextAlignmentOptions.Left, "");
        detailText = MenuUI.Text("Text", panel, new Vector2(82, -12), new Vector2(106, 48), 5, MenuUI.Cream, TextAlignmentOptions.TopLeft, "", true);
        detailAction = MenuUI.Text("Action", panel, new Vector2(82, -46), new Vector2(106, 7), 5, MenuUI.Yellow, TextAlignmentOptions.Center, "");
        Hint("L1/R1 OR Q/E: SHELVES    CROSS/ENTER: BUY    CIRCLE/ESC: LEAVE", new Vector2(0, -71));
    }

    public void Open(VillageShop shop)
    {
        title.text = shop != null ? shop.ShopName.ToUpperInvariant() : "VILLAGE SHOP";
        allStock = shop != null ? shop.Stock : cat.villageStock;
        // SHOPFOOD: health food, chi food, then everything else, each in stock order (a shelf shows MaxRows at most)
        foreach (List<ItemDefinition> list in shelves) list.Clear();
        foreach (ItemDefinition item in allStock)
        {
            if (item == null) continue;
            List<ItemDefinition> list = shelves[ShelfOf(item)];
            if (list.Count < MaxRows) list.Add(item);
        }
        shelf = 0;
        while (shelf < shelves.Length - 1 && shelves[shelf].Count == 0) shelf++; // open on the first shelf with goods
    }

    private static int ShelfOf(ItemDefinition item) =>
        item.Effect == ItemEffect.ExtraLife ? 0 : item.Effect == ItemEffect.RestoreChi ? 1 : 2;

    protected override void OnShow()
    {
        SetShelf(shelf, false);
        SelectFirstRow();
    }

    protected override void OnTick()
    {
        coinsText.text = CollectableCollisiondetector.coinCollected.ToString();
        // SHOPFOOD: L1/R1 or Q/E change shelf from anywhere; left/right do too while a row is selected
        Keyboard kb = Keyboard.current;
        Gamepad pad = Gamepad.current;
        int step = 0;
        if ((kb != null && kb.qKey.wasPressedThisFrame) || (pad != null && pad.leftShoulder.wasPressedThisFrame)) step = -1;
        if ((kb != null && kb.eKey.wasPressedThisFrame) || (pad != null && pad.rightShoulder.wasPressedThisFrame)) step = 1;
        if (step == 0 && RowSelected())
        {
            if ((kb != null && (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame)) || (pad != null && pad.dpad.left.wasPressedThisFrame)) step = -1;
            if ((kb != null && (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame)) || (pad != null && pad.dpad.right.wasPressedThisFrame)) step = 1;
        }
        if (step == 0) return;
        SetShelf((shelf + step + shelves.Length) % shelves.Length, false);
        Sound(cat.moveSound);
        SelectFirstRow();
    }

    private bool RowSelected()
    {
        GameObject selected = UnityEngine.EventSystems.EventSystem.current != null ? UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject : null;
        foreach (Button row in rows)
            if (row != null && row.gameObject == selected) return true;
        return false;
    }

    private void SelectFirstRow()
    {
        if (stock.Length == 0)
        {
            MenuUI.Select(shelfTabs[shelf]);
            ShowEmpty();
            return;
        }
        MenuUI.Select(rows[0]);
        ShowRow(0);
    }

    // SHOPFOOD: show a shelf: its rows, the highlighted tab, and up/down links that wrap through the shelf tab
    private void SetShelf(int index, bool jumpToRows)
    {
        shelf = index;
        stock = shelves[shelf].ToArray();
        for (int i = 0; i < shelfTabs.Length; i++)
        {
            shelfTabs[i].image.sprite = i == shelf ? cat.tabSelected : cat.tab;
            shelfLabels[i].color = i == shelf ? MenuUI.Yellow : MenuUI.Muted;
        }
        for (int i = 0; i < MaxRows; i++) rows[i].gameObject.SetActive(i < stock.Length);
        Selectable first = stock.Length > 0 ? rows[0] : null, last = stock.Length > 0 ? rows[stock.Length - 1] : null;
        for (int i = 0; i < shelfTabs.Length; i++)
            MenuUI.Link(shelfTabs[i], last, first, shelfTabs[(i + shelfTabs.Length - 1) % shelfTabs.Length], shelfTabs[(i + 1) % shelfTabs.Length]);
        for (int i = 0; i < stock.Length; i++)
            MenuUI.Link(rows[i], i == 0 ? shelfTabs[shelf] : rows[i - 1], i == stock.Length - 1 ? shelfTabs[shelf] : rows[i + 1], null, null);
        Refresh();
        if (!jumpToRows) return;
        Sound(cat.selectSound);
        SelectFirstRow();
    }

    private void ShowEmpty()
    {
        detailIcon.color = new Color(1, 1, 1, 0);
        detailName.text = "NOTHING ON THIS SHELF";
        detailOwned.text = "";
        detailText.text = "";
        detailAction.text = "";
    }

    private static Material greyMaterial; // FX: HNLUI with _Grey 1

    private bool SoldOut(ItemDefinition item) => item.Unique && Inventory.Count(item) > 0;

    private void Refresh()
    {
        for (int i = 0; i < stock.Length; i++)
        {
            ItemDefinition item = stock[i];
            rowIcons[i].sprite = item.Icon;
            rowIcons[i].color = item.Icon != null ? Color.white : new Color(1, 1, 1, 0);
            rowNames[i].text = item.DisplayName.ToUpperInvariant();
            bool sold = SoldOut(item);
            rowNames[i].color = sold ? MenuUI.Muted : MenuUI.Cream;
            rowPrices[i].text = sold ? "OWNED" : item.Price.ToString();
            rowPrices[i].color = sold ? MenuUI.Muted : CollectableCollisiondetector.coinCollected >= item.Price ? MenuUI.Yellow : MenuUI.Red;
            // FX: goods Harvey can't afford are shown in greyscale
            bool cantAfford = !sold && CollectableCollisiondetector.coinCollected < item.Price;
            if (greyMaterial == null) greyMaterial = Resources.Load<Material>("FX/UIGrey");
            rowIcons[i].material = cantAfford ? greyMaterial : null;
        }
    }

    private void ShowRow(int index)
    {
        cursor = index;
        if (index >= stock.Length) return;
        ItemDefinition item = stock[index];
        detailIcon.sprite = item.Icon;
        detailIcon.color = item.Icon != null ? Color.white : new Color(1, 1, 1, 0);
        detailName.text = item.DisplayName.ToUpperInvariant();
        int n = Inventory.Count(item);
        detailOwned.text = n > 0 ? "IN YOUR BAG: " + n : "";
        detailText.text = (item.Description ?? "").ToUpperInvariant();
        detailAction.text = SoldOut(item) ? "ALREADY YOURS" : $"CROSS/ENTER: BUY ({item.Price})";
    }

    private void Buy(int index)
    {
        if (index >= stock.Length) return;
        ItemDefinition item = stock[index];
        if (SoldOut(item))
        {
            Sound(cat.blockedSound);
            Message("YOU ALREADY HAVE ONE", MenuUI.Muted);
            return;
        }
        if (CollectableCollisiondetector.coinCollected < item.Price)
        {
            Sound(cat.blockedSound);
            Message("NOT ENOUGH COINS", MenuUI.Red);
            return;
        }
        CollectableCollisiondetector.coinCollected -= item.Price;
        Inventory.Add(item);
        GameSession.SaveProgress(); // like a checkpoint: what you buy is kept even if you quit right after
        Sound(cat.buySound);
        Message("BOUGHT " + item.DisplayName.ToUpperInvariant(), MenuUI.Yellow);
        Refresh();
        ShowRow(cursor);
    }
}
