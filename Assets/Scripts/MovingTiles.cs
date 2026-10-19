using UnityEngine;

public class MovingTiles : MonoBehaviour
{
    [SerializeField] private Transform pointA;
    [SerializeField] private Transform pointB;
    [SerializeField] private GameObject objToMove;
    [SerializeField] private float speed;
    [Tooltip("GHOSTSYNC: keep in step with this patrol instead of running free (empty = a free patrol). Give both the same length and speed.")]
    [SerializeField] private MovingTiles followPatrol;
    [Tooltip("GHOSTSYNC: how many seconds this patrol runs behind followPatrol.")]
    [SerializeField] private float followLag;
    public bool enemyTurn;
    private Vector3 CurTargetPos;
    private Vector3 originalScale;

    /// <summary>SLASH: the object this patrol moves, and the x range of the patrol (for the samurai's dash).</summary>
    public Transform ObjToMove => objToMove != null ? objToMove.transform : null;
    public float MinX => Mathf.Min(pointA.position.x, pointB.position.x);
    public float MaxX => Mathf.Max(pointA.position.x, pointB.position.x);

    /// <summary>
    /// GHOSTSYNC (2026-10-19): seconds into the patrol's loop, read from where the object is and which way it is heading:
    /// 0 at pointA setting off for pointB, half a loop at pointB turning back. A follower patrol uses it to stay in step.
    /// </summary>
    public float LoopTime
    {
        get
        {
            bool backToA = CurTargetPos == pointA.position;
            Vector3 from = backToA ? pointB.position : pointA.position;
            Vector3 to = backToA ? pointA.position : pointB.position;
            float legLength = Vector3.Distance(from, to);
            if (legLength <= 0f || speed <= 0f) return 0f;
            float along = Mathf.Clamp(Vector3.Dot(objToMove.transform.position - from, to - from) / legLength, 0f, legLength);
            return ((backToA ? legLength : 0f) + along) / speed;
        }
    }


    void Start()
    {
        CurTargetPos = pointB.position;
        originalScale = objToMove.transform.localScale;
    }


    void Update()
    {
        if (followPatrol != null) FollowPatrol(); // GHOSTSYNC:
        else MoveTile();
    }

    // GHOSTSYNC: put the object where its own loop is followLag seconds behind the leader's. Two free patrols don't
    // stay in step: a respawn puts each enemy back at its start but keeps the way it was heading, so after a death the
    // pair ends up in a random timing. A follower always comes back into step.
    private void FollowPatrol()
    {
        float legTime = speed > 0f ? Vector3.Distance(pointA.position, pointB.position) / speed : 0f;
        if (legTime <= 0f) return;
        float t = Mathf.Repeat(followPatrol.LoopTime - followLag, 2f * legTime);
        bool backToA = t >= legTime;
        Vector3 from = backToA ? pointB.position : pointA.position;
        CurTargetPos = backToA ? pointA.position : pointB.position;
        objToMove.transform.position = Vector3.Lerp(from, CurTargetPos, (backToA ? t - legTime : t) / legTime);
        enemyTurn = backToA;
        FlipObject();
    }

    private void MoveTile()
    {

        objToMove.transform.position = Vector3.MoveTowards(objToMove.transform.position, CurTargetPos, Time.deltaTime * speed);


        if (objToMove.transform.position == pointB.position)
        {
            enemyTurn = true;
            CurTargetPos = pointA.position;
        }

        if (objToMove.transform.position == pointA.position)
        {
            enemyTurn = false;
            CurTargetPos = pointB.position;
        }

        FlipObject();
    }

    private void FlipObject()
    {

        if (CurTargetPos.x < objToMove.transform.position.x)
        {
            objToMove.transform.localScale = new Vector3(-Mathf.Abs(originalScale.x), originalScale.y, originalScale.z);
        }

        else if (CurTargetPos.x > objToMove.transform.position.x)
        {
            objToMove.transform.localScale = new Vector3(Mathf.Abs(originalScale.x), originalScale.y, originalScale.z);
        }
    }
}
