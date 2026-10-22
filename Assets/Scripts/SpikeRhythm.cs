using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// RHYTHM: drives a group of spike traps on one shared beat so they pop out of the stone and sink back in a
/// predictable rhythm (Z1A6's floor/ceiling spike corridor). Each spike gets a phase (0-1 of the period): it pops
/// at that point in the cycle, stays out for extendedTime, then slides back into the stone it is fixed to
/// (along its own down, so ceiling spikes rotated 180 sink upward). Before popping it peeks a few pixels out and
/// rattles for warnTime as a tell. The spike's own collider (and HurtPlayer) only works while the spike is at
/// least half out, so retracted spikes are safe to cross. The tilemap draws over the Traps layer, which hides
/// the sunken part. Spikes that move on the same frame share one sound, quieter the further they are from the
/// camera, so a row of traps doesn't stack up into a racket.
/// SPIKES (2026-10-21): every Tomb spike trap runs on one of these (Tools/HNL/Set Up Tomb Spike Rhythms). A spike
/// can sink by its own depth (the short floor spikes and the taller wall spikes share one controller), and only one
/// pop and one retract sound play per frame across all controllers.
/// </summary>
public class SpikeRhythm : MonoBehaviour
{
    [System.Serializable]
    public class Spike
    {
        public Transform spike;
        [Range(0f, 1f)] public float phase;
        [Tooltip("How far this spike sinks (its own height). 0 = the controller's retractDepth.")]
        public float depth;
    }

    [SerializeField] private Spike[] spikes;
    [SerializeField] private float period = 3.6f;
    [SerializeField] private float extendedTime = 1.3f;
    [SerializeField] private float warnTime = 0.45f;
    [SerializeField] private float popTime = 0.07f;
    [SerializeField] private float retractTime = 0.22f;
    [Tooltip("How far a spike sinks when retracted (world units, along its own down). Its full height hides it.")]
    [SerializeField] private float retractDepth = 1.125f;
    [Tooltip("How far the tips rise out of the retracted position during the warning peek.")]
    [SerializeField] private float peekLift = 0.3f;
    [Tooltip("Distance from a spike's centre (along its down) to the stone surface it comes out of; dust puffs there.")]
    [SerializeField] private float surfaceOffset = 0.42f;
    [SerializeField] private AudioClip extendSound;
    [SerializeField] private AudioClip retractSound;
    [SerializeField, Range(0f, 1f)] private float extendVolume = 0.7f;
    [SerializeField, Range(0f, 1f)] private float retractVolume = 0.5f;
    [Tooltip("Full volume within this distance of the camera, fading to silence at hearMax.")]
    [SerializeField] private float hearFull = 8f;
    [SerializeField] private float hearMax = 16f;

    private const float Px = 1f / 32f;
    private static readonly Color[] Dust = { PixelFX.Hex("c8d6d0"), PixelFX.Hex("8a9aa8"), PixelFX.Hex("4b5878") };

    private class State
    {
        public Vector3 basePos;
        public Vector3 up;
        public Collider2D col;
        public SpriteRenderer sr;
        public float lastU;
        public float depth;
        public SpikeTrap trap;      // TOGGLE: unticked = static
        public bool colOn, srOn;    // TOGGLE: authored states a static spike keeps
    }

    private State[] states;
    private float clock;
    private static int extendFrame = -1, retractFrame = -1; // one of each sound per frame across every controller

    /// <summary>True while the spike at this index is out far enough to hurt (used by tests and tools).</summary>
    public bool IsDangerous(int i) => states != null && states[i].col != null && states[i].col.enabled;

    private static readonly List<SpikeRhythm> active = new List<SpikeRhythm>(); // TOGGLE: for SpikeTrap.Join

    private void OnEnable() => active.Add(this);
    private void OnDisable() => active.Remove(this);

    private void Awake()
    {
        if (spikes == null) spikes = new Spike[0]; // TOGGLE: made at runtime by Drive
        states = new State[spikes.Length];
        for (int i = 0; i < spikes.Length; i++) states[i] = MakeState(i);
        Apply();
    }

    private State MakeState(int i)
    {
        Transform t = spikes[i].spike;
        if (t == null) return null;
        var s = new State
        {
            basePos = t.position,
            up = t.up,
            col = t.GetComponent<Collider2D>(),
            sr = t.GetComponent<SpriteRenderer>(),
            lastU = LocalTime(i),
            depth = spikes[i].depth > 0f ? spikes[i].depth : retractDepth,
            trap = t.GetComponent<SpikeTrap>()
        };
        s.colOn = s.col != null && s.col.enabled;
        s.srOn = s.sr != null && s.sr.enabled;
        return s;
    }

