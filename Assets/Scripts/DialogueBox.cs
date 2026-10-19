using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// DIALOGUE: plays a DialogueScript in a text box at the bottom of the screen, in the HNL UI style (the HUD plate,
/// a Yellow name, Cream text typed out, Muted hints, plain-text choices with the paw). It creates itself the first
/// time a conversation starts. While it is open the game is paused like the shop.
///
/// Controls: Cross / Enter / E shows the whole line, then goes on; Up/Down (W/S, arrows, d-pad, stick) picks a
/// choice; Circle / Esc / Backspace leaves the conversation. Space and Triangle are Jump, so they are not used.
/// </summary>
public class DialogueBox : MonoBehaviour
{
    private const float CharsPerSecond = 45f;
    private const int MaxJumps = 64; // a loop of -> with no lines in between ends the conversation instead of hanging
    private const int MaxChoices = 5;

    private static DialogueBox instance;
    private static int closedFrame = -1;
    private static readonly Dictionary<string, int> cycles = new Dictionary<string, int>(); // -> cycle: progress, per villager

    private RectTransform box, choicePanel;
    private TMP_Text nameText, bodyText, hintText;
    private readonly List<Button> choiceButtons = new List<Button>();
    private readonly List<TMP_Text> choiceLabels = new List<TMP_Text>();

    private DialogueScript script;
    private List<DialogueScript.Step> steps;
    private string nodeName;
    private int index;
    private Villager villager;
    private List<DialogueScript.Choice> choices;
    private int choiceIndex;
    private float typed;
    private bool typing;
    private int openedFrame;
    private bool closing;
    private Rigidbody2D playerBody;
    private float lastNav;

    /// <summary>True while a conversation is on screen.</summary>
    public static bool IsOpen => instance != null && instance.script != null;

