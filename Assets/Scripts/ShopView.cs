using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// MENUS: a village shop. A shelf of goods with prices on the left and a note about the selected one on the
/// right. Cross/Enter buys with coins; what's bought goes in the Village Goods tab of Harvey's Bag.
/// </summary>
public class ShopView : MenuView
{
    private const int MaxRows = 6;

    private readonly TMP_Text title, coinsText;
    private readonly Button[] rows = new Button[MaxRows];
    private readonly Image[] rowIcons = new Image[MaxRows];
    private readonly TMP_Text[] rowNames = new TMP_Text[MaxRows];
    private readonly TMP_Text[] rowPrices = new TMP_Text[MaxRows];
    private readonly Image detailIcon;
    private readonly TMP_Text detailName, detailOwned, detailText, detailAction;

    private ItemDefinition[] stock = new ItemDefinition[0];
    private int cursor;

    public ShopView(RectTransform parent) : base("VillageShop", parent, Inventory.Catalog.shopPanel)
    {
        var panel = Panel;
        MenuUI.Image("CoinPlate", panel, new Vector2(110, 52), new Vector2(46, 13), cat.frame, true);
        MenuUI.Image("CoinIcon", panel, new Vector2(95, 52), new Vector2(8, 8), cat.coin);
        coinsText = MenuUI.Text("Coins", panel, new Vector2(114, 52), new Vector2(26, 8), 5, MenuUI.Yellow, TextAlignmentOptions.Right, "");
        title = MenuUI.Text("Title", panel, new Vector2(0, 70), new Vector2(200, 10), 8, MenuUI.Yellow, TextAlignmentOptions.Center, "VILLAGE SHOP");

        for (int i = 0; i < MaxRows; i++)
        {
            int index = i;
            var rt = MenuUI.Rect("Row" + i, panel, new Vector2(-64, 46 - 21 * i), new Vector2(150, 19));
            rows[i] = MenuUI.Button(rt, cat.card, cat.cardSelected, true, () => ShowRow(index), () => Buy(index));
            rowIcons[i] = MenuUI.Image("Icon", rt, new Vector2(-62, 0), new Vector2(16, 16), null);
            rowNames[i] = MenuUI.Text("Name", rt, new Vector2(-10, 0), new Vector2(86, 8), 5, MenuUI.Cream, TextAlignmentOptions.Left, "");
            rowPrices[i] = MenuUI.Text("Price", rt, new Vector2(56, 0), new Vector2(30, 8), 5, MenuUI.Yellow, TextAlignmentOptions.Right, "");
        }

        MenuUI.Image("Note", panel, new Vector2(82, -6), new Vector2(118, 96), cat.frame, true);
        detailIcon = MenuUI.Image("Icon", panel, new Vector2(36, 28), new Vector2(16, 16), null);
        detailName = MenuUI.Text("Name", panel, new Vector2(96, 31), new Vector2(84, 16), 5, MenuUI.Yellow, TextAlignmentOptions.Left, "", true);
        detailOwned = MenuUI.Text("Owned", panel, new Vector2(96, 20), new Vector2(84, 7), 5, MenuUI.Aqua, TextAlignmentOptions.Left, "");
        detailText = MenuUI.Text("Text", panel, new Vector2(82, -12), new Vector2(106, 48), 5, MenuUI.Cream, TextAlignmentOptions.TopLeft, "", true);
        detailAction = MenuUI.Text("Action", panel, new Vector2(82, -46), new Vector2(106, 7), 5, MenuUI.Aqua, TextAlignmentOptions.Center, "");
        Hint("CROSS/ENTER: BUY    CIRCLE/ESC: LEAVE", new Vector2(0, -71));
    }

    public void Open(VillageShop shop)
    {
        title.text = shop != null ? shop.ShopName.ToUpperInvariant() : "VILLAGE SHOP";
        stock = shop != null ? shop.Stock : cat.villageStock;
        if (stock.Length > MaxRows) System.Array.Resize(ref stock, MaxRows);
        for (int i = 0; i < MaxRows; i++) rows[i].gameObject.SetActive(i < stock.Length);
        for (int i = 0; i < stock.Length; i++)
            MenuUI.Link(rows[i], rows[(i + stock.Length - 1) % stock.Length], rows[(i + 1) % stock.Length], null, null);
    }

    protected override void OnShow()
    {
        Refresh();
        if (stock.Length == 0) return;
        MenuUI.Select(rows[0]);
        ShowRow(0);
    }

    protected override void OnTick() => coinsText.text = CollectableCollisiondetector.coinCollected.ToString();

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
            rowNames[i].color = sold ? MenuUI.Grey : MenuUI.Cream;
            rowPrices[i].text = sold ? "OWNED" : item.Price.ToString();
            rowPrices[i].color = sold ? MenuUI.Grey : CollectableCollisiondetector.coinCollected >= item.Price ? MenuUI.Yellow : MenuUI.Red;
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
            Message("YOU ALREADY HAVE ONE", MenuUI.Grey);
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
        Message("BOUGHT " + item.DisplayName.ToUpperInvariant(), MenuUI.Aqua);
        Refresh();
        ShowRow(cursor);
    }
}
