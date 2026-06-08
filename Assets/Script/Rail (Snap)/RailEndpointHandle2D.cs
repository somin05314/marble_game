using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using static UnityEngine.Rendering.DebugUI.Table;
using UnityEngine.UI;

[RequireComponent(typeof(Collider2D))]

public class RailEndpointHandle2D : MonoBehaviour

{

    [Header("Debug")]

    [SerializeField] bool debugLog = false;

    RailSpan2D rail;
    bool isStart;
    Collider2D col;

    // ✅ 진짜 드래그 상태일 때만 힌트/이동 로직 실행

    bool isDragging;

    // ✅ 드래그 시작 시 "직전 연결 PO" 캐시 (스냅이 풀려도 드래그 중에는 예외 유지)
    PlacementObject _dragPrevOwner;


    // =========================================================
    // Fast Reject Cache (for drag perf)
    // =========================================================
    static GridOccupancy2D _occ;
    static int _occEnsureFrame = -1;

    static void EnsureOccBaked()
    {
        if (_occ == null) _occ = GridOccupancy2D.Instance;
        if (_occ == null) return;
        if (_occEnsureFrame != Time.frameCount)
        {
            _occ.EnsureBaked();
            _occEnsureFrame = Time.frameCount;
        }
    }


    [Header("Commit: Update PO Binding")]
    [Tooltip("레일 끝점을 PO의 SnapPoint에 붙여서 커밋할 때, 해당 PO의 RailNodeFollowBinding2D(Entries)를 즉시 갱신한다")]
    [SerializeField] bool updateOwnerBindingOnCommit = true;

    // ✅ 설치(Placement) 상태: start 찍고 end 고르는 중에도 힌트를 보여주기 위함

    bool isPlacing;
    Vector2 placeFixedPos;                 // 설치 시 고정점(= start 찍은 위치)
    RailSnapNode2D placeExcludeNode;       // 병합 후보 검색에서 제외할 노드(보통 새로 만든 moving 노드)

    // Drag 최적화(같은 좌표 반복 Refresh 방지)

    Vector2 lastAppliedPos;
    bool hasLast;
    const float DUP_EPS = 0.002f;
    const float MOVE_EPS = 1e-5f;
    const float MOVE_EPS_SQ = MOVE_EPS * MOVE_EPS;

    // Drag cache(롤백/복원 기준)

    bool hasDragCache;
    Vector2 cachedMovingPos;
    RailSnapNode2D cachedStartNode;
    RailSnapNode2D cachedEndNode;
    Vector2 cachedStartPos;
    Vector2 cachedEndPos;

    [Header("Move Hints (Accurate)")]

    [SerializeField] bool showMoveHints = true;

    [SerializeField] int hintRadiusCells = 14;

    [SerializeField] int hintMaxDots = 800;

    [SerializeField] float hintUpdateInterval = 0.12f;

    [SerializeField] int moveHintPriority = 250;

    [SerializeField] int placeHintPriority = 200;

    [Header("Hint - Exclude PlacementObject")]

    [SerializeField] bool hintExcludePlacedObjects = true;

    [SerializeField, Range(0.2f, 2f)] float hintPlacedRadiusScale = 1.0f;

    static readonly Collider2D[] _placedHits = new Collider2D[64];

    [SerializeField, Range(0.1f, 1f)] float hintWallRadiusScale = 0.35f;

    [SerializeField] RailPlacementRuleProfile2D ruleProfile;

    float EffectiveHintWallScale => (ruleProfile != null ? ruleProfile.hintWallRadiusScale : hintWallRadiusScale);
    float EffectiveHintPlacedScale => (ruleProfile != null ? ruleProfile.hintPlacedRadiusScale : hintPlacedRadiusScale);

    [SerializeField] int hintCellStride = 1;

    [SerializeField] bool hintUseConnectedRailLimits = false;

    [SerializeField] bool hintUseSegmentWallCheck = true; // (드래그 힌트용) 너는 false 원함

    [Header("Drag Constraint")]
    [Tooltip("드래그 중 끝점이 힌트 점으로만 이동하도록 제한 (힌트가 비어있으면 이동 자체를 막음)")]
    [SerializeField] bool restrictDragToHints = true;

    // ✅ 설치 힌트에서는 “중간 벽 막힘(선분 체크)”를 적용하고 싶다 했으니 별도 토글 제공

    [Header("Place Hints (after start picked)")]

    [SerializeField] bool showPlaceHints = true;

    [SerializeField] bool placeHintUseSegmentWallCheck = true;

    [SerializeField] int maxPhysicsChecksPerUpdate = 80;

    float _nextHintTime;
    static readonly Collider2D[] nodeHits = new Collider2D[64];
    readonly List<Vector2> _hintPositions = new List<Vector2>(512);

    // 연결 레일 캐시

    RailSpan2D[] _connectedRails = new RailSpan2D[16];
    int _connectedRailsCount;

    [Header("SnapPoint Detect (for snapped PO exception)")]

    [SerializeField] LayerMask snapPointMask;

    [SerializeField] float snapPointPickRadius = 0.25f;

    static readonly Collider2D[] _spHits = new Collider2D[32];

    [Header("Hint Rule: SnapPoint Occupancy Exceptions")]

    [Tooltip("연결되지 않은 스냅포인트는 '끝점 셀'만 예외 허용(선분 중간은 여전히 막힘)")]
    [SerializeField] bool allowUnconnectedSnapPointEndpointCellOnly = true;

    // (no-alloc) connected-rail placement check temp owners
    static readonly List<PlacementObject> _tmpIgnoreOwners4 = new List<PlacementObject>(4);

    bool TryPickSnapPoint(Vector2 world, float radius, out SnapPoint sp)

    {

        sp = null;
        int count = Physics2D.OverlapCircleNonAlloc(world, radius, _spHits, snapPointMask);
        float best = float.MaxValue;
        for (int i = 0; i < count; i++)

        {

            var c = _spHits[i];
            if (c == null) continue;
            var p = c.GetComponentInParent<SnapPoint>();
            if (p == null) continue;
            float d = ((Vector2)p.transform.position - world).sqrMagnitude;
            if (d < best) { best = d; sp = p; }

        }

        return sp != null;

    }

    PlacementObject GetSnapOwner(SnapPoint sp)

