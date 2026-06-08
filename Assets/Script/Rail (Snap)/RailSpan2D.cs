using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]

[RequireComponent(typeof(PolygonCollider2D))]

[RequireComponent(typeof(Rigidbody2D))]

public class RailSpan2D : MonoBehaviour

{

    [Header("Grid")]

    public GridManager grid;

    [Header("Edit Camera (for handles)")]

    [SerializeField] Camera editCamera;

    public Camera EditCamera => editCamera;
    public void SetEditCamera(Camera cam) { if (cam != null) editCamera = cam; }

    [Header("Endpoints (World, snapped)")]

    public Vector2 start;
    public Vector2 end;

    [Header("Length Limits")]

    public float minLength = 0.5f;
    public float maxLength = 3.0f;

    [Header("Shape")]

    public float thickness = 0.12f;

    [Header("Handles")]

    public GameObject handlePrefab;

    [SerializeField] float fallbackHandleRadius = 0.12f;

    [Tooltip("Build 모드에서 레일이 생성되자마자 끝점 핸들을 항상 활성(클릭/드래그 가능) 상태로 유지")]
    [SerializeField] bool handlesAlwaysActiveInBuild = true;

    [Header("Selection Visual")]

    [SerializeField] bool useSelectionTint = true;

    [SerializeField] Color normalColor = Color.white;

    [SerializeField] Color selectedColor = new Color(1f, 0.85f, 0.2f, 1f);

    [SerializeField] Color blockedColor = Color.red;

    [Header("Sorting (LineRenderer)")]

    [SerializeField] string railSortingLayer = "Default";

    [SerializeField] int railOrder = 0;

    [Header("Sorting (Handle SpriteRenderer)")]

    [SerializeField] string handleSortingLayer = "Handles";

    [SerializeField] int handleBaseOrder = 1000;

    [SerializeField] int startHandleOrderOffset = 2;

    [SerializeField] int endHandleOrderOffset = 1;

    // ✅ Node 기반

    public RailSnapNode2D startNode;
    public RailSnapNode2D endNode;

    [Header("Node Capacity (Rail-Rail)")]

    public int maxRailsPerNode = 3;

    [Header("Wall Blocking (for move too)")]

    public LayerMask wallMask;
    public float endpointBlockRadius = 0.12f;
    public bool blockIfSegmentHitsWall = true;
    public int ignoreWallNearThisNodeCells = 1;

    [Header("Placement Blocking")]

    public LayerMask placedMask;

    [Header("Rail Layer (Rail-Rail overlap allowed)")]

    public LayerMask railMask;

    [Header("Perf / Safety")]

    [SerializeField] bool failIfOverlapBufferFull = false;

    [Header("Debug")]

    [SerializeField] bool debugNotifyNodeMoved = false;

    [Header("Debug Refresh Count")]
    [SerializeField] bool debugRefreshCount = false;

    int _refreshCount;
    float _refreshTimer;

    // -------------------------

    // Private

    // -------------------------

    const string HANDLE_LAYER_NAME = "RailHandle";
    int HandleLayer => LayerMask.NameToLayer(HANDLE_LAYER_NAME);
    LineRenderer lr;
    PolygonCollider2D poly;
    Rigidbody2D rb;
    Renderer lrRenderer;
    MaterialPropertyBlock mpb;
    string resolvedColorProp;
    RailEndpointHandle2D hStart;
    RailEndpointHandle2D hEnd;
    bool isSelected;
    bool isBlockedPreview; // ✅ 추가: GridPlacer가 막힌 레일을 빨갛게 표시


    RailSpanVisual2D railVisual;
    // ✅ Non-Alloc caches

    readonly List<Collider2D> _colsCache = new List<Collider2D>(8);
    readonly Vector2[] _polyPath4 = new Vector2[4];
    static readonly Collider2D[] _overlap = new Collider2D[512];
    readonly HashSet<PlacementObject> _ignoreOwnersSet = new HashSet<PlacementObject>();

    // ✅ NotifyNodeMoved 최적화: 레일 레지스트리

    static readonly HashSet<RailSpan2D> _allRails = new HashSet<RailSpan2D>(256);

    public Color NormalColor => normalColor;
    public Color BlockedColor => blockedColor;

    // -------------------------

    // Convenience

    // -------------------------

