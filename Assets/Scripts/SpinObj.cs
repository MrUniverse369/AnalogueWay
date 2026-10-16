using UnityEngine;

public class SpinObj : MonoBehaviour
{
    // Update is called once per frame
    void Update()
    {
        transform.Rotate(0,0,60,Space.Self);
    }
}
