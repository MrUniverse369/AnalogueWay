using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// DIALOGUE: someone Harvey can talk to. When he stands next to them on the ground a prompt shows ("UP / E: TALK
/// TO HANA", the same keys as the shop counter) and Up / E starts their conversation from the dialogue file at
/// Start Node. Only the nearest villager in reach answers, and a shop counter Harvey is standing at comes first.
/// Set up the Japan village's villagers with Tools/HNL/Set Up Village Villagers; write their lines in
/// Assets/Dialogue (the format is described in DialogueScript).
/// </summary>
public class Villager : MonoBehaviour
{
    [SerializeField] private string displayName = "Villager";
    [Tooltip("The dialogue file (.txt in Assets/Dialogue).")]
    [SerializeField] private TextAsset dialogue;
    [Tooltip("The node the conversation starts at (== name in the file).")]
    [SerializeField] private string startNode = "start";
    [Tooltip("How close Harvey must stand (world units, sideways).")]
    [SerializeField] private float talkRange = 1.4f;
    [Tooltip("How far above or below the villager's feet Harvey can be.")]
    [SerializeField] private float heightRange = 1.5f;

    private static readonly List<Villager> all = new List<Villager>();
    private static readonly Dictionary<TextAsset, DialogueScript> parsed = new Dictionary<TextAsset, DialogueScript>();

    private PlayerController player;
    private SpriteRenderer sprite;
    private bool upWasHeld = true; // don't start on the frame Harvey walks up holding Up

    public string DisplayName => displayName;
    public string Key => gameObject.scene.name + "/" + name;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        all.Clear();
        parsed.Clear();
    }

    private void Awake() => sprite = GetComponent<SpriteRenderer>();
    private void OnEnable() => all.Add(this);
    private void OnDisable() => all.Remove(this);

    private void Update()
    {
        bool up = UpHeld();
        bool pressed = up && !upWasHeld;
        upWasHeld = up;
        if (dialogue == null || DialogueBox.IsOpen || HarveyMenus.IsOpen || MenueControl.isGamePaused || VillageShop.PlayerAtCounter) return;
        if (!InReach(out float distance) || Nearest() != this) return;

        HarveyMenus.Prompt(this, "UP / E: TALK TO " + displayName.ToUpperInvariant());
        if (pressed) DialogueBox.Open(Script(), startNode, this);
    }

    /// <summary>Called by DialogueBox when the conversation is over.</summary>
    public void OnConversationEnded()
    {
        upWasHeld = true; // the key that closed it (E) mustn't start it again
    }

    private bool InReach(out float distance)
    {
        distance = float.MaxValue;
        if (player == null) player = FindFirstObjectByType<PlayerController>();
        if (player == null || !player.isActiveAndEnabled || !player.GetIsGrounded) return false;
        Vector3 me = sprite != null ? new Vector3(sprite.bounds.center.x, sprite.bounds.min.y, 0f) : transform.position;
        Vector3 him = player.FeetPosition;
        distance = Mathf.Abs(him.x - me.x);
        return distance <= talkRange && Mathf.Abs(him.y - me.y) <= heightRange;
    }

    private static Villager Nearest()
    {
        Villager best = null;
        float bestDistance = float.MaxValue;
        foreach (Villager v in all)
            if (v.dialogue != null && v.InReach(out float d) && d < bestDistance)
            {
                best = v;
                bestDistance = d;
            }
        return best;
    }

    private DialogueScript Script()
    {
        if (!parsed.TryGetValue(dialogue, out DialogueScript script))
        {
            script = DialogueScript.Parse(dialogue.text, dialogue.name);
            foreach (string e in script.errors) Debug.LogWarning("Dialogue: " + e, this);
            parsed[dialogue] = script;
        }
        return script;
    }

    private static bool UpHeld()
    {
        Keyboard kb = Keyboard.current;
        if (kb != null && (kb.wKey.isPressed || kb.upArrowKey.isPressed || kb.eKey.isPressed)) return true;
        Gamepad pad = Gamepad.current;
        return pad != null && (pad.dpad.up.isPressed || pad.leftStick.ReadValue().y > 0.7f);
    }

    private void OnDrawGizmosSelected()
    {
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        Vector3 feet = sr != null ? new Vector3(sr.bounds.center.x, sr.bounds.min.y, 0f) : transform.position;
        Gizmos.color = new Color(1f, 0.9f, 0.45f, 0.6f);
        Gizmos.DrawWireCube(feet, new Vector3(talkRange * 2f, heightRange * 2f, 0f));
    }
}
