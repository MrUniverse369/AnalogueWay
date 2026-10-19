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
        FollowPlayer();
    }
    private void FollowPlayer()
    {
        targetPos = TargetPosition();
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
        transform.position = TargetPosition();
    }

    /// <summary>LOADING: true when the camera already sits where it would settle on Harvey.</summary>
    public bool IsOnPlayer(float tolerance) => pObj == null || (transform.position - TargetPosition()).sqrMagnitude <= tolerance * tolerance;
}
