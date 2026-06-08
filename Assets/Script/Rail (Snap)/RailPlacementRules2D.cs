using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ✅ Occupancy-only Rail Placement Rules
/// - Physics2D 기반 벽/오브젝트/선분 판정 제거
/// - GridOccupancy2D(점유 맵)만으로:
///   1) endpoint 근처 벽/점유
///   2) 선분(두께 포함)이 지나가는 셀의 벽/점유
///   를 판정한다.
/// 
/// 주의:
/// - railMask(레일-레일 겹침)은 여기서 다루지 않는다(기존 그래프/중복edge/노드제약으로 충분).
/// - placedMask/wallMask/railMask 파라미터는 "호출부 호환"을 위해 유지하지만, 실제 판정은 occupancy 데이터만 사용한다.
/// </summary>
public static class RailPlacementRules2D
{
    // =========================================================
    // Public API (kept for compatibility)
    // =========================================================

    public static bool ValidateEndpointCandidate(
        RailPlacementRuleProfile2D profile,
        GridManager grid,
        Vector2 rawWorld,
        out Vector2 resolvedWorld,
        PlacementObject ignoreA = null,
        PlacementObject ignoreB = null
    )
    {
        resolvedWorld = rawWorld;
        if (grid == null) return false;

        var occ = GridOccupancy2D.Instance;
        if (occ == null) return true;

        occ.EnsureBaked();

        // profile.wallMask는 "의미상" 유지하되, 실제 벽 판정은 occupancy(-1)만 본다.
        if (profile != null && profile.wallMask.value != 0)
        {
            if (occ.IsWallCell(grid.WorldToCell(rawWorld)))
                return false;
        }

        // placed 충돌은 이 API에선 최소 유지(기존 정책). 필요하면 여기서도 체크 가능.
        return true;
    }
    /// <summary>
    /// ✅ 통일 진입점(호출부 호환 유지)
    /// - Physics 제거
    /// - Occupancy만 사용
    /// </summary>
    public static bool CanPlaceRailSpan_WithMasks(
        GridManager grid,
        RailSpan2D rail,
        Vector2 startWorld,
        Vector2 endWorld,
        LayerMask wallMask,
        LayerMask placedMask,
        LayerMask railMask,
        bool allowStartInsideWall,
        bool allowEndInsideWall,
        float endpointAllowRadius,
        IReadOnlyList<PlacementObject> ignoreOwners = null,
        RailSpan2D ignoreRail = null,
        float placedOwnerAllowPenetration = 0f,
        bool useSegmentWallCheck = true,
        float ignoreOwnerRelaxTotalCells = 1f,
        bool endpointCellOnlyA = false,
        bool endpointCellOnlyB = false
    )
    {
        if (grid == null || rail == null) return false;

        var occ = GridOccupancy2D.Instance;
        if (occ == null) return true; // occ가 없으면 막지 않음(개발 중 안전장치)

        occ.EnsureBaked();

        // ignore owner ids (최대 2개까지만 공식 파라미터가 있으니, 초과는 extraSet 사용)
        int ignoreAId = 0, ignoreBId = 0;
        HashSet<int> extraIgnore = null;
        if (ignoreOwners != null && ignoreOwners.Count > 0)
        {
            if (ignoreOwners[0] != null) ignoreAId = ignoreOwners[0].GetInstanceID();
            if (ignoreOwners.Count > 1 && ignoreOwners[1] != null) ignoreBId = ignoreOwners[1].GetInstanceID();
            if (ignoreOwners.Count > 2)
            {
                extraIgnore = new HashSet<int>();
                for (int i = 2; i < ignoreOwners.Count; i++)
                    if (ignoreOwners[i] != null)
                        extraIgnore.Add(ignoreOwners[i].GetInstanceID());
            }
        }

        float gridStep = GetGridStep(grid);
        float half = gridStep * 0.5f;

        // endpoint radius -> cell radius
        float endpointR = Mathf.Max(0f, endpointAllowRadius);
        int endpointCells = Mathf.Max(0, Mathf.FloorToInt(endpointR / gridStep));


        // rail thickness -> expand cell radius (선분이 지나가는 셀 확장)
        float thick = Mathf.Max(0.001f, rail.thickness);
        int thickCells = Mathf.Max(0, Mathf.CeilToInt((thick * 0.5f) / gridStep));

        // ignore owner relax cells (TOTAL cells across -> half extent)
        int relaxHalfCells = Mathf.Max(0, Mathf.FloorToInt((Mathf.Max(1f, ignoreOwnerRelaxTotalCells) - 1f) * 0.5f));

        // 1) endpoint wall check (occupancy wall only)
        if (!allowStartInsideWall)
        {
            if (EndpointHitsWallCells(grid, occ, startWorld, endpointCells))
                return false;
        }
        if (!allowEndInsideWall)
        {
            if (EndpointHitsWallCells(grid, occ, endWorld, endpointCells))
                return false;
        }

        // 2) endpoint placed check (endpointCellOnly 옵션 반영)
        //    - endpointCellOnly=true면 "끝점 셀"만 체크
        //    - 아니면 endpointCells 범위까지 체크
        if (EndpointHitsPlacedCells(
                grid, occ, startWorld,
                radiusCells: endpointCellOnlyA ? 0 : endpointCells,
                ignoreAId, ignoreBId, extraIgnore))
            return false;

        if (EndpointHitsPlacedCells(
                grid, occ, endWorld,
                radiusCells: endpointCellOnlyB ? 0 : endpointCells,
                ignoreAId, ignoreBId, extraIgnore))
            return false;

        // 3) segment check (✅ 드래그와 동일한 샘플링 방식으로 통일)
        if (useSegmentWallCheck)
        {

            // ✅ 스냅 예외 반경: "총 허용 칸 수"를 월드 반경으로 변환
            // - 최소: endpointAllowRadius / 레일 두께 절반 / gridStep*ignoreOwnerRelaxTotalCells 중 최대
            float baseR = Mathf.Max(endpointAllowRadius, rail.thickness * 0.5f);
            float relaxR = Mathf.Max(baseR, gridStep * Mathf.Max(0f, ignoreOwnerRelaxTotalCells));

            if (SegmentHitsOccupied_WithSnapExceptions_NoSync(
                    grid,
                    startWorld,
                    endWorld,
                    rail.thickness,
                    ignoreAId, relaxR, endpointCellOnlyA,
                    ignoreBId, relaxR, endpointCellOnlyB
                ))
                return false;
        }



        return true;
    }

