using System.Collections.Generic;
using UnityEngine;

public static class RailSnapNodeUtil
{
    // 노드가 많아질 수 있으니 좀 더 넉넉히
    static readonly Collider2D[] hits = new Collider2D[64];

    // ✅ 기존 호환 (그대로 유지)
    public static bool TryPickNode(
        Vector2 worldPos,
        float radius,
        LayerMask nodeMask,
        out RailSnapNode2D node,
        bool syncTransforms = true
    )
    {
        return TryPickNode(worldPos, radius, nodeMask, out node, excludeNodes: null, syncTransforms: syncTransforms);
    }

    /// <summary>
    /// worldPos 주변에서 nodeMask에 해당하는 RailSnapNode2D 중 "가장 가까운 노드"를 찾는다.
    /// - excludeNodes에 들어있는 노드는 후보에서 제외 (SnapPoint 여러 개일 때 핵심)
    /// - NonAlloc 버퍼가 꽉 차면(=누락 가능) OverlapCircleAll로 안전 재탐색
    /// </summary>
    public static bool TryPickNode(
        Vector2 worldPos,
        float radius,
        LayerMask nodeMask,
        out RailSnapNode2D node,
        ISet<RailSnapNode2D> excludeNodes,
        bool syncTransforms = true
    )
    {
        node = null;

        if (radius <= 0f) return false;
        if (nodeMask == 0) return false;

        if (syncTransforms)
            Physics2D.SyncTransforms();

        int count = Physics2D.OverlapCircleNonAlloc(worldPos, radius, hits, nodeMask);
        if (count <= 0) return false;

        // ✅ 버퍼가 꽉 찼으면, 가까운 게 누락될 수 있음 -> 안전하게 전체 다시
        if (count >= hits.Length)
        {
            var all = Physics2D.OverlapCircleAll(worldPos, radius, nodeMask);
            return PickNearestFrom(all, worldPos, excludeNodes, out node);
        }

        // ✅ NonAlloc 결과에서 고르기
        float bestSqr = float.PositiveInfinity;

        for (int i = 0; i < count; i++)
        {
            var col = hits[i];
            if (col == null) continue;
            if (!col.enabled) continue;

            var n = col.GetComponentInParent<RailSnapNode2D>();
            if (n == null) continue;

            if (excludeNodes != null && excludeNodes.Contains(n))
                continue;

            float dSqr = ((Vector2)n.transform.position - worldPos).sqrMagnitude;
            if (dSqr < bestSqr)
            {
                bestSqr = dSqr;
                node = n;
            }
        }

        return node != null;
    }

    static bool PickNearestFrom(Collider2D[] cols, Vector2 worldPos, ISet<RailSnapNode2D> excludeNodes, out RailSnapNode2D node)
    {
        node = null;
        if (cols == null || cols.Length == 0) return false;

        float bestSqr = float.PositiveInfinity;

        for (int i = 0; i < cols.Length; i++)
        {
            var col = cols[i];
            if (col == null) continue;
            if (!col.enabled) continue;

            var n = col.GetComponentInParent<RailSnapNode2D>();
            if (n == null) continue;

            if (excludeNodes != null && excludeNodes.Contains(n))
                continue;

            float dSqr = ((Vector2)n.transform.position - worldPos).sqrMagnitude;
            if (dSqr < bestSqr)
            {
                bestSqr = dSqr;
                node = n;
            }
        }

        return node != null;
    }
}
