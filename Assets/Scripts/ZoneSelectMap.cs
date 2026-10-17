using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// ZONEMAP: the overworld stage select. Harvey walks node to node along the path; Cross/Enter plays the
/// level under him and Circle/Esc goes back to the main menu. Levels open up to the furthest one reached
/// (GameSession.FurthestBuildIndex); you can only walk onto open nodes.
/// </summary>
public class ZoneSelectMap : MonoBehaviour
{
    public const string SceneName = "ZoneSelect";

    [Header("Nodes, in path order")]
    [SerializeField] private ZoneMapNode[] nodes;
    [Tooltip("Opens every node. For testing only.")]
    [SerializeField] private bool unlockAll = false;

    [Header("Harvey")]
    [SerializeField] private Transform player;
    [SerializeField] private SpriteRenderer playerSprite;
    [SerializeField] private Sprite[] idleFrames;
    [SerializeField] private Sprite[] runFrames;
    [SerializeField] private float framesPerSecond = 10f;
    [SerializeField] private float walkSpeed = 6f;

    [Header("Icons")]
    [Tooltip("Ring under the selected node; it pulses gently.")]
    [SerializeField] private SpriteRenderer cursor;
    [SerializeField] private Vector3 cursorOffset = new Vector3(0f, -0.28f, 0f);
    [SerializeField] private Sprite openIcon;
    [SerializeField] private Sprite clearedIcon;
    [SerializeField] private Sprite lockedIcon;
    [SerializeField] private Color lockedTint = new Color(0.35f, 0.35f, 0.42f);

    [Header("Label (floats above the selected node)")]
    [SerializeField] private RectTransform label;
    [SerializeField] private TMP_Text zoneText;
    [SerializeField] private TMP_Text levelText;
    [Tooltip("World units above the node.")]
    [SerializeField] private float labelHeight = 1.5f;
    [SerializeField] private TMP_Text coinsText;
    [SerializeField] private Image fade;
    [SerializeField] private float fadeTime = 0.35f;

    [Header("Clouds")]
    [SerializeField] private Transform[] clouds;
    [SerializeField] private float cloudSpeed = 0.25f;
    [Tooltip("Clouds wrap around between -x and +x.")]
    [SerializeField] private float cloudWrapX = 12f;

    [Header("Gates")]
    [Tooltip("Gates Harvey walks through on the path (the torii between the sunny foothills and the snow). Their sprites sort above Harvey; crossing one plays gateSound.")]
    [SerializeField] private Transform[] gates;
    [Tooltip("World units either side of a gate's centre that count as walking through it.")]
    [SerializeField] private float gateHalfWidth = 0.5f;
    [SerializeField] private AudioClip gateSound;

    [Header("Sounds")]
    [SerializeField] private AudioClip moveSound;
    [SerializeField] private AudioClip blockedSound;
    [SerializeField] private AudioClip selectSound;
    [SerializeField] private AudioClip backSound;

    private static int lastNode = -1; // where Harvey stood last time the map was open this session

    private bool[] open;
    private int current;
    private bool busy = true; // walking or fading
    private Vector2 queuedDir;
    private Vector2 lastDir;
    private float animTime;

    private void Start()
    {
        int furthest = unlockAll ? int.MaxValue : GameSession.FurthestBuildIndex;
        open = new bool[nodes.Length];
        for (int i = 0; i < nodes.Length; i++)
        {
            int index = GameSession.BuildIndexOf(nodes[i].sceneName);
            open[i] = index > 0 && index <= furthest;
            bool cleared = index > 0 && index < furthest;
            StyleNode(nodes[i], open[i], cleared);
        }

        current = PickStartNode();
        player.position = nodes[current].transform.position;
        ShowNode(current);
        if (coinsText != null)
            coinsText.text = $"COINS {(SaveSystem.TryLoad(out SaveData data) ? data.coins : 0)}"; // what a level picked here starts with
        StartCoroutine(FadeTo(0f, () => busy = false));
    }

    private void StyleNode(ZoneMapNode node, bool isOpen, bool cleared)
    {
        if (node.icon == null) return;
        if (node.kind == ZoneNodeKind.Level)
        {
            bool ownGem = isOpen && !cleared && node.openIcon != null;
            node.icon.sprite = !isOpen ? lockedIcon : cleared ? clearedIcon : ownGem ? node.openIcon : openIcon;
            node.icon.color = isOpen && !cleared && !ownGem ? node.zoneColour : Color.white;
        }
        else
        {
            // villages, the boss and the goal keep their own icon and are greyed out while locked
            node.icon.color = isOpen ? Color.white : lockedTint;
        }
    }

    // The node Harvey was on last time, else the level Continue would load, else the furthest open one.
    private int PickStartNode()
    {
        if (lastNode >= 0 && lastNode < nodes.Length && open[lastNode]) return lastNode;
        if (SaveSystem.TryLoad(out SaveData data))
            for (int i = 0; i < nodes.Length; i++)
                if (open[i] && nodes[i].sceneName == data.sceneName) return i;
        for (int i = nodes.Length - 1; i >= 0; i--)
            if (open[i]) return i;
        return 0;
    }