    {

        if (sp == null) return null;
        if (sp.root != null && sp.root.owner != null) return sp.root.owner;
        return sp.GetComponentInParent<PlacementObject>();

    }

    PlacementObject GetPrevOwnerForDrag(RailSnapNode2D movingNode)
    {
        if (movingNode == null) return null;

        // 1) 현재 위치에서 SnapPoint로 owner 찾기(가장 안정적)
        if (TryPickSnapPoint(movingNode.WorldPos, snapPointPickRadius, out var sp))
        {
            var o = GetSnapOwner(sp);
            if (o != null) return o;
        }

        // 2) fallback: 주변에서 SnapPoint 잡기(아주 약간 여유)
        if (TryPickSnapPoint(movingNode.WorldPos, snapPointPickRadius * 1.2f, out var sp2))
        {
            var o2 = GetSnapOwner(sp2);
            if (o2 != null) return o2;
        }

        return null;
    }

    // =========================================================
    // ✅ Commit 시점: "이번에 스냅된 PO"만 골라서 바인딩(Entries) 갱신
    // - 레일을 PO에 연결(레일 이동/설치 커밋)할 때, PO 쪽에 레일 정보가 저장되지 않는 문제 해결
    // - 반대로 스냅이 풀린 경우에는 직전 owner에서 해당 node 엔트리 제거
    // =========================================================

    void UpdateOwnerBindingAfterCommit(RailSnapNode2D movingNode)
    {
        if (!updateOwnerBindingOnCommit) return;
        if (movingNode == null) return;

        // "현재" 스냅 owner
        PlacementObject newOwner = null;
        Transform snapTr = null;

        if (TryPickSnapPoint(movingNode.WorldPos, snapPointPickRadius, out var sp) && sp != null)
        {
            newOwner = GetSnapOwner(sp);
            snapTr = sp.transform;
        }

        // 1) 기존 owner가 있었는데, 이번 커밋에서 owner가 바뀌거나/없어졌으면 prune
        if (_dragPrevOwner != null && _dragPrevOwner != newOwner)
            PruneOwnerBindingByNode(_dragPrevOwner, movingNode);

        // 2) 새 owner가 있으면 단일 엔드포인트 엔트리 갱신/추가
        if (newOwner != null && snapTr != null)
            EnsureSingleEndpointBinding(newOwner, movingNode, snapTr);

        _dragPrevOwner = newOwner;
    }

    void EnsureSingleEndpointBinding(PlacementObject owner, RailSnapNode2D node, Transform snapPointTr)
    {
        if (owner == null || node == null || snapPointTr == null) return;

        var bind = owner.GetComponent<RailNodeFollowBinding2D>();
        if (bind == null) bind = owner.gameObject.AddComponent<RailNodeFollowBinding2D>();

        int myId = owner.GetInstanceID();

        var old = bind.Entries;
        var newEntries = new List<RailNodeFollowBinding2D.Entry>((old != null ? old.Count : 0) + 1);

        if (old != null && old.Count > 0)
        {
            for (int i = 0; i < old.Count; i++)
            {
                var e = old[i];

                // 같은 node 엔트리는 교체
                if (e.node == node) continue;
                // 파괴/유실된 엔트리는 정리
                if (e.node == null) continue;

                newEntries.Add(e);
            }
        }

        node.EnsurePersistentId();

        newEntries.Add(new RailNodeFollowBinding2D.Entry
        {
            node = node,
            anchorPoint = snapPointTr,
            localOffset = (Vector2)owner.transform.InverseTransformPoint(snapPointTr.position),
            ownerId = myId,
            nodeId = node.PersistentId
        });

        bind.SetEntries(newEntries);

        // built 조건은 "보험" 수준으로만 기록(정확한 radius/mask는 GridPlacer가 정식으로 재빌드 가능)
        bind.builtRevision = RailGraphRevision.Value;
        if (bind.builtRadius <= 0f) bind.builtRadius = 0.25f;
        if (bind.builtMaskValue == 0)
        {
            var mgr = RailSnapNodeManager.Instance;
            if (mgr != null) bind.builtMaskValue = mgr.railNodeMask.value;
        }

        // Follow 컴포넌트는 node에 1개만 유지
        var follow = node.GetComponent<RailNodeFollow2D>();
        if (follow == null) follow = node.gameObject.AddComponent<RailNodeFollow2D>();
        follow.Attach(snapPointTr, myId);
    }

    void PruneOwnerBindingByNode(PlacementObject owner, RailSnapNode2D node)
    {
        if (owner == null || node == null) return;

        var bind = owner.GetComponent<RailNodeFollowBinding2D>();
        if (bind == null) return;

        var src = bind.Entries;
        if (src == null || src.Count == 0) return;

        var newEntries = new List<RailNodeFollowBinding2D.Entry>(src.Count);
        for (int i = 0; i < src.Count; i++)
        {
            var e = src[i];
            if (e.node == null) continue;
            if (e.node == node) continue;
            newEntries.Add(e);
        }

        // Follow도 내가 붙인 ownerId면 제거(보험)
        var follow = node.GetComponent<RailNodeFollow2D>();
        if (follow != null && follow.ownerId == owner.GetInstanceID())
        {
            follow.Detach();
            Destroy(follow);
        }

        if (newEntries.Count == 0)
        {
            bind.Clear();
            Destroy(bind);
        }
        else
        {
            bind.SetEntries(newEntries);
            bind.SyncNow(syncPhysics: true, broadcastMoved: false);
        }
    }

    void Awake()

    {

        col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;

    }

    public void Bind(RailSpan2D owner, bool isStart)

    {

        HideHints();
        rail = owner;
        this.isStart = isStart;
        hasLast = false;
        hasDragCache = false;
        isDragging = false;

        // 설치 힌트 상태도 초기화

        isPlacing = false;
        placeFixedPos = Vector2.zero;
        placeExcludeNode = null;
        UpdateLockState();
        HideHints();

    }

    public void SetWorldPosition(Vector2 world)

    {

        transform.position = world;
        lastAppliedPos = world;
        hasLast = true;

    }

    public void SetColliderEnabled(bool enabled)

    {

        if (col != null) col.enabled = enabled;
        UpdateLockState();

    }

    void Log(string msg)

