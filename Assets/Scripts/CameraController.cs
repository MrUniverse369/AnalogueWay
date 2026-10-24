using UnityEngine;

public class CameraController : MonoBehaviour
{
    [SerializeField] private Transform pObj;

    [SerializeField] private float followAheadDefault;
    [SerializeField] private float followAhead;
    [SerializeField] private float smoothing;
    [SerializeField] private float yAxisOffset;
    [SerializeField] private GameObject pRef;
    private Vector3 targetPos;

    // CHEST: a short screen shake any script can ask for (a trapped chest exploding). The offset is added after the
    // follow and taken off again before the next follow, so the smoothing never drifts.
    private static float shakeUntil, shakeLength, shakeAmount;
    private Vector3 shakeOffset;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetShake() => shakeUntil = shakeLength = shakeAmount = 0f;

    /// <summary>CHEST: shake the following camera for 'time' seconds, up to 'amount' world units, fading out.</summary>
    public static void Shake(float time, float amount)
    {
        if (time <= 0f || amount <= 0f) return;
        float left = Mathf.Max(0f, shakeUntil - Time.time);
        if (shakeLength > 0f && amount < shakeAmount * left / shakeLength) return; // a stronger shake is still running
        shakeLength = time;
        shakeAmount = amount;
        shakeUntil = Time.time + time;
    }

    private static Vector3 ShakeOffset()
    {
        float left = shakeUntil - Time.time;
        if (left <= 0f || shakeLength <= 0f) return Vector3.zero;
        Vector2 o = Random.insideUnitCircle * shakeAmount * (left / shakeLength);
        return new Vector3(Mathf.Round(o.x * 16f) / 16f, Mathf.Round(o.y * 16f) / 16f, 0f); // whole art pixels
    }

    // RESPAWNHOLD (2026-10-23): after a respawn the camera glides back to Harvey in a set time (eased in and out)
    // instead of the slow follow, so the respawn wait stays short; the normal follow takes over where it ends.
    private float returnStart = -1f, returnTime = 1f;
    private Vector3 returnFrom;

    /// <summary>RESPAWNHOLD: true while the camera is gliding back to Harvey after a respawn.</summary>
    public bool Returning => returnStart >= 0f;

    /// <summary>RESPAWNHOLD: glide from here onto Harvey in 'seconds' (following him if he moves meanwhile).</summary>
    public void ReturnToPlayer(float seconds)
    {
        returnFrom = transform.position - shakeOffset;
        returnStart = Time.time;
        returnTime = Mathf.Max(0.05f, seconds);
    }

    public float SetyAxisOffset
    {
        set { yAxisOffset = value; }
    }
    public float Smoothing
    {
        get { return smoothing; }
        set { smoothing = value; }
    }
    public float FollowAhead
    {
        get { return followAhead; }
        set { followAhead = value; }
    }

    public float FollowAheadDefault
    {
        get { return followAheadDefault; }
    }
    // PERF: LateUpdate so the camera follows the player's final (interpolated) position for this frame;
    // in Update it could run before the player moved and jitter.
    void LateUpdate()
    {
        transform.position -= shakeOffset; // CHEST: follow from the unshaken position
        FollowPlayer();
        shakeOffset = ShakeOffset(); // CHEST:
        transform.position += shakeOffset;
    }
    private void FollowPlayer()
    {
        targetPos = TargetPosition();
        if (Returning) // RESPAWNHOLD:
        {
            float t = (Time.time - returnStart) / returnTime;
            if (t >= 1f) returnStart = -1f;
            transform.position = t >= 1f ? targetPos : Vector3.Lerp(returnFrom, targetPos, Mathf.SmoothStep(0f, 1f, t));
            return;
        }
        transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * Smoothing);
    }

    // where the camera is heading this frame (it eases towards this)
    private Vector3 TargetPosition()
    {
        Vector3 target = new Vector3(pObj.position.x - FollowAhead, pObj.transform.position.y + 1.5f, transform.position.z);
        if (pObj.localScale.x > 0) target = new Vector3(pObj.position.x + FollowAhead, pObj.transform.position.y - yAxisOffset, transform.position.z);
        if (pObj.localScale.x < 0) target = new Vector3(pObj.position.x - FollowAhead, pObj.transform.position.y - yAxisOffset, transform.position.z);
        return target;
    }

    /// <summary>LOADING: jump straight onto Harvey (LevelLoader does this while the level is hidden), no glide.</summary>
    public void SnapToPlayer()
    {
        if (pObj == null) return;
        returnStart = -1f; // RESPAWNHOLD: a snap ends a glide
        shakeOffset = Vector3.zero; // CHEST:
        transform.position = TargetPosition();
    }

    /// <summary>LOADING: true when the camera already sits where it would settle on Harvey.</summary>
    public bool IsOnPlayer(float tolerance) => pObj == null || (transform.position - TargetPosition()).sqrMagnitude <= tolerance * tolerance;
}
