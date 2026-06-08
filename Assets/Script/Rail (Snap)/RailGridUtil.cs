using UnityEngine;

/// <summary>
/// Rail endpoint helper:
/// - world -> grid snap
/// - clamp length by min/max (continuous)
/// - snap again
/// - after snap, fix overflow/underflow by searching nearby grid points (stable)
/// </summary>
public static class RailGridUtil
{
    const float EPS = 0.0001f;

    // 스냅 보정 후보 탐색 반경(셀)
    const int FIX_RADIUS_MIN = 2;
    const int FIX_RADIUS_MAX = 6;

    public static Vector2 SnapToGrid(GridManager grid, Vector2 world)
    {
        // ⚠️ GridManager 구현에 따라 CellToWorld가 코너/중심일 수 있음.
        // 필요하면 grid.GetCellCenterWorld 같은 걸로 바꾸는 걸 추천.
        var cell = grid.WorldToCell(world);
        return grid.CellToWorld(cell);
    }

    public static float GetGridStep(GridManager grid)
    {
        Vector2 p0 = grid.CellToWorld(Vector2Int.zero);
        Vector2 px = grid.CellToWorld(Vector2Int.right);
        Vector2 py = grid.CellToWorld(Vector2Int.up);

        float sx = (px - p0).magnitude;
        float sy = (py - p0).magnitude;

        float step = Mathf.Min(sx, sy);
        if (step <= EPS) step = 0.25f; // fallback
        return step;
    }

    /// <summary>
    /// start는 이미 grid 위 점(스냅된 점)이라고 가정.
    /// desiredWorld는 마우스 월드 좌표.
    /// </summary>
    public static Vector2 GetSnappedClampedEnd(
        GridManager grid,
        Vector2 start,
        Vector2 desiredWorld,
        float minLen,
        float maxLen
    )
    {
        if (grid == null) return desiredWorld;

        float step = Mathf.Max(GetGridStep(grid), EPS);

        // 1) desired를 grid에 스냅(방향 안정화용)
        Vector2 desiredSnapped = SnapToGrid(grid, desiredWorld);

        Vector2 delta = desiredSnapped - start;
        float dist = delta.magnitude;
        if (dist <= EPS) return start;

        Vector2 dir = delta / dist;

        // 2) 연속 공간에서 거리 클램프
        float target = dist;
        if (maxLen > 0f) target = Mathf.Min(target, maxLen);
        if (minLen > 0f) target = Mathf.Max(target, minLen);

        Vector2 continuous = start + dir * target;

        // 3) 다시 grid 스냅 (기본 후보)
        Vector2 snapped = SnapToGrid(grid, continuous);

        // 제약 없으면 여기서 끝
        if (minLen <= 0f && maxLen <= 0f)
            return snapped;

        // 4) 스냅 때문에 조건을 살짝 벗어나는 경우를 “주변 셀 검색”으로 보정
        Vector2 fixedPoint = FixBySearchingNearbyGridPoints(
            grid, start, continuous, snapped, minLen, maxLen, step
        );

        return fixedPoint;
    }

    static Vector2 FixBySearchingNearbyGridPoints(
        GridManager grid,
        Vector2 start,
        Vector2 targetContinuous,
        Vector2 snapped,
        float minLen,
        float maxLen,
        float step
    )
    {
        float d = (snapped - start).magnitude;

        bool tooShort = (minLen > 0f && d < minLen - EPS);
        bool tooLong = (maxLen > 0f && d > maxLen + EPS);

        // 이미 OK
        if (!tooShort && !tooLong)
            return snapped;

        // 반경: 스냅 오차는 보통 1칸 내외지만, 상황에 따라 약간 더 보자
        // (step 기반으로 대충 추정)
        int radius = FIX_RADIUS_MIN;
        radius = Mathf.Clamp(radius, FIX_RADIUS_MIN, FIX_RADIUS_MAX);

        Vector2Int baseCell = grid.WorldToCell(targetContinuous);

        // 1) 조건 만족하는 후보 중 targetContinuous에 가장 가까운 점 선택
        bool found = false;
        Vector2 best = snapped;
        float bestScore = float.PositiveInfinity;

        for (int y = -radius; y <= radius; y++)
        {
            for (int x = -radius; x <= radius; x++)
            {
                Vector2Int cell = baseCell + new Vector2Int(x, y);
                Vector2 p = grid.CellToWorld(cell);

                float ds = (p - start).magnitude;
                if (ds <= EPS) continue;

                if (maxLen > 0f && ds > maxLen + EPS) continue;
                if (minLen > 0f && ds < minLen - EPS) continue;

                float score = (p - targetContinuous).sqrMagnitude;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = p;
                    found = true;
                }
            }
        }

        if (found)
            return best;

        // 2) “완벽히 만족” 후보가 없다면:
        // - maxLen이 있는 경우: maxLen 이하 중 가장 가까운 점
        // - minLen만 있는 경우: minLen 이상 중 가장 가까운 점
        // (최소한 start로 튀는 걸 막음)
        found = false;
        best = snapped;
        bestScore = float.PositiveInfinity;

        for (int y = -FIX_RADIUS_MAX; y <= FIX_RADIUS_MAX; y++)
        {
            for (int x = -FIX_RADIUS_MAX; x <= FIX_RADIUS_MAX; x++)
            {
                Vector2Int cell = baseCell + new Vector2Int(x, y);
                Vector2 p = grid.CellToWorld(cell);

                float ds = (p - start).magnitude;
                if (ds <= EPS) continue;

                if (maxLen > 0f && ds > maxLen + EPS) continue;
                if (minLen > 0f && ds < minLen - EPS) continue;

                float score = (p - targetContinuous).sqrMagnitude;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = p;
                    found = true;
                }
            }
        }

        // 그래도 없으면 기존 snapped 유지(최선)
        return found ? best : snapped;
    }
}