    {

        if (!debugLog) return;
        Debug.Log($"[RailHandle {(isStart ? "START" : "END")}] {msg}", this);

    }

    // ✅ Anchor면 드래그/분리 자체 금지

    public bool IsLockedByAnchor

    {

        get

        {

            var n = GetMovingNode();
            return n != null && n.IsAnchor;

        }

    }

    void UpdateLockState()

    {

        if (col == null) return;
        col.enabled = !IsLockedByAnchor;

    }

    void HideHints()

    {

        MoveHintBroker2D.Instance?.Clear(this);

    }

    void StopDragging()

    {

        _dragPrevOwner = null;

        isDragging = false;
        HideHints();

    }


    void CacheDragState(RailSnapNode2D movingNode)

    {

        hasDragCache = true;
        cachedStartNode = rail ? rail.startNode : null;
        cachedEndNode = rail ? rail.endNode : null;
        cachedStartPos = cachedStartNode ? cachedStartNode.WorldPos : (rail ? rail.start : Vector2.zero);
        cachedEndPos = cachedEndNode ? cachedEndNode.WorldPos : (rail ? rail.end : Vector2.zero);
        cachedMovingPos = movingNode ? movingNode.WorldPos : (isStart ? cachedStartPos : cachedEndPos);


        _dragPrevOwner = GetPrevOwnerForDrag(movingNode);
    }

    void RollbackToCached()

    {

        if (!hasDragCache) return;
        if (rail != null)

        {

            rail.startNode = cachedStartNode;
            rail.endNode = cachedEndNode;

        }

        if (cachedStartNode != null) ApplyMove(cachedStartNode, cachedStartPos, notifyAllRails: false);
        if (cachedEndNode != null) ApplyMove(cachedEndNode, cachedEndPos, notifyAllRails: false);

        // Physics2D.SyncTransforms(); // (Occupancy-only wall check)

        if (rail != null) rail.Refresh(syncFromNodes: true);

        // ✅ Duplicate registry: 롤백 시 원래 edge 복구 등록
        if (rail != null)
            RailEdgeRegistry2D.Register(rail.gameObject.GetInstanceID(), rail.StartWorld, rail.EndWorld);

        if (cachedStartNode != null) RailSpan2D.NotifyNodeMoved(cachedStartNode);
        if (cachedEndNode != null) RailSpan2D.NotifyNodeMoved(cachedEndNode);
        if (cachedStartNode != null) RefreshRailsUsingNode(cachedStartNode);
        if (cachedEndNode != null) RefreshRailsUsingNode(cachedEndNode);
        lastAppliedPos = cachedMovingPos;
        hasLast = true;
        hasDragCache = false;
        UpdateLockState();
        StopDragging();

    }

    void Fail(bool commit, string reason)

    {

        if (commit)
            Debug.LogWarning($"[RailHandle {(isStart ? "START" : "END")}] FAIL(commit): {reason}", this);
        else
            Log($"Fail: {reason}");
        if (commit) RollbackToCached();

    }

    // =========================================================

    // Public API (Drag)

    // =========================================================

    public void BeginDrag()

    {
        if (IsPointerOverUI())
            return;

        if (rail == null) return;
        if (RailSnapNodeManager.Instance == null) return;
        EnsureRailNodes();
        UpdateLockState();

        // ✅ Anchor면 드래그 시작 자체 금지

        if (IsLockedByAnchor)

        {

            Log("BeginDrag blocked: endpoint is Anchor (locked)");
            hasDragCache = false;
            StopDragging();
            return;

        }

        // ✅ “진짜 마우스 드래그”가 아니면 BeginDrag 무시

        if (!Input.GetMouseButton(0))

        {

            Log("BeginDrag ignored: LMB not held");
            hasDragCache = false;
            StopDragging();
            return;

        }

        var moving = GetMovingNode();
        if (moving == null) return;

        // ✅ 드래그 시작 시점의 "직전 연결 PO"를 기억(커밋에서 detach 판단에 사용)
        _dragPrevOwner = GetPrevOwnerForDrag(moving);

        isDragging = true;
        hasDragCache = false;
        CacheDragState(moving);

        // ✅ Duplicate registry: 드래그 중에는 '자기 자신' edge 때문에 중복 판정이 막히지 않게 잠시 제거
        RailEdgeRegistry2D.Unregister(rail.gameObject.GetInstanceID());

        // ✅ 드래그 중 매 프레임 전체 레일 스캔(FindObjectsByType) 방지: 연결 캐시 1회 구축
        RebuildConnectedRailsCache(moving);
        Log("BeginDrag cached");
        _nextHintTime = 0f;
        UpdateMoveHintsAccurate(force: true);

    }

    public void DragTo(Vector2 mouseWorld, bool commit)

