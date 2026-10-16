using UnityEngine;

[DefaultExecutionOrder(100)] // PERF: run after CameraController (order 0) so backgrounds use this frame's camera position
public class ParallaxBackGround : MonoBehaviour
{
    [SerializeField] private float startPos;
    [SerializeField] private float lenght;
    [SerializeField] private bool smallBackGround;
    [SerializeField] private float movement;
    [SerializeField] private GameObject cam;
    [SerializeField] private float parallaxEffect;
    [SerializeField] private bool isPlayerInBackGroundView;
    // Start is called before the first frame update
    void Start()
    {
        startPos = transform.position.x;
        lenght = GetComponent<SpriteRenderer>().bounds.size.x;
        smallBackGround = false;

    }

    // PERF: LateUpdate (every rendered frame, after the camera) instead of FixedUpdate (50 Hz), so backgrounds
    // move in step with the camera instead of stuttering against it.
    void LateUpdate()
    {
        float dist = cam.transform.position.x * parallaxEffect;
        movement = cam.transform.position.x * (1 - parallaxEffect);

        if (isPlayerInBackGroundView)
        {
            transform.position = new Vector3(startPos, transform.position.y, transform.position.z);
        }
        else
        {
            transform.position = new Vector3(startPos + dist, transform.position.y, transform.position.z);

            if (movement > startPos + lenght)
            {
                startPos += lenght;
            }
            else if (movement < startPos - lenght)
            {
                startPos -= lenght;
            }
        }
    }

    private void OnTriggerStay2D(Collider2D other)
    {

        if (other.gameObject.CompareTag("Player"))
        {
            isPlayerInBackGroundView = true;
        }

    }

    private void OnTriggerExit2D(Collider2D other)
    {
        isPlayerInBackGroundView = false;
    }
}


