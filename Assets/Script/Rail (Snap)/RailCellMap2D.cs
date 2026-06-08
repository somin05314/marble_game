using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 레일이 "어느 Grid 셀을 점유하는지"를 캐시로 들고 있는 매니저.
/// - 레일이 생성/이동/삭제될 때만 UpdateRail/RemoveRail을 호출하면 됨.
/// - PO 드래그 중에는 Physics 쿼리 없이, 셀 단위로 "레일 있음/없음"을 O(1)에 가깝게 판단 가능.
/// </summary>
public class RailCellMap2D : MonoBehaviour
{

    // === Drag-time optimization ===
    // During drag preview we don't want to rebuild rail cell coverage every cell move.
    // We keep the map representing the last committed (stable) rail layout.
    int _suspendUpdatesCounter = 0;
    public bool IsUpdatesSuspended => _suspendUpdatesCounter > 0;

    public void SuspendUpdates()
    {
        _suspendUpdatesCounter++;
    }

    public void ResumeUpdates()
    {
        if (_suspendUpdatesCounter > 0) _suspendUpdatesCounter--;
    }

    public static RailCellMap2D Instance { get; private set; }

    [Header("Sampling (only when rail updates)")]
    [Tooltip("레일 점유 셀 계산 시, 선분을 샘플링하는 간격(셀 크기 대비 비율). 작을수록 더 정확.")]
    [Range(0.05f, 1f)]
    public float sampleStepCells = 0.25f;

    [Tooltip("최소 샘플링 간격(월드 단위)")]
    public float minSampleStepWorld = 0.05f;

    [Header("Thickness Expansion")]
    [Tooltip("레일 두께(thickness/2)만큼 셀 점유를 확장할 때 추가로 더하는 여유(월드 단위)")]
    public float extraRadiusWorld = 0.0f;

    [Header("Cell Coverage")]
    [Tooltip("레일 점유 셀 계산을 '주변 셀 확장'이 아니라 '레일 두께(반경)로 실제로 닿는 셀만' 포함하도록 합니다.")]
    public bool preciseCoverage = true;

    // cell -> count (겹친 레일 처리/안전한 제거용)
    readonly Dictionary<Vector2Int, int> _railCountByCell = new Dictionary<Vector2Int, int>(2048);

    // rail -> occupied cells (제거/갱신용)
    readonly Dictionary<RailSpan2D, List<Vector2Int>> _cellsByRail = new Dictionary<RailSpan2D, List<Vector2Int>>(256);

    // temp buffers
    readonly HashSet<Vector2Int> _tmpUnique = new HashSet<Vector2Int>();
    readonly List<Vector2Int> _tmpList = new List<Vector2Int>(256);

