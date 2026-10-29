using UnityEngine;

/// <summary>
/// YARNCHASE (2026-10-28): a Time Boost on the Time Trail's route: a stopwatch that adds seconds to the chase clock.
/// It shows (and can be taken) only while a chase runs, once per chase: TimeTrail.Begin puts every boost back, and
/// the end of a chase hides them all. Taking one plays the shared pickup motes, a high coin chime, and the clock's
/// "+2.5 SECONDS" on the banner.
/// </summary>
[RequireComponent(typeof(SpriteRenderer), typeof(Collider2D))]
public class TimeBoost : MonoBehaviour
{
    [SerializeField] private TimeTrail trail;
    [SerializeField, Min(0.5f)] private float seconds = 2.5f;
    [Tooltip("Two frames: plain and with a glint (Tools/Art/time_trail.py).")]
    [SerializeField] private Sprite[] frames;

    private SpriteRenderer sr;
    private Collider2D col;
    private bool taken;

    public float Seconds => seconds;
    public bool Taken => taken;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        col = GetComponent<Collider2D>();
        col.isTrigger = true;
    }

    private void Start()
    {
        SpriteAura.On(sr).Set("glow", new Color(1f, 0.89f, 0.47f, 0.6f), 2, 3.5f); // FX: gold, not the chi stone's aqua
        if (trail == null || !trail.Running) Show(false);
    }

    private void Update()
    {
        if (frames != null && frames.Length > 1 && sr.enabled)
            sr.sprite = frames[Mathf.Repeat(Time.time * 1.5f, 1f) < 0.8f ? 0 : 1];
    }

    /// <summary>Ready to take again (a new chase).</summary>
    public void ResetBoost()
    {
        taken = false;
        Show(true);
    }

    /// <summary>On or off as a whole (art, glow and trigger).</summary>
    public void Show(bool on)
    {
        if (sr == null) Awake();
        sr.enabled = on && !taken;
        col.enabled = on && !taken;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (taken || trail == null || !trail.Running || !other.CompareTag("PlayerDetectionArea")) return;
        taken = true;
        Show(false);
        ItemPickup.PickupMotes(transform.position, other.transform);
        trail.AddTime(seconds, transform.position);
    }
}