    public Vector2 StartWorld => (startNode != null) ? startNode.WorldPos : start;
    public Vector2 EndWorld => (endNode != null) ? endNode.WorldPos : end;
    static readonly List<RailSpan2D> _allRailsTmp = new List<RailSpan2D>(256);
    public RailEndpointHandle2D GetHandle(bool wantStart)

    {

        EnsureHandles();
        return wantStart ? hStart : hEnd;

    }

    // -------------------------

    // Unity

    // -------------------------

    void OnEnable()

    {

        _allRails.Add(this);

    }

    void OnDisable()

    {
        RailEdgeRegistry2D.Unregister(gameObject.GetInstanceID());
        RailCellMap2D.Instance?.RemoveRail(this);
        _allRails.Remove(this);

    }

    void Update()
    {
        if (!debugRefreshCount) return;

        _refreshTimer += Time.unscaledDeltaTime;
        if (_refreshTimer >= 1f)
        {
            _refreshCount = 0;
            _refreshTimer = 0f;
        }
    }

    void Awake()

    {

        CacheComponents();
        SetupRigidAndRenderer();
        ApplySorting();
        SetupMaterialColorBinding();
        Refresh(syncFromNodes: true);

        if (railVisual != null)
            railVisual.SyncNow();

        // ✅ Build 모드에서는 레일 생성 직후부터 끝점 핸들이 즉시 등장하고,
        // 선택/드래그 상태가 아니어도 비활성(콜라이더 OFF) 상태가 되지 않도록 유지한다.
        if (IsBuildMode())
        {
            EnsureHandles();
            ApplyHandleColliderEnabled(true);
        }

        ApplySelectionVisual();

    }

    void CacheComponents()
    {
        if (!lr) lr = GetComponent<LineRenderer>();
        if (!poly) poly = GetComponent<PolygonCollider2D>();
        if (!rb) rb = GetComponent<Rigidbody2D>();
        if (!railVisual) railVisual = GetComponent<RailSpanVisual2D>();
    }

    void SetupRigidAndRenderer()

    {

        rb.bodyType = RigidbodyType2D.Static;
        rb.simulated = true;
        lr.positionCount = 2;
        lr.useWorldSpace = false;
        lr.numCapVertices = 6;

    }

    void ApplySorting()
    {
        if (!lr) return;
        lr.sortingLayerName = railSortingLayer;
        lr.sortingOrder = railOrder;

    }

    void SetupMaterialColorBinding()

    {

        if (lr == null) return;
        lrRenderer = lr.GetComponent<Renderer>();
        if (lrRenderer == null) return;
        mpb = new MaterialPropertyBlock();
        var mat = lr.sharedMaterial;
        if (mat == null) { resolvedColorProp = null; return; }
        if (mat.HasProperty("_BaseColor")) resolvedColorProp = "_BaseColor";
        else if (mat.HasProperty("_Color")) resolvedColorProp = "_Color";
        else if (mat.HasProperty("_TintColor")) resolvedColorProp = "_TintColor";
        else resolvedColorProp = null;

    }

    bool IsBuildMode()

    {

        if (GameModeManager.Instance == null) return true;
        return GameModeManager.Instance.currentMode == GameMode.Build;

    }

    // -------------------------

    // Public API (GridPlacer / RailTool 에서 쓰는 부분)

    // -------------------------

    public void InitializeNodes(GridManager gridRef, RailSnapNode2D a, RailSnapNode2D b)

    {

        grid = gridRef;
        startNode = a;
        endNode = b;
        Refresh(syncFromNodes: true);

    }

    public void Initialize(GridManager gridRef, Vector2 startSnapped, Vector2 endSnapped)

    {

        grid = gridRef;
        start = startSnapped;
        end = endSnapped;
        Refresh(syncFromNodes: false);

    }

    public void SetSelected(bool selected)

    {

        if (!IsBuildMode()) selected = false;
        isSelected = selected;
        ApplySelectionVisual();
        EnsureHandles();

        // ✅ Build 모드에서 "항상 활성" 옵션이면 선택 해제되어도 핸들이 비활성(콜라이더 OFF)로 떨어지지 않게 한다.
        bool enable = IsBuildMode() && (handlesAlwaysActiveInBuild || isSelected);
        ApplyHandleColliderEnabled(enable);

    }

    // ✅ GridPlacer에서 호출할 “막힘 표시” API

    public void SetBlockedPreview(bool blocked)