    private void Update()
    {
        Vector2 dir = ReadDirection();
        bool pressed = dir != Vector2.zero && dir != lastDir;
        lastDir = dir;
        if (pressed) queuedDir = dir; // remembered while walking, so a quick tap isn't lost

        AnimatePlayer();
        UpdateCursor();
        DriftClouds();
        if (busy) return;

        if (ConfirmPressed())
        {
            StartCoroutine(EnterLevel());
            return;
        }
        if (BackPressed())
        {
            StartCoroutine(BackToMenu());
            return;
        }
        if (queuedDir != Vector2.zero)
        {
            Vector2 wanted = queuedDir;
            queuedDir = Vector2.zero;
            TryWalk(wanted);
        }
    }

    // ---------------------------------------------------------------- walking
    private void TryWalk(Vector2 dir)
    {
        int bestTarget = -1;
        float bestDot = 0.5f;
        List<Vector3> bestRoute = null;
        foreach (int target in new[] { current - 1, current + 1 })
        {
            if (target < 0 || target >= nodes.Length) continue;
            List<Vector3> route = Route(current, target);
            float dot = Vector2.Dot(dir, ((Vector2)(route[1] - route[0])).normalized);
            if (dot > bestDot)
            {
                bestDot = dot;
                bestTarget = target;
                bestRoute = route;
            }
        }
        if (bestTarget < 0) return;

        if (!open[bestTarget])
        {
            PlaySound(blockedSound);
            StartCoroutine(Shake());
            return;
        }
        PlaySound(moveSound);
        if (label != null) label.gameObject.SetActive(false);
        StartCoroutine(Walk(bestRoute, bestTarget));
    }

    // Points from node a to its neighbour b, including both ends.
    private List<Vector3> Route(int a, int b)
    {
        int from = Mathf.Min(a, b);
        var route = new List<Vector3> { nodes[from].transform.position };
        foreach (Vector2 bend in nodes[from].bendsToNext) route.Add(bend);
        route.Add(nodes[from + 1].transform.position);
        if (a > b) route.Reverse();
        return route;
    }

    private IEnumerator Walk(List<Vector3> route, int target)
    {
        busy = true;
        for (int i = 1; i < route.Count; i++)
        {
            Vector3 goal = route[i];
            goal.z = player.position.z;
            if (Mathf.Abs(goal.x - player.position.x) > 0.01f) playerSprite.flipX = goal.x < player.position.x;
            while (player.position != goal)
            {
                Vector3 before = player.position;
                player.position = Vector3.MoveTowards(player.position, goal, walkSpeed * Time.deltaTime);
                CheckGates(before, player.position);
                yield return null;
            }
        }
        current = target;
        lastNode = current;
        ShowNode(current);
        busy = false;
    }

    // a chime as Harvey passes under a gate (either direction)
    private void CheckGates(Vector3 before, Vector3 after)
    {
        if (gates == null) return;
        foreach (Transform gate in gates)
        {
            if (gate == null || Mathf.Abs(after.x - gate.position.x) > gateHalfWidth) continue;
            if ((before.y < gate.position.y) != (after.y < gate.position.y))
                PlaySound(gateSound);
        }
    }

    private IEnumerator Shake()
    {
        busy = true;
        Vector3 home = player.position;
        for (float t = 0; t < 0.2f; t += Time.deltaTime)
        {
            player.position = home + Vector3.right * Mathf.Sin(t * 90f) * 0.08f;
            yield return null;
        }
        player.position = home;
        busy = false;
    }

    private bool IsWalking => busy && player.position != nodes[current].transform.position;

    private void AnimatePlayer()
    {
        Sprite[] frames = IsWalking && runFrames.Length > 0 ? runFrames : idleFrames;
        if (frames.Length == 0) return;
        animTime += Time.deltaTime;
        playerSprite.sprite = frames[(int)(animTime * framesPerSecond) % frames.Length];
    }

    private void UpdateCursor()
    {
        if (cursor == null) return;
        cursor.transform.position = nodes[current].transform.position + cursorOffset;
        cursor.enabled = !IsWalking;
        Color c = cursor.color;
        c.a = 0.65f + 0.35f * Mathf.Sin(Time.time * 4f); // gentle pulse
        cursor.color = c;
    }

    private void DriftClouds()
    {
        if (clouds == null) return;
        foreach (Transform cloud in clouds)
        {
            if (cloud == null) continue;
            Vector3 p = cloud.position;
            p.x += cloudSpeed * Time.deltaTime;
            if (p.x > cloudWrapX) p.x = -cloudWrapX;
            cloud.position = p;
        }
    }

