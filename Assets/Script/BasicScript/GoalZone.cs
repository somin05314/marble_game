using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Collider2D))]
public class GoalZone2D : MonoBehaviour, IPoResettable
{
    [Header("Events")]
    [Tooltip("구슬이 골에 들어왔을 때 호출")]
    public UnityEvent onReached;

    bool _reached;
    public bool IsReached => _reached;

    void Reset()
    {
        var col = GetComponent<Collider2D>();
        if (col != null)
            col.isTrigger = true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (_reached) return;
        if (GameModeManager.Instance == null) return;
        if (GameModeManager.Instance.currentMode != GameMode.Play) return;

        var marble = other.GetComponent<Marble>();
        if (marble == null) return;

        _reached = true;

        onReached?.Invoke();

        // 도착한 구슬 삭제가 필요하면 사용
        // Destroy(marble.gameObject, 0.05f);

        GameModeManager.Instance.OnGoalReached(this);
    }

    public void ResetState()
    {
        _reached = false;
    }
}