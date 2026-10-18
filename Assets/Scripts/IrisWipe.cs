using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// FX: the cat-head iris wipe used for scene transitions (FadeIn, FadeOut, the Zone Select). A full-screen
/// Image is drawn with the HNLUI shader's iris: covered everywhere except a cat-head shaped hole, which grows
/// to open the scene or shrinks onto Harvey to close it.
/// </summary>
public static class IrisWipe
{
    /// <summary>Hole size (screen heights) that clears even a wide screen.</summary>
    public const float OpenSize = 6f;

    private static readonly int SizeId = Shader.PropertyToID("_IrisSize");
    private static readonly int CenterId = Shader.PropertyToID("_IrisCenter");
    private static Material source;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => source = null;

    /// <summary>Give an Image its own iris material (false if the material is missing; then keep the old fade).</summary>
    public static bool Attach(Image image)
    {
        if (image == null) return false;
        if (image.material != null && image.material.HasProperty(SizeId) && image.material.IsKeywordEnabled("_HNL_IRIS")) return true;
        if (source == null) source = Resources.Load<Material>("FX/UIIris");
        if (source == null) return false;
        image.material = new Material(source) { hideFlags = HideFlags.DontSave };
        return true;
    }

    /// <summary>Set the hole: size in screen heights (0 = fully covered), centred on a world point (Harvey).</summary>
    public static void Set(Image image, float size, Vector3? worldCentre = null)
    {
        if (image == null || image.material == null) return;
        Vector2 centre = new Vector2(0.5f, 0.5f);
        Camera cam = Camera.main;
        Vector3 world = worldCentre ?? (PlayerFX.Instance != null ? PlayerFX.Instance.transform.position : Vector3.zero);
        if (cam != null && (worldCentre.HasValue || PlayerFX.Instance != null))
        {
            Vector3 v = cam.WorldToViewportPoint(world);
            centre = new Vector2(Mathf.Clamp01(v.x), Mathf.Clamp01(v.y));
        }
        image.material.SetFloat(SizeId, Mathf.Max(0f, size));
        image.material.SetVector(CenterId, centre);
    }

    /// <summary>0..1 open -> hole size. Eased in, so the cat head stays readable while it's small.</summary>
    public static float SizeFor(float open01)
    {
        float x = Mathf.Clamp01(open01);
        return OpenSize * x * x;
    }
}
