using System;
using UnityEngine;

/// <summary>
/// CHEST: one reward popping out of an opened treasure chest (a spirit peach, a chi crystal, a gem). It springs out
/// of the lid with a little overshoot, hangs in the air glowing and twinkling, then streaks into Harvey and is
/// collected: the callback from OpenChest applies it (a life back, chi, or an item in the bag). Made in code by
/// OpenChest; nothing needs setting up in a scene. If Harvey isn't around (respawning), it is collected where it is.
/// </summary>
public class ChestReward : MonoBehaviour
{
    private const float RiseTime = 0.42f;
    private const float HomeStartSpeed = 2f;
    private const float HomeAcceleration = 32f;
    private const float MaxHomeTime = 1.2f;
    private const float CollectDistance = 0.25f;

    private SpriteRenderer sr;
    private Vector3 from, peak, position;
    private float hover, scale, age, homeTime, speed, nextTwinkle;
    private Color[] sparkle;
    private Action<Vector3> collected;

    /// <summary>
    /// Pops a reward out of a chest. 'from' is the lid, 'peak' where it hangs, 'hover' how long it hangs there.
    /// 'onCollected' gets the point it was collected at.
    /// </summary>
    public static ChestReward Spawn(Sprite icon, Vector3 from, Vector3 peak, float hover, Color glow, Color[] sparkle,
        int sortingLayerID, int sortingOrder, Action<Vector3> onCollected, float scale = 0.85f)
    {
        var go = new GameObject("ChestReward");
        go.transform.position = from;
        var reward = go.AddComponent<ChestReward>();
        reward.sr = go.AddComponent<SpriteRenderer>();
        reward.sr.sprite = icon;
        // a SpriteRenderer made in code starts on the lit material and draws black on unlit layers
        if (SpriteFX.ActorMaterial != null) reward.sr.sharedMaterial = SpriteFX.ActorMaterial;
        reward.sr.sortingLayerID = sortingLayerID;
        reward.sr.sortingOrder = sortingOrder;
        reward.from = reward.position = from;
        reward.peak = peak;
        reward.hover = Mathf.Max(0f, hover);
        reward.scale = scale;
        reward.sparkle = sparkle != null && sparkle.Length > 0 ? sparkle : PixelFX.Sparks;
        reward.collected = onCollected;
        go.transform.localScale = Vector3.one * scale * 0.3f;
        SpriteAura.On(reward.sr)?.Set("reward", glow, 2, 7f); // FX: a pulsing glow in the reward's own colour
        return reward;
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        age += dt;
        if (age < RiseTime) Rise(age / RiseTime);
        else if (age < RiseTime + hover) Hover(age - RiseTime);
        else Home(dt);
        // whole art pixels, like the rest of the game
        transform.position = new Vector3(Mathf.Round(position.x * 16f) / 16f, Mathf.Round(position.y * 16f) / 16f, 0f);
    }

    // out of the lid with a springy overshoot, growing from small to a little too big and settling
    private void Rise(float k)
    {
        float back = 1f + 2.2f * Mathf.Pow(k - 1f, 3f) + 1.2f * Mathf.Pow(k - 1f, 2f); // ease-out-back
        position = Vector3.LerpUnclamped(from, peak, back);
        float s = Mathf.Lerp(0.3f, 1f, Mathf.Clamp01(back));
        transform.localScale = Vector3.one * scale * (k > 0.7f ? Mathf.Lerp(1.15f, 1f, (k - 0.7f) / 0.3f) : s * 1.15f);
        if (Time.frameCount % 2 == 0)
            PixelFX.Emit(position + (Vector3)(UnityEngine.Random.insideUnitCircle * 0.12f), new Vector2(UnityEngine.Random.Range(-0.3f, 0.3f), -0.4f),
                PixelFX.Pick(sparkle), UnityEngine.Random.Range(0.2f, 0.35f), 1, PixelShape.Square, PixelMotion.Float, true);
    }

    // hangs in the air with a gentle one-pixel bob and the odd twinkle
    private void Hover(float t)
    {
        transform.localScale = Vector3.one * scale;
        position = peak + new Vector3(0f, Mathf.Sin(t * 7f) * PixelFX.Pixel, 0f);
        if (Time.time >= nextTwinkle)
        {
            nextTwinkle = Time.time + UnityEngine.Random.Range(0.12f, 0.22f);
            Vector3 at = position + new Vector3(UnityEngine.Random.Range(-0.35f, 0.35f), UnityEngine.Random.Range(-0.3f, 0.35f), 0f);
            PixelFX.Emit(at, Vector2.zero, PixelFX.Pick(sparkle), 0.25f, 1, PixelShape.Star, PixelMotion.Float, true);
        }
    }

    // streaks into Harvey, speeding up and shrinking, leaving a short trail
    private void Home(float dt)
    {
        PlayerFX harvey = PlayerFX.Instance;
        if (harvey == null || !harvey.isActiveAndEnabled)
        {
            Collect();
            return;
        }
        homeTime += dt;
        if (speed <= 0f) speed = HomeStartSpeed;
        speed += HomeAcceleration * dt;
        Vector3 target = harvey.transform.position + new Vector3(0f, 0.1f, 0f);
        position = Vector3.MoveTowards(position, target, speed * dt);
        float distance = Vector3.Distance(position, target);
        transform.localScale = Vector3.one * scale * Mathf.Lerp(0.5f, 1f, Mathf.Clamp01(distance / 2f));
        PixelFX.Emit(position, Vector2.zero, PixelFX.Pick(sparkle), 0.18f, 1, PixelShape.Square, PixelMotion.Float, true);
        if (distance <= CollectDistance || homeTime >= MaxHomeTime) Collect();
    }

    private void Collect()
    {
        Vector3 at = position;
        PlayerFX harvey = PlayerFX.Instance;
        if (harvey != null && harvey.isActiveAndEnabled)
        {
            at = harvey.transform.position;
            PixelFX.Ring(at, 10, 2.2f, sparkle, 0.28f, 1, PixelShape.Square, true);
        }
        try { collected?.Invoke(at); }
        finally { Destroy(gameObject); }
    }
}