    {

        if (rail == null) { Fail(commit, "rail null"); return; }
        if (rail.grid == null) { Fail(commit, "rail.grid null"); return; }
        if (RailSnapNodeManager.Instance == null) { Fail(commit, "RailSnapNodeManager null"); return; }
        EnsureRailNodes();
        UpdateLockState();

        // ✅ Anchor면 드래그/커밋 모두 무시

        if (IsLockedByAnchor)

        {

            if (commit) Log("Commit blocked: endpoint is Anchor (locked)");
            hasDragCache = false;
            StopDragging();
            return;

        }

        // ✅ BeginDrag가 정상 시작된 상태가 아니면 아무 것도 안 함

        if (!isDragging)

        {

            if (commit) StopDragging();
            return;

        }

        if (!Input.GetMouseButton(0) && !commit)

        {

            StopDragging();
            return;

        }

        var moving = GetMovingNode();
        var fixedN = GetFixedNode();
        if (moving == null || fixedN == null) { Fail(commit, "moving/fixed null"); return; }
        if (!hasDragCache) CacheDragState(moving);
        Vector2 fixedPoint = fixedN.WorldPos;

        // 1) 기본 스냅/클램프(현재 레일 min/max 기반)

        // 2) 연결 레일 제약으로 한번 더 보정(+ 현재 레일 길이 재클램프 포함 버전)

        Vector2 snapped = RailGridUtil.GetSnappedClampedEnd(
            rail.grid,
            fixedPoint,
            mouseWorld,
            rail.minLength,
            rail.maxLength
        );

        // ✅ 힌트 제한(restrictDragToHints): 힌트가 비어있으면 이동 자체를 막음
        if (restrictDragToHints && (_hintPositions == null || _hintPositions.Count == 0))
        {
            if (commit) { Fail(true, "no hint positions"); }
            return; // 프리뷰 단계: 그냥 멈춤(튕김/롤백 없음)
        }

        // ✅ 힌트에 가장 가까운 점으로 스냅 (SnapPoint도 힌트 후보에 포함됨)
        snapped = FindClosestHint(mouseWorld, snapped);


        // =========================================================
        // ✅ Fast reject (drag perf)
        // 1) endpoint cell already occupied by placed object (quick cut)
        //    - run BEFORE expensive common validation / connected-rails scans
        // =========================================================
        EnsureOccBaked();
        if (_occ != null)
        {
            Vector2Int cEnd = rail.grid.WorldToCell(snapped);

            // drag 중에는 '이전 부착 PO'를 무시해주면 경계에서 뻑뻑해지는 걸 줄일 수 있음
            int ignoreAId = (_dragPrevOwner != null) ? _dragPrevOwner.GetInstanceID() : 0;

            // 벽(-1) 또는 다른 PO 점유면 바로 탈락
            if (_occ.IsWallCell(cEnd) || _occ.IsPlacedCellOtherThan(cEnd, ignoreAId, 0, null))
            {
                if (commit) Fail(true, "endpoint cell blocked (fast reject)");
                return; // ✅ 이동 자체를 안 함
            }
        }


        // 같은 위치 반복 적용 방지

        if (!commit && hasLast && (snapped - lastAppliedPos).sqrMagnitude < MOVE_EPS_SQ)
            return;

        // (옵션) 연결 레일 제한을 쓰는 경우 캐시 준비

        if (hintUseConnectedRailLimits)
            RebuildConnectedRailsCache(moving);
        float hintWallR = Mathf.Max(0.001f, (ruleProfile != null && ruleProfile.endpointAllowRadius > 0f) ? ruleProfile.endpointAllowRadius : rail.endpointBlockRadius);

        // ✅ 이제서야 실제 이동

        ApplyMove(moving, snapped, notifyAllRails: commit);

        // 프리뷰 단계(마우스 누르고 있는 중)

        if (!commit)

        {

            // Physics2D.SyncTransforms(); // (Occupancy-only wall check)

            rail.Refresh(syncFromNodes: true);
            RefreshConnectedRailsCached(moving);
            return;

        }


        // 커밋 단계

        if (!CommitMove(moving, fixedN, snapped))

        {

            StopDragging();
            return;

        }

        // Physics2D.SyncTransforms(); // (Occupancy-only wall check)

        rail.Refresh(syncFromNodes: true);
        // ✅ Duplicate registry: 커밋(이동 완료)된 최종 edge 등록
        RailEdgeRegistry2D.Register(rail.gameObject.GetInstanceID(), rail.StartWorld, rail.EndWorld);

        // ✅ (중요) 레일 끝점이 PO의 SnapPoint에 붙었다면, 그 PO의 바인딩(Entries)을 즉시 갱신
        // - 레일을 PO에 연결했을 때 PO 드래그에서 레일 정보가 안 보이던 문제 해결
        UpdateOwnerBindingAfterCommit(moving);

        RefreshConnectedRailsCached(isStart ? rail.startNode : rail.endNode);
        hasDragCache = false;
        UpdateLockState();
        StopDragging();

    }

    public void CancelDrag()

    {

        if (hasDragCache)
            RollbackToCached();
        hasDragCache = false;
        hasLast = false;
        lastAppliedPos = new Vector2(99999, 99999);
        UpdateLockState();
        StopDragging();

    }

    // =========================================================

    // ✅ 설치 힌트: start 찍은 뒤에도 가능한 end 지점 표시

    // ========================================================

    // =========================================================

    // Move Hints (Drag 중)

    // =========================================================

    void UpdateMoveHintsAccurate(bool force)

    {

        if (!showMoveHints) return;
        if (rail == null || rail.grid == null) { HideHints(); return; }
        if (IsLockedByAnchor) { HideHints(); return; }
        if (!isDragging) { HideHints(); return; }
        var moving = GetMovingNode();
        var fixedN = GetFixedNode();
        if (moving == null || fixedN == null) { HideHints(); return; }
        BuildHints_Common(
            force: force,
            fixedPos: fixedN.WorldPos,
            movingNode: moving,
            fixedNode: fixedN,
            excludeNode: moving,
            priority: moveHintPriority,
            useConnectedRailLimits: hintUseConnectedRailLimits,
            useSegmentWallCheck: hintUseSegmentWallCheck,
            excludePlacedObjects: hintExcludePlacedObjects
        );

    }

    // =========================================================

    // Candidate resolve (공용)

    // =========================================================


    // -------------------------

    // Commit pipeline + helpers

    // -------------------------

    bool CommitMove(RailSnapNode2D moving, RailSnapNode2D fixedN, Vector2 snapped)
    {
        var mgr = RailSnapNodeManager.Instance;
        RailSnapNode2D merged = FindExistingNodeAtPosExcluding(mgr, snapped, moving) ?? moving;

        RailSnapNode2D finalStart = isStart ? merged : fixedN;
        RailSnapNode2D finalEnd = isStart ? fixedN : merged;

        if (merged != moving)
        {
            // 기존 moving 노드에서 이 레일 해제
            moving?.UnregisterRail(rail);

            // 레일의 endpoint를 새 노드로 교체
            if (isStart) rail.startNode = merged;
            else rail.endNode = merged;

            // 새 노드에 이 레일 등록
            merged?.RegisterRail(rail);

            TryDestroyNodeIfUnused(moving);
        }

        UpdateLockState();
        return true;
    }

    void ApplyMove(RailSnapNode2D moving, Vector2 snapped, bool notifyAllRails = true)

    {

        if (moving == null) return;
        if (moving.IsAnchor) return;

        moving.transform.position = snapped;

        // ✅ PO/바인딩/그래프 전체 재계산은 '커밋' 순간에만 (프리뷰 드래그 중에는 억제)
        if (notifyAllRails)
        {
            if (RailSnapNodeManager.Instance != null)
                RailSnapNodeManager.Instance.OnNodeMoved(moving);

            RailSpan2D.NotifyNodeMoved(moving);   // ✅ 이 노드를 쓰는 모든 레일/바인딩 갱신
        }

        lastAppliedPos = snapped;
        hasLast = true;

    }
    void EnsureRailNodes()

