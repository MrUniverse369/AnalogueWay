using AnalogueWay;
using UnityEngine;

public class ToriGateAppearanceManagerTwo : MonoBehaviour
{
    [SerializeField] private Animator animatorRef;

    void Update()
    {
        if (changeWorld.lworldGateActive != true)
        {
            gameObject.GetComponent<Animator>().SetBool("SFA", false);
            animatorRef.SetBool("SFA", false);
        }

        if (changeWorld.lworldGateActive && !changeWorld.SworldGateActive)
        {
            Debug.Log("Red tori being turned on");
            gameObject.GetComponent<Animator>().SetBool("SFA", true);
            animatorRef.SetBool("SFA", true);
        }
    }
}
