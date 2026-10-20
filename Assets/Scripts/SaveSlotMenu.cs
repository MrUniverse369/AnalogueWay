using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// SLOTS: the save file picker the main menu opens from Play and Continue, in the pause menu's style.
/// It lists the SaveSystem.SlotCount files with where each one is, its coins and when it was saved.
///   Play:     an empty file starts a new game in it. A used file asks: CONTINUE it, start a NEW GAME in it
///             (after an OVERWRITE confirmation, CANCEL selected by default) or CANCEL. When every file is in use
///             the hint says to pick one to continue or replace.
///   Continue: picks a used file to continue; empty files can't be picked.
/// Only the chosen file is ever loaded, written or replaced (GameSession.StartNewGame / ContinueSlot).
/// FLOW: whichever way a file is chosen, the Zone Select opens next (Main Menu -> Play -> save file -> Zone Select).
/// Circle/Esc goes back: out of a question, then back to the main menu. Built in code, so the scene needs no setup.
/// </summary>
public class SaveSlotMenu : MonoBehaviour
{
    public enum Mode { Play, Continue }

    private const float CardWidth = 216f, CardHeight = 168f, RowWidth = 196f, RowHeight = 22f, RowStep = 25f;
    private const float DialogWidth = 204f, DialogHeight = 66f, DialogButtonWidth = 52f, DialogButtonHeight = 12f;
    private const float DialogButtonGap = 16f; // room for the paw between answers

    private static SaveSlotMenu instance;

    private HNLCatalog cat;
    private Mode mode;
    private bool busy; // a level is loading: ignore further input
    private GameObject mainMenu; // hidden while the picker is open
    private GameObject reselectOnClose;
    private RectTransform canvasRoot, card, dialog, dialogPanel, paw;
    private TMP_Text title, hint;
    private TMP_Text dialogText;
    private string hintText;
    private Color hintColour;
    private float messageUntil;
    private int muteCursorUntilFrame;
    private int lastRow;
    private readonly Vector3[] corners = new Vector3[4]; // reused by FollowSelection every frame

    private readonly Button[] rows = new Button[SaveSystem.SlotCount];
    private readonly TMP_Text[] rowName = new TMP_Text[SaveSystem.SlotCount];
    private readonly TMP_Text[] rowPlace = new TMP_Text[SaveSystem.SlotCount];
    private readonly TMP_Text[] rowCoins = new TMP_Text[SaveSystem.SlotCount];
    private readonly TMP_Text[] rowDate = new TMP_Text[SaveSystem.SlotCount];
    private readonly Image[] rowCoinIcon = new Image[SaveSystem.SlotCount];
    private readonly SaveData[] files = new SaveData[SaveSystem.SlotCount];
    private readonly List<Button> dialogButtons = new List<Button>();
    private Button dialogDefault;

    // ---------------------------------------------------------------- opening
    /// <summary>Show the save files for Play or Continue (main menu only).</summary>
    public static void Open(Mode mode)
    {
        HNLCatalog catalog = Inventory.Catalog;
        if (catalog == null || catalog.pauseFrame == null || catalog.card == null)
        {
            Fallback(mode);
            return;
        }
        if (instance == null) instance = new GameObject("SaveSlotMenu").AddComponent<SaveSlotMenu>();
        if (instance.canvasRoot == null) instance.Build(catalog);
        instance.Show(mode);
    }

    // without the menu art: continue the last file, or start a new game in an empty one (never overwrites)
    private static void Fallback(Mode mode)
    {
        int active = SaveSystem.SlotNumber(SaveSystem.ActiveSlot);
        if (SaveSystem.HasSave(SaveSystem.ActiveSlot) && GameSession.ContinueSlot(active)) return;
        if (mode == Mode.Continue) return;
        int empty = FirstEmptySlot();
        if (empty > 0) GameSession.StartNewGame(empty);
    }

