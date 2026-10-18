using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// FX: whole-screen colour effects through a global URP Volume made at runtime (no scene setup):
///   Ripple(at)       a quick lens-distortion ripple centred on a world point (switching worlds)
///   SpiritWorld(on)  a cool spirit-world colour shift while in the spirit world
///   GreyPulse()      a brief greyscale pulse (death / game over)
/// The main camera's post-processing is switched on the first time an effect is used.
/// </summary>
public class ScreenGrade : MonoBehaviour
{
    private static ScreenGrade instance;

    private Volume volume;
    private VolumeProfile profile;
    private ColorAdjustments colour;
    private LensDistortion lens;

    private float rippleTime = -1f;
    private Vector2 rippleCentre = new Vector2(0.5f, 0.5f);
    private float spirit, spiritTarget;
    private float greyTime = -1f;

    private const float RippleLength = 0.55f;
    private const float GreyLength = 0.9f;
    private static readonly Color SpiritFilter = new Color(0.86f, 0.92f, 1.1f);

    private static ScreenGrade Get()
    {
        if (instance != null) return instance;
        var go = new GameObject("ScreenGrade");
        SceneManager.MoveGameObjectToScene(go, SceneManager.GetActiveScene());
        instance = go.AddComponent<ScreenGrade>();
        return instance;
    }

    public static void Ripple(Vector3 worldPoint)
    {
        ScreenGrade g = Get();
        Camera cam = Camera.main;
        if (cam != null)
        {
            Vector3 v = cam.WorldToViewportPoint(worldPoint);
            g.rippleCentre = new Vector2(Mathf.Clamp01(v.x), Mathf.Clamp01(v.y));
        }
        g.rippleTime = 0f;
    }

    public static void SpiritWorld(bool on, bool instant = false)
    {
        ScreenGrade g = Get();
        g.spiritTarget = on ? 1f : 0f;
        if (instant) g.spirit = g.spiritTarget;
    }

    public static void GreyPulse() => Get().greyTime = 0f;

    private void Awake()
    {
        profile = ScriptableObject.CreateInstance<VolumeProfile>();
        profile.hideFlags = HideFlags.DontSave;
        colour = profile.Add<ColorAdjustments>(true);
        lens = profile.Add<LensDistortion>(true);
        colour.active = lens.active = false;
        volume = gameObject.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 100;
        volume.sharedProfile = profile;
        Camera cam = Camera.main;
        if (cam != null) cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
        if (profile != null) Destroy(profile);
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;

        // ripple: pull in, overshoot out, settle
        if (rippleTime >= 0f)
        {
            rippleTime += dt;
            float k = rippleTime / RippleLength;
            lens.active = k < 1f;
            lens.center.Override(rippleCentre);
            lens.intensity.Override(k < 1f ? -0.45f * Mathf.Sin(k * Mathf.PI * 2f) * (1f - k) : 0f);
            lens.scale.Override(1f);
            if (k >= 1f) rippleTime = -1f;
        }

        spirit = Mathf.MoveTowards(spirit, spiritTarget, dt * 2.5f);
        float grey = 0f;
        if (greyTime >= 0f)
        {
            greyTime += dt;
            float k = greyTime / GreyLength;
            grey = k < 0.25f ? k / 0.25f : 1f - (k - 0.25f) / 0.75f; // fast drain, slower return
            if (k >= 1f) { greyTime = -1f; grey = 0f; }
        }

        bool any = spirit > 0.001f || grey > 0.001f;
        colour.active = any;
        if (!any) return;
        colour.colorFilter.Override(Color.Lerp(Color.white, SpiritFilter, spirit));
        colour.saturation.Override(Mathf.Lerp(-12f * spirit, -100f, grey));
        colour.contrast.Override(6f * spirit);
    }
}
