using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// MENUS: Harvey's Talisman Holder. Three equipped slots on top (Z/Square, X/Cross, C/Circle) and the six
/// talismans below. Cross/Enter on a talisman equips it into a free slot or takes it off; when all three slots
/// are full it is picked up and the next slot chosen is replaced. Cross on a slot picks it up, then another slot
/// swaps them, a talisman goes into it, or the same slot again empties it. Circle/Esc drops a pick, then closes.
/// </summary>
public class TalismanHolderView : MenuView
{
    private static readonly string[] SlotKeys = { "Z / SQUARE", "X / CROSS", "C / CIRCLE" };
    private static readonly string[] SlotLetters = { "Z", "X", "C" };

    private readonly Button[] sockets = new Button[Inventory.SlotCount];
    private readonly Image[] socketIcons = new Image[Inventory.SlotCount];
    private readonly Button[] cards;
    private readonly Image[] cardIcons;
    private readonly TMP_Text[] cardNames;
    private readonly TMP_Text[] cardBadges;
    private readonly TMP_Text detailName, detailText, detailStats;

    private int heldSocket = -1, heldCard = -1;
    private int lastCard;

    public TalismanHolderView(RectTransform parent) : base("TalismanHolder", parent, Inventory.Catalog.holderPanel)
    {
        var panel = Panel;
        MenuUI.Text("Title", panel, new Vector2(0, 70), new Vector2(200, 10), 8, MenuUI.Yellow, TextAlignmentOptions.Center, "TALISMAN HOLDER");

        for (int i = 0; i < Inventory.SlotCount; i++)
        {
            int slot = i;
            var rt = MenuUI.Rect("Slot" + i, panel, new Vector2(-56 + 56 * i, 44), new Vector2(28, 28));
            sockets[i] = MenuUI.Button(rt, cat.socket, cat.socketSelected, false, () => ShowSlot(slot), () => SubmitSlot(slot));
            socketIcons[i] = MenuUI.Image("Icon", rt, Vector2.zero, new Vector2(16, 16), null);
            MenuUI.Text("Key", panel, new Vector2(-56 + 56 * i, 25), new Vector2(54, 7), 5, MenuUI.Cream, TextAlignmentOptions.Center, SlotKeys[i]);
        }

        int count = cat.talismans.Length;
        cards = new Button[count];
        cardIcons = new Image[count];
        cardNames = new TMP_Text[count];
        cardBadges = new TMP_Text[count];
        for (int i = 0; i < count; i++)
        {
            int index = i;
            var pos = new Vector2(-92 + 92 * (i % 3), 6 - 24 * (i / 3));
            var rt = MenuUI.Rect("Talisman" + i, panel, pos, new Vector2(88, 22));
            cards[i] = MenuUI.Button(rt, cat.card, cat.cardSelected, true, () => ShowCard(index), () => SubmitCard(index));
            cardIcons[i] = MenuUI.Image("Icon", rt, new Vector2(-33, 0), new Vector2(16, 16), null);
            cardNames[i] = MenuUI.Text("Name", rt, new Vector2(10, 0), new Vector2(62, 8), 5, MenuUI.Cream, TextAlignmentOptions.Left, "");
            cardBadges[i] = MenuUI.Text("Badge", rt, new Vector2(37, 6), new Vector2(10, 7), 5, MenuUI.Yellow, TextAlignmentOptions.Right, "");
        }

        MenuUI.Image("Details", panel, new Vector2(0, -46), new Vector2(276, 32), cat.frame, true);
        detailName = MenuUI.Text("Name", panel, new Vector2(-6, -36), new Vector2(256, 8), 6, MenuUI.Yellow, TextAlignmentOptions.Left, "");
        detailStats = MenuUI.Text("Stats", panel, new Vector2(-6, -36), new Vector2(256, 8), 5, MenuUI.Aqua, TextAlignmentOptions.Right, "");
        detailText = MenuUI.Text("Text", panel, new Vector2(-6, -50), new Vector2(256, 18), 5, MenuUI.Cream, TextAlignmentOptions.TopLeft, "", true);
        Hint("CROSS/ENTER: EQUIP OR SWAP    CIRCLE/ESC: BACK", new Vector2(0, -71));

        WireNavigation();
    }