    // ---------------------------------------------------------------- label
    private void ShowNode(int i)
    {
        ZoneMapNode node = nodes[i];
        if (zoneText != null) zoneText.text = node.zoneName.ToUpperInvariant();
        if (levelText != null)
        {
            levelText.text = node.levelName.ToUpperInvariant();
            levelText.color = Color.Lerp(node.zoneColour, Color.white, 0.45f);
        }
        if (label == null) return;

        // size the label to its text, then float it above the node, kept inside the screen
        float width = 0f;
        foreach (TMP_Text t in new[] { zoneText, levelText })
            if (t != null) width = Mathf.Max(width, t.GetPreferredValues(t.text).x);
        label.sizeDelta = new Vector2(width + 14f, label.sizeDelta.y);

        var parent = (RectTransform)label.parent;
        Vector2 screen = Camera.main.WorldToScreenPoint(node.transform.position + Vector3.up * labelHeight);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screen, null, out Vector2 local);
        float halfRoom = parent.rect.width / 2f - label.sizeDelta.x / 2f - 4f;
        local.x = Mathf.Clamp(local.x, -halfRoom, halfRoom);
        local.y = Mathf.Min(local.y, parent.rect.height / 2f - label.sizeDelta.y - 4f); // the label's pivot is its bottom edge
        label.anchoredPosition = local;
        label.gameObject.SetActive(true);
        StartCoroutine(PopLabel());
    }

    private IEnumerator PopLabel()
    {
        for (float t = 0; t < 0.12f; t += Time.unscaledDeltaTime)
        {
            label.localScale = Vector3.one * Mathf.Lerp(0.85f, 1f, t / 0.12f);
            yield return null;
        }
        label.localScale = Vector3.one;
    }

    // ---------------------------------------------------------------- leaving
    private IEnumerator EnterLevel()
    {
        busy = true;
        lastNode = current;
        PlaySound(selectSound);
        // a little hop on the spot before the fade
        Vector3 home = player.position;
        for (float t = 0; t < 0.25f; t += Time.deltaTime)
        {
            player.position = home + Vector3.up * Mathf.Sin(t / 0.25f * Mathf.PI) * 0.3f;
            yield return null;
        }
        player.position = home;
        yield return FadeTo(1f, null);
        GameSession.StartLevel(nodes[current].sceneName);
    }

    private IEnumerator BackToMenu()
    {
        busy = true;
        PlaySound(backSound);
        yield return FadeTo(1f, null);
        SceneManager.LoadScene(0);
    }

    private IEnumerator FadeTo(float alpha, System.Action done)
    {
        if (fade != null)
        {
            fade.gameObject.SetActive(true);
            Color c = fade.color;
            float start = c.a;
            for (float t = 0; t < fadeTime; t += Time.unscaledDeltaTime)
            {
                c.a = Mathf.Lerp(start, alpha, t / fadeTime);
                fade.color = c;
                yield return null;
            }
            c.a = alpha;
            fade.color = c;
            fade.raycastTarget = alpha > 0f;
        }
        done?.Invoke();
    }

    private void PlaySound(AudioClip clip)
    {
        if (clip != null && AudioManager.Instance != null) AudioManager.Instance.PlaySfxOneShot(clip);
    }

    // ---------------------------------------------------------------- input (keyboard + gamepad)
    private static Vector2 ReadDirection()
    {
        Vector2 v = Vector2.zero;
        Keyboard kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.leftArrowKey.isPressed || kb.aKey.isPressed) v.x -= 1;
            if (kb.rightArrowKey.isPressed || kb.dKey.isPressed) v.x += 1;
            if (kb.downArrowKey.isPressed || kb.sKey.isPressed) v.y -= 1;
            if (kb.upArrowKey.isPressed || kb.wKey.isPressed) v.y += 1;
        }
        Gamepad pad = Gamepad.current;
        if (pad != null)
        {
            Vector2 stick = pad.leftStick.ReadValue() + pad.dpad.ReadValue();
            if (stick.magnitude > 0.5f) v += stick;
        }
        if (v == Vector2.zero) return v;
        // snap to the nearest of the four directions so diagonals still pick one path
        return Mathf.Abs(v.x) >= Mathf.Abs(v.y) ? new Vector2(Mathf.Sign(v.x), 0) : new Vector2(0, Mathf.Sign(v.y));
    }

    private static bool ConfirmPressed()
    {
        Keyboard kb = Keyboard.current;
        Gamepad pad = Gamepad.current;
        return (kb != null && (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame))
            || (pad != null && (pad.buttonSouth.wasPressedThisFrame || pad.startButton.wasPressedThisFrame));
    }

    private static bool BackPressed()
    {
        Keyboard kb = Keyboard.current;
        Gamepad pad = Gamepad.current;
        return (kb != null && (kb.escapeKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame))
            || (pad != null && pad.buttonEast.wasPressedThisFrame);
    }

    private void OnDrawGizmos()
    {
        if (nodes == null) return;
        Gizmos.color = Color.yellow;
        for (int i = 0; i + 1 < nodes.Length; i++)
        {
            if (nodes[i] == null || nodes[i + 1] == null) continue;
            Vector3 last = nodes[i].transform.position;
            foreach (Vector2 bend in nodes[i].bendsToNext)
            {
                Gizmos.DrawLine(last, bend);
                last = bend;
            }
            Gizmos.DrawLine(last, nodes[i + 1].transform.position);
        }
    }
}