    private static int FirstEmptySlot()
    {
        for (int n = 1; n <= SaveSystem.SlotCount; n++)
            if (!SaveSystem.HasSave(SaveSystem.SlotKey(n))) return n;
        return 0;
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    // ---------------------------------------------------------------- building
    private void Build(HNLCatalog catalog)
    {
        cat = catalog;
        var canvasGo = new GameObject("SaveSlotCanvas", typeof(RectTransform));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 60; // above the main menu
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(320, 180);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1f;
        scaler.referencePixelsPerUnit = 16;
        canvasGo.AddComponent<GraphicRaycaster>();
        canvasRoot = (RectTransform)canvasGo.transform;

        var shade = MenuUI.Image(Stretch("Shade", canvasRoot), null);
        shade.color = new Color(0.08f, 0.1f, 0.14f, 0.55f);
        shade.raycastTarget = true; // the main menu behind can't be clicked

        float top = CardHeight / 2f;
        card = MenuUI.Image("Card", canvasRoot, Vector2.zero, new Vector2(CardWidth, CardHeight), cat.pauseFrame, true).rectTransform;
        title = MenuUI.Title(card, top - 13, CardWidth - 24, ""); // UI: the standard title

        float y = top - 36f;
        for (int i = 0; i < rows.Length; i++, y -= RowStep) BuildRow(i, y);
        for (int i = 0; i < rows.Length; i++)
            MenuUI.Link(rows[i], rows[(i + rows.Length - 1) % rows.Length], rows[(i + 1) % rows.Length], null, null);

        hint = MenuUI.Text("Hint", card, new Vector2(0, -top + 17), new Vector2(CardWidth - 16, 7), 5, MenuUI.Cream, TextAlignmentOptions.Center, "");
        MenuUI.Text("Controls", card, new Vector2(0, -top + 10), new Vector2(CardWidth - 16, 7), 5, MenuUI.Muted, TextAlignmentOptions.Center,
            "CROSS/ENTER: SELECT   CIRCLE/ESC: BACK");

        BuildDialog();
        paw = MenuUI.Image("Paw", canvasRoot, Vector2.zero, new Vector2(11, 10), cat.paw).rectTransform;
        paw.gameObject.SetActive(false);
        canvasGo.SetActive(false);
    }

    private static RectTransform Stretch(string name, Transform parent)
    {
        var rt = MenuUI.Rect(name, parent, Vector2.zero, Vector2.zero);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;
        return rt;
    }

    private void BuildRow(int i, float y)
    {
        var rt = MenuUI.Rect("File" + (i + 1), card, new Vector2(0, y), new Vector2(RowWidth, RowHeight));
        int index = i;
        rows[i] = MenuUI.Button(rt, cat.card, cat.cardSelected, true, () => OnRowSelected(index), () => OnRowChosen(index));
        float left = -RowWidth / 2f + 8f;
        rowName[i] = MenuUI.Text("Name", rt, new Vector2(left + 60, 4), new Vector2(120, 8), 6, MenuUI.Yellow, TextAlignmentOptions.Left, "");
        rowPlace[i] = MenuUI.Text("Place", rt, new Vector2(left + 60, -5), new Vector2(120, 8), 5, MenuUI.Cream, TextAlignmentOptions.Left, "");
        rowCoinIcon[i] = MenuUI.Image("CoinIcon", rt, new Vector2(RowWidth / 2f - 44, 4), new Vector2(8, 8), cat.coin);
        rowCoins[i] = MenuUI.Text("Coins", rt, new Vector2(RowWidth / 2f - 21, 4), new Vector2(34, 8), 6, MenuUI.Yellow, TextAlignmentOptions.Right, "");
        rowDate[i] = MenuUI.Text("Date", rt, new Vector2(RowWidth / 2f - 30, -5), new Vector2(52, 8), 5, MenuUI.Muted, TextAlignmentOptions.Right, "");
    }

    private void BuildDialog()
    {
        dialog = Stretch("Question", canvasRoot);
        var dim = MenuUI.Image(dialog, null);
        dim.color = new Color(0.08f, 0.1f, 0.14f, 0.5f);
        dim.raycastTarget = true; // the files behind can't be clicked while it asks
        dialogPanel = MenuUI.Image("Panel", dialog, Vector2.zero, new Vector2(DialogWidth, DialogHeight), cat.pauseFrame, true).rectTransform;
        dialogText = MenuUI.Text("Text", dialogPanel, new Vector2(0, 11), new Vector2(DialogWidth - 20, 22), 6, MenuUI.Cream, TextAlignmentOptions.Center, "", true);
        dialog.gameObject.SetActive(false);
    }

    // ---------------------------------------------------------------- showing
    private void Show(Mode newMode)
    {
        mode = newMode;
        busy = false;
        MenueControl menu = FindFirstObjectByType<MenueControl>();
        mainMenu = menu != null ? menu.gameObject : null;
        reselectOnClose = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        if (mainMenu != null) mainMenu.SetActive(false);

        canvasRoot.gameObject.SetActive(true);
        MenuBlur.Show(canvasRoot.GetComponent<Canvas>(), this); // FX:
        CloseDialog(false);
        Refresh();
        title.text = mode == Mode.Play ? "PLAY" : "CONTINUE";

        bool anySaved = false, allFull = true;
        foreach (SaveData f in files)
        {
            anySaved |= f != null;
            allFull &= f != null;
        }
        if (mode == Mode.Continue)
            SetHint(anySaved ? "PICK A FILE TO CONTINUE" : "NO SAVED FILES YET: CHOOSE PLAY TO START ONE", anySaved ? MenuUI.Cream : MenuUI.Yellow);
        else
            SetHint(allFull ? "ALL FILES IN USE: PICK ONE TO CONTINUE OR REPLACE" : "PICK A FILE: EMPTY FOR A NEW GAME, USED TO CONTINUE", allFull ? MenuUI.Yellow : MenuUI.Cream); // FLOW: Zone Select follows

        lastRow = StartRow();
        muteCursorUntilFrame = Time.frameCount + 1; // the first selection isn't a cursor move
        HarveyMenus.PlayUiSound(cat.selectSound);
        MenuUI.Select(rows[lastRow]);
    }

    // Continue: the file played last (else the first saved one). Play: the first empty file (else the one played last).
    private int StartRow()
    {
        int active = SaveSystem.SlotNumber(SaveSystem.ActiveSlot) - 1;
        if (mode == Mode.Continue)
        {
            if (active >= 0 && files[active] != null) return active;
            for (int i = 0; i < files.Length; i++)
                if (files[i] != null) return i;
            return Mathf.Max(0, active);
        }
        for (int i = 0; i < files.Length; i++)
            if (files[i] == null) return i;
        return Mathf.Max(0, active);
    }

    private void Close()
    {
        HarveyMenus.PlayUiSound(cat.backSound);
        CloseDialog(false);
        canvasRoot.gameObject.SetActive(false);
        MenuBlur.Hide(this); // FX:
        if (mainMenu != null) mainMenu.SetActive(true);
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(reselectOnClose);
    }

    private void Refresh()
    {
        int active = SaveSystem.SlotNumber(SaveSystem.ActiveSlot) - 1;
        for (int i = 0; i < files.Length; i++)
        {
            files[i] = SaveSystem.TryLoad(out SaveData data, SaveSystem.SlotKey(i + 1)) ? data : null;
            SaveData f = files[i];
            bool used = f != null;
            bool usable = used || mode == Mode.Play;
            rowName[i].text = $"FILE {i + 1}" + (used && i == active ? "  <color=#98b2b1>LAST PLAYED</color>" : "");
            rowName[i].color = usable ? MenuUI.Yellow : MenuUI.Muted;
            rowPlace[i].text = used ? PlaceName(f.sceneName) : mode == Mode.Play ? "EMPTY: START A NEW GAME" : "EMPTY";
            rowPlace[i].color = used ? MenuUI.Cream : MenuUI.Muted;
            rowCoinIcon[i].enabled = used;
            rowCoins[i].text = used ? f.coins.ToString() : "";
            rowDate[i].text = used ? SavedWhen(f.savedAtUtc) : "";
        }
    }

    // ---------------------------------------------------------------- choosing
    private void OnRowSelected(int i)
    {
        lastRow = i;
        PlayCursorSound();
    }

    private void OnRowChosen(int i)
    {
        if (busy || dialog.gameObject.activeSelf) return;
        lastRow = i; // a question closes back onto this file
        int number = i + 1;
        if (mode == Mode.Continue)
        {
            if (files[i] == null)
            {
                HarveyMenus.PlayUiSound(cat.blockedSound);
                Message($"FILE {number} IS EMPTY");
                return;
            }
            ContinueFile(number);
            return;
        }
        if (files[i] == null)
        {
            StartFile(number);
            return;
        }
        HarveyMenus.PlayUiSound(cat.selectSound);
        Ask($"FILE {number}: {PlaceName(files[i].sceneName)}",
            ("CONTINUE", () => ContinueFile(number)),
            ("NEW GAME", () => ConfirmOverwrite(number)),
            ("CANCEL", () => CloseDialog(true)));
    }

    private void ConfirmOverwrite(int number)
    {
        HarveyMenus.PlayUiSound(cat.selectSound);
        Ask($"START OVER IN FILE {number}?\nITS SAVED PROGRESS WILL BE LOST.",
            ("OVERWRITE", () => StartFile(number)),
            ("CANCEL", () => CloseDialog(true)));
        dialogDefault = dialogButtons[1]; // erasing a file is never the default
        MenuUI.Select(dialogDefault);
    }

    private void ContinueFile(int number)
    {
        if (busy) return;
        busy = true;
        HarveyMenus.PlayUiSound(cat.selectSound);
        if (GameSession.ContinueSlot(number)) return; // FLOW: the Zone Select is loading
        busy = false;
        CloseDialog(true);
        HarveyMenus.PlayUiSound(cat.blockedSound);
        Message($"FILE {number} CAN'T BE LOADED");
    }

    private void StartFile(int number)
    {
        if (busy) return;
        busy = true;
        HarveyMenus.PlayUiSound(cat.equipSound);
        GameSession.StartNewGame(number);
    }

    // ---------------------------------------------------------------- the question box
    private void Ask(string question, params (string label, Action action)[] answers)
    {
        foreach (Button b in dialogButtons) Destroy(b.gameObject);
        dialogButtons.Clear();
        dialogText.text = question;

        float step = DialogButtonWidth + DialogButtonGap;
        float x = -(answers.Length - 1) * step / 2f;
        foreach (var (label, action) in answers)
        {
            var rt = MenuUI.Rect(label, dialogPanel, new Vector2(x, -15), new Vector2(DialogButtonWidth, DialogButtonHeight));
            Button b = MenuUI.TextButton(rt, label, PlayCursorSound, () => { if (!busy) action(); }); // UI: text answers
            dialogButtons.Add(b);
            x += step;
        }
        for (int i = 0; i < dialogButtons.Count; i++)
        {
            Button left = dialogButtons[(i + dialogButtons.Count - 1) % dialogButtons.Count];
            Button right = dialogButtons[(i + 1) % dialogButtons.Count];
            MenuUI.Link(dialogButtons[i], null, null, left, right);
        }
        dialog.gameObject.SetActive(true);
        dialog.SetAsLastSibling();
        paw.SetAsLastSibling();
        dialogDefault = dialogButtons[0];
        muteCursorUntilFrame = Time.frameCount + 1;
        MenuUI.Select(dialogDefault);
    }

    private void CloseDialog(bool reselectRow)
    {
        if (dialog == null || !dialog.gameObject.activeSelf) return;
        dialog.gameObject.SetActive(false);
        if (!reselectRow) return;
        HarveyMenus.PlayUiSound(cat.backSound);
        muteCursorUntilFrame = Time.frameCount + 1;
        MenuUI.Select(rows[lastRow]);
    }

    // ---------------------------------------------------------------- every frame
    private void Update()
    {
        if (canvasRoot == null || !canvasRoot.gameObject.activeSelf || busy) return;
        if (messageUntil > 0f && Time.unscaledTime > messageUntil)
        {
            messageUntil = 0f;
            hint.text = hintText;
            hint.color = hintColour;
        }
        if (BackPressed())
        {
            if (dialog.gameObject.activeSelf) CloseDialog(true);
            else Close();
            return;
        }
        KeepSelection();
        FollowSelection();
    }

    // a mouse click on empty space (or the main menu's own selection keeper) mustn't leave nothing to navigate from
    private void KeepSelection()
    {
        if (EventSystem.current == null) return;
        GameObject selected = EventSystem.current.currentSelectedGameObject;
        bool inDialog = dialog.gameObject.activeSelf;
        bool ours = selected != null && selected.activeInHierarchy && selected.transform.IsChildOf(inDialog ? dialogPanel : card);
        if (ours) return;
        muteCursorUntilFrame = Time.frameCount + 1;
        MenuUI.Select(inDialog ? dialogDefault : rows[lastRow]);
    }

    // the paw sits just left of the selected file and nudges toward it, like on the main menu (UI: the answers are
    // text buttons, which bring their own paw)
    private void FollowSelection()
    {
        GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        if (selected != null && selected.GetComponent<MenuTextButton>() != null) selected = null;
        var target = selected != null ? selected.transform as RectTransform : null;
        paw.gameObject.SetActive(target != null);
        if (target == null) return;
        target.GetWorldCorners(corners);
        Vector3 left = (corners[0] + corners[1]) * 0.5f;
        float scale = paw.lossyScale.x;
        float nudge = Mathf.Round(Mathf.Sin(Time.unscaledTime * 6f) * 1.5f);
        paw.position = left + new Vector3((-3f - paw.rect.width * 0.5f + nudge) * scale, 0f, 0f);
    }

    private static bool BackPressed()
    {
        Keyboard kb = Keyboard.current;
        Gamepad pad = Gamepad.current;
        return (kb != null && (kb.escapeKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame))
            || (pad != null && pad.buttonEast.wasPressedThisFrame);
    }

    private void PlayCursorSound()
    {
        if (Time.frameCount > muteCursorUntilFrame) HarveyMenus.PlayUiSound(cat.moveSound);
    }

    private void SetHint(string text, Color colour)
    {
        hintText = text;
        hintColour = colour;
        messageUntil = 0f;
        hint.text = text;
        hint.color = colour;
    }

    private void Message(string text)
    {
        hint.text = text;
        hint.color = MenuUI.Red;
        messageUntil = Time.unscaledTime + 2f;
    }

    // ---------------------------------------------------------------- labels
    private static string SavedWhen(string savedAtUtc)
    {
        if (!DateTime.TryParse(savedAtUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime when)) return "";
        return when.ToLocalTime().ToString("d MMM  HH:mm", CultureInfo.InvariantCulture).ToUpperInvariant();
    }

    // the zone select's names for each level (ZoneSelect scene, ZoneMapNode zoneName + levelName)
    private static readonly Dictionary<string, string> Places = new Dictionary<string, string>
    {
        { "Z1A1Tomb", "THE OLD TOMB - ACT 1" }, { "Z1A2Tomb", "THE OLD TOMB - ACT 2" }, { "Z1A3Tomb", "THE OLD TOMB - ACT 3" },
        { "Z1A4Tomb", "THE OLD TOMB - ACT 4" }, { "Z1A5Tomb", "THE OLD TOMB - ACT 5" }, { "Z1BossTomb", "THE OLD TOMB - BOSS" },
        { "Z2A1Sandy", "SANDY DUNES - ACT 1" }, { "Z2A2Sandy", "SANDY DUNES - ACT 2" }, { "Z2A3Sandy", "SANDY DUNES - ACT 3" },
        { "Z2A4Sandy", "SANDY DUNES - ACT 4" }, { "Z2A5Sandy", "SANDY DUNES - ACT 5" }, { "Z2BossSandy", "SANDY DUNES - BOSS" },
        { "Z3A1Japan", "SUNLIT SHRINE ROAD - ACT 1" }, { "Z3A2Japan", "SUNLIT SHRINE ROAD - ACT 2" },
        { "Z3A3Japan", "SUNLIT SHRINE ROAD - ACT 3" }, { "Z3A4Japan", "SUNLIT SHRINE ROAD - ACT 4" },
        { "Z3JapanVillage", "SNOW TEMPLE PASS - VILLAGE" }, { "Z3BossBattle", "SNOW TEMPLE PASS - BOSS" },
        { "Z4A6GrassGreen", "GRASSY MEADOW - ACT 1" }, { "Z4A7GrassGreen", "GRASSY MEADOW - ACT 2" },
        { "Z4A8GrassGreen", "GRASSY MEADOW - ACT 3" }, { "Z4A9GrassGreen", "GRASSY MEADOW - ACT 4" },
        { "Z4A10GrassGreen", "GRASSY MEADOW - ACT 5" }, { "Z4BossGrass", "GRASSY MEADOW - BOSS" },
        { "Win", "JOURNEY'S END" },
        // VILLAGES (2026-10-20)
        { "Z1TombVillage", "THE OLD TOMB - VILLAGE" }, { "Z2SandyVillage", "SANDY DUNES - VILLAGE" }, { "Z4GrassVillage", "GRASSY MEADOW - VILLAGE" },
    };

    private static string PlaceName(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName)) return "UNKNOWN";
        return Places.TryGetValue(sceneName, out string place) ? place : sceneName.ToUpperInvariant();
    }
}
