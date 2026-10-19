using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI; // TITLE:

/// <summary>
/// MENU: the living parts of the main menu scene: Harvey idling at the dig, the lantern flickering, the title
/// bobbing, and the paw pointer that sits beside the selected button. FX: the particles are PixelFX (the game's
/// pixel particle system), replacing the hand-made dust sprites: dust motes drifting through the air, embers
/// rising from the lantern, and dirt flicked up out of the dig pit every few seconds.
/// Selection itself (and the buttons' OnClick) stays with the EventSystem and MenueControl.
/// TITLE: the simplified title (2026-10-18) turns the ambient particles off and uses plain text buttons, so the
/// selected button's label turns yellow and the rest stay cream.
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
    [Tooltip("FX: dust motes alive at once (roughly).")]
    [SerializeField] private int dustCount = 14;
    [SerializeField] private Rect dustArea = new Rect(-10f, -5.6f, 20f, 7f);
    [Tooltip("FX: where dirt flies up out of the dig pit (world).")]
    [SerializeField] private Vector2 digSpot = new Vector2(-6.5f, -4.6f);
    [Tooltip("TITLE: motes, lantern embers and dig dirt. Off for the simple title.")]
    [SerializeField] private bool ambientParticles = true;

    [Header("Menu")]
    [SerializeField] private RectTransform pointer;
    [SerializeField] private float pointerGap = 4f;
    [SerializeField] private GameObject firstSelected;
    [SerializeField] private Color labelColour = new Color(1f, 1f, 0.92f);       // TITLE: cream ffffeb
    [SerializeField] private Color selectedLabelColour = new Color(1f, 0.894f, 0.47f); // TITLE: yellow ffe478
    private Button[] menuButtons; // TITLE:

    private Vector3 titleHome;
    private readonly Vector3[] corners = new Vector3[4]; // PERF: reused by FollowSelection instead of a new array each frame
    private float animTime;
    private float moteTimer, emberTimer, digTimer = 1.2f; // FX:

    private void Start()
    {
        if (title != null) titleHome = title.localPosition;
        if (ambientParticles)
            for (int i = 0; i < dustCount; i++) Mote(true); // FX: start with the air already full of motes
        if (firstSelected != null) menuButtons = firstSelected.transform.parent.GetComponentsInChildren<Button>(); // TITLE:
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

        if (ambientParticles) Particles(Time.unscaledDeltaTime); // FX:
        KeepSomethingSelected();
        FollowSelection();
    }

    // FX: motes, lantern embers and dig-pit dirt, all through PixelFX (unscaled, so they keep moving in menus)
    private static readonly Color[] MoteColours = { PixelFX.Hex("ffffeb"), PixelFX.Hex("ffe478"), PixelFX.Hex("c9b8ff") };
    private static readonly Color[] EmberColours = { PixelFX.Hex("ffe478"), PixelFX.Hex("f2a65e"), PixelFX.Hex("ffffeb") };
    private static readonly Color[] DirtColours = { PixelFX.Hex("ce812c"), PixelFX.Hex("a35a3c"), PixelFX.Hex("663931"), PixelFX.Hex("e3ba66") };

    private void Particles(float dt)
    {
        moteTimer -= dt;
        if (moteTimer <= 0f)
        {
            moteTimer = 5f / Mathf.Max(1, dustCount);
            Mote(false);
        }
        emberTimer -= dt;
        if (emberTimer <= 0f && lanternGlow != null)
        {
            emberTimer = Random.Range(0.25f, 0.6f);
            Vector3 lamp = lanternGlow.transform.position + new Vector3(Random.Range(-0.08f, 0.08f), 0.1f, 0f);
            PixelFX.Emit(lamp, new Vector2(Random.Range(-0.15f, 0.25f), Random.Range(0.35f, 0.8f)), PixelFX.Pick(EmberColours),
                Random.Range(1f, 1.8f), 1, PixelShape.Square, PixelMotion.Drift, true, PixelLayer.Front, true);
        }
        digTimer -= dt;
        if (digTimer <= 0f)
        {
            digTimer = Random.Range(1.6f, 2.8f);
            Vector3 pit = new Vector3(digSpot.x + Random.Range(-0.6f, 0.6f), digSpot.y, 0f);
            float side = Random.value < 0.5f ? -1f : 1f;
            PixelFX.Burst(pit, Random.Range(4, 7), 2.4f, 3.6f, DirtColours, 0.55f, 0.85f, 1, PixelShape.Square, PixelMotion.Fall, false,
                PixelLayer.Front, 90f + side * 25f, 40f, 0f, true);
        }
    }

    // one dust mote drifting slowly up and to the right somewhere in the dust area
    private void Mote(bool anywhere)
    {
        float x = anywhere ? Random.Range(dustArea.xMin, dustArea.xMax) : dustArea.xMin + Random.Range(0f, dustArea.width * 0.5f);
        var p = new Vector3(x, Random.Range(dustArea.yMin, dustArea.yMax), 0f);
        PixelFX.Emit(p, new Vector2(Random.Range(0.15f, 0.4f), Random.Range(0.04f, 0.15f)), PixelFX.Pick(MoteColours),
            Random.Range(4f, 7f), Random.value < 0.25f ? 2 : 1, PixelShape.Square, PixelMotion.Drift, true, PixelLayer.Front, true);
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
        if (menuButtons != null) // TITLE: the selected label lights up
            foreach (Button b in menuButtons)
            {
                var label = b.GetComponentInChildren<TMPro.TMP_Text>();
                if (label != null) label.color = b.gameObject == selected ? selectedLabelColour : labelColour;
            }
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