    /// <summary>MenueControl skips its pause toggle while talking (and on the frame Esc ends a conversation).</summary>
    public static bool BlocksPause => IsOpen || closedFrame == Time.frameCount;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
        closedFrame = -1;
        cycles.Clear();
    }

    /// <summary>Starts a conversation at a node. Returns false if one can't start now (another menu, a freeze).</summary>
    public static bool Open(DialogueScript script, string node, Villager who)
    {
        if (script == null || IsOpen || MenueControl.isGamePaused || Time.timeScale == 0f) return false;
        if (!script.HasNode(node))
        {
            Debug.LogWarning($"DialogueBox: {script.source} has no node '{node}'.");
            return false;
        }
        if (instance == null) instance = Build();
        instance.Begin(script, node, who);
        return true;
    }

    // ---------------------------------------------------------------- running
    private void Begin(DialogueScript s, string node, Villager who)
    {
        script = s;
        villager = who;
        openedFrame = Time.frameCount;
        closing = false;
        Time.timeScale = 0f;
        MenueControl.isGamePaused = true;
        PlayerController player = FindFirstObjectByType<PlayerController>();
        playerBody = player != null ? player.GetComponent<Rigidbody2D>() : null;
        box.gameObject.SetActive(true);
        HarveyMenus.PlayUiSound(Inventory.Catalog != null ? Inventory.Catalog.selectSound : null);
        GoTo(node);
    }

    private void GoTo(string node)
    {
        nodeName = node;
        steps = script.Node(node);
        index = 0;
        Continue();
    }

    // runs steps until something needs the player: a line to read, choices to pick, or the end
    private void Continue()
    {
        HideChoices();
        for (int jumps = 0; jumps < MaxJumps; jumps++)
        {
            if (steps == null || index >= steps.Count) { Close(); return; }
            DialogueScript.Step step = steps[index++];
            switch (step.kind)
            {
                case DialogueScript.StepKind.Line:
                    ShowLine(step);
                    return;
                case DialogueScript.StepKind.Choices:
                    ShowChoices(step.choices);
                    return;
                case DialogueScript.StepKind.Goto:
                    string target = Pick(step);
                    if (target == DialogueScript.End) { Close(); return; }
                    nodeName = target;
                    steps = script.Node(target);
                    index = 0;
                    break;
            }
        }
        Debug.LogWarning($"DialogueBox: {script.source} keeps jumping without a line (near node '{nodeName}'); ending.");
        Close();
    }

    private string Pick(DialogueScript.Step step)
    {
        if (step.targets.Length == 1 || step.mode == "go") return step.targets[0];
        if (step.mode == "random") return step.targets[Random.Range(0, step.targets.Length)];
        // cycle: per villager and per -> line, kept for this play session
        string key = (villager != null ? villager.Key : "") + "|" + script.source + "|" + step.lineNumber;
        cycles.TryGetValue(key, out int n);
        cycles[key] = n + 1;
        return step.targets[n % step.targets.Length];
    }

    private void ShowLine(DialogueScript.Step step)
    {
        string speaker = Fill(step.speaker);
        nameText.text = speaker.ToUpperInvariant();
        nameText.gameObject.SetActive(speaker.Length > 0);
        bodyText.text = Fill(step.text);
        bodyText.ForceMeshUpdate();
        bodyText.maxVisibleCharacters = 0;
        typed = 0f;
        typing = true;
        hintText.text = "CROSS/ENTER: NEXT   CIRCLE/ESC: LEAVE";
    }

    private string Fill(string s) =>
        s.Replace("{name}", villager != null ? villager.DisplayName : "Villager").Replace("{player}", "Harvey");

    private void ShowChoices(List<DialogueScript.Choice> list)
    {
        choices = list;
        int count = Mathf.Min(list.Count, MaxChoices);
        float width = 60f;
        for (int i = 0; i < count; i++) width = Mathf.Max(width, choiceLabels[i].GetPreferredValues(Fill(list[i].text).ToUpperInvariant()).x + 30f);
        width = Mathf.Min(width, 200f);
        choicePanel.sizeDelta = new Vector2(width, count * 11f + 8f);
        for (int i = 0; i < choiceButtons.Count; i++)
        {
            bool used = i < count;
            choiceButtons[i].gameObject.SetActive(used);
            if (!used) continue;
            choiceLabels[i].text = Fill(list[i].text).ToUpperInvariant();
            var rt = (RectTransform)choiceButtons[i].transform;
            rt.sizeDelta = new Vector2(width - 8f, 10f);
            choiceLabels[i].rectTransform.sizeDelta = rt.sizeDelta - new Vector2(4, 0);
            rt.anchoredPosition = new Vector2(0f, (count - 1) * 5.5f - i * 11f);
        }
        choicePanel.gameObject.SetActive(true);
        choiceIndex = 0;
        MenuUI.Select(choiceButtons[0]);
        hintText.text = "UP/DOWN: CHOOSE   CROSS/ENTER: SAY IT";
    }

    private void HideChoices()
    {
        choices = null;
        if (choicePanel != null) choicePanel.gameObject.SetActive(false);
    }

    private void Choose(int i)
    {
        if (choices == null || i < 0 || i >= choices.Count) return;
        string target = choices[i].target;
        HarveyMenus.PlayUiSound(Inventory.Catalog != null ? Inventory.Catalog.selectSound : null);
        HideChoices();
        if (target == DialogueScript.End) { Close(); return; }
        GoTo(target);
    }

    private void Close()
    {
        if (script == null) return;
        script = null;
        steps = null;
        HideChoices();
        box.gameObject.SetActive(false);
        closedFrame = Time.frameCount;
        closing = true; // the game resumes in LateUpdate, after the player has read this frame's input
        HarveyMenus.PlayUiSound(Inventory.Catalog != null ? Inventory.Catalog.backSound : null);
        if (villager != null) villager.OnConversationEnded();
    }

    // ---------------------------------------------------------------- input
    private void Update()
    {
        if (script == null || Time.frameCount == openedFrame) return; // not on the press that started it
        if (BackPressed()) { Close(); return; }

        if (choices != null)
        {
            int move = NavPressed();
            if (move != 0)
            {
                int count = Mathf.Min(choices.Count, MaxChoices);
                choiceIndex = (choiceIndex + move + count) % count;
                MenuUI.Select(choiceButtons[choiceIndex]);
                HarveyMenus.PlayUiSound(Inventory.Catalog != null ? Inventory.Catalog.moveSound : null);
            }
            if (ConfirmPressed()) Choose(choiceIndex);
            return;
        }

        if (typing)
        {
            typed += Time.unscaledDeltaTime * CharsPerSecond;
            int total = bodyText.textInfo.characterCount;
            bodyText.maxVisibleCharacters = Mathf.Min(total, (int)typed);
            if ((int)typed >= total) typing = false;
            if (ConfirmPressed())
            {
                bodyText.maxVisibleCharacters = total; // first press: the whole line at once
                typing = false;
            }
            return;
        }
        if (ConfirmPressed()) Continue();
    }

    private void LateUpdate()
    {
        if (script != null && playerBody != null) playerBody.linearVelocity = Vector2.zero; // no jump queued while paused
        if (!closing) return;
        closing = false;
        if (playerBody != null) playerBody.linearVelocity = Vector2.zero;
        Time.timeScale = 1f;
        MenueControl.isGamePaused = false;
        if (PowerUpManager.Instance != null) PowerUpManager.Instance.CancelQueuedPresses();
    }

    private static bool ConfirmPressed()
    {
        Keyboard kb = Keyboard.current;
        Gamepad pad = Gamepad.current;
        return (kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame))
            || (pad != null && pad.buttonSouth.wasPressedThisFrame);
    }

    private static bool BackPressed()
    {
        Keyboard kb = Keyboard.current;
        Gamepad pad = Gamepad.current;
        return (kb != null && (kb.escapeKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame))
            || (pad != null && pad.buttonEast.wasPressedThisFrame);
    }

    // -1 up, +1 down, on the press (and repeating while a stick is held)
    private int NavPressed()
    {
        Keyboard kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame) return -1;
            if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame) return 1;
        }
        Gamepad pad = Gamepad.current;
        if (pad == null) return 0;
        if (pad.dpad.up.wasPressedThisFrame) return -1;
        if (pad.dpad.down.wasPressedThisFrame) return 1;
        float y = pad.leftStick.ReadValue().y;
        if (Mathf.Abs(y) < 0.6f) { lastNav = 0f; return 0; }
        if (Time.unscaledTime - lastNav < 0.25f) return 0;
        lastNav = Time.unscaledTime;
        return y > 0 ? -1 : 1;
    }

    // ---------------------------------------------------------------- the box
    private static DialogueBox Build()
    {
        var go = new GameObject("DialogueBox");
        var me = go.AddComponent<DialogueBox>();
        var canvasGo = new GameObject("DialogueCanvas", typeof(RectTransform));
        canvasGo.transform.SetParent(go.transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 95; // over the HUD, under the Talisman Holder/Bag/shop canvas
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(320, 180);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1f;
        scaler.referencePixelsPerUnit = 16;
        canvasGo.AddComponent<GraphicRaycaster>();
        Transform root = canvasGo.transform;
        Sprite frame = Inventory.Catalog != null ? Inventory.Catalog.frame : null;

        // the text box along the bottom
        me.box = MenuUI.Rect("Box", root, Vector2.zero, new Vector2(296, 50));
        me.box.anchorMin = me.box.anchorMax = me.box.pivot = new Vector2(0.5f, 0f);
        me.box.anchoredPosition = new Vector2(0, 6);
        MenuUI.Image(me.box, frame, true);
        me.nameText = MenuUI.Text("Name", me.box, new Vector2(0, 17), new Vector2(280, 8), MenuUI.BodySize, MenuUI.Yellow, TextAlignmentOptions.Left, "");
        me.bodyText = MenuUI.Text("Line", me.box, new Vector2(0, 0), new Vector2(280, 26), MenuUI.BodySize, MenuUI.Cream, TextAlignmentOptions.TopLeft, "", true);
        me.bodyText.lineSpacing = 0f;
        me.bodyText.rectTransform.anchoredPosition = new Vector2(0, -1);
        me.hintText = MenuUI.Text("Hint", me.box, new Vector2(0, -19), new Vector2(280, 7), MenuUI.SmallSize, MenuUI.Muted, TextAlignmentOptions.Right, "");

        // the choices, in a plate over the box's right end
        me.choicePanel = MenuUI.Rect("Choices", root, Vector2.zero, new Vector2(120, 40));
        me.choicePanel.anchorMin = me.choicePanel.anchorMax = me.choicePanel.pivot = new Vector2(1f, 0f);
        me.choicePanel.anchoredPosition = new Vector2(-12, 60);
        MenuUI.Image(me.choicePanel, frame, true);
        for (int i = 0; i < MaxChoices; i++)
        {
            RectTransform rt = MenuUI.Rect("Choice" + i, me.choicePanel, Vector2.zero, new Vector2(110, 10));
            Button b = MenuUI.TextButton(rt, "", null, null);
            b.navigation = new Navigation { mode = Navigation.Mode.None }; // DialogueBox moves the cursor itself
            TMP_Text label = rt.GetComponentInChildren<TMP_Text>();
            label.fontSize = MenuUI.BodySize; // centred like every TextButton, so the paw lines up with the words
            int captured = i;
            b.onClick.AddListener(() => { if (Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame) me.Choose(captured); });
            me.choiceButtons.Add(b);
            me.choiceLabels.Add(label);
        }
        me.choicePanel.gameObject.SetActive(false);
        me.box.gameObject.SetActive(false);
        return me;
    }

    private void OnDestroy()
    {
        if (instance != this) return;
        instance = null;
        if (script != null)
        {
            // the scene unloaded mid-conversation
            Time.timeScale = 1f;
            MenueControl.isGamePaused = false;
        }
    }
}
