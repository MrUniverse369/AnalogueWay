using UnityEngine;

/// <summary>
/// TUTORIAL (2026-10-20): one practice target in the Basic Controls level. Either a trigger Harvey has to reach
/// (past a gap, on top of a ledge) or, when <see cref="enemies"/> is filled, a group of enemies he has to defeat.
/// TutorialGuide moves on to the next step once the goals of the current step are done.
/// </summary>
public class TutorialGoal : MonoBehaviour
{
    [Tooltip("The TutorialGuide step (0-based) this goal belongs to.")]
    [SerializeField] private int step;
    [Tooltip("Enemies that must all be defeated. Empty = Harvey has to reach this object's trigger.")]
    [SerializeField] private GameObject[] enemies = new GameObject[0];

    private bool reached;

    public int Step => step;
    public bool IsEnemyGoal => enemies != null && enemies.Length > 0;
    public bool Done => IsEnemyGoal ? EnemiesLeft == 0 : reached;

    /// <summary>Enemies still standing (dying ones count as beaten).</summary>
    public int EnemiesLeft
    {
        get
        {
            int left = 0;
            if (enemies == null) return 0;
            foreach (GameObject e in enemies)
            {
                if (e == null || !e.activeInHierarchy) continue;
                EnemyController ec = e.GetComponent<EnemyController>();
                if (ec == null || !ec.IsDying) left++;
            }
            return left;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("PlayerDetectionArea")) reached = true;
    }
}
