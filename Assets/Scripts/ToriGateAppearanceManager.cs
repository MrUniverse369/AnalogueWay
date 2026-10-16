using AnalogueWay;
using UnityEngine;

public class ToriGateAppearanceManager : MonoBehaviour
{
    [SerializeField] private Animator animatorRef;
    private static readonly int LBAParam = Animator.StringToHash("LBA"); // PERF:
    private Animator ownAnimator; // PERF: was GetComponent<Animator>() twice per frame

    private void Awake()
    {
        ownAnimator = GetComponent<Animator>(); // PERF:
    }

    void Update()
    {
        if (changeWorld.SworldGateActive != true)
        {
            ownAnimator.SetBool(LBAParam, false);
            animatorRef.SetBool(LBAParam, false);
        }

        if (changeWorld.SworldGateActive && !changeWorld.lworldGateActive)
        {
            ownAnimator.SetBool(LBAParam, true);
            animatorRef.SetBool(LBAParam, true);
        }
    }
}

