using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-500)]
[ExecuteAlways]
public class GridOccupancy2D : MonoBehaviour
{
    public static GridOccupancy2D Instance { get; private set; }

    [Header("Refs")]
    public GridManager grid;

    [Header("Masks")]
    public LayerMask wallMask;
    public LayerMask placedMask;
    public LayerMask specialBlockMask;

    [Header("SnapPoint (Optional)")]
    public LayerMask snapPointMask;

    [Tooltip("true면 SnapPoint가 위치한 셀을 별도 마킹해서, 레일 규칙에서 'SnapPoint 셀 예외'를 줄 수 있게 합니다.")]
    public bool bakeSnapPointCells = true;
    
    [Tooltip("true면 SnapPoint가 위치한 셀(및 반경)을 'placed 점유 굽기'에서 제외합니다. (즉, 그 셀은 빈 칸 취급)")]
    public bool excludeSnapPointCellsFromPlacedBake = true;

[   Header("Trigger Options")]
    public bool ignoreTriggersWhenBaking = true;     // 벽/배치 굽기
    public bool ignoreTriggersWhenQuerying = false;  // 내 콜라이더 검사(고스트 포함)

    [Tooltip("셀 점유 판정 샘플링. 1=정확(느림), 2=절반 셀만(빠름)")]
    [Min(1)] public int cellStride = 1;

    [Tooltip("셀 중앙만 찍으면 얇은 콜라이더를 놓칠 수 있어서, 중앙+4코너를 같이 찍을지")]
    public bool sampleCorners = true;

    [Tooltip("코너 샘플이 셀 바깥으로 튀지 않게 내부로 당기는 비율(0~0.49)")]
    [Range(0f, 0.49f)] public float cornerInset01 = 0.25f;

    [Header("Placed Owner Resolve (No compile dependency)")]
    [Tooltip("PlacementObject를 직접 참조하지 않고, 이 컴포넌트 이름으로 부모에서 찾습니다.")]
    public string placedOwnerComponentName = "PlacementObject";

    [Tooltip("true면 placed는 반드시 placedOwnerComponentName을 가진 루트가 있어야만 점유로 굽습니다.")]
    public bool requirePlacedOwnerComponent = true;

    [Header("Debug")]
    public bool rebuildImmediatelyOnMarkDirty = true;

    [Header("Hotkeys")]
    [Tooltip("플레이 모드에서 Input.GetKeyDown 으로 핫키를 처리할지")]
    public bool enableHotkeysInPlayMode = true;

    [Tooltip("에디터(플레이 모드 아님)에서도 SceneView 단축키를 처리할지")]
    public bool enableHotkeysInEditMode = true;

    [Tooltip("점유 기즈모 표시 토글 키")]
    public KeyCode toggleGizmosKey = KeyCode.F8;

    [Tooltip("점유맵 강제 Rebuild(적용) 키")]
    public KeyCode forceRebuildKey = KeyCode.F9;

    [Tooltip("자동 주기 Rebuild 토글 키")]
    public KeyCode toggleAutoRebuildKey = KeyCode.F10;

    [Header("Auto Rebuild (Optional)")]
    [Tooltip("true면 일정 주기로 자동 Rebuild해서, 이동/드래그 후에도 점유맵이 따라오게 합니다(비용 있음).")]
    public bool autoRebuildIntervalEnabled = false;

    [Tooltip("자동 Rebuild 주기(초). 0.08~0.20 추천")]
    [Min(0.01f)] public float autoRebuildInterval = 0.12f;

    float _nextAutoRebuildT = 0f;

    public bool debugLogs = false;
    public bool debugLogPlacedDetails = true;
    public int debugLogMaxPerRebuild = 30;

    // ✅ manual occupancy 굽기용 임시 캐시 (할당 방지)
    static readonly List<Vector2Int> _tmpManualCells = new List<Vector2Int>(128);
    static readonly HashSet<int> _tmpProcessedOwners = new HashSet<int>(256);
    readonly HashSet<Vector2Int> _poBlockedCells = new(1024);
    void DLog(string msg)
    {
        if (!debugLogs) return;
        Debug.Log(msg, this);
    }

    // cell -> ownerId  (벽:-1, 배치/오브젝트: instanceId(음수 가능))
    readonly Dictionary<Vector2Int, int> _cellOwner = new(4096);



