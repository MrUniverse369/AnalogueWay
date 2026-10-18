using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// MENU: the living parts of the main menu scene: Harvey idling at the dig, the lantern flickering, the title
/// bobbing, dust drifting through the dawn light, and the paw pointer that sits beside the selected button.
/// Selection itself (and the buttons' OnClick) stays with the EventSystem and MenueControl.
/// </summary>
public class MainMenuScene : MonoBehaviour
{
    private const float PixelSize = 1f / 16f; // world units per art pixel (16 PPU)

    [Header("Harvey")]
    [SerializeField] private SpriteRenderer harvey;
    [SerializeField] private Sprite[] harveyIdle;
    [SerializeField] private float harveyFps = 6f;

    [Header("Scene")]
    [SerializeField] private SpriteRenderer lanternGlow;
    [SerializeField] private Transform title;
    [SerializeField] private float titleBobPixels = 2f;
    [SerializeField] private float titleBobSpeed = 1.2f;
    [SerializeField] private Sprite dustSprite;
    [SerializeField] private int dustCount = 14;
    [SerializeField] private Rect dustArea = new Rect(-10f, -5.6f, 20f, 7f);

    [Header("Menu")]
    [SerializeField] private RectTransform pointer;
    [SerializeField] private float pointerGap = 4f;
    [SerializeField] private GameObject firstSelected;

    private Vector3 titleHome;
    private readonly Vector3[] corners = new Vector3[4]; // PERF: reused by FollowSelection instead of a new array each frame
    private Transform[] dust;
    private Vector2[] dustVelocity;
    private float animTime;

    private void Start()
    {
        if (title != null) titleHome = title.localPosition;
        SpawnDust();
        if (EventSystem.current != null && firstSelected != null) EventSystem.current.SetSelectedGameObject(firstSelected);
    }

    private void Update()
    {
        animTime += Time.unscaledDeltaTime;
        if (harvey != null && harveyIdle != null && harveyIdle.Length > 0)
            harvey.sprite = harveyIdle[(int)(animTime * harveyFps) % harveyIdle.Length];

        if (lanternGlow != null)
        {
            float flicker = 0.75f + 0.15f * Mathf.Sin(animTime * 9f) + 0.1f * Mathf.PerlinNoise(animTime * 3f, 0.3f);
            lanternGlow.color = new Color(1f, 1f, 1f, flicker);
        }

        if (title != null)
        {
            // whole-pixel steps keep the pixel art crisp
            float bob = Mathf.Round(Mathf.Sin(animTime * titleBobSpeed) * titleBobPixels) * PixelSize;
            title.localPosition = titleHome + new Vector3(0f, bob, 0f);
        }

        DriftDust();
        KeepSomethingSelected();
        FollowSelection();
    }

    private void SpawnDust()
    {
        if (dustSprite == null || dustCount <= 0) return;
        dust = new Transform[dustCount];
        dustVelocity = new Vector2[dustCount];
        var rng = new System.Random(7);
        for (int i = 0; i < dustCount; i++)
        {
            var go = new GameObject("Dust");
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = dustSprite;
            sr.sortingOrder = 50;
            sr.color = new Color(1f, 1f, 1f, 0.35f + 0.4f * (float)rng.NextDouble());
            go.transform.position = new Vector3(dustArea.x + dustArea.width * (float)rng.NextDouble(), dustArea.y + dustArea.height * (float)rng.NextDouble(), 0f);
            dust[i] = go.transform;
            dustVelocity[i] = new Vector2(0.15f + 0.25f * (float)rng.NextDouble(), 0.05f + 0.1f * (float)rng.NextDouble());
        }
    }

    private void DriftDust()
    {
        if (dust == null) return;
        for (int i = 0; i < dust.Length; i++)
        {
            Vector3 p = dust[i].position + (Vector3)(dustVelocity[i] * Time.unscaledDeltaTime);
            p.y += Mathf.Sin(animTime * 1.3f + i) * 0.002f;
            if (p.x > dustArea.xMax) p.x = dustArea.xMin;
            if (p.y > dustArea.yMax) p.y = dustArea.yMin;
            dust[i].position = p;
        }
    }

    // a controller or the keyboard needs a selected button to navigate from (a mouse click can clear it)
    private void KeepSomethingSelected()
    {
        if (EventSystem.current == null || firstSelected == null) return;
        if (EventSystem.current.currentSelectedGameObject != null && EventSystem.current.currentSelectedGameObject.activeInHierarchy) return;
        bool navigating = (Keyboard.current != null && (Keyboard.current.upArrowKey.wasPressedThisFrame || Keyboard.current.downArrowKey.wasPressedThisFrame ||
                                                         Keyboard.current.wKey.wasPressedThisFrame || Keyboard.current.sKey.wasPressedThisFrame))
                          || (Gamepad.current != null && (Gamepad.current.dpad.ReadValue() != Vector2.zero || Gamepad.current.leftStick.ReadValue().magnitude > 0.5f));
        if (navigating) EventSystem.current.SetSelectedGameObject(firstSelected);
    }

    private void FollowSelection()
    {
        if (pointer == null) return;
        var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        var target = selected != null ? selected.transform as RectTransform : null;
        pointer.gameObject.SetActive(target != null);
        if (target == null) return;
        // sit just left of the button, nudging toward it like a beckoning paw
        target.GetWorldCorners(corners);
        float scale = pointer.lossyScale.x;
        float nudge = Mathf.Round(Mathf.Sin(animTime * 6f) * 1.5f);
        Vector3 left = (corners[0] + corners[1]) * 0.5f;
        pointer.position = left + new Vector3((-pointerGap - pointer.rect.width * 0.5f + nudge) * scale, 0f, 0f);
    }
}
