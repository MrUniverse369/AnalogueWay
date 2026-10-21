using UnityEngine;

/// <summary>
/// GHOST (2026-10-20): marks an enemy as a ghost (the Tomb's purple ghoul). Harvey passes straight through it: touching
/// it still hurts him exactly as before (HurtPlayer is unchanged), but it doesn't knock him back, so it can't block,
/// push or pin him. Its look comes from the HNLSprite ghost material (Resources/FX/HNLSprite_Ghost: translucent
/// shimmering body, purple pixel halo, solid green eyes and flames), set on the prefab's sprite.
/// </summary>
[DisallowMultipleComponent]
public class GhostEnemy : MonoBehaviour
{
    /// <summary>True when the collider belongs to a ghost enemy.</summary>
    public static bool Is(Collider2D other) => other != null && other.GetComponent<GhostEnemy>() != null;
}
