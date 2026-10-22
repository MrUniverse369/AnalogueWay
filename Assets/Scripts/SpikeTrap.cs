using UnityEngine;

/// <summary>
/// TOGGLE (2026-10-22): per-spike switch on Spikes.prefab for the SpikeRhythm pop-out / retract effect. Ticked (the
/// default) the spike pops out and retracts on the beat; a spike no SpikeRhythm lists yet (dragged into a level by
/// hand) joins the scene's rhythm when the level starts, beside a neighbouring run on that run's beat. Unticked it
/// stays fully out where it was placed, with its collider and damage as authored, and makes no rattle, dust or
/// sound. Works for floor, ceiling and wall spikes alike, and can be flipped in Play mode.
/// </summary>
public class SpikeTrap : MonoBehaviour
{
    [Tooltip("Pop out of the stone and retract on the SpikeRhythm beat. Off = the spike stays out, static.")]
    [SerializeField] private bool popOutAndRetract = true;
    [Tooltip("Beat offset (0-1) when this spike joins the rhythm by itself. Placed beside spikes already on the beat, it takes theirs.")]
    [SerializeField, Range(0f, 1f)] private float phase;
    [Tooltip("Used only when the scene has no SpikeRhythm yet and this spike makes one.")]
    [SerializeField] private AudioClip extendSound;
    [SerializeField] private AudioClip retractSound;

    private bool started, joined;

    public float Phase => phase;
    public AudioClip ExtendSound => extendSound;
    public AudioClip RetractSound => retractSound;

    public bool PopOutAndRetract
    {
        get => popOutAndRetract;
        set { popOutAndRetract = value; Join(); }
    }

    private void Start()
    {
        started = true;
        Join();
    }

    private void OnValidate() => Join(); // ticking it in the Inspector during Play mode

    private void Join()
    {
        if (!started || joined || !popOutAndRetract) return;
        joined = true;
        SpikeRhythm.Drive(this);
    }
}
