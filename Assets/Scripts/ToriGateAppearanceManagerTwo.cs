using AnalogueWay;
using UnityEngine;

public class ToriGateAppearanceManagerTwo : MonoBehaviour
{
    [SerializeField] private Animator animatorRef;
    private static readonly int SFAParam = Animator.StringToHash("SFA"); // PERF:
    private Animator ownAnimator; // PERF: was GetComponent<Animator>() twice per frame

    private void Awake()
    {
        ownAnimator = GetComponent<Animator>(); // PERF:
    }

    void Update()
    {
        if (changeWorld.lworldGateActive != true)
        {
            ownAnimator.SetBool(SFAParam, false);
            animatorRef.SetBool(SFAParam, false);
        }

        if (changeWorld.lworldGateActive && !changeWorld.SworldGateActive)
        {
            ownAnimator.SetBool(SFAParam, true);
            animatorRef.SetBool(SFAParam, true);
        }
    }
}