    // ✅ SnapPoint가 존재하는 셀 캐시(레일 배치 예외용)
    readonly HashSet<Vector2Int> _snapCells = new(1024);
    bool _dirty = true;
    float _gridStep = -1f;

    void Awake()
    {
        // ExecuteAlways 환경에서 도메인 리로드/리컴파일 시 중복 인스턴스가 잠깐 생길 수 있어서 안전 처리
        if (Instance != null && Instance != this)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying) DestroyImmediate(gameObject);
            else Destroy(gameObject);
#else
            Destroy(gameObject);
#endif
            return;
        }

        Instance = this;

        if (grid == null)
            grid = FindFirstObjectByType<GridManager>();

        MarkDirty();
    }

    void OnEnable()
    {
        MarkDirty();

#if UNITY_EDITOR
        // 에디터(플레이 모드 아님)에서 키 입력을 받으려면 SceneView 이벤트로 받아야 함
        UnityEditor.SceneView.duringSceneGui += OnSceneGUI;
        UnityEditor.EditorApplication.update += OnEditorUpdate;
#endif
    }

    void OnDisable()
    {
#if UNITY_EDITOR
        UnityEditor.SceneView.duringSceneGui -= OnSceneGUI;
        UnityEditor.EditorApplication.update -= OnEditorUpdate;
#endif
    }

    void LateUpdate()
    {
        // (옵션) 주기 자동 Rebuild - 이동/드래그 후 점유 적용이 안될 때 켜기
        // 플레이 모드에서는 Time 기반
        if (Application.isPlaying && autoRebuildIntervalEnabled)
        {
            if (Time.unscaledTime >= _nextAutoRebuildT)
            {
                _dirty = true;
                _nextAutoRebuildT = Time.unscaledTime + Mathf.Max(0.01f, autoRebuildInterval);
            }
        }

        if (_dirty)
            Rebuild();
    }

    void Update()
    {
        // 플레이 모드 핫키 (Edit Mode는 SceneView 콜백에서 처리)
        if (!Application.isPlaying) return;
        if (!enableHotkeysInPlayMode) return;

#if UNITY_EDITOR
        if (Input.GetKeyDown(toggleGizmosKey))
            drawGizmos = !drawGizmos;
#endif

        if (Input.GetKeyDown(forceRebuildKey))
            ForceRebuildNow();

        if (Input.GetKeyDown(toggleAutoRebuildKey))
        {
            autoRebuildIntervalEnabled = !autoRebuildIntervalEnabled;
            if (autoRebuildIntervalEnabled)
                _nextAutoRebuildT = 0f; // 즉시 1회 갱신
        }
    }

    public void MarkDirty()
    {
        _dirty = true;

        // 디버그 편의: 바로 갱신해서 기즈모가 즉시 바뀌게
        if (rebuildImmediatelyOnMarkDirty)
            Rebuild();
    }

    /// <summary>
    /// 점유맵을 즉시 다시 굽습니다(적용).
    /// - 드래그/이동 후 점유가 바로 안 따라올 때, 키(F9)나 버튼에서 호출하세요.
    /// </summary>
    public void ForceRebuildNow()
    {
        _dirty = true;
        Rebuild();
    }

    void EnsureBuilt()
    {
        if (_dirty) Rebuild();
    }

    // ----------------------------
    // Query API
    // ----------------------------

    public bool IsOccupiedCell(Vector2Int cell, int ignoreOwnerId = 0, HashSet<int> extraIgnoreOwnerIds = null)
    {
        EnsureBuilt();


        // ✅ SnapPoint 셀을 "점유 구멍"으로 쓰는 옵션이면,
        //    wall(-1)만 예외적으로 점유로 유지하고, 그 외는 빈 칸 취급.
        if (excludeSnapPointCellsFromPlacedBake && _snapCells.Contains(cell))
        {
            if (_cellOwner.TryGetValue(cell, out int so) && so == -1)
                return true;
            return false;
        }
        if (!_cellOwner.TryGetValue(cell, out int owner))
            return false;

        if (owner == ignoreOwnerId) return false;
        if (extraIgnoreOwnerIds != null && extraIgnoreOwnerIds.Contains(owner)) return false;

        return true;
    }

    public bool IsOccupiedWorld(Vector2 world, int ignoreOwnerId = 0, HashSet<int> extraIgnoreOwnerIds = null)
    {
        if (grid == null) return false;
        return IsOccupiedCell(grid.WorldToCell(world), ignoreOwnerId, extraIgnoreOwnerIds);
    }

    // wall 셀인지(-1)
    public bool IsWallCell(Vector2Int cell)
    {
        EnsureBuilt();
        return _cellOwner.TryGetValue(cell, out int owner) && owner == -1;
    }

    // placed 셀인지(벽 제외 전부). instanceId는 음수도 가능.
    public bool IsPlacedCell(Vector2Int cell)
    {
        EnsureBuilt();

        if (excludeSnapPointCellsFromPlacedBake && _snapCells.Contains(cell))
            return false;

        return _cellOwner.TryGetValue(cell, out int owner) && owner != -1;
    }

    // RailEndpointHandle2D 호환용

    // ✅ SnapPoint가 있는 셀인지
    public bool IsSnapPointCell(Vector2Int cell)
    {
        EnsureBuilt();
        return _snapCells.Contains(cell);
    }
    public void EnsureBaked() => EnsureBuilt();

    public bool WouldOverlapOccupied(Collider2D[] myColliders, int selfId, HashSet<int> extraIgnoreOwnerIds = null)
    {
        EnsureBuilt();
        if (grid == null || myColliders == null) return false;

        for (int i = 0; i < myColliders.Length; i++)
        {
            var col = myColliders[i];
            if (col == null) continue;
            if (ignoreTriggersWhenQuerying && col.isTrigger) continue;

            if (ColliderWouldHitOccupied(col, selfId, extraIgnoreOwnerIds))
                return true;
        }
        return false;
    }

    // ----------------------------
    // Bake / Rasterize
    // ----------------------------

    void Rebuild()
    {
        _dirty = false;
        _cellOwner.Clear();
        _snapCells.Clear();
        _poBlockedCells.Clear();

        if (grid == null) return;

        Physics2D.SyncTransforms();
        _gridStep = GetGridStep();

#if UNITY_2022_2_OR_NEWER
        var allCols = Object.FindObjectsByType<Collider2D>(FindObjectsSortMode.None);
#else
    var allCols = Object.FindObjectsOfType<Collider2D>();
#endif

        DLog($"[Occ] Rebuild start. cols={allCols.Length} wallMask={wallMask.value} placedMask={placedMask.value} specialBlockMask={specialBlockMask.value}");

        // 우선순위: wall > special block > placed
        RasterizeWalls(allCols);
        BuildSnapPointCells();
        BuildPOBlockedAnchorCells();
        RasterizeSpecialBlocks(allCols);
        RasterizePlaced(allCols);

        int wallCells = 0, placedCells = 0;
        foreach (var kv in _cellOwner)
        {
            if (kv.Value == -1) wallCells++;
            else placedCells++;
        }

        DLog($"[Occ] Rebuild done. dictCells={_cellOwner.Count} wallCells={wallCells} placedCells={placedCells}");
    }

    void RasterizeSpecialBlocks(Collider2D[] allCols)
    {
        int stride = Mathf.Max(1, cellStride);

        for (int i = 0; i < allCols.Length; i++)
        {
            var col = allCols[i];
            if (col == null || !col.enabled) continue;
            if (ignoreTriggersWhenBaking && col.isTrigger) continue;

            int layerBit = 1 << col.gameObject.layer;

            // specialBlockMask에 포함된 것만 굽기
            if ((specialBlockMask.value & layerBit) == 0)
                continue;

            // wall/placed와 별개로 owner를 하나 주긴 해야 하므로
            // attachedRigidbody -> root -> 자기 자신 순으로 잡아도 충분함
            int ownerId = ResolveSpecialBlockOwnerId(col);

            // 이미 벽이나 다른 점유가 있으면 덮어쓰지 않음
            RasterizeCollider(col, ownerId: ownerId, stride: stride, allowOverwrite: false);
        }
    }

    static int ResolveSpecialBlockOwnerId(Collider2D col)
    {
        if (col == null) return 0;

        if (col.attachedRigidbody != null)
            return col.attachedRigidbody.GetInstanceID();

        return col.transform.root != null
            ? col.transform.root.GetInstanceID()
            : col.GetInstanceID();
    }

    void RasterizeWalls(Collider2D[] allCols)
    {
        int stride = Mathf.Max(1, cellStride);

        for (int i = 0; i < allCols.Length; i++)
        {
            var col = allCols[i];
            if (col == null || !col.enabled) continue;
            if (ignoreTriggersWhenBaking && col.isTrigger) continue;

            int layerBit = 1 << col.gameObject.layer;

            // ✅ placedMask에 걸리면 "벽으로 굽지 않음"
            if ((placedMask.value & layerBit) != 0)
                continue;

            // placed 오브젝트 소속이면 벽으로 굽지 않음(레이어가 wallMask여도)
            if (LooksLikePlacedOwner(col))
                continue;

            if ((wallMask.value & layerBit) == 0) continue;

            RasterizeCollider(col, ownerId: -1, stride: stride, allowOverwrite: false);
        }
    }

    void RasterizePlaced(Collider2D[] allCols)
    {
        int stride = Mathf.Max(1, cellStride);
        int debugCount = 0;

        _tmpProcessedOwners.Clear();

        for (int i = 0; i < allCols.Length; i++)
        {
            var col = allCols[i];
            if (col == null || !col.enabled) continue;
            if (ignoreTriggersWhenBaking && col.isTrigger) continue;

            // ✅ placedMask는 "콜라이더 레이어" 기준
            int colLayerBit = 1 << col.gameObject.layer;
            if ((placedMask.value & colLayerBit) == 0) continue;

            // ✅ owner 루트(PlacementObject 등) 찾기 - 기존 로직 유지
            Component ownerComp = FindComponentInParentsByName(col.transform, placedOwnerComponentName);

            if (requirePlacedOwnerComponent && ownerComp == null)
            {
                if (debugLogs && debugLogPlacedDetails && debugCount < debugLogMaxPerRebuild)
                {
                    debugCount++;
                    DLog($"[Occ][Placed] SKIP(no owner). col={col.name} layer={LayerMask.LayerToName(col.gameObject.layer)}");
                }
                continue;
            }

            int ownerId = ResolveOwnerId(col, ownerComp);

            // ✅ 드래그 중인 오브젝트는 점유 굽기에서 제외
            if (_tempIgnoreOwnerId != 0 && ownerId == _tempIgnoreOwnerId)
                continue;

            // =========================================
            // ✅ NEW: PlacementObject가 manual occupancy를 제공하면
            //         콜라이더 래스터 대신 "배열"을 굽는다.
            //         (owner당 1회만)
            // =========================================
            if (ownerComp != null)
            {
                var po = ownerComp as PlacementObject;
                if (po != null && po.UseManualOccupancy)
                {
                    // owner당 1회만 처리
                    if (!_tmpProcessedOwners.Add(ownerId))
                        continue;

                    BakeManualCellsFromPO(po, ownerId);
                    continue;
                }
            }

            // =========================================
            // ✅ fallback: 기존처럼 collider 기반 래스터
            // =========================================
            if (debugLogs && debugLogPlacedDetails && debugCount < debugLogMaxPerRebuild)
            {
                debugCount++;
                string ownerName = ownerComp != null ? ownerComp.name : "(fallback-root)";
                DLog($"[Occ][Placed] USE owner={ownerName} col={col.name} colLayer={LayerMask.LayerToName(col.gameObject.layer)} ownerId={ownerId}");
            }

            RasterizeCollider(col, ownerId: ownerId, stride: stride, allowOverwrite: false);
        }
    }

    void BakeManualCellsFromPO(PlacementObject po, int ownerId)
    {
        if (po == null || grid == null) return;

        po.GetManualOccupiedCells(grid, _tmpManualCells);

        for (int k = 0; k < _tmpManualCells.Count; k++)
        {
            var cell = _tmpManualCells[k];

            // ✅ 이미 벽(-1) 또는 다른 점유가 있으면 overwrite 금지(벽 우선 정책 유지)
            if (_cellOwner.ContainsKey(cell))
                continue;

            // ✅ SnapPoint 셀은 placed 점유로 굽지 않음 옵션 유지
            if (excludeSnapPointCellsFromPlacedBake && _snapCells.Contains(cell))
                continue;

            _cellOwner[cell] = ownerId;
        }
    }


    void BuildSnapPointCells()
    {
        // ✅ SnapPoint 셀 캐시(레일 규칙에서 "SnapPoint가 있는 점유 셀은 예외적으로 허용"에 사용)
        _snapCells.Clear();
        if (!bakeSnapPointCells) return;
        if (grid == null) return;

#if UNITY_2022_2_OR_NEWER
        var sps = Object.FindObjectsByType<SnapPoint>(FindObjectsSortMode.None);
#else
        var sps = Object.FindObjectsOfType<SnapPoint>();
#endif
        if (sps == null) return;

        int mask = snapPointMask.value;
        for (int i = 0; i < sps.Length; i++)
        {
            var sp = sps[i];
            if (sp == null) continue;
            if (mask != 0)
            {
                int bit = 1 << sp.gameObject.layer;
                if ((mask & bit) == 0) continue;
            }

            var cell = grid.WorldToCell(sp.transform.position);
            _snapCells.Add(new Vector2Int(cell.x, cell.y));
}
    }

    void RasterizeCollider(Collider2D col, int ownerId, int stride, bool allowOverwrite)
    {
        var b = col.bounds;
        Vector2Int cMin = grid.WorldToCell(b.min);
        Vector2Int cMax = grid.WorldToCell(b.max);

        for (int y = cMin.y; y <= cMax.y; y += stride)
        {
            for (int x = cMin.x; x <= cMax.x; x += stride)
            {
                var cell = new Vector2Int(x, y);
                if (!CellSampleHitsCollider(cell, col)) continue;

                if (!allowOverwrite && _cellOwner.ContainsKey(cell))
                    continue;

                // ✅ 옵션: SnapPoint 셀은 placed 점유로 굽지 않음(=빈 칸 취급)
                if (ownerId != -1 && excludeSnapPointCellsFromPlacedBake && _snapCells.Contains(cell))
                    continue;

                _cellOwner[cell] = ownerId;
            }
        }
    }

    // ----------------------------
    // Overlap check (query)
    // ----------------------------

    bool ColliderWouldHitOccupied(Collider2D col, int selfId, HashSet<int> extraIgnoreOwnerIds)
    {
        var b = col.bounds;
        Vector2Int cMin = grid.WorldToCell(b.min);
        Vector2Int cMax = grid.WorldToCell(b.max);

        int stride = Mathf.Max(1, cellStride);

        for (int y = cMin.y; y <= cMax.y; y += stride)
        {
            for (int x = cMin.x; x <= cMax.x; x += stride)
            {
                var cell = new Vector2Int(x, y);
                if (!CellSampleHitsCollider(cell, col)) continue;

                if (IsOccupiedCell(cell, ignoreOwnerId: selfId, extraIgnoreOwnerIds))
                    return true;
            }
        }

        return false;
    }

    // ----------------------------
    // Sampling
    // ----------------------------

    bool CellSampleHitsCollider(Vector2Int cell, Collider2D col)
    {
        Vector2 center = grid.CellToWorld(cell);

        if (col.OverlapPoint(center)) return true;
        if (!sampleCorners) return false;

        float inset = Mathf.Clamp01(cornerInset01);
        float half = _gridStep * 0.5f;
        float d = half * (1f - inset);

        if (col.OverlapPoint(center + new Vector2(+d, +d))) return true;
        if (col.OverlapPoint(center + new Vector2(+d, -d))) return true;
        if (col.OverlapPoint(center + new Vector2(-d, +d))) return true;
        if (col.OverlapPoint(center + new Vector2(-d, -d))) return true;

        return false;
    }

    float GetGridStep()
    {
        Vector2 p0 = grid.CellToWorld(Vector2Int.zero);
        Vector2 p1 = grid.CellToWorld(Vector2Int.right);
        float step = Vector2.Distance(p0, p1);
        return (step > 0f) ? step : 1f;
    }

    // ----------------------------
    // Owner resolve helpers (NO PlacementObject type)
    // ----------------------------

    bool LooksLikePlacedOwner(Collider2D col)
    {
        // placedOwnerComponentName이 비어있으면 "placed owner" 판정을 못하니 false
        if (string.IsNullOrEmpty(placedOwnerComponentName)) return false;
        return FindComponentInParentsByName(col.transform, placedOwnerComponentName) != null;
    }

    static Component FindComponentInParentsByName(Transform t, string componentName)
    {
        if (string.IsNullOrEmpty(componentName)) return null;

        while (t != null)
        {
            // Unity의 string 기반 GetComponent는 컴파일 의존성이 없음
            var c = t.GetComponent(componentName);
            if (c != null) return c;
            t = t.parent;
        }
        return null;
    }

    static int ResolveOwnerId(Collider2D col, Component ownerComp)
    {
        // 1) ownerComp가 있으면 그 instanceId (음수 가능)
        if (ownerComp != null) return ownerComp.GetInstanceID();

        // 2) 없으면 Rigidbody 우선 (여러 콜라이더 묶음에 유리)
        if (col.attachedRigidbody != null) return col.attachedRigidbody.GetInstanceID();

        // 3) 최후: 루트 오브젝트
        return col.transform.root.GetInstanceID();
    }

    // ✅ 셀 ownerId를 꺼내기
    public bool TryGetCellOwner(Vector2Int cell, out int ownerId)
    {
        EnsureBuilt();
        return _cellOwner.TryGetValue(cell, out ownerId);
    }

    // ✅ placed이면서 "특정 owner는 무시" 판정
    public bool IsPlacedCellOtherThan(
        Vector2Int cell,
        int ignoreOwnerIdA,
        int ignoreOwnerIdB,
        HashSet<int> extraIgnoreOwnerIds = null
    )
    {
        EnsureBuilt();


        if (excludeSnapPointCellsFromPlacedBake && _snapCells.Contains(cell))
            return false;
        if (!_cellOwner.TryGetValue(cell, out int owner))
            return false;

        // wall(-1)은 placed로 치지 않음
        if (owner == -1) return false;

        if (owner == ignoreOwnerIdA) return false;
        if (owner == ignoreOwnerIdB) return false;
        if (extraIgnoreOwnerIds != null && extraIgnoreOwnerIds.Contains(owner)) return false;

        return true;
    }

    public int GetOwnerIdAtCell(Vector2Int cell)
    {
        EnsureBuilt();

        if (excludeSnapPointCellsFromPlacedBake && _snapCells.Contains(cell))
        {
            // wall(-1)은 유지, 그 외는 빈 칸(0)
            if (_cellOwner.TryGetValue(cell, out int so) && so == -1) return -1;
            return 0;
        }

        return _cellOwner.TryGetValue(cell, out int owner) ? owner : 0;
    }

    public void DebugDumpAreaOwners(Vector2 worldCenter, int radiusCells = 3)
    {
        EnsureBuilt();
        if (grid == null) return;

        Vector2Int c0 = grid.WorldToCell(worldCenter);

        Debug.Log($"[Occ][DumpArea] centerWorld={worldCenter} centerCell={c0} r={radiusCells}", this);

        for (int y = c0.y + radiusCells; y >= c0.y - radiusCells; y--)
        {
            string line = "";
            for (int x = c0.x - radiusCells; x <= c0.x + radiusCells; x++)
            {
                int id = GetOwnerIdAtCell(new Vector2Int(x, y));
                // 표기: 0(비어있음) / -1(벽) / 그외(placed ownerId)
                line += (id == 0 ? " . " : (id == -1 ? " W " : " P "));
            }
            Debug.Log(line, this);
        }
    }

    int _tempIgnoreOwnerId = 0;

    public void SetTempIgnoreOwner(int ownerId)
    {
        _tempIgnoreOwnerId = ownerId;
        MarkDirty();
    }

    public void ClearTempIgnoreOwner()
    {
        if (_tempIgnoreOwnerId == 0) return;
        _tempIgnoreOwnerId = 0;
        MarkDirty();
    }

    public bool WouldOverlapOccupiedCells(
    IReadOnlyList<Vector2Int> cells,
    int selfId
    )
        {
            EnsureBuilt();

            if (grid == null) return false;
            if (cells == null || cells.Count == 0) return false;

            for (int i = 0; i < cells.Count; i++)
            {
                // selfId만 무시(=본인 점유는 충돌로 보지 않음)
                if (IsOccupiedCell(cells[i], ignoreOwnerId: selfId, extraIgnoreOwnerIds: null))
                    return true;
            }

            return false;
        }

    public bool IsPOBlockedCell(Vector2Int cell)
    {
        EnsureBuilt();
        return _poBlockedCells.Contains(cell);
    }

    public bool WouldOverlapPOBlockedCells(IReadOnlyList<Vector2Int> cells)
    {
        EnsureBuilt();
        if (cells == null || cells.Count == 0) return false;

        for (int i = 0; i < cells.Count; i++)
        {
            if (_poBlockedCells.Contains(cells[i]))
                return true;
        }
        return false;
    }

    void BuildPOBlockedAnchorCells()
    {
        _poBlockedCells.Clear();
        if (grid == null) return;

#if UNITY_2022_2_OR_NEWER
        var nodes = Object.FindObjectsByType<RailSnapNode2D>(FindObjectsSortMode.None);
#else
    var nodes = Object.FindObjectsOfType<RailSnapNode2D>();
#endif

        if (nodes == null) return;

        for (int i = 0; i < nodes.Length; i++)
        {
            var node = nodes[i];
            if (node == null) continue;
            if (!node.IsAnchor) continue;
            if (!node.BlockPOPlacement) continue;

            Vector2Int center = grid.WorldToCell(node.transform.position);
            int r = Mathf.Max(0, node.POBlockRadiusCells);

            for (int y = -r; y <= r; y++)
            {
                for (int x = -r; x <= r; x++)
                {
                    _poBlockedCells.Add(new Vector2Int(center.x + x, center.y + y));
                }
            }
        }
    }