    /// <summary>
    /// TOGGLE: puts a ticked spike that no controller lists yet (one dragged into a level by hand) on the scene's
    /// beat. It takes the phase of a driven spike right beside it on the same surface (so it pops with that run), or
    /// its own phase. A scene with no SpikeRhythm gets one, using the spike's sounds.
    /// </summary>
    public static void Drive(SpikeTrap trap)
    {
        Transform t = trap.transform;
        foreach (SpikeRhythm r in active)
            if (System.Array.Exists(r.spikes, s => s != null && s.spike == t)) return;

        SpikeRhythm rhythm = active.Count > 0 ? active[0] : null;
        if (rhythm == null)
        {
            rhythm = new GameObject("SpikeRhythm").AddComponent<SpikeRhythm>();
            rhythm.extendSound = trap.ExtendSound;
            rhythm.retractSound = trap.RetractSound;
        }

        SpriteRenderer sr = t.GetComponent<SpriteRenderer>();
        float depth = sr != null && sr.sprite != null ? Mathf.Round(sr.sprite.bounds.size.y * t.lossyScale.y / Px) * Px : 0f;
        var spike = new Spike { spike = t, phase = rhythm.NeighbourPhase(t, trap.Phase), depth = depth };
        System.Array.Resize(ref rhythm.spikes, rhythm.spikes.Length + 1);
        System.Array.Resize(ref rhythm.states, rhythm.states.Length + 1);
        int i = rhythm.spikes.Length - 1;
        rhythm.spikes[i] = spike;
        rhythm.states[i] = rhythm.MakeState(i);
    }

    // phase of the nearest driven spike facing the same way on the same surface within a run gap, else fallback
    private float NeighbourPhase(Transform t, float fallback)
    {
        float best = 1.3f;
        foreach (Spike s in spikes)
        {
            if (s == null || s.spike == null || Vector2.Dot(s.spike.up, t.up) < 0.9f) continue;
            Vector2 d = s.spike.position - t.position;
            float along = Mathf.Abs(Vector2.Dot(d, t.right)), across = Mathf.Abs(Vector2.Dot(d, t.up));
            if (across < 0.3f && along < best) { best = along; fallback = s.phase; }
        }
        return fallback;
    }

    private void Update()
    {
        clock += Time.deltaTime;
        Apply();
    }

    // time since spike i last popped out, 0..period
    private float LocalTime(int i) => Mathf.Repeat(clock + spikes[i].phase * period, period);

    private void Apply()
    {
        Vector3 cam = Camera.main != null ? Camera.main.transform.position : transform.position;
        float popNear = float.MaxValue, retractNear = float.MaxValue;

        for (int i = 0; i < states.Length; i++)
        {
            State s = states[i];
            if (s == null) continue;
            float u = LocalTime(i);
            Transform t = spikes[i].spike;
            if (s.trap != null && !s.trap.PopOutAndRetract)
            {
                // TOGGLE: static spike: fully out as placed, collider and renderer as authored, no sound or dust
                t.position = s.basePos;
                if (s.col != null) s.col.enabled = s.colOn;
                if (s.sr != null) s.sr.enabled = s.srOn;
                s.lastU = u; // re-ticking in Play mode picks the beat up without a false pop
                continue;
            }
            float ext = Extension(u, out float rattle);
            Vector3 offset = -s.up * Mathf.Round((1f - ext) * s.depth / Px) * Px;
            t.position = s.basePos + offset + t.right * rattle; // SPIKES: sideways to the spike, so wall spikes shake along the wall
            if (s.col != null) s.col.enabled = ext >= 0.5f;
            if (s.sr != null) s.sr.enabled = ext > 0f;

            float d = Vector2.Distance(cam, s.basePos);
            if (u < s.lastU) // wrapped round: popped out
            {
                popNear = Mathf.Min(popNear, d);
                if (d < hearMax) PuffDust(s);
            }
            else if (s.lastU < extendedTime && u >= extendedTime) retractNear = Mathf.Min(retractNear, d);
            s.lastU = u;
        }

        AudioManager audio = AudioManager.Instance;
        if (audio == null) return;
        if (popNear < hearMax && extendFrame != Time.frameCount)
        {
            extendFrame = Time.frameCount;
            audio.PlaySfxOneShot(extendSound, extendVolume * Falloff(popNear));
        }
        if (retractNear < hearMax && retractFrame != Time.frameCount)
        {
            retractFrame = Time.frameCount;
            audio.PlaySfxOneShot(retractSound, retractVolume * Falloff(retractNear));
        }
    }

    // 0 = sunk in the stone, 1 = fully out; rattle is a sideways shake (world units) during the warning peek
    private float Extension(float u, out float rattle)
    {
        rattle = 0f;
        float peek = Mathf.Clamp01(peekLift / retractDepth);
        if (u < popTime)
        {
            float k = 1f - u / popTime;
            return peek + (1f - peek) * (1f - k * k);
        }
        if (u < extendedTime) return 1f;
        if (u < extendedTime + retractTime) return 1f - Mathf.SmoothStep(0f, 1f, (u - extendedTime) / retractTime);
        float w = u - (period - warnTime);
        if (w < 0f) return 0f;
        rattle = (Mathf.FloorToInt(w / 0.05f) % 2 == 0 ? 1f : -1f) * Px;
        return peek * Mathf.SmoothStep(0f, 1f, w / 0.1f);
    }

    private float Falloff(float d) => 1f - Mathf.Clamp01((d - hearFull) / Mathf.Max(0.01f, hearMax - hearFull));

    private void PuffDust(State s)
    {
        Vector3 surface = s.basePos - s.up * surfaceOffset * s.depth / retractDepth;
        float angle = Mathf.Atan2(s.up.y, s.up.x) * Mathf.Rad2Deg;
        for (int side = -1; side <= 1; side += 2)
            PixelFX.Burst(surface + Vector3.right * side * 0.25f, 3, 0.8f, 1.8f, Dust, 0.2f, 0.4f, 1, PixelShape.Square,
                PixelMotion.Fall, false, PixelLayer.Front, angle - side * 50f, 40f);
    }
}