    {

        var mgr = RailSnapNodeManager.Instance;
        if (mgr == null) return;
        if (rail.startNode == null) rail.startNode = mgr.GetOrCreate(rail.start);
        if (rail.endNode == null) rail.endNode = mgr.GetOrCreate(rail.end);
        UpdateLockState();

    }

    RailSnapNode2D GetMovingNode() => isStart ? rail.startNode : rail.endNode;
    RailSnapNode2D GetFixedNode() => isStart ? rail.endNode : rail.startNode;
    static RailSnapNode2D FindExistingNodeAtPosExcluding(RailSnapNodeManager mgr, Vector2 pos, RailSnapNode2D exclude)

    {

        if (mgr == null) return null;

        // Physics2D.SyncTransforms(); // (Occupancy-only wall check)

        int count = Physics2D.OverlapCircleNonAlloc(pos, mgr.mergeRadius, nodeHits, mgr.railNodeMask);
        RailSnapNode2D best = null;
        float bestDist = float.MaxValue;
        for (int i = 0; i < count; i++)

        {

            var c = nodeHits[i];
            if (c == null) continue;
            var node = c.GetComponentInParent<RailSnapNode2D>();
            if (node == null || node == exclude) continue;
            var follow = node.GetComponent<RailNodeFollow2D>();
            if (follow != null && follow.IsFollowing) continue;
            float d = ((Vector2)node.transform.position - pos).sqrMagnitude;
            if (d < bestDist)

            {

                bestDist = d;
                best = node;

            }

        }

        return best;

    }


    // =========================================================
    // Drag perf: connected rails refresh without FindObjectsByType
    // =========================================================
    void RefreshConnectedRailsCached(RailSnapNode2D node)
    {
        if (node == null) return;

        // 연결 캐시가 비어있으면(혹은 갱신이 필요하면) 1회만 구축
        if (_connectedRailsCount <= 0)
            RebuildConnectedRailsCache(node);

        for (int i = 0; i < _connectedRailsCount; i++)
        {
            var r = _connectedRails[i];
            if (r == null) continue;
            if (r == rail) continue;
            if (r.startNode == node || r.endNode == node)
                r.Refresh(syncFromNodes: true);
        }
    }

    static void RefreshRailsUsingNode(RailSnapNode2D node)

    {

        if (node == null) return;

#if UNITY_2022_2_OR_NEWER

        var rails = Object.FindObjectsByType<RailSpan2D>(FindObjectsSortMode.None);

#else

        var rails = Object.FindObjectsOfType<RailSpan2D>();

#endif

        foreach (var r in rails)

        {

            if (r == null) continue;
            if (r.startNode == node || r.endNode == node)
                r.Refresh(syncFromNodes: true);

        }

    }

    static int CountRailsUsingNode(RailSnapNode2D n)

    {

        if (n == null) return 0;

#if UNITY_2022_2_OR_NEWER

        var rails = Object.FindObjectsByType<RailSpan2D>(FindObjectsSortMode.None);

#else

        var rails = Object.FindObjectsOfType<RailSpan2D>();

#endif

        int count = 0;
        foreach (var r in rails)

        {

            if (r == null) continue;
            if (r.startNode == n || r.endNode == n) count++;

        }

        return count;

    }

    static void TryDestroyNodeIfUnused(RailSnapNode2D n)

    {

        if (n == null) return;
        if (n.IsAnchor) return;
        if (CountRailsUsingNode(n) == 0)
            Object.Destroy(n.gameObject);

    }

    void RebuildConnectedRailsCache(RailSnapNode2D node)

    {

        _connectedRailsCount = 0;
        if (node == null) return;

#if UNITY_2022_2_OR_NEWER

        var railsAll = Object.FindObjectsByType<RailSpan2D>(FindObjectsSortMode.None);

#else

    var railsAll = Object.FindObjectsOfType<RailSpan2D>();

#endif

        for (int i = 0; i < railsAll.Length; i++)

        {

            var r = railsAll[i];
            if (r == null) continue;
            if (r.startNode != node && r.endNode != node) continue;
            if (_connectedRailsCount >= _connectedRails.Length)
                System.Array.Resize(ref _connectedRails, _connectedRails.Length * 2);
            _connectedRails[_connectedRailsCount++] = r;

        }

        // 남는 슬롯 null로 (SatisfiesAllConnectedRails_Cached가 null 스킵하긴 하지만 안전)

        for (int i = _connectedRailsCount; i < _connectedRails.Length; i++)
            _connectedRails[i] = null;

    }



    // 후보(rawCandidate)를 넣으면

    // 1) TryResolveHintCandidate로 resolvedPos/mergeTarget 만들고

    // 2) 길이/병합/연결레일/벽(선분)/PO까지 동일 규칙으로 검사

    // ✅ 주의: 이 함수는 내부에서 Physics2D.SyncTransforms()를 호출하지 않는다.

    //        호출하는 쪽(힌트/드래그)에서 프레임당 1회만 SyncTransforms 하고 들어와야 한다.

    // =========================================================
    // ✅ 추가: 연결된 모든 레일이 '설치 가능'해야 이동 허용 (PO/레일 겹침 포함)
    // - penetration(침투량) 계산은 하지 않음 (RailPlacementRules2D / RailSpan2D 정책 유지)
    // - ignoreOwners(스냅/연결된 PO) 예외는 '엔드포인트 근처'에서만 허용되는 정책을 그대로 따른다.
    // ========================================================


    static readonly Collider2D[] _wallHits2 = new Collider2D[64];
    // =========================================================
    // Hint Rule: "진짜 설치 가능"(요청한 0~3 규칙)
    // - 길이 제한: BuildHints_Common에서 이미 필터
    // - 점유(occ) 충돌: 벽/PO 점유 셀에 막힘
    // - 예외:
    //   (2) "연결되지 않은" PO SnapPoint가 있는 셀은 '끝점 셀'만 허용
    //   (3) "연결된" PO SnapPoint는 반경 N칸(connectedSnapIgnoreRadiusCells)만큼 해당 PO 점유 무시
    // =========================================================