#if UNITY_EDITOR
    // ----------------------------
    // Editor Hotkeys (SceneView)
    // ----------------------------

    void OnSceneGUI(UnityEditor.SceneView sv)
    {
        if (Application.isPlaying) return;
        if (!enableHotkeysInEditMode) return;

        var e = Event.current;
        if (e == null) return;

        if (e.type == EventType.KeyDown)
        {
            if (e.keyCode == toggleGizmosKey)
            {
                drawGizmos = !drawGizmos;
                UnityEditor.SceneView.RepaintAll();
                e.Use();
            }
            else if (e.keyCode == forceRebuildKey)
            {
                ForceRebuildNow();
                UnityEditor.SceneView.RepaintAll();
                e.Use();
            }
            else if (e.keyCode == toggleAutoRebuildKey)
            {
                autoRebuildIntervalEnabled = !autoRebuildIntervalEnabled;
                // EditMode에서는 EditorApplication.timeSinceStartup 기반으로 바로 1회 갱신
                _nextAutoRebuildT = 0f;
                e.Use();
            }
        }
    }

    void OnEditorUpdate()
    {
        if (Application.isPlaying) return;
        if (!enableHotkeysInEditMode) return;
        if (!autoRebuildIntervalEnabled) return;

        float tf = (float)UnityEditor.EditorApplication.timeSinceStartup;
        if (tf >= _nextAutoRebuildT)
        {
            _dirty = true;
            _nextAutoRebuildT = tf + Mathf.Max(0.01f, autoRebuildInterval);
        }
    }


    // ----------------------------
    // Debug Gizmos
    // ----------------------------
    [Header("Debug Gizmos")]
    public bool drawGizmos = true;

    [Tooltip("씬 뷰에서 그릴 최대 셀 수(너무 크면 렉남).")]
    public int gizmoMaxCells = 6000;

    [Tooltip("벽/배치 셀만 그릴지, 전체(=딕셔너리 있는 것) 그릴지")]
    public bool gizmoDrawOnlyWallAndPlaced = true;

    [Tooltip("내부적으로 EnsureBaked를 호출해서 항상 최신으로 그릴지(무거울 수 있음)")]
    public bool gizmoAutoEnsureBaked = true;

    public Color gizmoWallColor = new Color(1f, 0.2f, 0.2f, 0.18f);
    public Color gizmoPlacedColor = new Color(0.2f, 0.9f, 0.2f, 0.18f);
    public Color gizmoOutlineColor = new Color(1f, 1f, 1f, 0.10f);

    void OnDrawGizmos()
    {
        if (!drawGizmos) return;
        if (grid == null) return;

        if (gizmoAutoEnsureBaked)
            EnsureBuilt();

        float step = (_gridStep > 0f) ? _gridStep : GetGridStep();
        Vector3 size = new Vector3(step, step, 0.01f);

        int drawn = 0;

        foreach (var kv in _cellOwner)
        {
            if (drawn >= gizmoMaxCells) break;

            int owner = kv.Value;
            bool isWall = owner == -1;
            bool isPlaced = owner != -1;

            if (gizmoDrawOnlyWallAndPlaced && !(isWall || isPlaced))
                continue;

            Vector3 center = (Vector3)grid.CellToWorld(kv.Key);

            Gizmos.color = isWall ? gizmoWallColor : gizmoPlacedColor;
            Gizmos.DrawCube(center, size);

            Gizmos.color = gizmoOutlineColor;
            Gizmos.DrawWireCube(center, size);

            drawn++;
        }
    }
#endif
}
