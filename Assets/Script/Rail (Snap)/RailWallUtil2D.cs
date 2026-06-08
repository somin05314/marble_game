using UnityEngine;

public static class RailWallUtil2D
{
    static readonly RaycastHit2D[] _hits = new RaycastHit2D[64];
    static readonly Collider2D[] _overlap = new Collider2D[64];

    // ✅ 기존 호출부 호환(예전 코드 그대로 호출 가능)
    public static bool SegmentOverlapsWallThick(
        Vector2 a,
        Vector2 b,
        float thickness,
        LayerMask wallMask,
        float endpointBlockRadius,
        System.Func<Vector2, bool> isWallAt,
        bool allowStartInside,
        bool endAllowedByExistingNodeInWall,
        float gridStep,
        int ignoreCells
    )
    {
        return SegmentOverlapsWallThick(
            a, b, thickness, wallMask, endpointBlockRadius,
            isWallAt, allowStartInside, endAllowedByExistingNodeInWall,
            gridStep, ignoreCells,
            ignoreRoot: null,
            ignoreSnappedPORootA: null,
            ignoreSnappedPORootB: null
        );
    }

    // ✅ 중간 호환: ignoreRoot까지만 받는 호출부를 살려줌 (RailToolPlacer2D가 여기 걸림)
    public static bool SegmentOverlapsWallThick(
        Vector2 a,
        Vector2 b,
        float thickness,
        LayerMask wallMask,
        float endpointBlockRadius,
        System.Func<Vector2, bool> isWallAt,
        bool allowStartInside,
        bool endAllowedByExistingNodeInWall,
        float gridStep,
        int ignoreCells,
        Transform ignoreRoot
    )
    {
        return SegmentOverlapsWallThick(
            a, b, thickness, wallMask, endpointBlockRadius,
            isWallAt, allowStartInside, endAllowedByExistingNodeInWall,
            gridStep, ignoreCells,
            ignoreRoot,
            ignoreSnappedPORootA: null,
            ignoreSnappedPORootB: null
        );
    }


    // ✅ 새 오버로드: "스냅된 PO" 양쪽을 예외로 허용
    public static bool SegmentOverlapsWallThick(
        Vector2 a,
        Vector2 b,
        float thickness,
        LayerMask wallMask,
        float endpointBlockRadius,
        System.Func<Vector2, bool> isWallAt, // (선택) 캐시/occupancy 기반 빠른 판정용
        bool allowStartInside,
        bool endAllowedByExistingNodeInWall,
        float gridStep,
        int ignoreCells,
        Transform ignoreRoot,              // 자기 레일/핸들 루트 등
        Transform ignoreSnappedPORootA,    // ✅ 스냅된 PO A (통과 허용)
        Transform ignoreSnappedPORootB     // ✅ 스냅된 PO B (통과 허용)
    )
    {
        Physics2D.SyncTransforms();

        bool IsSnappedPOToIgnore(PlacementObject po)
        {
            if (po == null) return false;
            var t = po.transform;
            if (ignoreSnappedPORootA != null && t.IsChildOf(ignoreSnappedPORootA)) return true;
            if (ignoreSnappedPORootB != null && t.IsChildOf(ignoreSnappedPORootB)) return true;
            return false;
        }

        bool IsRealWallCollider(Collider2D col)
        {
            if (col == null) return false;
            if (col.isTrigger) return false;

            if (ignoreRoot != null && col.transform.IsChildOf(ignoreRoot))
                return false;

            // ✅ PO는 "스냅된 PO만" 예외, 나머지는 막는다
            var po = col.GetComponentInParent<PlacementObject>();
            if (po != null)
                return !IsSnappedPOToIgnore(po); // ignore면 false(벽 아님), 아니면 true(벽)

            // 레일/노드/핸들/스냅포인트는 벽 취급 X
            if (col.GetComponentInParent<RailSpan2D>() != null) return false;
            if (col.GetComponentInParent<RailSnapNode2D>() != null) return false;
            if (col.GetComponentInParent<RailEndpointHandle2D>() != null) return false;
            if (col.GetComponentInParent<SnapPoint>() != null) return false;

            return true;
        }

        // ✅ inside 판정도 동일한 필터를 적용해야 "한쪽만 연결해도 막히는" 케이스가 사라짐
        bool IsWallAtFiltered(Vector2 p)
        {
            // (1) 빠른 판정: 캐시가 false면 바로 false
            if (isWallAt != null && !isWallAt(p))
                return false;

            // (2) 캐시가 true거나 캐시가 없으면, 진짜 콜라이더 필터로 확정
            int count = Physics2D.OverlapCircleNonAlloc(p, endpointBlockRadius, _overlap, wallMask);
            for (int i = 0; i < count; i++)
            {
                if (IsRealWallCollider(_overlap[i]))
                    return true;
            }
            return false;
        }

        Vector2 ab = b - a;
        float abLen = ab.magnitude;
        if (abLen < 1e-6f) return false;

        Vector2 nAB = ab / abLen;

        bool aInside = IsWallAtFiltered(a);
        if (aInside && !allowStartInside) return true;

        bool bInside = IsWallAtFiltered(b);
        if (bInside && !endAllowedByExistingNodeInWall) return true;

        Vector2 start = aInside
            ? MoveOutsideWall(a, +1, nAB, abLen, endpointBlockRadius, IsWallAtFiltered)
            : (a + nAB * 0.001f);

        Vector2 end = bInside
            ? MoveOutsideWall(b, -1, nAB, abLen, endpointBlockRadius, IsWallAtFiltered)
            : (b - nAB * 0.001f);

        Vector2 dir = end - start;
        float dist = dir.magnitude;
        if (dist < 1e-6f) return false;

        Vector2 n = dir / dist;

        float radius = Mathf.Max(0.001f, thickness * 0.5f);

        float ignoreDist = endpointBlockRadius;
        if (ignoreCells > 0 && gridStep > 0f)
            ignoreDist = Mathf.Max(ignoreDist, gridStep * ignoreCells);

        float ignoreStart = (aInside && allowStartInside) ? ignoreDist : 0f;
        float ignoreEnd = (bInside && endAllowedByExistingNodeInWall) ? ignoreDist : 0f;

        int hitCount = Physics2D.CircleCastNonAlloc(start, radius, n, _hits, dist, wallMask);

        for (int i = 0; i < hitCount; i++)
        {
            var h = _hits[i];
            var col = h.collider;
            if (!IsRealWallCollider(col)) continue;

            if (ignoreStart > 0f && h.distance <= ignoreStart) continue;
            if (ignoreEnd > 0f && h.distance >= dist - ignoreEnd) continue;

            return true;
        }

        return false;
    }

    static Vector2 MoveOutsideWall(
        Vector2 origin,
        int sign,
        Vector2 n,
        float maxDist,
        float endpointBlockRadius,
        System.Func<Vector2, bool> isWallAtFiltered
    )
    {
        float maxAdvance = Mathf.Min(maxDist, endpointBlockRadius * 10f);
        int steps = 30;
        float step = maxAdvance / steps;

        for (int i = 0; i <= steps; i++)
        {
            Vector2 p = origin + n * (sign * i * step);
            if (!isWallAtFiltered(p))
                return p;
        }
        return origin;
    }
}