    bool IsSnapPointConnectedToOwner(SnapPoint sp, PlacementObject owner)
    {
        if (sp == null || owner == null) return false;

        var bind = owner.GetComponent<RailNodeFollowBinding2D>();
        if (bind == null) return false;

        // 멀티 바인딩 우선
        var es = bind.Entries;
        if (es != null && es.Count > 0)
        {
            for (int i = 0; i < es.Count; i++)
            {
                var e = es[i];
                if (e.anchorPoint != sp.transform) continue;
                if (e.node != null) return true;
            }
            return false;
        }

        // 구버전 단일 호환: anchorPoint가 없으니 "노드가 존재"하면 연결된 것으로 간주
        return bind.node != null;
    }

    void ResolveSnapOwnerAt(
        Vector2 world,
        out PlacementObject owner,
        out int ownerId,
        out bool isConnected,
        out bool endpointCellOnly
    )
    {
        owner = null;
        ownerId = 0;
        isConnected = false;
        endpointCellOnly = false;

        if (!TryPickSnapPoint(world, snapPointPickRadius, out var sp) || sp == null)
            return;

        owner = GetSnapOwner(sp);
        if (owner == null) return;

        ownerId = owner.GetInstanceID();
        isConnected = IsSnapPointConnectedToOwner(sp, owner);
        endpointCellOnly = (!isConnected && allowUnconnectedSnapPointEndpointCellOnly);
    }

    void ResolveSnapOwnerAt_NodeAware(
    Vector2 world,
    RailSnapNode2D node,
    out PlacementObject owner,
    out int ownerId,
    out bool isConnected,
    out bool endpointCellOnly
)
    {
        owner = null;
        ownerId = 0;
        isConnected = false;
        endpointCellOnly = false;

        // ✅ 1) 노드가 이미 PO를 따라가고 있으면(= 실제 연결) 그 ownerId를 최우선 사용
        if (node != null)
        {
            var follow = node.GetComponent<RailNodeFollow2D>();
            if (follow != null && follow.ownerId != 0)
            {
                ownerId = follow.ownerId;
                isConnected = true;      // “연결된 PO”로 취급
                endpointCellOnly = true; // 연결된 경우는 endpointCellOnly로 완화 유지(원하는 정책)
                return;
            }
        }

        // ✅ 2) fallback: 기존 방식(좌표에서 SnapPoint 집기)
        ResolveSnapOwnerAt(world, out owner, out ownerId, out isConnected, out endpointCellOnly);
    }


    bool SegmentHitsOccupied_WithSnapExceptions(
    Vector2 a,
    Vector2 b,
    float thickness,
    int ignoreOwnerIdA,
    bool ignoreOwnerAllA,
    bool endpointCellOnlyA,
    int ignoreOwnerIdB,
    bool ignoreOwnerAllB,
    bool endpointCellOnlyB
)
    {
        // ✅ Virtual-cell fast path (occupancy-only).
        // NOTE: Snap exceptions / owner exceptions are intentionally ignored per current design
        //       (SnapPoint cells should have no occupancy baked).
        var occ = GridOccupancy2D.Instance;
        if (occ == null || rail == null || rail.grid == null) return false;

        occ.EnsureBaked();

        GridManager g = rail.grid;

        float rWorld = Mathf.Max(0f, thickness * 0.5f);
        var cells = ComputeVirtualRailCellsPrecise(g, a, b, rWorld);

        for (int i = 0; i < cells.Count; i++)
        {
            int ownerId = occ.GetOwnerIdAtCell(cells[i]);
            if (ownerId == 0) continue;      // empty
            if (ownerId == -1) return true;  // wall
            return true;                     // any placed object
        }

        return false;
    }


    static float GetGridStep(GridManager grid)
    {
        if (grid == null) return 1f;
        Vector2 p0 = grid.CellToWorld(Vector2Int.zero);
        Vector2 p1 = grid.CellToWorld(Vector2Int.right);
        float step = Vector2.Distance(p0, p1);
        return (step > 0.0001f) ? step : 1f;
    }


    // 클래스 안(메서드 밖)
    private struct ConnectedCheck
    {
        public Vector2 otherPos;
        public float thickness;
        public float minSq;
        public float maxSq;
        public int otherOwnerId;
        public bool otherIgnoreAll;
        public bool otherEndpointOnly;
    }

    // ✅ 매번 new 하지 말고 재사용 (GC 감소)
    private readonly List<ConnectedCheck> _connectedChecks = new List<ConnectedCheck>(16);