    private int Columns => 3;

    private void WireNavigation()
    {
        int n = cards.Length, rows = (n + Columns - 1) / Columns;
        for (int i = 0; i < sockets.Length; i++)
        {
            Button left = sockets[(i + sockets.Length - 1) % sockets.Length], right = sockets[(i + 1) % sockets.Length];
            Button below = cards[Mathf.Min(i, n - 1)];
            Button bottom = cards[Mathf.Min((rows - 1) * Columns + i, n - 1)];
            MenuUI.Link(sockets[i], bottom, below, left, right);
        }
        for (int i = 0; i < n; i++)
        {
            int row = i / Columns, col = i % Columns;
            int rowStart = row * Columns, rowLen = Mathf.Min(Columns, n - rowStart);
            Button left = cards[rowStart + (col + rowLen - 1) % rowLen], right = cards[rowStart + (col + 1) % rowLen];
            Selectable up = row == 0 ? sockets[Mathf.Min(col, sockets.Length - 1)] : (Selectable)cards[i - Columns];
            Selectable down = row == rows - 1 ? sockets[Mathf.Min(col, sockets.Length - 1)] : (Selectable)cards[Mathf.Min(i + Columns, n - 1)];
            MenuUI.Link(cards[i], up, down, left, right);
        }
    }

    protected override void OnShow()
    {
        heldSocket = heldCard = -1;
        Refresh();
        MenuUI.Select(sockets[0]);
        ShowSlot(0);
    }

    public override bool HandleBack()
    {
        if (heldSocket < 0 && heldCard < 0) return false;
        int card = heldCard;
        heldSocket = heldCard = -1;
        ClearMessage();
        Refresh();
        if (card >= 0) MenuUI.Select(cards[card]);
        return true;
    }