    // === Drag exclusion ===
    // While dragging a rail (or connected rails), we may want to temporarily remove their committed coverage
    // so they don't block themselves during preview checks. We only remove/restore coverage at drag begin/end.
    readonly HashSet<RailSpan2D> _dragExcludedRails = new HashSet<RailSpan2D>();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        // ✅ 씬에 이미 존재하는 레일을 초기 스캔해서 셀 맵을 구성
        var rails = FindObjectsByType<RailSpan2D>(FindObjectsSortMode.None);
        for (int i = 0; i < rails.Length; i++)
            UpdateRail(rails[i]);
    }


    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // -----------------------
    // Reset / Full rebuild
    // -----------------------
    /// <summary>
    /// 내부 캐시를 전부 비우고, 현재 씬에 존재하는 레일들을 다시 스캔해 셀맵을 재구성합니다.
    /// - 스냅샷 리셋(레일 Destroy/Instantiate) 이후 반드시 한 번 호출하는 것을 권장합니다.
    /// </summary>
    public void ResetAndRescanRails()
    {
        _railCountByCell.Clear();
        _cellsByRail.Clear();
        _dragExcludedRails.Clear();
        _tmpUnique.Clear();
        _tmpList.Clear();

        // suspend 상태가 남아있으면 영원히 갱신이 막히므로 안전하게 해제
        _suspendUpdatesCounter = 0;

#if UNITY_2022_2_OR_NEWER
        var rails = FindObjectsByType<RailSpan2D>(FindObjectsSortMode.None);
#else
        var rails = FindObjectsOfType<RailSpan2D>();
#endif
        for (int i = 0; i < rails.Length; i++)
            UpdateRail_Force(rails[i]);
    }


    public bool HasRailAtCell(Vector2Int cell)
    {
        return _railCountByCell.TryGetValue(cell, out int c) && c > 0;
    }

    public int GetRailCountAtCell(Vector2Int cell)
    {
        return _railCountByCell.TryGetValue(cell, out int c) ? c : 0;
    }

    /// <summary>
    /// 레일의 현재 start/end/thickness 기준으로 점유 셀을 다시 계산해 반영.
    /// </summary>

    public void UpdateRail(RailSpan2D rail)
    {
        if (rail == null) return;

        // ✅ 드래그 제외 중인 레일은 다시 굽지 않는다 (삭제 유지)
        if (_dragExcludedRails.Contains(rail)) return;

        if (IsUpdatesSuspended) return;
        UpdateRail_Force(rail);
    }

    /// <summary>
    /// 레일 제거/파괴 시 점유 셀을 map에서 제거.
    /// </summary>

    public void RemoveRail(RailSpan2D rail)
    {
        // Skip live updates while dragging; the map stays on last committed state.
        if (IsUpdatesSuspended) return;

        RemoveRail_Force(rail);
    }

    // -----------------------
    // Drag exclusion API (supports multi-rail drags)
    // -----------------------
    public void BeginExcludeRailForDrag(RailSpan2D rail)
    {
        if (rail == null) return;
        if (_dragExcludedRails.Add(rail))
        {
            // ✅ 등록 안 된 레일이면 먼저 굽고(Force), 그 다음 제거
            if (!_cellsByRail.ContainsKey(rail))
                UpdateRail_Force(rail);

            RemoveRail_Force(rail);
        }
    }

    public void BeginExcludeRailsForDrag(IEnumerable<RailSpan2D> rails)
    {
        if (rails == null) return;
        foreach (var r in rails)
        {
            if (r == null) continue;
            if (_dragExcludedRails.Add(r))
            {
                // ✅ 등록 안 된 레일이면 먼저 굽고(Force), 그 다음 제거
                if (!_cellsByRail.ContainsKey(r))
                    UpdateRail_Force(r);

                RemoveRail_Force(r);
            }
        }
    }
    public void EndExcludeRailForDrag(RailSpan2D rail)
    {
        if (rail == null) return;
        if (_dragExcludedRails.Remove(rail))
            UpdateRail_Force(rail); // rebuild once on commit
    }

    public void EndExcludeRailsForDrag(IEnumerable<RailSpan2D> rails)
    {
        if (rails == null) return;
        foreach (var r in rails)
        {
            if (r == null) continue;
            if (_dragExcludedRails.Remove(r))
                UpdateRail_Force(r);
        }
    }

    // -----------------------
    // Force variants (bypass SuspendUpdates)
    // -----------------------
    void UpdateRail_Force(RailSpan2D rail)
    {
        if (rail == null) return;
        if (rail.grid == null) return;
        if (!rail.gameObject.activeInHierarchy) return;
        if (!rail.enabled) return;

        // 이전 셀 제거(강제)
        RemoveRail_Force(rail);

        // 새 셀 계산
        var cells = ComputeRailCells(rail);
        if (cells == null || cells.Count == 0) return;

        _cellsByRail[rail] = cells;

        // map에 반영
        for (int i = 0; i < cells.Count; i++)
        {
            var c = cells[i];
            if (_railCountByCell.TryGetValue(c, out int prev)) _railCountByCell[c] = prev + 1;
            else _railCountByCell[c] = 1;
        }
    }

    void RemoveRail_Force(RailSpan2D rail)
    {
        if (rail == null) return;
        if (!_cellsByRail.TryGetValue(rail, out var prevCells) || prevCells == null) return;

        for (int i = 0; i < prevCells.Count; i++)
        {
            var c = prevCells[i];
            if (_railCountByCell.TryGetValue(c, out int prev))
            {
                prev--;
                if (prev <= 0) _railCountByCell.Remove(c);
                else _railCountByCell[c] = prev;
            }
        }

        _cellsByRail.Remove(rail);
    }

    List<Vector2Int> ComputeRailCells(RailSpan2D rail)
    {
        _tmpUnique.Clear();
        _tmpList.Clear();

        GridManager grid = rail.grid;
        Vector2 a = rail.start;
        Vector2 b = rail.end;

        // 셀 크기(월드) 추정: grid.cellSize가 있다고 가정
        float cellSize = Mathf.Max(0.0001f, grid.cellSize);

        // 레일 두께 반영 반경(월드)
        float rWorld = Mathf.Max(0f, rail.thickness * 0.5f + extraRadiusWorld);

        if (preciseCoverage)
        {
            // ✅ '주변 셀 확장'이 아니라, 레일(선분)+반경(rWorld)이 실제로 닿는 셀만 포함
            // 1) 레일 AABB(+rWorld)로 후보 셀 범위를 만든다.
            Vector2 minW = Vector2.Min(a, b) - Vector2.one * rWorld;
            Vector2 maxW = Vector2.Max(a, b) + Vector2.one * rWorld;
            Vector2Int cMin = grid.WorldToCell(minW);
            Vector2Int cMax = grid.WorldToCell(maxW);

            // (안전) 정렬
            int x0 = Mathf.Min(cMin.x, cMax.x);
            int x1 = Mathf.Max(cMin.x, cMax.x);
            int y0 = Mathf.Min(cMin.y, cMax.y);
            int y1 = Mathf.Max(cMin.y, cMax.y);

            float r2 = rWorld * rWorld;
            float half = cellSize * 0.5f;

            for (int x = x0; x <= x1; x++)
            {
                for (int y = y0; y <= y1; y++)
                {
                    Vector2Int cell = new Vector2Int(x, y);
                    // 셀 AABB (CellToWorld가 '셀 중심'을 준다는 프로젝트 규약 기준)
                    Vector2 c = grid.CellToWorld(cell);
                    Vector2 min = c + new Vector2(-half, -half);
                    Vector2 max = c + new Vector2(half, half);

                    float d2 = DistanceSq_SegmentAABB(a, b, min, max);
                    if (d2 <= r2) _tmpUnique.Add(cell);
                }
            }
        }
        else
        {
            // (레거시) 샘플링 + 사각 확장
            int rCells = Mathf.Max(0, Mathf.CeilToInt(rWorld / cellSize));
            float stepWorld = Mathf.Max(minSampleStepWorld, cellSize * Mathf.Clamp(sampleStepCells, 0.05f, 1f));

            float len = Vector2.Distance(a, b);
            int steps = Mathf.Max(1, Mathf.CeilToInt(len / stepWorld));

            for (int i = 0; i <= steps; i++)
            {
                float t = (steps <= 0) ? 0f : (i / (float)steps);
                Vector2 p = Vector2.Lerp(a, b, t);
                Vector2Int cell = grid.WorldToCell(p);
                AddExpandedCell(cell, rCells, _tmpUnique);
            }
        }

        // unique -> list
        foreach (var c in _tmpUnique) _tmpList.Add(c);

        // 다음 UpdateRail 전에 _tmpList를 재사용하면 안되니까 복사본 반환
        return new List<Vector2Int>(_tmpList);
    }

    // -----------------------
    // Geometry helpers
    // -----------------------

    // Segment vs AABB squared distance (0 if intersects)
    static float DistanceSq_SegmentAABB(Vector2 a, Vector2 b, Vector2 bbMin, Vector2 bbMax)
    {
        // If segment intersects the box => distance 0
        if (SegmentIntersectsAABB(a, b, bbMin, bbMax)) return 0f;

        // Otherwise min distance to 4 edges
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

    // Liang–Barsky style segment-AABB intersection
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
        if (Mathf.Approximately(p, 0f)) return q >= 0f;
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

    static float DistanceSq_SegmentSegment(Vector2 a0, Vector2 a1, Vector2 b0, Vector2 b1)
    {
        // If intersects, distance is 0
        if (SegmentsIntersect(a0, a1, b0, b1)) return 0f;

        float d0 = DistanceSq_PointSegment(a0, b0, b1);
        float d1 = DistanceSq_PointSegment(a1, b0, b1);
        float d2 = DistanceSq_PointSegment(b0, a0, a1);
        float d3 = DistanceSq_PointSegment(b1, a0, a1);
        return Mathf.Min(Mathf.Min(d0, d1), Mathf.Min(d2, d3));
    }

    static float DistanceSq_PointSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float ab2 = Vector2.SqrMagnitude(ab);
        if (ab2 <= 1e-8f) return Vector2.SqrMagnitude(p - a);
        float t = Vector2.Dot(p - a, ab) / ab2;
        t = Mathf.Clamp01(t);
        Vector2 c = a + ab * t;
        return Vector2.SqrMagnitude(p - c);
    }

    // Robust 2D segment intersection
    static bool SegmentsIntersect(Vector2 p1, Vector2 p2, Vector2 q1, Vector2 q2)
    {
        float o1 = Orient(p1, p2, q1);
        float o2 = Orient(p1, p2, q2);
        float o3 = Orient(q1, q2, p1);
        float o4 = Orient(q1, q2, p2);

        if (o1 * o2 < 0f && o3 * o4 < 0f) return true;

        // Collinear cases
        if (Mathf.Approximately(o1, 0f) && OnSegment(p1, p2, q1)) return true;
        if (Mathf.Approximately(o2, 0f) && OnSegment(p1, p2, q2)) return true;
        if (Mathf.Approximately(o3, 0f) && OnSegment(q1, q2, p1)) return true;
        if (Mathf.Approximately(o4, 0f) && OnSegment(q1, q2, p2)) return true;

        return false;
    }

    static float Orient(Vector2 a, Vector2 b, Vector2 c)
    {
        // cross((b-a),(c-a))
        return (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
    }

    static bool OnSegment(Vector2 a, Vector2 b, Vector2 p)
    {
        return p.x >= Mathf.Min(a.x, b.x) - 1e-6f && p.x <= Mathf.Max(a.x, b.x) + 1e-6f &&
               p.y >= Mathf.Min(a.y, b.y) - 1e-6f && p.y <= Mathf.Max(a.y, b.y) + 1e-6f;
    }

    static void AddExpandedCell(Vector2Int center, int rCells, HashSet<Vector2Int> set)
    {
        if (rCells <= 0)
        {
            set.Add(center);
            return;
        }

        // 사각 확장(간단/빠름). 더 정확히 하려면 원형(거리)로 바꿀 수 있음.
        for (int dx = -rCells; dx <= rCells; dx++)
        {
            for (int dy = -rCells; dy <= rCells; dy++)
            {
                set.Add(new Vector2Int(center.x + dx, center.y + dy));
            }
        }
    }

    // (주의) 아래는 과거 중복 정의가 있던 블록을 삭제했습니다.
    // Geometry helper들은 위쪽(단일 정의)만 사용합니다.

#if UNITY_EDITOR
    [Header("Debug")]
    public bool debugDraw = false;
    public Color debugColor = new Color(1f, 0f, 0f, 0.25f);

    // CellToWorld가 "센터"인지 "코너"인지에 따라 보정 토글
    public bool cellToWorldIsCorner = false;

    void OnDrawGizmosSelected()
    {
        if (!debugDraw) return;

        // RailSpan2D에 grid가 있으니 그걸로 그리드 참조 확보
        GridManager grid = null;
        if (_cellsByRail != null)
        {
            foreach (var kv in _cellsByRail)
            {
                if (kv.Key != null && kv.Key.grid != null) { grid = kv.Key.grid; break; }
            }
        }
        if (grid == null) return;

        float cs = Mathf.Max(0.0001f, grid.cellSize);
        Vector3 cubeSize = new Vector3(cs, cs, 0.02f);

        Gizmos.color = debugColor;

        int drawn = 0;
        const int MAX_DRAW = 8000; // 너무 많으면 씬뷰가 느려질 수 있음

        foreach (var kv in _railCountByCell)
        {
            if (drawn++ >= MAX_DRAW) break;

            Vector2Int cell = kv.Key;

            Vector2 p = grid.CellToWorld(cell);
            if (cellToWorldIsCorner)
                p += Vector2.one * (cs * 0.5f);

            Gizmos.DrawCube(new Vector3(p.x, p.y, 0f), cubeSize);
        }
    }
#endif

}