    void BuildHints_Common(
    bool force,
    Vector2 fixedPos,
    RailSnapNode2D movingNode,
    RailSnapNode2D fixedNode,
    RailSnapNode2D excludeNode,
    int priority,
    bool useConnectedRailLimits,
    bool useSegmentWallCheck,
    bool excludePlacedObjects
)
    {
        if (rail == null || rail.grid == null) { HideHints(); return; }

        // ✅ Occupancy baked (wall 판단 등에 사용)
        EnsureOccBaked();

        float stepW = GetGridStep(rail.grid);
        if (stepW <= 0.0001f) stepW = 1f;

        if (useConnectedRailLimits && movingNode != null)
            RebuildConnectedRailsCache(movingNode);

        _hintPositions.Clear();

        int R = Mathf.Max(1, hintRadiusCells);
        int stride = Mathf.Max(1, hintCellStride);
        int R2 = R * R;
        Vector2Int fixedCell = rail.grid.WorldToCell(fixedPos);

        float minSq = rail.minLength * rail.minLength;
        float maxSq = (rail.maxLength > 0f) ? rail.maxLength * rail.maxLength : float.PositiveInfinity;

        // fixed 쪽 스냅 owner 정보(예외용)
        ResolveSnapOwnerAt_NodeAware(
            fixedPos,
            fixedNode,
            out var fixedOwner,
            out int fixedOwnerId,
            out bool fixedConnected,
            out bool fixedEndpointOnly
        );

        bool fixedIgnoreAll = fixedConnected;

        // =========================
        // ✅ End resolve 단계 (TryResolveEndForPreview와 동일 정책을 힌트에도 적용)
        // - 벽 안: 즉시 불가
        // - SnapPoint 스냅: 좌표만 사용(노드 생성 금지) + 스냅 후 벽이면 불가
        // - 기존 RailNode가 근처에 있으면 그 노드로 스냅(단, exclude/moving은 제외)
        // =========================
        bool IsWallAtOcc(Vector2 world)
        {
            if (_occ == null || rail == null || rail.grid == null) return false;
            Vector2Int c = rail.grid.WorldToCell(world);
            return _occ.IsWallCell(c);
        }

        bool TryResolveEndForHints(
            Vector2 endCandidate,
            out RailSnapNode2D endNode,
            out Vector2 endPos,
            out bool endAllowed
        )
        {
            endNode = null;
            endPos = endCandidate;
            endAllowed = true;

            // ✅ 정책: endCandidate가 벽 안이면 무조건 불가능
            if (IsWallAtOcc(endCandidate))
            {
                endAllowed = false;
                return true;
            }

            // ✅ SnapPoint는 "좌표만" 사용 (노드 생성 금지)
            if (TryPickSnapPoint(endCandidate, snapPointPickRadius, out SnapPoint sp) && sp != null)
            {
                endPos = sp.transform.position;

                // ✅ 정책: SnapPoint로 스냅된 endPos가 벽 안이면 무조건 불가능
                if (IsWallAtOcc(endPos))
                {
                    endAllowed = false;
                    return true;
                }
            }

            // ✅ 기존 노드가 있으면 노드로 스냅 (exclude/moving 제외)
            var mgr = RailSnapNodeManager.Instance;
            if (mgr != null)
            {
                RailSnapNode2D pick = FindExistingNodeAtPosExcluding(mgr, endPos, excludeNode != null ? excludeNode : movingNode);
                if (pick != null && pick != movingNode && pick != excludeNode)
                {
                    endNode = pick;
                    endPos = pick.WorldPos;
                }
            }

            // (이 파일에는 CanAddRailToNode / forbidEndOnExistingRailNode / allowSnapPointAsRailNode 필드가 없으므로
            //  힌트 단계에서는 "노드에 붙는 것" 자체를 막지 않는다. 실제 설치/커밋 쪽에서 최종 제약을 적용.)

            return true;
        }

        bool IsNodeCapacityFull(RailSnapNode2D node)
        {
            if (node == null) return false;

            int cap = node.GetCapacity(rail.maxRailsPerNode);     // override 우선, 없으면 rail 기본
            int cnt = node.GetConnectedRailCount(exclude: rail);  // 현재 레일 제외

            Debug.Log($"노드 {node.name} IsAnchor={node.IsAnchor} 용량={cap} 현재개수={cnt}");
            return cnt >= cap;
        }

        // =========================
        // ✅ Connected rail 캐시 만들기 (이번 호출 1회)
        // =========================
        void BuildConnectedChecksOnce()
        {
            _connectedChecks.Clear();

            if (!useConnectedRailLimits) return;
            if (movingNode == null) return;

            if (_connectedRailsCount <= 0)
                RebuildConnectedRailsCache(movingNode);

            for (int i = 0; i < _connectedRailsCount; i++)
            {
                var r = _connectedRails[i];
                if (r == null) continue;

                bool movingIsStart = (r.startNode == movingNode);
                bool movingIsEnd = (r.endNode == movingNode);
                if (!movingIsStart && !movingIsEnd) continue;

                Vector2 otherPos = movingIsStart
                    ? ((r.endNode != null) ? r.endNode.WorldPos : r.end)
                    : ((r.startNode != null) ? r.startNode.WorldPos : r.start);

                RailSnapNode2D otherNode = movingIsStart ? r.endNode : r.startNode;

                ResolveSnapOwnerAt_NodeAware(
                    otherPos,
                    otherNode,
                    out var otherOwner,
                    out int otherOwnerId,
                    out bool otherConnected,
                    out bool otherEndpointOnly
                );

                _connectedChecks.Add(new ConnectedCheck
                {
                    otherPos = otherPos,
                    thickness = r.thickness,
                    minSq = r.minLength * r.minLength,
                    maxSq = (r.maxLength > 0f) ? r.maxLength * r.maxLength : float.PositiveInfinity,
                    otherOwnerId = otherOwnerId,
                    otherIgnoreAll = otherConnected,
                    otherEndpointOnly = otherEndpointOnly
                });
            }
        }

        BuildConnectedChecksOnce();

        bool AllConnectedRailsAllowCandidate_Cached(
            Vector2 candidatePos,
            int candOwnerId,
            bool candIgnoreAll,
            bool candEndpointOnly
        )
        {
            if (!useConnectedRailLimits) return true;
            if (movingNode == null) return true;

            for (int i = 0; i < _connectedChecks.Count; i++)
            {
                var cc = _connectedChecks[i];

                float lenSq = (candidatePos - cc.otherPos).sqrMagnitude;
                if (lenSq < cc.minSq) return false;
                if (lenSq > cc.maxSq) return false;

                if (SegmentHitsOccupied_WithSnapExceptions(
                        cc.otherPos,
                        candidatePos,
                        cc.thickness,
                        cc.otherOwnerId,
                        cc.otherIgnoreAll,
                        cc.otherEndpointOnly,
                        candOwnerId,
                        candIgnoreAll,
                        candEndpointOnly
                    ))
                    return false;
            }

            return true;
        }

        // =========================
        // 1) Grid 후보들
        // =========================
        for (int y = -R; y <= R; y += stride)
        {
            for (int x = -R; x <= R; x += stride)
            {
                if (x * x + y * y > R2) continue;

                Vector2Int cell = fixedCell + new Vector2Int(x, y);
                Vector2 raw = rail.grid.CellToWorld(cell);

                // ✅ end resolve 먼저 (벽 컷/스냅/기존노드)
                TryResolveEndForHints(raw, out var endNode, out var endPos, out bool endAllowed);
                if (!endAllowed) continue;

                // ✅ 추가: end가 ANCHOR(기존 노드)에 스냅된 경우, 용량 초과면 힌트 제외
                if (endNode != null && IsNodeCapacityFull(endNode))
                    continue;

                // 길이 체크는 resolve된 endPos 기준
                float dSq = (endPos - fixedPos).sqrMagnitude;
                if (dSq < minSq) continue;
                if (dSq > maxSq) continue;

                ResolveSnapOwnerAt_NodeAware(
                    endPos,
                    movingNode,
                    out var moveOwner,
                    out int moveOwnerId,
                    out bool moveConnected,
                    out bool moveEndpointOnly
                );
                bool moveIgnoreAll = moveConnected;

                if (SegmentHitsOccupied_WithSnapExceptions(
                        fixedPos,
                        endPos,
                        rail.thickness,
                        fixedOwnerId,
                        fixedIgnoreAll,
                        fixedEndpointOnly,
                        moveOwnerId,
                        moveIgnoreAll,
                        moveEndpointOnly
                    ))
                    continue;

                if (!AllConnectedRailsAllowCandidate_Cached(endPos, moveOwnerId, moveIgnoreAll, moveEndpointOnly))
                    continue;

                _hintPositions.Add(endPos);
                if (_hintPositions.Count >= hintMaxDots) goto DONE;
            }
        }

        // =========================
        // 2) SnapPoint 후보들(부족하면 추가)
        // =========================
        if (_hintPositions.Count < hintMaxDots)
        {
            float searchR = (rail.maxLength > 0f)
                ? rail.maxLength + snapPointPickRadius
                : (R * stepW) + snapPointPickRadius;

            int spCount = (snapPointMask.value != 0)
                ? Physics2D.OverlapCircleNonAlloc(fixedPos, searchR, _spHits, snapPointMask)
                : Physics2D.OverlapCircleNonAlloc(fixedPos, searchR, _spHits);

            for (int i = 0; i < spCount; i++)
            {
                if (_hintPositions.Count >= hintMaxDots) break;

                var c = _spHits[i];
                if (c == null) continue;

                var sp = c.GetComponentInParent<SnapPoint>();
                if (sp == null) continue;

                Vector2 pRaw = sp.transform.position;

                // ✅ end resolve 먼저
                TryResolveEndForHints(pRaw, out var endNode, out var endPos, out bool endAllowed);
                if (!endAllowed) continue;

                if (endNode != null && IsNodeCapacityFull(endNode))
                    continue;

                // 길이 체크는 resolve된 endPos 기준
                float dSq = (endPos - fixedPos).sqrMagnitude;
                if (dSq < minSq) continue;
                if (dSq > maxSq) continue;

                // 중복 제거도 endPos 기준
                float dupR = stepW * 0.15f;
                float dupSq = dupR * dupR;
                bool dup = false;
                for (int k = 0; k < _hintPositions.Count; k++)
                {
                    if ((_hintPositions[k] - endPos).sqrMagnitude <= dupSq) { dup = true; break; }
                }
                if (dup) continue;

                ResolveSnapOwnerAt_NodeAware(
                    endPos,
                    movingNode,
                    out var moveOwner,
                    out int moveOwnerId,
                    out bool moveConnected,
                    out bool moveEndpointOnly
                );
                bool moveIgnoreAll = moveConnected;

                if (SegmentHitsOccupied_WithSnapExceptions(
                        fixedPos,
                        endPos,
                        rail.thickness,
                        fixedOwnerId,
                        fixedIgnoreAll,
                        fixedEndpointOnly,
                        moveOwnerId,
                        moveIgnoreAll,
                        moveEndpointOnly
                    ))
                    continue;

                if (!AllConnectedRailsAllowCandidate_Cached(endPos, moveOwnerId, moveIgnoreAll, moveEndpointOnly))
                    continue;

                _hintPositions.Add(endPos);
            }
        }

    DONE:
        if (_hintPositions.Count == 0)
            MoveHintBroker2D.Instance?.Clear(this);
        else
            MoveHintBroker2D.Instance?.Request(this, priority, _hintPositions);
    }





