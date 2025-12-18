using UnityEngine;

public class GoalZone : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Marble"))
            return;

        GameModeManager.Instance.OnGoalReached();
    }
}
