using System.Collections.Generic;
using UnityEngine;

public static class RailNodeMoveBroadcaster2D
{
    static readonly HashSet<RailSnapNode2D> _dirty = new();
    static RailNodeMoveBroadcasterRunner _runner;

    public static void MarkMoved(RailSnapNode2D node)
    {
        if (node == null) return;
        _dirty.Add(node);
        EnsureRunner();
        _runner.RequestFlush();
    }

    static void EnsureRunner()
    {
        if (_runner != null) return;

        var go = new GameObject("RailNodeMoveBroadcasterRunner");
        Object.DontDestroyOnLoad(go);
        _runner = go.AddComponent<RailNodeMoveBroadcasterRunner>();
    }

    internal static void Flush()
    {
        if (_dirty.Count == 0) return;

#if UNITY_2022_2_OR_NEWER
        var rails = Object.FindObjectsByType<RailSpan2D>(FindObjectsSortMode.None);
#else
        var rails = Object.FindObjectsOfType<RailSpan2D>();
#endif

        var mgr = RailSnapNodeManager.Instance;

        foreach (var r in rails)
        {
            if (r == null) continue;

            // 노드 비어있으면 보정
            if (mgr != null)
            {
                if (r.startNode == null) r.startNode = mgr.GetOrCreate(r.start);
                if (r.endNode == null) r.endNode = mgr.GetOrCreate(r.end);
            }

            bool hit =
                (r.startNode != null && _dirty.Contains(r.startNode)) ||
                (r.endNode != null && _dirty.Contains(r.endNode));

            if (hit)
                r.Refresh(syncFromNodes: true);
        }

        _dirty.Clear();
    }
}

public class RailNodeMoveBroadcasterRunner : MonoBehaviour
{
    bool _needFlush;

    public void RequestFlush() => _needFlush = true;

    void LateUpdate()
    {
        if (!_needFlush) return;
        _needFlush = false;
        RailNodeMoveBroadcaster2D.Flush();
    }
}