    // =========================================================
    // Occupancy checks
    // =========================================================

    static bool EndpointHitsWallCells(GridManager grid, GridOccupancy2D occ, Vector2 world, int radiusCells)
    {
        Vector2Int c0 = grid.WorldToCell(world);
        for (int y = -radiusCells; y <= radiusCells; y++)
        {
            for (int x = -radiusCells; x <= radiusCells; x++)
            {
                var c = new Vector2Int(c0.x + x, c0.y + y);
                if (occ.IsWallCell(c)) return true;
            }
        }
        return false;
    }

    static bool EndpointHitsPlacedCells(
        GridManager grid,
        GridOccupancy2D occ,
        Vector2 world,
        int radiusCells,
        int ignoreAId,
        int ignoreBId,
        HashSet<int> extraIgnore
    )
    {
        Vector2Int c0 = grid.WorldToCell(world);
        for (int y = -radiusCells; y <= radiusCells; y++)
        {
            for (int x = -radiusCells; x <= radiusCells; x++)
            {
                var c = new Vector2Int(c0.x + x, c0.y + y);
                // placed이면서 ignore가 아니면 blocked
                if (occ.IsPlacedCellOtherThan(c, ignoreAId, ignoreBId, extraIgnore))
                    return true;

                // wall은 여기서 체크 안 함(EndpointHitsWallCells에서 처리)
            }
        }
        return false;
    }


    // =========================================================
    // Line traversal helpers
    // =========================================================

    static float GetGridStep(GridManager grid)
    {
        Vector2 p0 = grid.CellToWorld(Vector2Int.zero);
        Vector2 p1 = grid.CellToWorld(Vector2Int.right);
        float step = Vector2.Distance(p0, p1);
        return (step > 0f) ? step : 1f;
    }

    // ==============================
    // Compatibility helpers (legacy calls)
    // ==============================

    public static bool IsWallAt_NoSync(RailSpan2D rail, Vector2 worldPos, float radius)
    {
        if (rail == null) return false;
        return IsWallAtFiltered_WithOccupancy_NoSync(rail.grid, worldPos, radius, rail.wallMask);
    }

    public static bool IsWallAtFiltered_NoSync(RailSpan2D rail, Vector2 worldPos, float radius, Transform ignoreA, Transform ignoreB)
    {
        // ✅ Occupancy-only: ignoreA/B 의미 없음(콜라이더 기반이 아님)
        if (rail == null) return false;
        return IsWallAtFiltered_WithOccupancy_NoSync(rail.grid, worldPos, radius, rail.wallMask);
    }

