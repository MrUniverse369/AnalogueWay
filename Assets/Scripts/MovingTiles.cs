using UnityEngine;

public class MovingTiles : MonoBehaviour
{
    [SerializeField] private Transform pointA;
    [SerializeField] private Transform pointB;
    [SerializeField] private GameObject objToMove;
    [SerializeField] private float speed;
    public bool enemyTurn;
    private Vector3 CurTargetPos;
    private Vector3 originalScale;

    /// <summary>SLASH: the object this patrol moves, and the x range of the patrol (for the samurai's dash).</summary>
    public Transform ObjToMove => objToMove != null ? objToMove.transform : null;
    public float MinX => Mathf.Min(pointA.position.x, pointB.position.x);
    public float MaxX => Mathf.Max(pointA.position.x, pointB.position.x);


    void Start()
    {
        CurTargetPos = pointB.position;
        originalScale = objToMove.transform.localScale;
    }


    void Update()
    {
        MoveTile();
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
