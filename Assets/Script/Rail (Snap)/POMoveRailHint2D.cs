using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public class POMoveRailHint2D : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] GridPlacer gridPlacer;

    [Header("Tuning")]
    [SerializeField] int maxDots = 2000;
    [SerializeField] int priority = 250;

    [Tooltip("레일 maxLength=0(무제한)인 경우, 후보 탐색을 이 셀 반경으로 제한")]
    [SerializeField] int fallbackRadiusCells = 12;

    [Header("Stability (Anti Flicker)")]
    [Tooltip("pts가 0이 되는 순간 바로 Clear하지 않고, 이 프레임 수 동안 유지")]
    [SerializeField] int emptyGraceFrames = 6;

    [Header("Auto Rebuild")]
    [Tooltip("드래그 중 PO가 셀을 옮겼을 때, 이 프레임 간격 이상이면 리빌드")]
    [SerializeField] int rebuildIntervalFramesOnCellChange = 6;

    [Tooltip("pts가 비어있으면(힌트 안 보임) 이 프레임 간격으로 강제 리빌드")]
    [SerializeField] int rebuildIntervalFramesWhenEmpty = 2;

    [Tooltip("✅ true면 PO를 잡는 순간( Begin )에만 힌트를 1회 계산하고, 드래그 중에는 리빌드하지 않음")]
    [SerializeField] bool rebuildOnlyOnBegin = true;

    // ===========================
    // Buffers (NO-ALLOC)
    // ===========================
    readonly List<Vector2> _pts = new List<Vector2>(2048);          // 브로커에 보낼 점
    readonly List<Vector2> _tmpPts = new List<Vector2>(2048);       // 더블버퍼
    readonly List<Vector2> _ptsDisplay = new List<Vector2>(2048);    // ✅ hintCenter 적용 표시용
    readonly HashSet<Vector2Int> _seen = new HashSet<Vector2Int>(4096);
    readonly List<Constraint> _constraints = new List<Constraint>(32);
    readonly HashSet<RailSnapNode2D> _movedNodes = new HashSet<RailSnapNode2D>();
    readonly List<RailNodeFollowBinding2D.Entry> _tmpLegacyEntries = new List<RailNodeFollowBinding2D.Entry>(1);

    PlacementObject _po;
    bool _active;

    int _emptyFrames;
    int _lastRebuildFrame = -999999;
    Vector2Int _lastPoCell;
    int _lastBindRev = int.MinValue;

    void Awake()
    {
        if (gridPlacer == null)
            gridPlacer = GetComponent<GridPlacer>();
    }


    Vector2 GetHintCenterWorld()
    {
        if (_po == null) return Vector2.zero;
        Transform hc = POHintCenterAccessor.Get(_po);
        return (hc != null) ? (Vector2)hc.position : (Vector2)_po.transform.position;
    }

    void RebuildPtsDisplay()
    {
        _ptsDisplay.Clear();
        if (!_active || _po == null) return;

        Vector2 shift = GetHintCenterWorld() - (Vector2)_po.transform.position;
        for (int i = 0; i < _pts.Count; i++)
            _ptsDisplay.Add(_pts[i] + shift);
    }

    void Update()
    {
        if (!_active) return;

        // ======= 자가 복구 리빌드 트리거 =======
        if (!rebuildOnlyOnBegin)
        {
            if (_po != null && gridPlacer != null && gridPlacer.grid != null)
            {
                var bind = _po.GetComponent<RailNodeFollowBinding2D>();
                int curRev = (bind != null) ? BindRevAccessor.GetRev(bind) : int.MinValue;

                Vector2Int curCell = gridPlacer.grid.WorldToCell(_po.transform.position);
                bool cellChanged = (curCell != _lastPoCell);

                // (A) rev가 바뀌었는데 Begin이 다시 안 불리면 힌트가 멈추는 케이스 방지
                bool revChanged = (bind != null && curRev != int.MinValue && curRev != _lastBindRev);

                // (B) 힌트가 아예 없으면 빠르게 복구
                bool needRebuildBecauseEmpty = (_pts.Count == 0) && (Time.frameCount - _lastRebuildFrame >= rebuildIntervalFramesWhenEmpty);

                // (C) 셀이 바뀌면 어느 정도 간격으로 갱신
                bool needRebuildBecauseMovedCell = cellChanged && (Time.frameCount - _lastRebuildFrame >= rebuildIntervalFramesOnCellChange);

                if (revChanged || needRebuildBecauseEmpty || needRebuildBecauseMovedCell)
                {
                    RebuildByRailsToPoPositions_FullScan();
                    _lastRebuildFrame = Time.frameCount;

                    RebuildPtsDisplay();
                    RebuildPtsDisplay();
                    RebuildPtsDisplay();
                    _lastPoCell = curCell;
                    _lastBindRev = curRev;
                }
            }

        }

        // ======= 브로커 요청/유지 =======
        if (_pts.Count > 0)
        {
            _emptyFrames = 0;
            MoveHintBroker2D.Instance?.Request(this, priority, _ptsDisplay);
        }
        else
        {
            // ✅ 바로 Clear하면 깜빡임 원인이 됨 → grace 주기
            _emptyFrames++;
            if (_emptyFrames >= emptyGraceFrames)
                MoveHintBroker2D.Instance?.Clear(this);
        }
    }

    public void Begin(PlacementObject po)
    {

        _po = po;
        _active = (po != null);

        _emptyFrames = 0;
        _lastRebuildFrame = -999999;
        _lastBindRev = int.MinValue;

        if (!_active || gridPlacer == null || gridPlacer.grid == null)
        {
            _pts.Clear();
            MoveHintBroker2D.Instance?.Clear(this);
            return;
        }

        // ✅ hintCenter(PO 쪽에 저장된 기준 오브젝트)는 '드래그 중'에만 보이게
        _po?.SetHintCenterDragVisible(true);

        _lastPoCell = gridPlacer.grid.WorldToCell(_po.transform.position);

        var bind = _po.GetComponent<RailNodeFollowBinding2D>();
        _lastBindRev = (bind != null) ? BindRevAccessor.GetRev(bind) : int.MinValue;

        RebuildByRailsToPoPositions_FullScan();
        _lastRebuildFrame = Time.frameCount;
        RebuildPtsDisplay();
        RebuildPtsDisplay();
    }

    public void End()
    {

        // ✅ 드래그 종료: hintCenter 숨김(또는 원상복구)
        _po?.SetHintCenterDragVisible(false);

        _po = null;
        _active = false;
        _pts.Clear();
        _tmpPts.Clear();
        _emptyFrames = 0;
        MoveHintBroker2D.Instance?.Clear(this);
    }

    void RebuildByRailsToPoPositions_FullScan()
    {
        // UnityEngine.Debug.Log($"[HINT] Rebuild frame={Time.frameCount}");

        // 임시 버퍼만 클리어 (✅ _pts는 성공 시에만 교체)
        _tmpPts.Clear();
        _seen.Clear();
        _constraints.Clear();
        _movedNodes.Clear();

        if (_po == null || gridPlacer == null || gridPlacer.grid == null)
            return;

        var bind = _po.GetComponent<RailNodeFollowBinding2D>();
        if (bind == null)
            return;

        // ===== entries 확보 (멀티 우선 / 구버전 단일 호환) =====
        IReadOnlyList<RailNodeFollowBinding2D.Entry> entries = null;

        if (bind.Entries != null && bind.Entries.Count > 0)
        {
            entries = bind.Entries;
        }
        else
        {
            if (bind.node == null)
                return;

            _tmpLegacyEntries.Clear();

            Vector2 localOffset = Vector2.zero;
            if (bind.anchorPoint != null)
                localOffset = (Vector2)_po.transform.InverseTransformPoint(bind.anchorPoint.position);

            _tmpLegacyEntries.Add(new RailNodeFollowBinding2D.Entry
            {
                node = bind.node,
                anchorPoint = (bind.anchorPoint != null) ? bind.anchorPoint.transform : _po.transform,
                localOffset = localOffset
            });

            entries = _tmpLegacyEntries;
        }

        // 레일 캐시
        var rails = gridPlacer.GetAllRailsCached();
        if (rails == null || rails.Length == 0)
            return;

        // ✅ 힌트 계산 전에 레일 월드좌표 최신화
        for (int i = 0; i < rails.Length; i++)
            if (rails[i] != null)
                rails[i].Refresh(syncFromNodes: true);

        Physics2D.SyncTransforms();

        // 그리드 step
        Vector2 p0 = gridPlacer.grid.CellToWorld(Vector2Int.zero);
        Vector2 p1 = gridPlacer.grid.CellToWorld(Vector2Int.right);
        float step = Vector2.Distance(p0, p1);
        if (step <= 0.0001f) step = 1f;

        // ✅ 스캔 범위: 레일 제약 박스들의 교집합
        int minX = int.MinValue, minY = int.MinValue;
        int maxX = int.MaxValue, maxY = int.MaxValue;

        for (int i = 0; i < entries.Count; i++)
            if (entries[i].node != null)
                _movedNodes.Add(entries[i].node);

        Quaternion poRot = _po.transform.rotation;
        Vector3 poScale = _po.transform.localScale;

        // ===== 제약 만들기 =====
        for (int e = 0; e < entries.Count; e++)
        {
            var entry = entries[e];
            if (entry.node == null) continue;

            for (int i = 0; i < rails.Length; i++)
            {
                var r = rails[i];
                if (r == null) continue;

                bool touches = (r.startNode == entry.node || r.endNode == entry.node);
                if (!touches) continue;

                RailSnapNode2D otherNode = (r.startNode == entry.node) ? r.endNode : r.startNode;

                if (otherNode != null && _movedNodes.Contains(otherNode))
                    continue;

                Vector2 fixedPos = (otherNode != null)
                    ? (Vector2)otherNode.transform.position
                    : ((r.startNode == entry.node) ? r.EndWorld : r.StartWorld);

                float minLen = Mathf.Max(0f, r.minLength);
                float maxLen = r.maxLength;

                int maxCells = (maxLen > 0f) ? Mathf.CeilToInt(maxLen / step) : fallbackRadiusCells;
                int minCells = (minLen > 0f) ? Mathf.FloorToInt(minLen / step) : 0;

                Vector2 worldOffset = WorldOffsetFromLocal(entry.localOffset, poRot, poScale);
                Vector2 centerWorld = fixedPos - worldOffset;
                Vector2Int cCenter = gridPlacer.grid.WorldToCell(centerWorld);

                minX = Mathf.Max(minX, cCenter.x - maxCells);
                maxX = Mathf.Min(maxX, cCenter.x + maxCells);
                minY = Mathf.Max(minY, cCenter.y - maxCells);
                maxY = Mathf.Min(maxY, cCenter.y + maxCells);

                _constraints.Add(new Constraint
                {
                    fixedPos = fixedPos,
                    worldOffset = worldOffset,
                    minLen2 = minLen * minLen,
                    maxLen2 = maxLen * maxLen,
                    hasMax = (maxLen > 0f)
                });
            }
        }

        if (_constraints.Count == 0)
        {
            Vector2Int cPo = gridPlacer.grid.WorldToCell(GetHintCenterWorld());
            minX = cPo.x - fallbackRadiusCells;
            maxX = cPo.x + fallbackRadiusCells;
            minY = cPo.y - fallbackRadiusCells;
            maxY = cPo.y + fallbackRadiusCells;
        }

        if (minX > maxX || minY > maxY)
            return;

        bool stop = false;

        for (int y = minY; y <= maxY && !stop; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                if (_tmpPts.Count >= maxDots) { stop = true; break; }

                Vector2 gridCandidate = gridPlacer.grid.CellToWorld(new Vector2Int(x, y));

                // (A) 거리 제약 (✅ 셀 근사 말고 월드 거리로 정확히)
                bool okDist = true;
                for (int k = 0; k < _constraints.Count; k++)
                {
                    Vector2 movedNodeWorld = gridCandidate + _constraints[k].worldOffset;
                    float d2 = (movedNodeWorld - _constraints[k].fixedPos).sqrMagnitude;

                    if (d2 < _constraints[k].minLen2) { okDist = false; break; }
                    if (_constraints[k].hasMax && d2 > _constraints[k].maxLen2) { okDist = false; break; }
                }
                if (!okDist) continue;

                if (TryValidateLikeFinalCommit(_po, gridCandidate, out var finalPos))
                {
                    var key = gridPlacer.grid.WorldToCell(finalPos);
                    if (_seen.Add(key))
                        _tmpPts.Add(finalPos);
                }
            }
        }

        // ✅ 성공(>0)일 때만 _pts 갱신 (0이면 유지 → 깜빡임 방지)
        if (_tmpPts.Count > 0)
        {
            _pts.Clear();
            _pts.AddRange(_tmpPts);
        }

        RebuildPtsDisplay();
    }

    bool TryValidateLikeFinalCommit(PlacementObject po, Vector2 candidateGridPos, out Vector2 finalPos)
    {
        finalPos = candidateGridPos;
        if (po == null || gridPlacer == null || gridPlacer.grid == null) return false;

        // ✅ 캐시 없이 매번 계산
        return gridPlacer.CanMovePOWithAttachedRails_CombinedVirtual(po, candidateGridPos);
    }



    struct Constraint
    {
        public Vector2 fixedPos;
        public Vector2 worldOffset;
        public float minLen2;
        public float maxLen2;
        public bool hasMax;
    }

    static Vector2 WorldOffsetFromLocal(Vector2 localOffset, Quaternion rot, Vector3 scale)
    {
        Vector3 v = new Vector3(localOffset.x * scale.x, localOffset.y * scale.y, 0f);
        Vector3 w = rot * v;
        return new Vector2(w.x, w.y);
    }

    // =========================================================
    // Binding Revision access (reflection-safe)
    // =========================================================

    // =========================================================
    // ✅ PlacementObject의 hintCenter(Transform)를 '리플렉션'으로 안전하게 접근
    // - PlacementObject.cs가 바뀌어도(접근자/필드 private 등) 컴파일 깨짐 방지
    // =========================================================
    static class POHintCenterAccessor
    {
        static bool _inited;
        static FieldInfo _f;
        static PropertyInfo _p;

        public static Transform Get(PlacementObject po)
        {
            if (po == null) return null;
            EnsureInit(po.GetType());
            try
            {
                if (_f != null) return _f.GetValue(po) as Transform;
                if (_p != null) return _p.GetValue(po) as Transform;
            }
            catch { /* ignore */ }
            return null;
        }

        static void EnsureInit(Type t)
        {
            if (_inited) return;
            _inited = true;

            const BindingFlags BF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            _f = t.GetField("hintCenter", BF) ?? t.GetField("HintCenter", BF);
            _p = t.GetProperty("hintCenter", BF) ?? t.GetProperty("HintCenter", BF);

            if (_f != null && !typeof(Transform).IsAssignableFrom(_f.FieldType)) _f = null;
            if (_p != null && !typeof(Transform).IsAssignableFrom(_p.PropertyType)) _p = null;
        }
    }

    static class BindRevAccessor
    {
        static bool _inited;
        static FieldInfo _fRev;
        static PropertyInfo _pRev;

        public static int GetRev(RailNodeFollowBinding2D bind)
        {
            if (bind == null) return int.MinValue;
            EnsureInit(bind.GetType());

            try
            {
                if (_fRev != null) return (int)_fRev.GetValue(bind);
                if (_pRev != null) return (int)_pRev.GetValue(bind);
            }
            catch { /* ignore */ }

            return int.MinValue;
        }

        static void EnsureInit(Type t)
        {
            if (_inited) return;
            _inited = true;

            // 후보: "rev", "Rev", "revision", "Revision"
            const BindingFlags BF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            _fRev = t.GetField("rev", BF) ?? t.GetField("Rev", BF) ?? t.GetField("revision", BF) ?? t.GetField("Revision", BF);

            _pRev = t.GetProperty("rev", BF) ?? t.GetProperty("Rev", BF) ?? t.GetProperty("revision", BF) ?? t.GetProperty("Revision", BF);

            if (_fRev != null && _fRev.FieldType != typeof(int)) _fRev = null;
            if (_pRev != null && _pRev.PropertyType != typeof(int)) _pRev = null;
        }
    }

    // POMoveRailHint2D.cs 내부(클래스 멤버로) 추가

    public bool HasHints => _active && _pts != null && _pts.Count > 0;

    /// <summary>
    /// desiredGridPos(그리드 스냅된 좌표)를 가장 가까운 힌트 점으로 클램프.
    /// 힌트가 없으면 false.
    /// </summary>
    public bool TryClampToHint(Vector2 desiredGridPos, out Vector2 clamped)
    {
        clamped = desiredGridPos;
        if (!HasHints) return false;

        float best = float.MaxValue;
        int bestIdx = -1;

        // _pts는 max 2000 정도라(기본값) O(N)도 충분히 가벼움
        for (int i = 0; i < _pts.Count; i++)
        {
            float d = (_pts[i] - desiredGridPos).sqrMagnitude;
            if (d < best)
            {
                best = d;
                bestIdx = i;
            }
        }

        if (bestIdx < 0) return false;
        clamped = _pts[bestIdx];
        return true;
    }

}