    /// <summary>
    /// ✅ 기존 호출부 호환용: "벽인가?"를 오직 Occupancy(벽 셀)로만 판단.
    /// </summary>
    public static bool IsWallAtFiltered_WithOccupancy_NoSync(GridManager grid, Vector2 worldPos, float radius, LayerMask wallMask)
    {
        var occ = GridOccupancy2D.Instance;
        if (grid == null || occ == null) return false;
        occ.EnsureBaked();

        // 그리드 한 칸 월드 크기 추정
        Vector2 p0 = grid.CellToWorld(Vector2Int.zero);
        Vector2 p1 = grid.CellToWorld(Vector2Int.right);
        float step = Vector2.Distance(p0, p1);
        if (step <= 0f) step = 1f;

        float half = step * 0.5f;
        float r = Mathf.Max(0.001f, radius);
        int rCells = Mathf.CeilToInt(r / step) + 1;

        Vector2Int c0 = grid.WorldToCell(worldPos);

        for (int y = -rCells; y <= rCells; y++)
            for (int x = -rCells; x <= rCells; x++)
            {
                var cell = new Vector2Int(c0.x + x, c0.y + y);
                if (!occ.IsWallCell(cell)) continue;

                // cell AABB vs point distance
                Vector2 center = grid.CellToWorld(cell);
                float dx = Mathf.Max(0f, Mathf.Abs(worldPos.x - center.x) - half);
                float dy = Mathf.Max(0f, Mathf.Abs(worldPos.y - center.y) - half);
                if (dx * dx + dy * dy <= r * r)
                    return true;
            }

        return false;
    }

    static readonly HashSet<Vector2Int> _tmpVirtualRailCellsSet = new HashSet<Vector2Int>(1024);
    static readonly List<Vector2Int> _tmpVirtualRailCellsList = new List<Vector2Int>(1024);

    static List<Vector2Int> ComputeVirtualRailCellsPrecise(GridManager grid, Vector2 a, Vector2 b, float rWorld)
    {
        _tmpVirtualRailCellsSet.Clear();
        _tmpVirtualRailCellsList.Clear();

        if (grid == null) return _tmpVirtualRailCellsList;

        float cellSize = Mathf.Max(0.0001f, grid.cellSize);
        float half = cellSize * 0.5f;
        float r2 = rWorld * rWorld;

        Vector2 minW = Vector2.Min(a, b) - Vector2.one * rWorld;
        Vector2 maxW = Vector2.Max(a, b) + Vector2.one * rWorld;
        Vector2Int cMin = grid.WorldToCell(minW);
        Vector2Int cMax = grid.WorldToCell(maxW);

        int x0 = Mathf.Min(cMin.x, cMax.x);
        int x1 = Mathf.Max(cMin.x, cMax.x);
        int y0 = Mathf.Min(cMin.y, cMax.y);
        int y1 = Mathf.Max(cMin.y, cMax.y);

        for (int x = x0; x <= x1; x++)
        {
            for (int y = y0; y <= y1; y++)
            {
                Vector2Int cell = new Vector2Int(x, y);
                Vector2 c = grid.CellToWorld(cell);
                Vector2 bbMin = c + new Vector2(-half, -half);
                Vector2 bbMax = c + new Vector2(half, half);

                float d2 = DistanceSq_SegmentAABB(a, b, bbMin, bbMax);
                if (d2 <= r2) _tmpVirtualRailCellsSet.Add(cell);
            }
        }

        foreach (var c in _tmpVirtualRailCellsSet) _tmpVirtualRailCellsList.Add(c);
        return _tmpVirtualRailCellsList;
    }

    static float DistanceSq_SegmentAABB(Vector2 a, Vector2 b, Vector2 bbMin, Vector2 bbMax)
    {
        if (SegmentIntersectsAABB(a, b, bbMin, bbMax)) return 0f;

        Vector2 r0 = new Vector2(bbMin.x, bbMin.y);
        Vector2 r1 = new Vector2(bbMax.x, bbMin.y);
        Vector2 r2 = new Vector2(bbMax.x, bbMax.y);
        Vector2 r3 = new Vector2(bbMin.x, bbMax.y);

        float d01 = DistanceSq_SegmentSegment(a, b, r0, r1);
        float d12 = DistanceSq_SegmentSegment(a, b, r1, r2);
        float d23 = DistanceSq_SegmentSegment(a, b, r2, r3);
        float d30 = DistanceSq_SegmentSegment(a, b, r3, r0);

        return Mathf.Min(Mathf.Min(d01, d12), Mathf.Min(d23, d30));
    }