    {

        isBlockedPreview = blocked;
        ApplySelectionVisual();

    }

    public void SetEditModeVisible(bool visible)

    {

        if (!IsBuildMode()) visible = false;
        EnsureHandles();

        // ✅ 편집 가시성 토글이 꺼져도, Build 모드에서 "항상 활성" 옵션이면 핸들은 계속 활성 유지
        bool enable = IsBuildMode() && (handlesAlwaysActiveInBuild || visible || isSelected);
        ApplyHandleColliderEnabled(enable);

    }

    public void Refresh() => Refresh(syncFromNodes: true);
    public void Refresh(bool syncFromNodes)
    {
        if (debugRefreshCount)
        {
            _refreshCount++;
        }

        CacheComponents();

        if (syncFromNodes)
            SyncEndpointsFromNodes();

        RebuildGeometryFromEndpoints();

        if (hStart) hStart.SetWorldPosition(start);
        if (hEnd) hEnd.SetWorldPosition(end);

        ApplySelectionVisual();

        RailCellMap2D.Instance?.UpdateRail(this);

        if (railVisual != null)
            railVisual.MarkAllDirty();
    }

    void SyncEndpointsFromNodes()

    {

        if (startNode != null) start = startNode.WorldPos;
        if (endNode != null) end = endNode.WorldPos;

    }

    void RebuildGeometryFromEndpoints()

    {

        Vector2 dirWorld = (end - start);
        if (dirWorld.sqrMagnitude < 1e-10f)

        {

            lr.startWidth = thickness;
            lr.endWidth = thickness;
            lr.SetPosition(0, Vector3.zero);
            lr.SetPosition(1, Vector3.zero);
            if (poly) poly.enabled = false;
            return;

        }

        if (poly && !poly.enabled) poly.enabled = true;

        // ✅ 프리팹 offset 제거

        if (poly) poly.offset = Vector2.zero;
        Vector2 mid = (start + end) * 0.5f;
        transform.position = mid;
        transform.rotation = Quaternion.identity;
        transform.localScale = Vector3.one;
        Vector2 localA = start - mid;
        Vector2 localB = end - mid;
        lr.startWidth = thickness;
        lr.endWidth = thickness;
        lr.SetPosition(0, localA);
        lr.SetPosition(1, localB);
        Vector2 dir = (localB - localA);
        Vector2 n = new Vector2(-dir.y, dir.x).normalized;
        float half = thickness * 0.5f;
        _polyPath4[0] = localA + n * half;
        _polyPath4[1] = localA - n * half;
        _polyPath4[2] = localB - n * half;
        _polyPath4[3] = localB + n * half;
        if (poly.pathCount != 1) poly.pathCount = 1;
        poly.SetPath(0, _polyPath4);

    }

    // -------------------------

    // Visual

    // -------------------------

    void ApplySelectionVisual()

    {

        if (!lr || !useSelectionTint) return;

        // ✅ 우선순위: blocked > selected > normal

        Color c = isBlockedPreview ? blockedColor : (isSelected ? selectedColor : normalColor);
        lr.startColor = c;
        lr.endColor = c;
        if (lrRenderer != null && mpb != null && !string.IsNullOrEmpty(resolvedColorProp))

        {

            lrRenderer.GetPropertyBlock(mpb);
            mpb.SetColor(resolvedColorProp, c);
            lrRenderer.SetPropertyBlock(mpb);

        }

        // ✅ RailSpanVisual2D를 사용 중(복제 LR로 외곽선을 그리는 연출)인 경우,
        // 자식 LineRenderer들도 함께 틴트해야 선택 색이 실제로 보인다.
        var visual = GetComponent<RailSpanVisual2D>();
        if (visual != null)
            visual.ApplyTint(c);

    }

    // -------------------------

    // Handle Helpers

    // -------------------------

    void ApplyHandleColliderEnabled(bool enabled)

    {

        if (hStart) hStart.SetColliderEnabled(enabled);
        if (hEnd) hEnd.SetColliderEnabled(enabled);

    }

    void EnsureHandles()

