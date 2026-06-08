using System.Collections.Generic;
using UnityEngine;

public static class RailCleanupUtil
{
    public static void CleanupDisconnectedIslands(HashSet<RailSpan2D> preRemovedRails = null)
    {
#if UNITY_2022_2_OR_NEWER
        var rails = Object.FindObjectsByType<RailSpan2D>(FindObjectsSortMode.None);
        var nodes = Object.FindObjectsByType<RailSnapNode2D>(FindObjectsSortMode.None);
#else
        var rails = Object.FindObjectsOfType<RailSpan2D>();
        var nodes = Object.FindObjectsOfType<RailSnapNode2D>();
#endif

        // 1) node -> incident rails
        var incident = new Dictionary<RailSnapNode2D, List<RailSpan2D>>(nodes.Length);
        void AddIncident(RailSnapNode2D n, RailSpan2D r)
        {
            if (n == null || r == null) return;
            if (!incident.TryGetValue(n, out var list))
                incident[n] = list = new List<RailSpan2D>(4);
            list.Add(r);
        }

        for (int i = 0; i < rails.Length; i++)
        {
            var r = rails[i];
            if (r == null) continue;

            // preRemovedRails는 “이미 삭제 예정(블록 삭제로 끊기는 레일)” 같은 것들
            if (preRemovedRails != null && preRemovedRails.Contains(r))
                continue;

            AddIncident(r.startNode, r);
            AddIncident(r.endNode, r);
        }

        // 2) 루트(설치 가능 지점)에서 BFS로 reachable 표시
        var reachable = new HashSet<RailSnapNode2D>(nodes.Length);
        var q = new Queue<RailSnapNode2D>(nodes.Length);

        for (int i = 0; i < nodes.Length; i++)
        {
            var n = nodes[i];
            if (n == null) continue;

            // ✅ “설치 가능 지점”의 정의: Anchor 노드들
            if (!n.IsAnchor) continue;

            reachable.Add(n);
            q.Enqueue(n);
        }

        while (q.Count > 0)
        {
            var n = q.Dequeue();
            if (n == null) continue;

            if (!incident.TryGetValue(n, out var list)) continue;

            for (int i = 0; i < list.Count; i++)
            {
                var r = list[i];
                if (r == null) continue;

                var a = r.startNode;
                var b = r.endNode;

                var next = (a == n) ? b : (b == n ? a : null);
                if (next == null) continue;

                if (reachable.Add(next))
                    q.Enqueue(next);
            }
        }

        // 3) reachable이 아닌 “섬” 레일/노드 삭제
        // 레일부터 지우고 → 남은 노드 중 비-Anchor & 연결없음 지우기
        for (int i = 0; i < rails.Length; i++)
        {
            var r = rails[i];
            if (r == null) continue;

            if (preRemovedRails != null && preRemovedRails.Contains(r))
                continue;

            var a = r.startNode;
            var b = r.endNode;

            bool aOk = (a != null && reachable.Contains(a));
            bool bOk = (b != null && reachable.Contains(b));

            // 한쪽이라도 루트에서 도달 불가면 그 레일은 “설치 가능 점에서 떨어진 섬” 소속
            if (!aOk || !bOk)
                Object.Destroy(r.gameObject);
        }

        // 노드 정리: Anchor 아닌데 reachable 아니면 제거 (안전하게)
        for (int i = 0; i < nodes.Length; i++)
        {
            var n = nodes[i];
            if (n == null) continue;
            if (n.IsAnchor) continue;

            if (!reachable.Contains(n))
                Object.Destroy(n.gameObject);
        }
    }
}