    static bool SegmentIntersectsAABB(Vector2 a, Vector2 b, Vector2 bbMin, Vector2 bbMax)
    {
        float t0 = 0f, t1 = 1f;
        Vector2 d = b - a;

        if (!Clip(-d.x, a.x - bbMin.x, ref t0, ref t1)) return false;
        if (!Clip(d.x, bbMax.x - a.x, ref t0, ref t1)) return false;
        if (!Clip(-d.y, a.y - bbMin.y, ref t0, ref t1)) return false;
        if (!Clip(d.y, bbMax.y - a.y, ref t0, ref t1)) return false;

        return true;
    }

    static bool Clip(float p, float q, ref float t0, ref float t1)
    {
        if (Mathf.Abs(p) < 1e-8f)
            return q >= 0f;

        float r = q / p;
        if (p < 0f)
        {
            if (r > t1) return false;
            if (r > t0) t0 = r;
        }
        else
        {
            if (r < t0) return false;
            if (r < t1) t1 = r;
        }
        return true;
    }

    static float DistanceSq_SegmentSegment(Vector2 p1, Vector2 q1, Vector2 p2, Vector2 q2)
    {
        Vector2 d1 = q1 - p1;
        Vector2 d2 = q2 - p2;
        Vector2 r = p1 - p2;
        float aLen = Vector2.Dot(d1, d1);
        float eLen = Vector2.Dot(d2, d2);
        float f = Vector2.Dot(d2, r);

        float s, t;

        if (aLen <= 1e-8f && eLen <= 1e-8f)
            return (p1 - p2).sqrMagnitude;

        if (aLen <= 1e-8f)
        {
            s = 0f;
            t = Mathf.Clamp01(f / eLen);
        }
        else
        {
            float c = Vector2.Dot(d1, r);
            if (eLen <= 1e-8f)
            {
                t = 0f;
                s = Mathf.Clamp01(-c / aLen);
            }
            else
            {
                float bDot = Vector2.Dot(d1, d2);
                float denom = aLen * eLen - bDot * bDot;
                if (denom != 0f)
                    s = Mathf.Clamp01((bDot * f - c * eLen) / denom);
                else
                    s = 0f;

                t = (bDot * s + f) / eLen;

                if (t < 0f)
                {
                    t = 0f;
                    s = Mathf.Clamp01(-c / aLen);
                }
                else if (t > 1f)
                {
                    t = 1f;
                    s = Mathf.Clamp01((bDot - c) / aLen);
                }
            }
        }

        Vector2 c1 = p1 + d1 * s;
        Vector2 c2 = p2 + d2 * t;
        return (c1 - c2).sqrMagnitude;
    }

    public static bool SegmentHitsOccupied_WithSnapExceptions_NoSync(
       GridManager grid,
       Vector2 a,
       Vector2 b,
       float thickness,
       int ignoreOwnerIdA,
       float ignoreRadiusA,
       bool endpointCellOnlyA,
       int ignoreOwnerIdB,
       float ignoreRadiusB,
       bool endpointCellOnlyB
   )
    {

        var occ = GridOccupancy2D.Instance;
        if (occ == null || grid == null) return false;

        occ.EnsureBaked();

        float step = GetGridStep(grid);
        if (step <= 0.0001f) step = 1f;

        float endpointOnlySq = (step * 0.51f) * (step * 0.51f);
        float ignoreASq = Mathf.Max(0f, ignoreRadiusA) * Mathf.Max(0f, ignoreRadiusA);
        float ignoreBSq = Mathf.Max(0f, ignoreRadiusB) * Mathf.Max(0f, ignoreRadiusB);

        float rWorld = Mathf.Max(0f, thickness * 0.5f);
        var cells = ComputeVirtualRailCellsPrecise(grid, a, b, rWorld);

        for (int i = 0; i < cells.Count; i++)
        {
            Vector2Int cell = cells[i];
            int ownerId = occ.GetOwnerIdAtCell(cell);
            if (ownerId == 0) continue;
            if (ownerId == -1) return true;

            Vector2 cellCenter = grid.CellToWorld(cell);

            if (ignoreOwnerIdA != 0 && ownerId == ignoreOwnerIdA)
            {
                float dSqA = (cellCenter - a).sqrMagnitude;
                if (dSqA <= ignoreASq) continue;
                if (endpointCellOnlyA && dSqA <= endpointOnlySq) continue;
            }

            if (ignoreOwnerIdB != 0 && ownerId == ignoreOwnerIdB)
            {
                float dSqB = (cellCenter - b).sqrMagnitude;
                if (dSqB <= ignoreBSq) continue;
                if (endpointCellOnlyB && dSqB <= endpointOnlySq) continue;
            }

            return true;
        }

        return false;
    }
}