    {

        if (hStart && hEnd) return;
        GameObject CreateHandle(string name, int sortingOrder)

        {

            GameObject go;
            if (handlePrefab != null)

            {

                go = Instantiate(handlePrefab, transform);
                // ✅ 프리팹이 비활성 상태로 저장되어 있어도 레일 생성 직후부터 보이도록 강제
                if (!go.activeSelf) go.SetActive(true);

            }

            else

            {

                go = new GameObject(name);
                go.transform.SetParent(transform, true);
                var c = go.AddComponent<CircleCollider2D>();
                c.radius = fallbackHandleRadius;
                c.isTrigger = true;

            }

            int layer = HandleLayer;
            if (layer < 0)

            {

                Debug.LogWarning($"[RailSpan2D] Layer '{HANDLE_LAYER_NAME}' not found. Please create it.", this);

            }

            else

            {

                SetLayerRecursively(go, layer);

            }

            var srs = go.GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < srs.Length; i++)

            {

                srs[i].sortingLayerName = handleSortingLayer;
                srs[i].sortingOrder = sortingOrder;

            }

            go.name = name;
            return go;

        }

        var a = CreateHandle("HandleStart", handleBaseOrder + startHandleOrderOffset);
        var b = CreateHandle("HandleEnd", handleBaseOrder + endHandleOrderOffset);
        hStart = a.GetComponent<RailEndpointHandle2D>() ?? a.AddComponent<RailEndpointHandle2D>();
        hEnd = b.GetComponent<RailEndpointHandle2D>() ?? b.AddComponent<RailEndpointHandle2D>();
        hStart.Bind(this, true);
        hEnd.Bind(this, false);
        // ✅ Build 모드에서는 생성 직후부터 끝점 핸들이 비활성(콜라이더 OFF)로 남지 않게 한다.
        ApplyHandleColliderEnabled(IsBuildMode());
        hStart.SetWorldPosition(start);
        hEnd.SetWorldPosition(end);

    }


    static void SetLayerRecursively(GameObject obj, int layer)

    {

        if (obj == null) return;
        obj.layer = layer;
        var t = obj.transform;
        for (int i = 0; i < t.childCount; i++)
            SetLayerRecursively(t.GetChild(i).gameObject, layer);

    }

    // -------------------------

    // Placement Check

    // -------------------------

    public bool CanPlaceRail(
    LayerMask wallMask,
    LayerMask placedMask,
    LayerMask railMask,
    RailSpan2D ignoreRail = null,
    bool allowStartInsideWall = false,
    bool allowEndInsideWall = false,
    float endpointAllowRadius = 0.12f,
    IReadOnlyList<PlacementObject> ignorePlacedOwners = null,
    float placedOwnerAllowPenetration = 0f,   // ✅ "허용 침투 깊이" (월드 단위)
    float ignoreOwnerRelaxTotalCells = 1f   // ✅ 총 허용 칸 수
)

    {

        _colsCache.Clear();
        GetComponentsInChildren<Collider2D>(true, _colsCache);
        LayerMask queryMask = placedMask | railMask; // ✅ wall은 Occupancy로만 판정
        ContactFilter2D filter = new ContactFilter2D

        {

            useLayerMask = true,
            layerMask = queryMask,
            useTriggers = true

        };

        float rSq = endpointAllowRadius * endpointAllowRadius;
        float step = (grid != null) ? Mathf.Max(0.05f, RailGridUtil.GetGridStep(grid)) : 0.1f;
        float baseR = Mathf.Max(endpointAllowRadius, thickness * 0.5f);
        float desiredCells = Mathf.Max(0f, ignoreOwnerRelaxTotalCells); // ✅ 총 허용 칸 수
        float relaxR = Mathf.Max(baseR, step * desiredCells);
        float relaxRSq = relaxR * relaxR;


        _ignoreOwnersSet.Clear();
        if (ignorePlacedOwners != null)

        {

            for (int k = 0; k < ignorePlacedOwners.Count; k++)

            {

                var po = ignorePlacedOwners[k];
                if (po != null) _ignoreOwnersSet.Add(po);

            }

        }

        bool IsIgnoredOwner(PlacementObject po)
            => (po != null && _ignoreOwnersSet.Count > 0 && _ignoreOwnersSet.Contains(po));
        for (int c = 0; c < _colsCache.Count; c++)

        {

            var col = _colsCache[c];
            if (col == null || !col.enabled) continue;

            // ✅ 내 콜라이더가 트리거면 설치판정 소스에서 제외 (핸들/스냅 오탐 제거)

            if (col.isTrigger) continue;
            if (col.GetComponentInParent<RailEndpointHandle2D>() != null) continue;
            if (col.GetComponentInParent<SnapPoint>() != null) continue;
            int count = col.OverlapCollider(filter, _overlap);
            if (failIfOverlapBufferFull && count >= _overlap.Length)
                return false;
            for (int i = 0; i < count; i++)

            {

                var hit = _overlap[i];
                if (hit == null) continue;

                // 내 자신 제외

                if (hit.transform.IsChildOf(transform)) continue;

                // ✅ 스냅/핸들 트리거 무시

                if (hit.isTrigger &&
                    (hit.GetComponentInParent<SnapPoint>() != null ||
                     hit.GetComponentInParent<RailEndpointHandle2D>() != null))

                {

                    continue;

                }

                int hitLayerBit = 1 << hit.gameObject.layer;

                // 1) WALL (ignored here: wall is handled via occupancy rules)

                if ((hitLayerBit & wallMask.value) != 0)

                {

                    continue;

                }

                // 2) PLACED
                // ✅ penetration(침투량) 계산은 아예 하지 않는다.
                //    - ignorePlacedOwners(연결된 PO)만 완전 무시
                //    - 그 외 PO와 한 번이라도 겹치면 즉시 실패

                if ((hitLayerBit & placedMask.value) != 0)

                {

                    var owner = hit.GetComponentInParent<PlacementObject>();
                    if (IsIgnoredOwner(owner))
                    {
                        // ✅ ignorePlacedOwners(스냅/연결된 PO)는 "엔드포인트 근처"만 예외 허용
                        // - 선분 중간에서 PO를 관통하는 건 금지
                        Vector2 sW = StartWorld;
                        Vector2 eW = EndWorld;

                        Vector2 cpS = hit.ClosestPoint(sW);
                        Vector2 cpE = hit.ClosestPoint(eW);

                        if ((cpS - sW).sqrMagnitude <= relaxRSq || (cpE - eW).sqrMagnitude <= relaxRSq)
                            continue;

                        return false;
                    }

                    return false; // ✅ 연결되지 않은 PO와 겹치면 금지

                }

                // 3) RAIL ↔ RAIL : 겹침 허용, 단 ignoreRail은 완전 무시

                if ((hitLayerBit & railMask.value) != 0)

                {

                    var otherRail = hit.GetComponentInParent<RailSpan2D>();
                    if (otherRail == null) continue;
                    if (ignoreRail != null && otherRail == ignoreRail) continue;
                    continue;

                }

            }

        }

        return true;

    }

    // -------------------------

    // Debug

    // -------------------------

    public void DebugDumpGeometry(string tag)

    {

        CacheComponents();
        var mid = transform.position;
        var off = (poly != null) ? poly.offset : Vector2.zero;
        var b = (poly != null) ? poly.bounds : new Bounds();
        Debug.Log(
            $"{tag} rail={name} start={start} end={end} trPos={mid} polyOffset={off} bounds={b}",
            this
        );

    }

    public bool IsLengthValid(float eps = 0.0001f)

    {

        float d = Vector2.Distance(StartWorld, EndWorld);
        if (d <= eps) return false;
        if (d < minLength - eps) return false;
        if (maxLength > 0f && d > maxLength + eps) return false;
        return true;

    }

    // -------------------------

    // ✅ Node moved notify (OPTIMIZED)

    // -------------------------

    public static void NotifyNodeMoved(RailSnapNode2D node)

    {

        RailGraphDirty.MarkDirty();
        if (node == null) return;

        // ✅ 노드를 쓰는 레일만 즉시 갱신해서 1프레임 지연/첫 이동 실패 제거

        foreach (var r in _allRails)

        {

            if (r == null) continue;
            if (r.startNode == node || r.endNode == node)
                r.Refresh(syncFromNodes: true);

        }

    }

    public static void GetAllRailsNonAlloc(List<RailSpan2D> outList)

    {

        outList.Clear();
        foreach (var r in _allRails)
            if (r != null) outList.Add(r);

    }


    void OnDestroy()

    {
        RailEdgeRegistry2D.Unregister(gameObject.GetInstanceID());
        // ✅ 레일 삭제 시 점유 셀 캐시에서 제거
        RailCellMap2D.Instance?.RemoveRail(this);


    }

    public void CommitRegistry()
    {
        RailEdgeRegistry2D.Register(gameObject.GetInstanceID(), StartWorld, EndWorld);
    }

}