    Vector2 FindClosestHint(Vector2 mouseWorld, Vector2 fallback)
    {
        if (_hintPositions == null || _hintPositions.Count == 0)
            return fallback;

        float bestDist = float.MaxValue;
        Vector2 best = fallback;

        for (int i = 0; i < _hintPositions.Count; i++)
        {
            float d = (_hintPositions[i] - mouseWorld).sqrMagnitude;
            if (d < bestDist)
            {
                bestDist = d;
                best = _hintPositions[i];
            }
        }
        return best;
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

        // Candidate range: segment AABB + rWorld
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

                // IMPORTANT: This project assumes GridManager.CellToWorld returns cell center (same convention as RailCellMap2D)
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



    // -----------------------
    // Geometry helpers (copied minimal from GridPlacer / RailCellMap2D)
    // -----------------------
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



    // Geometry helper: segment intersection (2D)
    static bool SegmentsIntersect(Vector2 a0, Vector2 a1, Vector2 b0, Vector2 b1)
    {
        float o1 = Orient(a0, a1, b0);
        float o2 = Orient(a0, a1, b1);
        float o3 = Orient(b0, b1, a0);
        float o4 = Orient(b0, b1, a1);

        // General case
        if ((o1 > 0f && o2 < 0f || o1 < 0f && o2 > 0f) && (o3 > 0f && o4 < 0f || o3 < 0f && o4 > 0f))
            return true;

        const float EPS = 1e-6f;

        // Collinear / touching cases
        if (Mathf.Abs(o1) <= EPS && OnSegment(a0, a1, b0)) return true;
        if (Mathf.Abs(o2) <= EPS && OnSegment(a0, a1, b1)) return true;
        if (Mathf.Abs(o3) <= EPS && OnSegment(b0, b1, a0)) return true;
        if (Mathf.Abs(o4) <= EPS && OnSegment(b0, b1, a1)) return true;

        return false;
    }

    static float Orient(Vector2 a, Vector2 b, Vector2 c)
    {
        return (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
    }

    static bool OnSegment(Vector2 a, Vector2 b, Vector2 p)
    {
        return p.x >= Mathf.Min(a.x, b.x) - 1e-6f && p.x <= Mathf.Max(a.x, b.x) + 1e-6f &&
               p.y >= Mathf.Min(a.y, b.y) - 1e-6f && p.y <= Mathf.Max(a.y, b.y) + 1e-6f;
    }
    static float DistanceSq_SegmentSegment(Vector2 a0, Vector2 a1, Vector2 b0, Vector2 b1)
    {
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

    bool IsPointerOverUI()
    {
        if (EventSystem.current == null) return false;
        return EventSystem.current.IsPointerOverGameObject();
    }

}