    protected override void OnTick()
    {
        // the picked-up slot or talisman pulses while the player chooses where it goes
        float pulse = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 10f);
        Color held = new Color(1f, 0.9f * pulse + 0.1f, 0.5f * pulse + 0.2f);
        for (int i = 0; i < sockets.Length; i++) sockets[i].image.color = i == heldSocket ? held : Color.white;
        for (int i = 0; i < cards.Length; i++) cards[i].image.color = i == heldCard ? held : Color.white;
    }

    private void Refresh()
    {
        for (int i = 0; i < sockets.Length; i++)
        {
            Talisman t = Inventory.Equipped(i);
            socketIcons[i].sprite = t != null ? t.Icon : null;
            socketIcons[i].color = t != null && t.Icon != null ? Color.white : new Color(1, 1, 1, 0);
        }
        for (int i = 0; i < cards.Length; i++)
        {
            Talisman t = cat.talismans[i];
            bool owned = Inventory.Owns(t);
            cardIcons[i].sprite = owned ? t.Icon : cat.lockedTalisman;
            cardIcons[i].color = Color.white;
            cardNames[i].text = owned ? t.DisplayName.ToUpperInvariant() : "? ? ?";
            cardNames[i].color = owned ? MenuUI.Cream : MenuUI.Grey;
            int slot = Inventory.SlotOf(t);
            cardBadges[i].text = slot >= 0 ? SlotLetters[slot] : "";
        }
    }

    // ---------------------------------------------------------------- details
    private void ShowSlot(int slot)
    {
        Talisman t = Inventory.Equipped(slot);
        if (t == null)
        {
            detailName.text = SlotKeys[slot] + " SLOT";
            detailStats.text = "";
            detailText.text = "EMPTY. PICK A TALISMAN BELOW TO PUT IN THIS SLOT.";
            return;
        }
        Describe(t);
    }

    private void ShowCard(int index)
    {
        lastCard = index;
        Talisman t = cat.talismans[index];
        if (!Inventory.Owns(t))
        {
            detailName.text = "LOCKED TALISMAN";
            detailStats.text = "";
            detailText.text = string.IsNullOrEmpty(t.UnlockHint) ? "NOT FOUND YET." : t.UnlockHint.ToUpperInvariant();
            return;
        }
        Describe(t);
    }

    private void Describe(Talisman t)
    {
        int slot = Inventory.SlotOf(t);
        detailName.text = t.DisplayName.ToUpperInvariant() + (slot >= 0 ? "  (" + SlotLetters[slot] + ")" : "");
        detailStats.text = $"LASTS {t.Duration:0.#}S" + (t.Cooldown > 0f ? $"  REST {t.Cooldown:0.#}S" : "");
        detailText.text = (t.Description ?? "").ToUpperInvariant();
    }

    // ---------------------------------------------------------------- choosing
    private void SubmitCard(int index)
    {
        Talisman t = cat.talismans[index];
        if (!Inventory.Owns(t))
        {
            Sound(cat.blockedSound);
            Message("YOU HAVEN'T FOUND THIS TALISMAN YET", MenuUI.Red);
            return;
        }

        if (heldSocket >= 0)
        {
            Inventory.SetEquipped(heldSocket, t);
            Done(cards[index], t.DisplayName.ToUpperInvariant() + " EQUIPPED IN " + SlotLetters[heldSocket]);
            return;
        }
        if (heldCard >= 0)
        {
            heldCard = heldCard == index ? -1 : index;
            if (heldCard < 0) ClearMessage();
            else Message("CHOOSE A SLOT FOR " + t.DisplayName.ToUpperInvariant(), MenuUI.Yellow, 0f);
            Sound(cat.selectSound);
            return;
        }

        int slot = Inventory.SlotOf(t);
        if (slot >= 0)
        {
            Inventory.SetEquipped(slot, null);
            Done(cards[index], t.DisplayName.ToUpperInvariant() + " TAKEN OFF");
            return;
        }
        for (int i = 0; i < Inventory.SlotCount; i++)
        {
            if (Inventory.Equipped(i) != null) continue;
            Inventory.SetEquipped(i, t);
            Done(cards[index], t.DisplayName.ToUpperInvariant() + " EQUIPPED IN " + SlotLetters[i]);
            return;
        }
        // all three slots are full: pick it up and let the player choose which one to replace
        heldCard = index;
        Sound(cat.selectSound);
        Message("ALL SLOTS FULL: CHOOSE ONE TO REPLACE", MenuUI.Yellow, 0f);
        MenuUI.Select(sockets[0]);
    }

    private void SubmitSlot(int slot)
    {
        if (heldCard >= 0)
        {
            Talisman t = cat.talismans[heldCard];
            int card = heldCard;
            Inventory.SetEquipped(slot, t);
            Done(cards[card], t.DisplayName.ToUpperInvariant() + " EQUIPPED IN " + SlotLetters[slot]);
            return;
        }
        if (heldSocket >= 0)
        {
            if (heldSocket == slot)
            {
                Talisman t = Inventory.Equipped(slot);
                Inventory.SetEquipped(slot, null);
                Done(sockets[slot], t != null ? t.DisplayName.ToUpperInvariant() + " TAKEN OFF" : "SLOT LEFT EMPTY");
            }
            else
            {
                Inventory.SwapSlots(heldSocket, slot);
                Done(sockets[slot], "SWAPPED " + SlotLetters[heldSocket] + " AND " + SlotLetters[slot]);
            }
            return;
        }
        heldSocket = slot;
        Sound(cat.selectSound);
        Message(Inventory.Equipped(slot) != null
            ? "PICK A SLOT TO SWAP, A TALISMAN TO PUT HERE, OR THIS SLOT TO EMPTY IT"
            : "PICK A TALISMAN TO PUT IN " + SlotLetters[slot], MenuUI.Yellow, 0f);
        if (Inventory.Equipped(slot) == null) MenuUI.Select(cards[lastCard]);
    }

    private void Done(Selectable reselect, string message)
    {
        heldSocket = heldCard = -1;
        Sound(cat.equipSound);
        Refresh();
        Message(message, MenuUI.Aqua);
        MenuUI.Select(reselect);
    }
}
