using UnityEngine;
using System.Text;

public class ColliderStateWatcher2D : MonoBehaviour
{
    Collider2D col;
    bool lastEnabled;
    bool lastTrigger;

    void Awake()
    {
        col = GetComponent<Collider2D>();
        if (!col) { enabled = false; return; }
        lastEnabled = col.enabled;
        lastTrigger = col.isTrigger;
    }

    void Update()
    {
        if (!col) return;

        if (col.enabled != lastEnabled || col.isTrigger != lastTrigger)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"[ColliderChanged] {name} enabled:{lastEnabled}->{col.enabled} isTrigger:{lastTrigger}->{col.isTrigger}");
            sb.AppendLine(StackTraceUtility.ExtractStackTrace()); // ✅ 누가 건드렸는지 힌트

            Debug.Log(sb.ToString(), this);

            lastEnabled = col.enabled;
            lastTrigger = col.isTrigger;
        }
    }
}
