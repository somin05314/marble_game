using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 삭제 직후 호출:
/// - IsAnchor=true 인 노드(루트)에서 연결된 컴포넌트만 남기고
/// - 나머지 레일/노드(섬)는 전부 제거
/// </summary>
public static class RailGraphCleanup2D
{
    public static void Cleanup()
    {
        // 1) 현재 존재하는 "활성" 레일/노드 수집 (inactive 제외)
#if UNITY_2022_2_OR_NEWER
        var rails = Object.FindObjectsByType<RailSpan2D>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None
        );
        var nodes = Object.FindObjectsByType<RailSnapNode2D>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None
        );
#else
        var rails = Object.FindObjectsOfType<RailSpan2D>(includeInactive: false);
        var nodes = Object.FindObjectsOfType<RailSnapNode2D>(includeInactive: false);
#endif
        if (rails == null) rails = new RailSpan2D[0];
        if (nodes == null) nodes = new RailSnapNode2D[0];

        // 2) 인접 리스트 구성: node -> connected rails
        var adj = new Dictionary<RailSnapNode2D, List<RailSpan2D>>(nodes.Length);

        for (int i = 0; i < nodes.Length; i++)
        {
            var n = nodes[i];
            if (n == null || !n.isActiveAndEnabled) continue;
            if (!adj.ContainsKey(n)) adj.Add(n, new List<RailSpan2D>(4));
        }

        for (int i = 0; i < rails.Length; i++)
        {
            var r = rails[i];
            if (r == null || !r.isActiveAndEnabled) continue;

            var a = r.startNode;
            var b = r.endNode;
            if (a == null || b == null) continue;

            if (!a.isActiveAndEnabled || !b.isActiveAndEnabled) continue;

            if (!adj.TryGetValue(a, out var la))
            {
                la = new List<RailSpan2D>(4);
                adj.Add(a, la);
            }
            if (!adj.TryGetValue(b, out var lb))
            {
                lb = new List<RailSpan2D>(4);
                adj.Add(b, lb);
            }

            la.Add(r);
            lb.Add(r);
        }

        // 3) BFS/DFS: AnchorRoot(=IsAnchor)에서 reachable 마킹
        var keepNodes = new HashSet<RailSnapNode2D>();
        var keepRails = new HashSet<RailSpan2D>();
        var q = new Queue<RailSnapNode2D>();

        for (int i = 0; i < nodes.Length; i++)
        {
            var n = nodes[i];
            if (n == null || !n.isActiveAndEnabled) continue;

            if (n.IsAnchor) // ✅ 루트 앵커에서 시작
            {
                if (keepNodes.Add(n))
                    q.Enqueue(n);
            }
        }

        while (q.Count > 0)
        {
            var cur = q.Dequeue();
            if (cur == null || !cur.isActiveAndEnabled) continue;

            if (!adj.TryGetValue(cur, out var list) || list == null) continue;

            for (int i = 0; i < list.Count; i++)
            {
                var r = list[i];
                if (r == null || !r.isActiveAndEnabled) continue;

                if (!keepRails.Add(r))
                    continue;

                var other = (r.startNode == cur) ? r.endNode : r.startNode;
                if (other == null || !other.isActiveAndEnabled) continue;

                if (keepNodes.Add(other))
                    q.Enqueue(other);
            }
        }

        // 4) keep에 없는 레일부터 삭제
        for (int i = 0; i < rails.Length; i++)
        {
            var r = rails[i];
            if (r == null || !r.isActiveAndEnabled) continue;
            if (keepRails.Contains(r)) continue;

            // ✅ 바로 검색에서 빠지게 비활성화 후 Destroy
            r.enabled = false;
            r.gameObject.SetActive(false);
            Object.Destroy(r.gameObject);
        }

        // 5) keep에 없는 노드 삭제 (앵커는 절대 삭제 금지)
        for (int i = 0; i < nodes.Length; i++)
        {
            var n = nodes[i];
            if (n == null || !n.isActiveAndEnabled) continue;
            if (n.IsAnchor) continue;
            if (keepNodes.Contains(n)) continue;

            n.enabled = false;
            n.gameObject.SetActive(false);
            Object.Destroy(n.gameObject);
        }

        RailGraphRevision.Bump("Cleanup"); // ✅ 여기 추가

        // ✅ 6) (중요) 두 번째 FindObjects로 orphan 정리하는 단계는 제거
        // Destroy는 프레임 끝이라 여기서 다시 Find하면 삭제 예정이 섞여서 꼬일 수 있음.
        // orphan(레일 0개) 노드는 "앵커에서 reachable"로 keep에 들어올 수 없으므로,
        // 5단계에서 이미 정리됨.
    }
}
