using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;


public class GridPlacer : MonoBehaviour

{

    // =========================================================
    // GridPlacer.cs - NAVIGATION (TOC)
    // ---------------------------------------------------------
    // [UPDATE] Update() / IsBuildMode() / OnToolChanged()
    // [PLACE]  TickPlaceTool() / ApplyPlace() / CreateGhost()/Preview
    // [SELECT] TickSelectTool() / HandleSelection() / HandleDragMove()
    // [SNAP]   Snap backup/restore + SnapPoint helpers
    // [OCC]    Occupancy segment checks (Snap exceptions 포함)
    // [RAIL]   Rail bind / attached-rail validation helpers
    // [CACHE]  Placement/drag caches + world rev bump
    // Tip) Ctrl+F로 [PLACE] 같은 태그 검색하면 바로 점프됨
    // =========================================================

    #region Fields (Refs / Settings / Runtime State)

    // =========================================================

    // Refs / Data

    // =========================================================

    [Header("Refs")]

    public GridManager grid;
    public PlacementData placementData;


    // =========================================================
    // Fixed Root (Stage-placed PO container)
    // - If a PlacementObject is a child of this root, it cannot be selected / moved / rotated / flipped / deleted in-game.
    // =========================================================
    [Header("Fixed Root (Stage-placed POs)")]
    [Tooltip("이 Transform 아래(자식 포함)에 있는 PO는 선택/이동/회전/플립/삭제가 금지됩니다.")]
    [SerializeField] Transform fixedRoot;

    /// <summary>
    /// 스테이지 로드/전환 시점에 FixedRoot를 런타임으로 주입할 때 사용.
    /// (GridPlacer가 프리팹이라 씬마다 인스펙터 드래그가 번거로운 경우)
    /// </summary>
    public void SetFixedRoot(Transform root)
    {
        fixedRoot = root;

        // ✅ 이미 선택된 PO가 fixedRoot 아래로 들어가면 즉시 해제 (안전)
        if (selected != null && IsUnderFixedRoot(selected))
            ClearSelection(forceFinalize: false);
    }

    // =========================================================

    // Masks

    // =========================================================

    [Header("Masks")]

    public LayerMask wallMask;
    public LayerMask placedMask;
    public LayerMask railMask;

    // =========================================================
    // Rail ↔ PO Snap exceptions (used by PO-drag rail validity / hints)
    // =========================================================
    [Header("Rail ↔ PO Snap Exceptions")]
    [Tooltip("SnapPoint 레이어 마스크 (연결/미연결 스냅포인트 예외를 계산할 때 사용)")]
    [SerializeField] LayerMask snapPointMask;

    [Tooltip("엔드포인트가 스냅포인트 위에 있다고 볼 반경")]
    [SerializeField] float snapPointPickRadius = 0.25f;

    [Tooltip("(규칙2) 미연결 SnapPoint가 있는 셀은 '엔드포인트 셀 1칸'만 점유 예외로 허용")]
    [SerializeField] bool allowUnconnectedSnapPointEndpointCellOnly = true;

    [Header("Place")]
    [SerializeField] bool allowContinuousPlace = true; // ✅ 연속 설치 옵션

    static readonly Collider2D[] _spHitsTmp = new Collider2D[32];

    // ✅ SnapPoint 주변 RailEndpointHandle 충돌 검사 버퍼 (NonAlloc)
    static readonly Collider2D[] _tmpSnapHandleCols = new Collider2D[32];
    readonly HashSet<RailSpan2D> _tmpAllowedRailsForSnap = new HashSet<RailSpan2D>(32);

    [Header("Rail Rule Profile (Optional)")]
    [SerializeField] RailPlacementRuleProfile2D railRuleProfile;

    Vector2 _dragLastAllowedPos;
    bool _hasDragLastAllowedPos;
    // =========================================================
    // Rail Rule Profile helpers
    // =========================================================

    // =========================================================

    // Place Controls

    // =========================================================

    [Header("Place Rotation (Step)")]

    public float placeRotateStep = 90f;

    // =========================================================

    // Select Controls

    // =========================================================

    [Header("Select Drag")]

    [Tooltip("월드 좌표 기준. 이 거리 이상 움직여야 드래그 시작")]

    public float dragStartDistance = 0.10f;

    [Header("Select Rotation (Step)")]

    public bool enableSelectRotate = true;
    public float selectRotateStep = 90f;

    [Header("Rail Node Snap Exception (Place)")]

    public LayerMask railNodeMask;            // RailSnapNode2D가 잡히는 레이어(보통 "RailNode")
    public float railNodeSnapRadius = 0.18f;  // 0.15~0.25 사이로 튜닝

    [Header("Snap Local Exception (Cells)")]

    float _gridStepCached = -1f;

    [Header("Placement Area")]
    [SerializeField] HollowRectSpriteFrame placementFrame;
    [SerializeField, Min(0f)] float placementFrameMargin = 0.02f;

    public PlacementObject PreviewPlacementObject => previewPO;
    public bool HasPlacementPreview => previewPO != null || ghost != null;
    // Flip state (Place + Select 공용)

    bool isFlipX;

    // Grab offset

    Vector2 dragGrabOffset;
    bool hasGrabOffset;

    // Preview scale cache

    Vector3 basePreviewScale = Vector3.one;
    Vector3 baseGhostScale = Vector3.one;

    // =========================================================

    // Place Preview State

    // =========================================================

    GameObject ghost;
    PlacementObject previewPO;
    float placeRotZ;
    bool placeHasSnap;
    SnapPreviewPair placeSnap;
    PlacementObject placeSnapTarget;

    // =========================================================

    // Select State

    // =========================================================

    // Select (✅ GridPlacer는 PlacementObject만)

    PlacementObject selected;

    // Drag state

    bool dragCandidate;
    bool pointerDownOnSelected;
    bool isDragging;
    Vector2 pointerDownWorld;
    Vector3 dragStartPos;
    Quaternion dragStartRot;

    // 드래그 중 “직전 파트너 허용” 캐시

    readonly List<PlacementObject> dragPrevPartners = new();

    public enum SelectedDragPhase
    {
        None,
        Candidate,   // 선택은 되었고 아직 threshold 전
        Dragging     // 실제 드래그 중
    }

    public event Action<PlacementObject> SelectedDragStarted;
    public event Action<PlacementObject, bool> SelectedDragEnded; // bool committed
    public event Action<PlacementObject, SelectedDragPhase> SelectedDragPhaseChanged;

    public SelectedDragPhase CurrentDragPhase { get; private set; } = SelectedDragPhase.None;

    public bool IsDraggingSelectedPO => CurrentDragPhase == SelectedDragPhase.Dragging;
    public bool IsDragCandidateSelectedPO => CurrentDragPhase == SelectedDragPhase.Candidate;
    public bool IsPointerDownOnSelectedPO => pointerDownOnSelected;
    public PlacementObject DragTargetPO => selected;

    // ✅ 롤백 시 "스냅 연결" 복구용
    // =========================================================

    // Misc runtime

    // =========================================================

    readonly Collider2D[] pointHits = new Collider2D[16];
    BuildTool lastTool = BuildTool.Select;
    RailSpan2D _dragBlockedRail;

    // =========================================================

    // Move Hint / Rail Bind

    // =========================================================

    [SerializeField] POMoveRailHint2D poMoveRailHint;

    [SerializeField] bool showRailHintsWhileMovingPO = true;

    [Header("Rail Bind Penetration (Cells)")]

    [SerializeField] float railBindPenetrationCells = 0.5f; // ✅ 오너(바인딩 PO)와 레일 침투 허용: 0.5칸

    [SerializeField] bool railFollowDebug = false; // 로그 보고 싶으면 true

    [SerializeField] float railFollowRadius = 0.25f; // ✅ 0.18 너무 빡셈. 최소 0.25 권장

    [Header("Drag Performance")]
    [SerializeField, Min(1)] int dragRailsRecheckEveryNFrames = 2; // 레일 이동 판정은 비싸서 N프레임마다만 재검사

    [Header("PO Pick")]
    [SerializeField, Range(4f, 30f)] float poPickRadiusPx = 12f;

    [Header("Ghost Snap Visual")]
    [SerializeField] bool showSnapPointsOnGhost = true;

    bool _occIgnoreActive = false;
    int _occIgnoreOwnerId = 0;

    // 드래그 시작 전 바인딩 백업(원래 붙어있던 노드로 되돌리기용)
    RailNodeFollowBinding2D.Snapshot _dragFollowBackup;
    bool _hasDragFollowBackup;

    // Drag perf cache
    int _dragFrameCounter;
    Vector2 _dragCachedGridPos;
    Vector2 _dragCachedTargetPos;
    bool _dragCacheValid;
    bool _dragCachedCanPlaceBase;
    bool _dragCachedRailsOk;
    RailSpan2D _dragCachedBlockedRail;
    RailNodeFollowBinding2D _dragBind;

    OccupancyHintOverlay2D _occOverlay;

    // ✅ PO 드래그 중 cellmap에서 제외할 레일들
    readonly List<RailSpan2D> _dragExcludedRails = new List<RailSpan2D>(64);
    readonly HashSet<RailSpan2D> _dragExcludedRailsSet = new HashSet<RailSpan2D>();


    // ===========================

    // Node -> Owner cache (NO-ALLOC)

    // ===========================

    readonly Dictionary<RailSnapNode2D, PlacementObject> _nodeOwnerCache = new Dictionary<RailSnapNode2D, PlacementObject>(256);
    readonly Dictionary<RailSnapNode2D, PlacementObject> _nodeOwnerCache2 = new Dictionary<RailSnapNode2D, PlacementObject>(256);
    int _nodeOwnerCacheFrame = -1;
    bool _railFollowPreparedThisDrag = false;
    int _railFollowPreparedDragOwnerId = 0;

    // ===========================

    // NO-ALLOC reusable buffers

    // ===========================

    readonly List<RailNodeFollowBinding2D.Entry> _tmpLegacyEntries = new List<RailNodeFollowBinding2D.Entry>(1);
    readonly HashSet<RailSnapNode2D> _movedNodesBuf = new HashSet<RailSnapNode2D>();
    readonly Dictionary<RailSnapNode2D, Vector3> _oldPosBuf = new Dictionary<RailSnapNode2D, Vector3>();
    readonly List<PlacementObject> _ignoreOwnersBuf = new List<PlacementObject>(2);
    readonly List<PlacementObject> _ignoreOwnersBuf2 = new List<PlacementObject>(2);
    readonly List<PlacementObject> _prevPartnersBuf = new List<PlacementObject>(8);


    readonly HashSet<RailSpan2D> _blockedPreviewRails = new HashSet<RailSpan2D>();
    readonly List<RailSpan2D> _tmpPreviewRails = new List<RailSpan2D>(16);
    public bool IsSelectedLocked { get; private set; }

    [SerializeField] GameKeyBindingConfig keyConfig;

#if UNITY_EDITOR

    [SerializeField] bool debugRailMoveCheck = true;

#endif

#if UNITY_EDITOR

    [Header("Debug")]

    [SerializeField] bool debugRailBindDump = true;


#endif

#if UNITY_EDITOR
    [SerializeField] bool debugCache = true; // 원할 때만 켜기
#endif

    #endregion

    public static GridPlacer Instance { get; private set; }

    StageSaveManager _stageSaveManager;
    bool _poDragUndoBeginNotified;
    Coroutine _poDragDeferredCommitCo;
    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        _stageSaveManager = FindFirstObjectByType<StageSaveManager>();
    }

    public static bool IsPlacementPreviewActive
    {
        get
        {
            return Instance != null && Instance.IsPlacementPreviewActive_Internal();
        }
    }

    bool IsPlacementPreviewActive_Internal()
    {
        if (!enabled || !gameObject.activeInHierarchy)
            return false;

        return ghost != null || previewPO != null;
    }

    public void SetPlacementData(PlacementData data)
    {
        placementData = data;

        ClearPlacePreviewObjects();

        placeRotZ = 0f;
        isFlipX = false;
        placeStrengthLevel = 1;
    }
    // =========================================================
    // Fixed Root helpers
    // =========================================================
    bool IsUnderFixedRoot(PlacementObject po)
    {
        if (po == null) return false;
        if (fixedRoot == null) return false;
        return po.transform.IsChildOf(fixedRoot);
    }

    bool IsSelectedFixed()
    {
        return selected != null && IsUnderFixedRoot(selected);
    }


    public void SetPlacementFrame(HollowRectSpriteFrame frame)
    {
        if (frame == null) return;
        placementFrame = frame;
    }

    #region Update / Tool Switch
    // [UPDATE] Update - 프레임 루프: 현재 툴 상태에 따라 Place/Select 처리


    void Update()

    {

        if (!IsBuildMode())

        {

            if (lastTool != BuildTool.None)

            {

                ClearPlacePreviewObjects();
                ClearSelection(forceFinalize: false);
                lastTool = BuildTool.None;

            }

            return;

        }

        if (grid == null) return;
        var tool = (BuildToolManager.Instance != null)
            ? BuildToolManager.Instance.currentTool
            : BuildTool.Select;
        if (tool != lastTool)
            OnToolChanged(tool);
        switch (tool)

        {

            case BuildTool.Place:
                TickPlaceTool();
                break;
            case BuildTool.Select:
                TickSelectTool();
                break;

        }

    }

    // [UPDATE] IsBuildMode
    bool IsBuildMode()

    {

        if (GameModeManager.Instance == null) return true;
        return GameModeManager.Instance.currentMode == GameMode.Build;

    }

    // [UPDATE] OnToolChanged - 툴 변경 이벤트 처리 + 상태 초기화
    void OnToolChanged(BuildTool tool)

    {

        if (tool != BuildTool.Place)
            ClearPlacePreviewObjects();
        if (tool != BuildTool.Select)

        {

            ResetDragState();
            ClearSelection(forceFinalize: false);

        }

        ClearBlockedRailPreview(); // ✅ 추가
        lastTool = tool;

    }

    // =========================================================

    #endregion

    #region PLACE
    // PLACE

    // =========================================================

    // [PLACE] TickPlaceTool - 배치 모드: 고스트/프리뷰 업데이트 + 클릭 시 설치
    void TickPlaceTool()
    {
        if (placementData == null || placementData.prefab == null) return;

        EnsurePlacePreviewObjects();

        if (keyConfig != null && keyConfig.GetKeyDown(keyConfig.placeCancel))
        {
            ClearPlacePreviewObjects();
            BuildToolManager.Instance?.SetTool(BuildTool.Select);
            return;
        }

        HandlePlaceFlipStep();
        HandlePlaceStrengthStep();

        if (!MouseUtil.TryGetMouseWorld(Camera.main, out var mouseWorld3))
            return;

        // ✅ UI를 누른 프레임이면 "배치 입력" 자체를 무시
        if (Input.GetMouseButtonDown(0) && IsPointerOverUI())
            return;


        Vector2 mouseWorld = (Vector2)mouseWorld3;
        Vector2 snappedPos = SnapToGrid(mouseWorld);

        previewPO.transform.position = snappedPos;
        previewPO.transform.rotation = Quaternion.Euler(0, 0, placeRotZ);
        previewPO.transform.localScale = ApplyFlipX(basePreviewScale, isFlipX);

        // ✅ 한 번만
        Physics2D.SyncTransforms();

        // ====== Place canPlace 캐시 ======
        int prefabId = placementData.prefab.GetInstanceID();
        var cell = grid.WorldToCell(snappedPos);
        int rotZ10 = Mathf.RoundToInt(placeRotZ * 10f);
        bool flip = isFlipX;

        var key = new PlaceCacheKey
        {
            worldRev = _worldRev,
            prefabId = prefabId,
            cell = cell,
            rotZ10 = rotZ10,
            flipX = flip,
        };

        bool canPlace;
        if (!_placeCanCache.TryGetValue(key, out canPlace))
        {
            canPlace = IsInsidePlacementFrame(previewPO, snappedPos);

            if (canPlace)
                canPlace = previewPO.CanPlaceByRuleAtWorld(snappedPos, grid: grid);

            if (canPlace)
            {
                bool railOverlap = HasRailOverManualCells_CellMap(previewPO, snappedPos);
                canPlace &= !railOverlap;
            }

            // ✅ 추가: 앵커 기준 PO 금지 셀과 겹치면 설치 불가
            if (canPlace && previewPO != null && previewPO.UseManualOccupancy)
            {
                _tmpCells.Clear();
                previewPO.GetManualOccupiedCellsAtWorld(grid, snappedPos, _tmpCells);

                var occ = GridOccupancy2D.Instance;
                if (occ != null && occ.WouldOverlapPOBlockedCells(_tmpCells))
                    canPlace = false;
            }

            _placeCanCache[key] = canPlace;
        }

        ghost.transform.position = snappedPos;
        ghost.transform.rotation = previewPO.transform.rotation;
        ghost.transform.localScale = ApplyFlipX(baseGhostScale, isFlipX);

        if (Input.GetMouseButtonDown(0) && canPlace)
            ApplyPlace();
    }

    void HandlePlaceStrengthStep()
    {
        if (placementData == null || !placementData.allowStrengthControl)
            return;

        int delta = 0;

        if (keyConfig != null && keyConfig.GetKeyDown(keyConfig.placeStrengthUp))
            delta = +1;

        if (keyConfig != null && keyConfig.GetKeyDown(keyConfig.placeStrengthDown))
            delta = -1;

        if (delta == 0)
            return;

        var comp = previewPO != null
            ? previewPO.GetComponent<StrengthBasedOccupancyCells>()
            : null;

        if (comp == null)
            return;

        int target = Mathf.Clamp(placeStrengthLevel + delta, comp.MinLevel, comp.MaxLevel);

        if (target == placeStrengthLevel)
            return;

        placeStrengthLevel = target;

        comp.SetLevel(placeStrengthLevel);

        var ghostComp = ghost != null
            ? ghost.GetComponent<StrengthBasedOccupancyCells>()
            : null;

        if (ghostComp != null)
            ghostComp.SetLevel(placeStrengthLevel);

        Physics2D.SyncTransforms();

        _placeCanCache.Clear(); // 강도별 점유칸 달라지면 필요
    }


    // [PLACE] HandlePlaceRotationStep
    void HandlePlaceRotationStep()

    {

        if (placementData != null && !placementData.allowRotate)
            return;
        float delta = 0f;
        if (Mathf.Approximately(delta, 0f)) return;
        placeRotZ = Mathf.Repeat(placeRotZ + delta, 360f);
        if (previewPO != null) previewPO.transform.rotation = Quaternion.Euler(0, 0, placeRotZ);
        if (ghost != null) ghost.transform.rotation = Quaternion.Euler(0, 0, placeRotZ);

    }

    // [PLACE] HandlePlaceFlipStep
    void HandlePlaceFlipStep()
    {
        if (keyConfig == null || !keyConfig.GetKeyDown(keyConfig.placeFlipX))
            return;

        if (placementData != null && !placementData.allowFlipX)
            return;

        isFlipX = !isFlipX;
        ApplyPlaceFlipToPreview();

        _placeCanCache.Clear(); // 있으면 추천
    }

    int placeStrengthLevel = 1;



    // [PLACE] ApplyPlaceFlipToPreview
    void ApplyPlaceFlipToPreview()

    {

        if (previewPO != null)
            previewPO.transform.localScale = ApplyFlipX(basePreviewScale, isFlipX);
        if (ghost != null)
            ghost.transform.localScale = ApplyFlipX(baseGhostScale, isFlipX);

    }

    // [UTIL] ApplyFlipX
    static Vector3 ApplyFlipX(Vector3 baseScale, bool flipX)

    {

        float sx = Mathf.Abs(baseScale.x) * (flipX ? -1f : 1f);
        return new Vector3(sx, baseScale.y, baseScale.z);

    }

    // [PLACE] ApplyPlace - 현재 고스트 상태를 실제 오브젝트로 설치(커밋)
    void ApplyPlace()
    {
        Vector3 finalPos = previewPO.transform.position;
        Quaternion finalRot = previewPO.transform.rotation;

        // ✅ 마지막 설치 직전 재검사
        bool canPlace = IsInsidePlacementFrame(previewPO, finalPos);

        if (canPlace)
            canPlace = previewPO.CanPlaceByRuleAtWorld((Vector2)finalPos, grid: grid);

        if (canPlace)
        {
            bool railOverlap = HasRailOverManualCells_CellMap(previewPO, finalPos);
            canPlace &= !railOverlap;
        }

        if (canPlace && previewPO != null && previewPO.UseManualOccupancy)
        {
            _tmpCells.Clear();
            previewPO.GetManualOccupiedCellsAtWorld(grid, finalPos, _tmpCells);

            var occ = GridOccupancy2D.Instance;
            if (occ != null && occ.WouldOverlapPOBlockedCells(_tmpCells))
                canPlace = false;
        }

        if (!canPlace)
            return;

        var obj = Instantiate(placementData.prefab, finalPos, finalRot);
        obj.transform.localScale = previewPO.transform.localScale;

        var po = obj.GetComponent<PlacementObject>();
        if (po != null)
        {
            po.placementData = placementData;
            ApplyPlacementDefaultsToPO(po);

            var strength = po.GetComponent<StrengthBasedOccupancyCells>();
            if (strength != null)
                strength.SetLevel(placeStrengthLevel);
        }

        if (po != null)
        {
            po.SetPlaced();
            Physics2D.SyncTransforms();
            EnsureRailGraphUpToDate();
            po.AutoRailAttach = true;
            FinalizeRailBindingNow(po);
            UISoundManager.I?.PlayPOPlace();
        }

        SetSelectedPO(po, applyVisual: true);

        MarkOccupancyDirty();
        NotifyStageChanged();

        if (_stageSaveManager == null)
            _stageSaveManager = FindFirstObjectByType<StageSaveManager>();

        if (_stageSaveManager != null)
            _stageSaveManager.ForceSaveNow(_stageSaveManager.GetCurrentStageIdForUndo());

        if (!allowContinuousPlace)
        {
            BuildToolManager.Instance?.SetTool(BuildTool.Select);
        }
    }

    // =========================================================

    #endregion

    #region SELECT
    // SELECT

    // =========================================================

    // [SELECT/DRAG] TickSelectTool - 선택 모드: 선택/삭제/회전/드래그 처리
    void TickSelectTool()
    {
        // ✅ 선택된 PO가 FixedRoot면 "잠금 상태"만 갱신 (선택은 유지)
        if (selected != null)
            IsSelectedLocked = IsSelectedFixed(); // 또는 IsUnderFixedRoot(selected)

        if (HandleDeleteSelected())
            return;

        HandleSelection();
        HandleDragMove();
    }

    // [SELECT/DRAG] HandleDeleteSelected
    bool HandleDeleteSelected()

    {

        if (selected == null) return false;


        // ✅ Fixed Root 아래 PO는 삭제 금지
        if (IsSelectedFixed()) return false;
        // 드래그 중이면 먼저 드래그 상태/스냅 백업을 정리하고 삭제로 진행

        return false;

    }

    // [SELECT/DRAG] TryDeleteSelectedObject
    void TryDeleteSelectedObject()

    {

        if (selected == null) return;
        // ✅ Fixed Root 아래 PO는 삭제 금지(이중 안전장치)
        if (IsSelectedFixed()) return;
        ResetDragState();
        DetachRailFollowIfAnyAndRefreshRails(selected);
        InvalidateRailCaches();            // ✅ Detach 직후 1번
        var go = selected.gameObject;
        SetSelectedPO(null, applyVisual: false);
        // ✅ 일반 삭제음
        UISoundManager.I?.PlayPODelete();


        go.SetActive(false);
        Destroy(go);

        CleanupDeadSnapConnectionsScene();
        InvalidateRailCaches();            // ✅ Destroy 직후 1번(같은 프레임 안전)
        MarkOccupancyDirty();
        RailGraphCleanup2D.Cleanup();
        RailBudget2D.Instance?.SyncUsedWithScene();
        NotifyStageChanged();

    }

    public void DeletePlacementObjectForReset(PlacementObject po)
    {
        if (po == null) return;

        // 리셋 중이면 드래그 상태 등은 안전하게 초기화
        ResetDragState();

        DetachRailFollowIfAnyAndRefreshRails(po);
        InvalidateRailCaches(); // detach 직후

        var go = po.gameObject;
        go.SetActive(false);
        Destroy(go);

        CleanupDeadSnapConnectionsScene();
        InvalidateRailCaches(); // destroy 직후

        MarkOccupancyDirty();
        RailGraphCleanup2D.Cleanup();
        // NotifyStageChanged();  // 리셋은 마지막에 1번만 호출하는 게 보통 더 좋음

        RailBudget2D.Instance?.SyncUsedWithScene();
    }


    // [SELECT/DRAG] HandleSelection
    void HandleSelection()
    {
        if (Input.GetMouseButtonDown(0) && IsPointerOverUI())
            return;

        if (RailToolPlacer2D.IsInputBusyNow)
        {
            ResetDragState();
            SetDragPhase(SelectedDragPhase.None);
            ClearSelection(forceFinalize: false);
            return;
        }

        if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
        {
            if (selected != null && (isDragging || (dragCandidate && pointerDownOnSelected && Input.GetMouseButton(0))))
            {
                CancelDragRollback();
                SetDragPhase(SelectedDragPhase.None);
                ClearBlockedRailPreview();
                return;
            }

            ResetDragState();
            SetDragPhase(SelectedDragPhase.None);
            ClearSelection(forceFinalize: false);
            ClearBlockedRailPreview();
            return;
        }

        if (!Input.GetMouseButtonDown(0)) return;
        if (!MouseUtil.TryGetMouseWorld(Camera.main, out var mouseWorld3)) return;

        Vector2 mouse = (Vector2)mouseWorld3;

        if (RailToolPlacer2D.HasPriorityAtPointer(mouse))
        {
            if (selected != null && !isDragging)
            {
                ResetDragState();
                SetDragPhase(SelectedDragPhase.None);
                ClearSelection(forceFinalize: false);
                ClearBlockedRailPreview();
            }

            pointerDownOnSelected = false;
            dragCandidate = false;
            return;
        }

        Physics2D.SyncTransforms();

        Camera cam = Camera.main;
        if (cam == null) return;

        float worldPerPixel = (cam.orthographicSize * 2f) / Screen.height;
        float radiusWorld = worldPerPixel * poPickRadiusPx;

        int hitCount = Physics2D.OverlapCircleNonAlloc(
            mouse,
            radiusWorld,
            pointHits
        );

        bool hasRailHandle = false;
        bool hasRailBody = false;

        PlacementObject poHit = null;
        float bestDist = float.PositiveInfinity;

        for (int i = 0; i < hitCount; i++)
        {
            var c = pointHits[i];
            if (c == null) continue;

            if (c.GetComponentInParent<RailEndpointHandle2D>() != null)
            {
                hasRailHandle = true;
                break;
            }

            if (c.GetComponentInParent<RailSpan2D>() != null)
            {
                hasRailBody = true;
                break;
            }

            if (((1 << c.gameObject.layer) & placedMask.value) != 0)
            {
                var po = c.GetComponentInParent<PlacementObject>();
                if (po == null) continue;

                if (!po.Selectable)
                    continue;

                float d = ((Vector2)po.transform.position - mouse).sqrMagnitude;
                if (d < bestDist)
                {
                    bestDist = d;
                    poHit = po;
                }
            }
        }

        if (hasRailHandle || hasRailBody)
        {
            // ✅ 레일이 실제로 잡히면 이번 클릭은 레일에 양보
            pointerDownOnSelected = false;
            dragCandidate = false;
            SetDragPhase(SelectedDragPhase.None);
            ClearBlockedRailPreview();
            return;
        }

        if (poHit != null)
        {
            bool locked = IsUnderFixedRoot(poHit);

            SetSelectedPO(poHit, applyVisual: true, locked: locked);

            // ✅ PO를 선택한 순간, 기존 레일 선택은 해제
            RailToolPlacer2D.ClearSelectedRail();

            if (locked)
            {
                pointerDownOnSelected = false;
                dragCandidate = false;
                SetDragPhase(SelectedDragPhase.None);
                return;
            }

            dragCandidate = true;
            pointerDownOnSelected = true;
            pointerDownWorld = mouse;

            isDragging = false;
            SetDragPhase(SelectedDragPhase.Candidate);
            dragStartPos = selected.transform.position;
            dragStartRot = selected.transform.rotation;

            hasGrabOffset = false;
            dragGrabOffset = Vector2.zero;

            dragPrevPartners.Clear();
            return;
        }

        ResetDragState();
        SetDragPhase(SelectedDragPhase.None);
        ClearSelection(forceFinalize: false);

        // 선택 대상이 아무것도 없을 때는 레일 선택도 같이 해제하고 싶으면 이 줄 추가
        // RailToolPlacer2D.ClearSelectedRail();
    }
    bool TryTransformSelected(
    Vector3 testPos,
    Quaternion testRot,
    Vector3 testScale,
    out RailSpan2D blockedRail
)
    {
        blockedRail = null;
        if (selected == null) return false;

        // 1. 상태 백업
        Vector3 beforePos = selected.transform.position;
        Quaternion beforeRot = selected.transform.rotation;
        Vector3 beforeScale = selected.transform.localScale;

        // 2. 임시 적용
        selected.transform.position = testPos;
        selected.transform.rotation = testRot;
        selected.transform.localScale = testScale;

        Physics2D.SyncTransforms();

        bool canPlace = IsInsidePlacementFrame(selected, testPos);

        if (canPlace)
            canPlace = selected.CanPlaceByRuleAtWorld((Vector2)testPos, grid: grid);

        if (canPlace)
        {
            bool railOverlap = HasRailOverManualCells_CellMap(selected, testPos);
            canPlace &= !railOverlap;
        }

        // 4. 레일 이동 가능 여부
        bool railsOk = CanMoveAttachedRails(selected, out blockedRail);

        // 5. 실패 → 롤백
        if (!canPlace || !railsOk)
        {
            selected.transform.position = beforePos;
            selected.transform.rotation = beforeRot;
            selected.transform.localScale = beforeScale;
            Physics2D.SyncTransforms();
            return false;
        }

        // 성공 → 유지
        return true;
    }

    bool CanTransformSelected()
    {
        if (!CanTransformSelectedBase()) return false;

        // 실제 실행 정책: 레일 바운드면 회전/플립 금지
        if (IsRailBound(selected))
            return false;

        return true;
    }

    bool CanTransformSelectedBase()
    {
        if (selected == null) return false;
        if (StageSaveManager.IsRestoringNow) return false;
        if (StageSaveManager.IsRestoreStabilizingNow) return false;
        return true;
    }

    bool CheckSelectedTransformPossible(
        Vector3 testPos,
        Quaternion testRot,
        Vector3 testScale,
        out RailSpan2D blockedRail)
    {
        blockedRail = null;
        if (selected == null) return false;

        Vector3 beforePos = selected.transform.position;
        Quaternion beforeRot = selected.transform.rotation;
        Vector3 beforeScale = selected.transform.localScale;

        selected.transform.position = testPos;
        selected.transform.rotation = testRot;
        selected.transform.localScale = testScale;

        Physics2D.SyncTransforms();

        bool canPlace = IsInsidePlacementFrame(selected, testPos);

        if (canPlace)
            canPlace = selected.CanPlaceByRuleAtWorld((Vector2)testPos, grid: grid);

        if (canPlace)
        {
            bool railOverlap = HasRailOverManualCells_CellMap(selected, testPos);
            canPlace &= !railOverlap;
        }

        // ✅ 이거 추가
        if (canPlace && selected.UseManualOccupancy)
        {
            _tmpCells.Clear();
            selected.GetManualOccupiedCellsAtWorld(grid, testPos, _tmpCells);

            var occ = GridOccupancy2D.Instance;
            if (occ != null && occ.WouldOverlapPOBlockedCells(_tmpCells))
                canPlace = false;
        }

        bool railsOk = CanMoveAttachedRails(selected, out blockedRail);

        // ✅ 항상 롤백 (UI 미리보기용 검사이기 때문)
        selected.transform.position = beforePos;
        selected.transform.rotation = beforeRot;
        selected.transform.localScale = beforeScale;
        Physics2D.SyncTransforms();

        return canPlace && railsOk;
    }

    public bool CanRotateSelectedNow(float deltaDegrees)
    {
        if (!CanTransformSelectedBase())
        {
            return false;
        }

        if (selected.placementData != null && !selected.placementData.allowRotate)
        {
            return false;
        }

        if (IsRailBound(selected))
            return false;

        Quaternion testRot =
            Quaternion.Euler(0, 0, Mathf.Repeat(selected.transform.eulerAngles.z + deltaDegrees, 360f));

        return CheckSelectedTransformPossible(
            selected.transform.position,
            testRot,
            selected.transform.localScale,
            out _
        );
    }

    public bool CanFlipSelectedXNow()
    {
        if (!CanTransformSelectedBase())
        {
            return false;
        }

        if (selected.placementData != null && !selected.placementData.allowFlipX)
        {
            return false;
        }

        if (IsRailBound(selected))
            return false;

        Vector3 testScale = selected.transform.localScale;
        testScale.x *= -1f;

        return CheckSelectedTransformPossible(
            selected.transform.position,
            selected.transform.rotation,
            testScale,
            out _
        );
    }

    public bool CanFlipSelectedYNow()
    {
        if (!CanTransformSelectedBase())
        {
            return false;
        }

        if (selected.placementData != null && !selected.placementData.allowFlipY)
        {
            return false;
        }

        if (IsRailBound(selected))
            return false;

        Vector3 testScale = selected.transform.localScale;
        testScale.y *= -1f;

        return CheckSelectedTransformPossible(
            selected.transform.position,
            selected.transform.rotation,
            testScale,
            out _
        );
    }


    // =========================
    // [SELECT ACTION HELPERS] 공용(키보드/버튼) 회전/플립
    // =========================
    void RotateSelectedBy(float deltaDegrees)
    {
        if (selected == null) return;
        // ✅ PlacementData에서 회전 허용 여부 반영
        if (selected.placementData != null && !selected.placementData.allowRotate)
            return;
        if (!CanTransformSelected()) return;


        Quaternion testRot =
            Quaternion.Euler(0, 0, Mathf.Repeat(selected.transform.eulerAngles.z + deltaDegrees, 360f));

        bool ok = TryTransformSelected(
            selected.transform.position,
            testRot,
            selected.transform.localScale,
            out _
        );

        if (ok)
        {
            MarkOccupancyDirty();
            FinalizeRailBindingNow(selected);
            NotifyStageChanged();
        }
    }

    void FlipSelectedX()
    {
        if (selected == null) return;
        if (!CanTransformSelected()) return;
        if (selected.placementData != null && !selected.placementData.allowFlipX) return;


        Vector3 testScale = selected.transform.localScale;
        testScale.x *= -1f;
        bool ok = TryTransformSelected(
            selected.transform.position,
            selected.transform.rotation,
            testScale,
            out _
        );

        if (ok)
        {
            MarkOccupancyDirty();
            FinalizeRailBindingNow(selected);
            NotifyStageChanged();
        }
    }

    void FlipSelectedY()
    {
        if (selected == null) return;
        if (!CanTransformSelected()) return;
        if (selected.placementData != null && !selected.placementData.allowFlipY) return;

        Vector3 testScale = selected.transform.localScale;
        testScale.y *= -1f;

        bool ok = TryTransformSelected(
            selected.transform.position,
            selected.transform.rotation,
            testScale,
            out _
        );


        if (ok)
        {
            MarkOccupancyDirty();
            FinalizeRailBindingNow(selected);
            NotifyStageChanged();
        }
    }
    // [SELECT/DRAG] HandleSelectRotationStep
    void HandleSelectRotationStep()
    {
        if (!enableSelectRotate) return;
        if (selected == null) return;
        // ✅ PlacementData에서 회전 허용 여부 반영
        if (selected.placementData != null && !selected.placementData.allowRotate)
            return;
        if (!CanTransformSelected()) return;

        float delta = 0f;
        if (Mathf.Approximately(delta, 0f)) return;

        RotateSelectedBy(delta);
    }


    // [SELECT/DRAG] HandleSelectFlip
    void HandleSelectFlip()
    {
        if (selected == null) return;
        if (!CanTransformSelected()) return;
        if (!Input.GetKeyDown(KeyCode.F)) return;
        FlipSelectedX();
    }


    #endregion

    #region DRAG MOVE
    // DRAG MOVE (스냅 복구 포함 마무리)

    // =========================================================

    // [SELECT/DRAG] HandleDragMove - 드래그 이동 처리(스냅 백업/복구 포함)
    // GridPlacer 필드에 추가 권장
    // OccupancyHintOverlay2D _occOverlay;  // Awake에서 FindFirstObjectByType로 1회 캐시

    void HandleDragMove()
    {
        if (selected == null) return;
        if (IsSelectedFixed()) return;
        if (!dragCandidate) return;
        if (!MouseUtil.TryGetMouseWorld(Camera.main, out var mouseWorld3)) return;

        Vector2 mouse = (Vector2)mouseWorld3;

        // =========================
        // DRAGGING (Mouse Hold)
        // =========================
        if (Input.GetMouseButton(0))
        {
            // Cancel during drag (우클릭/ESC)
            if (isDragging && (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape)))
            {
                CancelDragRollback();
                return;
            }

            if (!pointerDownOnSelected) return;

            // Drag Begin (threshold)
            if (!isDragging)
            {
                float moved = Vector2.Distance(mouse, pointerDownWorld);
                if (moved < dragStartDistance) return;
                BeginDrag(mouse);
            }

            // Drag Update
            UpdateDrag(mouse);
            return;
        }

        // =========================
        // DRAG END (Mouse Up)
        // =========================
        if (Input.GetMouseButtonUp(0))
        {
            if (!isDragging)
            {
                pointerDownOnSelected = false;
                return;
            }

            EndDrag(mouse);
            return;
        }
    }

    bool TryClampToRailHint(ref Vector2 gridPos)
    {
        if (!showRailHintsWhileMovingPO) return true;
        if (poMoveRailHint == null) return true;
        if (!HasMovableRailBinding(selected)) return true;

        if (poMoveRailHint.TryClampToHint(gridPos, out var clamped))
        {
            gridPos = clamped;
            _dragLastAllowedPos = gridPos;
            _hasDragLastAllowedPos = true;
            return true;
        }

        if (_hasDragLastAllowedPos)
        {
            gridPos = _dragLastAllowedPos;
            return true;
        }

        return false;
    }

    void ApplyPosAndSync(Vector2 pos, bool ensureRailGraph)
    {
        selected.transform.position = pos;
        Physics2D.SyncTransforms();
        if (ensureRailGraph) EnsureRailGraphUpToDate();
    }

    void CancelDragRollback()
    {
        selected.transform.position = dragStartPos;
        selected.transform.rotation = dragStartRot;
        Physics2D.SyncTransforms();

        var bind = selected.GetComponent<RailNodeFollowBinding2D>();
        _dragBind = bind;

        if (bind != null && _hasDragFollowBackup)
            bind.RestoreSnapshot(_dragFollowBackup);

        _occOverlay?.ClearHideOwnerId();

        bind?.SyncNow(syncPhysics: true, broadcastMoved: true);
        RefreshRailsBoundTo(selected);

        SetRuntimeFollowEnabledForPO(selected, false);
        SyncBoundNodesNow(selected, true);

        InvalidateRailCaches();
        selected.SetPlaced();
        MarkOccupancyDirty();

        if (_stageSaveManager == null)
            _stageSaveManager = FindFirstObjectByType<StageSaveManager>();

        if (_stageSaveManager != null && _poDragUndoBeginNotified)
        {
            _stageSaveManager.NotifyStageChangeBeginCanceled();
            _stageSaveManager.EndDeferredStageChanged(false); // 추가
        }

        NotifyDragEnded(selected, false);
        SetDragPhase(SelectedDragPhase.None);
        ResetDragState();
        _poDragUndoBeginNotified = false;
    }

    void BeginDrag(Vector2 mouse)
    {
        if (_stageSaveManager == null)
            _stageSaveManager = FindFirstObjectByType<StageSaveManager>();

        // ✅ 먼저 데모 코루틴부터 끊기
        var demoLinks = selected.GetComponentsInChildren<PoDemoLink>(true);
        for (int i = 0; i < demoLinks.Length; i++)
        {
            if (demoLinks[i] != null)
                demoLinks[i].StopDemoAndReset();
        }


        isDragging = true;

        SetDragPhase(SelectedDragPhase.Dragging);

        // ✅ 여기서 각 PO 컨트롤러의 BeginDragState()가 호출되며
        //    PoMove 기반 오브젝트는 즉시 초기 상태로 돌아간다.
        NotifyDragStarted(selected);

        // ✅ 중요:
        // BeginDragState() 이후의 "초기화된 현재 상태"를
        // 드래그 롤백 기준점으로 다시 저장해야 함
        dragStartPos = selected.transform.position;
        dragStartRot = selected.transform.rotation;
        Physics2D.SyncTransforms();

        _occOverlay?.SetHideOwnerId(selected.GetInstanceID());

        _hasDragLastAllowedPos = true;
        _dragLastAllowedPos = SnapToGrid((Vector2)selected.transform.position);

        _dragFrameCounter = 0;
        _dragCacheValid = false;
        _dragCachedCanPlaceBase = true;

        EnsureRailGraphUpToDate();
        PrepareRailFollowForDrag(selected);

        // ✅ Prepare 이후 최종 바인딩 상태 다시 획득
        _dragBind = selected.GetComponent<RailNodeFollowBinding2D>();

        SetRuntimeFollowEnabledForPO(selected, true);

        // ✅ cancel rollback용 백업도 "실제 드래그 시작 직전 상태"로 저장
        if (_dragBind != null)
        {
            _dragFollowBackup = _dragBind.CreateSnapshot();
            _hasDragFollowBackup = true;
        }
        else
        {
            _hasDragFollowBackup = false;
            _dragFollowBackup = default;
        }

        if (_stageSaveManager != null && !_poDragUndoBeginNotified)
        {
            _stageSaveManager.NotifyStageChangeBegin();
            _stageSaveManager.BeginDeferredStageChanged();
            _poDragUndoBeginNotified = true;
        }

        // ✅ 초기화된 현재 위치 기준으로 grab offset 계산
        dragGrabOffset = (Vector2)selected.transform.position - mouse;
        hasGrabOffset = true;

        dragPrevPartners.Clear();

        BumpOcc();

        var occ = GridOccupancy2D.Instance;
        if (occ != null)
        {
            occ.SetTempIgnoreOwner(selected.GetInstanceID());
            _occIgnoreActive = true;
            _occIgnoreOwnerId = selected.GetInstanceID();
        }

        SetRuntimeFollowEnabledForPO(selected, true);

        _dragExcludedRails.Clear();
        CollectBoundRailsForDrag(selected, _dragBind, _dragExcludedRails);

        {
            Debug.Log($"[PO Drag] selected='{selected?.name}' id={selected?.GetInstanceID()} " +
              $"bindEntries={_dragBind?.Entries?.Count ?? 0} boundRails={_dragExcludedRails.Count}");

            for (int i = 0; i < _dragExcludedRails.Count; i++)
            {
                var r = _dragExcludedRails[i];
                if (r == null)
                {
                    Debug.Log($"  - rail[{i}] = <null>");
                    continue;
                }

                Debug.Log(
                    $"  - rail[{i}] '{r.name}' id={r.GetInstanceID()} " +
                    $"startNode={(r.startNode ? r.startNode.name : "null")} " +
                    $"endNode={(r.endNode ? r.endNode.name : "null")} " +
                    $"active={r.gameObject.activeInHierarchy} enabled={r.enabled}"
                );
            }
        }

        RailCellMap2D.Instance?.BeginExcludeRailsForDrag(_dragExcludedRails);

        if (showRailHintsWhileMovingPO && HasMovableRailBinding(selected))
            poMoveRailHint?.Begin(selected);

        RailCellMap2D.Instance?.SuspendUpdates();
    }

    void UpdateDrag(Vector2 mouse)
    {
        _dragFrameCounter++;

        Vector2 desired = hasGrabOffset ? (mouse + dragGrabOffset) : mouse;
        Vector2 gridPos = SnapToGrid(desired);

        if (!TryClampToRailHint(ref gridPos))
            return;

        bool cellChanged = !_dragCacheValid || (gridPos != _dragCachedGridPos);

        if (cellChanged)
        {
            _dragCachedGridPos = gridPos;

            ApplyPosAndSync(gridPos, ensureRailGraph: true);

            _dragCachedTargetPos = gridPos;

            // ✅ 앵커 시작 레일까지 실시간 반영되도록 broadcast 켬
            _dragBind?.SyncNow(syncPhysics: true, broadcastMoved: true);
            RefreshRailsBoundTo(selected);
            Physics2D.SyncTransforms();

            // ✅ 핵심 판정(occupancy + ignorePrevPartners)
            _dragCachedCanPlaceBase = IsInsidePlacementFrame(selected, _dragCachedTargetPos);

            if (_dragCachedCanPlaceBase)
                _dragCachedCanPlaceBase = selected.CanPlaceByRuleAtWorld(_dragCachedTargetPos, grid: grid);

            if (_dragCachedCanPlaceBase)
            {
                bool railOverlap = HasRailOverManualCells_CellMap(selected, _dragCachedTargetPos);
                _dragCachedCanPlaceBase &= !railOverlap;
            }


            // ✅ 추가
            if (_dragCachedCanPlaceBase && selected != null && selected.UseManualOccupancy)
            {
                _tmpCells.Clear();
                selected.GetManualOccupiedCellsAtWorld(grid, _dragCachedTargetPos, _tmpCells);

                var occ = GridOccupancy2D.Instance;
                if (occ != null && occ.WouldOverlapPOBlockedCells(_tmpCells))
                    _dragCachedCanPlaceBase = false;
            }

            _dragCacheValid = true;
        }

        selected.transform.position = _dragCachedTargetPos;
    }

    void EndDrag(Vector2 mouse)
    {
        Vector2 desired = hasGrabOffset ? (mouse + dragGrabOffset) : mouse;
        Vector2 gridPos = SnapToGrid(desired);

        if (!TryClampToRailHint(ref gridPos))
            return;

        // 1) 그리드로 확정 (여기 1회만)
        ApplyPosAndSync(gridPos, ensureRailGraph: true);

        Vector2 finalPos = gridPos;

        // ✅ 드래그 종료 순간, 레일 노드를 최종 PO 위치로 즉시 따라오게 함
        SyncBoundNodesNow(selected, true);

        bool canPlaceFinal = IsInsidePlacementFrame(selected, finalPos);

        if (canPlaceFinal)
            canPlaceFinal = selected.CanPlaceByRuleAtWorld(finalPos, grid: grid);

        bool railsOkFinal = CanMoveAttachedRailsAtPos_Virtual(selected, finalPos, out var blockedRailFinal);
        canPlaceFinal &= railsOkFinal;

        if (canPlaceFinal)
        {
            bool railOverlapFinal = HasRailOverManualCells_CellMap(selected, finalPos);
            canPlaceFinal &= !railOverlapFinal;
        }

        // ✅ 추가
        if (canPlaceFinal && selected != null && selected.UseManualOccupancy)
        {
            _tmpCells.Clear();
            selected.GetManualOccupiedCellsAtWorld(grid, finalPos, _tmpCells);

            var occ = GridOccupancy2D.Instance;
            if (occ != null && occ.WouldOverlapPOBlockedCells(_tmpCells))
                canPlaceFinal = false;
        }

        SetBlockedRailPreview(blockedRailFinal, blocked: !railsOkFinal);

        bool changed = false;

        if (!canPlaceFinal)
        {
            selected.transform.position = dragStartPos;
            selected.transform.rotation = dragStartRot;
            Physics2D.SyncTransforms();

            if (_dragBind != null && _hasDragFollowBackup)
                _dragBind.RestoreSnapshot(_dragFollowBackup);

            _dragBind?.SyncNow(syncPhysics: true, broadcastMoved: true);
            RefreshRailsBoundTo(selected);

            MarkOccupancyDirty();
            GridOccupancy2D.Instance?.EnsureBaked();

            if (_stageSaveManager == null)
                _stageSaveManager = FindFirstObjectByType<StageSaveManager>();

            if (_stageSaveManager != null && _poDragUndoBeginNotified)
                _stageSaveManager.NotifyStageChangeBeginCanceled();
        }
        else
        {
            changed = true;

            MarkOccupancyDirty();
            GridOccupancy2D.Instance?.EnsureBaked();

            selected.AutoRailAttach = true;

            var bind = _dragBind != null ? _dragBind : selected.GetComponent<RailNodeFollowBinding2D>();

            // ✅ 기존 바인딩이 있어도, 드래그 종료 시 새 SnapPoint 후보를 다시 스캔
            if (selected.AutoRailAttach)
            {
                EnsureRailGraphUpToDate();

                var rails = GetAllRailsCached();

                RailNodeSnapBinder.TryAttachAllNearestNodesBySnapPoints(
                    selected,
                    railFollowRadius,
                    railNodeMask,
                    rails,
                    railFollowDebug
                );

                bind = selected.GetComponent<RailNodeFollowBinding2D>();
            }

            if (bind != null && bind.CleanupInvalidEntriesAndHasAnyBound())
            {
                bind.SyncNow(syncPhysics: true, broadcastMoved: true);
                RefreshRailsBoundTo(selected);
            }
            else
            {
                FinalizeRailBindingNow(selected);
            }

            Physics2D.SyncTransforms();

            // 드래그 중 제외했던 레일까지 최종 위치로 한번 더 강제 refresh
            for (int i = 0; i < _dragExcludedRails.Count; i++)
            {
                var r = _dragExcludedRails[i];
                if (r == null) continue;
                r.Refresh(syncFromNodes: true);
            }

            RefreshRailsBoundTo(selected);
            Physics2D.SyncTransforms();
        }

        _occOverlay?.ClearHideOwnerId();

        var committedPo = selected;

        committedPo.SetPlaced();

        ClearBlockedRailPreview();

        NotifyDragEnded(committedPo, canPlaceFinal);
        SetDragPhase(SelectedDragPhase.None);

        bool hadUndoBegin = _poDragUndoBeginNotified;

        if (_stageSaveManager == null)
            _stageSaveManager = FindFirstObjectByType<StageSaveManager>();

        // ✅ 성공했을 때 최종 정리
        if (changed)
        {
            var committedBind = _dragBind != null
                ? _dragBind
                : committedPo.GetComponent<RailNodeFollowBinding2D>();

            if (committedBind != null && committedBind.Entries != null && committedBind.Entries.Count > 0)
            {
                // ✅ 저장/Undo 커밋 직전 마지막으로 한 번 더 레일을 PO 위치에 강제 동기화
                committedBind.SyncNow(syncPhysics: true, broadcastMoved: true);
                RefreshRailsBoundTo(committedPo);
                Physics2D.SyncTransforms();

                // ✅ 동기화된 현재 상태 기준으로 localOffset 베이크
                committedBind.BakeLocalOffsetsFromCurrent();

                RefreshRailsBoundTo(committedPo);
                InvalidateRailCaches();
                Physics2D.SyncTransforms();
            }
            else
            {
                // 바인딩이 없는 경우만 기존 finalize 경로
                FinalizeRuntimeRailBinding(committedPo);
                Physics2D.SyncTransforms();
            }
        }
        if (_stageSaveManager == null)
            _stageSaveManager = FindFirstObjectByType<StageSaveManager>();

        if (hadUndoBegin)
        {
            if (changed)
            {
                // ✅ 드래그 성공: Undo 커밋을 확실히 발생시킨다.
                _stageSaveManager?.NotifyStageChanged();
                _stageSaveManager?.EndDeferredStageChanged(true);

                // ✅ 커밋 후 저장
                _stageSaveManager?.ForceSaveNow(_stageSaveManager.GetCurrentStageIdForUndo());
            }
            else
            {
                // ✅ 드래그 실패/원위치/불가 배치: begin 취소
                _stageSaveManager?.NotifyStageChangeBeginCanceled();
                _stageSaveManager?.EndDeferredStageChanged(false);
            }
        }
        else
        {
            if (changed)
            {
                // ✅ 혹시 begin 없이 여기까지 왔더라도 최소한 일반 변경으로 커밋
                NotifyStageChanged();
                _stageSaveManager?.ForceSaveNow(_stageSaveManager.GetCurrentStageIdForUndo());
            }
        }

        // ✅ 다음 드래그를 위해 반드시 리셋
        _poDragUndoBeginNotified = false;

        ResetDragState();
    }

    void ResetDragState()
    {
        // ✅ 추가: OCC ignore 강제 해제 (드래그가 어떤 루트로 끝나도 안전)
        if (_occIgnoreActive)
        {
            var occ = GridOccupancy2D.Instance;
            if (occ != null) occ.ClearTempIgnoreOwner();
            _occIgnoreActive = false;
            _occIgnoreOwnerId = 0;
        }

        dragCandidate = false;
        pointerDownOnSelected = false;
        isDragging = false;

        _hasDragFollowBackup = false;
        _dragFollowBackup = default;
        _dragBind = null;

        hasGrabOffset = false;
        dragGrabOffset = Vector2.zero;

        dragPrevPartners.Clear();

        ClearBlockedRailPreview();

        if (showRailHintsWhileMovingPO) poMoveRailHint?.End();

        ClearRailFollowDragPrepared();

        // ✅ 드래그 동안 제외했던 레일 셀맵 복구(최종 위치로 1회 재굽기)
        if (_dragExcludedRails.Count > 0)
        {
            RailCellMap2D.Instance?.EndExcludeRailsForDrag(_dragExcludedRails);
            _dragExcludedRails.Clear();
            _dragExcludedRailsSet.Clear(); // (Set 쓰는 경우에만. 지금 필드에 있으니 같이 정리)
        }

        RailCellMap2D.Instance?.ResumeUpdates(); // ✅ 드래그 끝나면 다시 켬

        SetDragPhase(SelectedDragPhase.None);

        SetRuntimeFollowEnabledForPO(selected, false);

    }


    // =========================================================

    #endregion

    #region Prev partner helpers
    // Prev partner helpers

    // =========================================================


    // [SELECT/DRAG] ClearSelection
    void ClearSelection(bool forceFinalize)

    {
        IsSelectedLocked = false;
        ResetDragState();
        if (selected != null)
        {
            if (forceFinalize) selected.SetPlaced();
            // 비주얼은 SetSelectedPO에서 처리하게 할 수도 있지만,
            // 여기선 기존 동작 유지
            SetSelectedPO(null, applyVisual: false);
        }

        ClearBlockedRailPreview(); // ✅ 추가(중복이어도 안전)

    }

    // =========================================================

    #endregion

    #region PREVIEW OBJECTS
    // PREVIEW OBJECTS

    // =========================================================

    // [PLACE] EnsurePlacePreviewObjects
    void EnsurePlacePreviewObjects()

    {

        if (ghost == null) CreateGhost();
        if (previewPO == null) CreatePreviewObject();

    }

    // [PLACE] ClearPlacePreviewObjects
    public void ClearPlacePreviewObjects()

    {

        var g = ghost;
        var p = previewPO ? previewPO.gameObject : null;
        ghost = null;
        previewPO = null;
        placeSnapTarget = null;
        placeHasSnap = false;
        placeSnap = default;
        baseGhostScale = Vector3.one;
        basePreviewScale = Vector3.one;
        if (g) Destroy(g);
        if (p) Destroy(p);

    }


    // [PLACE] Preview/ghost에서는 Rigidbody를 잠깐 멈춤 (떨어지는 문제 방지)
    static void SetPreviewRigidbodiesSimulated(GameObject go, bool simulated)
    {
        if (!go) return;

        foreach (var rb2d in go.GetComponentsInChildren<Rigidbody2D>(true))
        {
            if (!rb2d) continue;

            if (!simulated && rb2d.bodyType != RigidbodyType2D.Static)
            {
                rb2d.velocity = Vector2.zero;
                rb2d.angularVelocity = 0f;
            }
            rb2d.simulated = simulated;
        }

        // 3D가 섞여있을 수도 있어서 방어
        foreach (var rb in go.GetComponentsInChildren<Rigidbody>(true))
        {
            if (!rb) continue;

            if (!simulated)
            {
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.detectCollisions = false;
                rb.isKinematic = true;
            }
            else
            {
                rb.detectCollisions = true;
                rb.isKinematic = false;
            }
        }
    }

    // [PLACE] CreateGhost
    void CreateGhost()
    {
        ghost = Instantiate(placementData.prefab);
        ghost.name = "Ghost";
        SetLayerRecursively(ghost, LayerMask.NameToLayer("Ghost"));
        Destroy(ghost.GetComponent<PlacementObject>());

        foreach (var col in ghost.GetComponentsInChildren<Collider2D>())
            col.enabled = false;

        SetPreviewRigidbodiesSimulated(ghost, simulated: false);

        baseGhostScale = ghost.transform.localScale;
        ghost.transform.localScale = ApplyFlipX(baseGhostScale, isFlipX);

        if (showSnapPointsOnGhost)
            SetSnapPointVisualsVisible(ghost, true);
    }

    void SetSnapPointVisualsVisible(GameObject root, bool visible)
    {
        if (root == null) return;

        var snapPoints = root.GetComponentsInChildren<SnapPoint>(true);
        foreach (var sp in snapPoints)
        {
            if (sp == null) continue;

            var srs = sp.GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < srs.Length; i++)
            {
                if (srs[i] != null)
                    srs[i].enabled = visible;
            }
        }
    }

    // [PLACE] CreatePreviewObject
    void CreatePreviewObject()

    {

        var go = Instantiate(placementData.prefab);
        SetLayerRecursively(go, LayerMask.NameToLayer("Ghost"));
        previewPO = go.GetComponent<PlacementObject>();
        if (previewPO != null) previewPO.placementData = placementData;

        // 룰 체크용 콜라이더는 켜야 함 (trigger 상태로)

        if (previewPO != null)
        {
            previewPO.placementData = placementData;
            ApplyPlacementDefaultsToPO(previewPO); // 추가
        }

        // 프리뷰는 안 보이게만 처리

        foreach (var sr in go.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (sr == null) continue;

            bool isSnapVisual = sr.GetComponentInParent<SnapPoint>(true) != null;
            sr.enabled = showSnapPointsOnGhost && isSnapVisual;
        }
        basePreviewScale = (previewPO != null) ? previewPO.transform.localScale : go.transform.localScale;
        if (previewPO != null)
            previewPO.transform.localScale = ApplyFlipX(basePreviewScale, isFlipX);
        else
            go.transform.localScale = ApplyFlipX(basePreviewScale, isFlipX);

    }

    // [PLACE] SetGhostAlpha
    void SetGhostAlpha(float a)

    {

        if (ghost == null) return;
        foreach (var sr in ghost.GetComponentsInChildren<SpriteRenderer>())

        {

            var c = sr.color;
            c.a = a;
            sr.color = c;

        }

    }

    // GridPlacer.cs

    // [PLACE] SetGhostTint
    void SetGhostTint(bool canPlace)
    {
        if (ghost == null) return;
    }

    // [UTIL] SetLayerRecursively
    static void SetLayerRecursively(GameObject obj, int layer)

    {

        obj.layer = layer;
        foreach (Transform t in obj.transform)
            SetLayerRecursively(t.gameObject, layer);

    }

    // =========================================================

    #endregion

    #region Small Helpers
    // Small Helpers

    // =========================================================

    // [SNAP] SnapToGrid
    Vector2 SnapToGrid(Vector2 world)

    {

        return grid.CellToWorld(grid.WorldToCell(world));

    }

    bool IsPointerOverUI()
    {
        if (EventSystem.current == null) return false;
        return EventSystem.current.IsPointerOverGameObject();
    }

    // =========================================================

    #endregion

    #region SNAP BACKUP / RESTORE
    // SNAP BACKUP / RESTORE

    // =========================================================


    // [SNAP] RestoreSnapsFromBackup - 스냅 상태 백업/복구


    // [UTIL] GetGridStep
    float GetGridStep()

    {

        if (_gridStepCached > 0f) return _gridStepCached;

        // Grid 한 칸 월드 거리 추정

        Vector2 p0 = grid.CellToWorld(Vector2Int.zero);
        Vector2 p1 = grid.CellToWorld(Vector2Int.right);
        float step = Vector2.Distance(p0, p1);
        _gridStepCached = (step > 0f) ? step : 1f;
        return _gridStepCached;

    }

    // [OCC] MarkOccupancyDirty
    void MarkOccupancyDirty()

    {
        GridOccupancy2D.Instance?.MarkDirty();

        // ✅ 월드 상태 변경 → 캐시 무효화
        BumpWorldRevAndClearPlacementCaches();

    }

    // ✅ 기존 호출들(AttachRailFollowIfPossible(po);) 살리기

    // 기존 호출들 호환용(1파라미터로 부르는 곳 많을 거라서)

    // ✅ 기존 호출들(AttachRailFollowIfPossible(po);) 살리기

    // [RAIL] AttachRailFollowIfPossible
    void AttachRailFollowIfPossible(PlacementObject po, bool isDragging)
    {
        if (po == null) return;
        if (!po.AutoRailAttach) return;

        EnsureRailGraphUpToDate();

        var rails = GetAllRailsCached();
        var bind = po.GetComponent<RailNodeFollowBinding2D>();

        bool hasEntries =
            bind != null &&
            bind.CleanupInvalidEntriesAndHasAnyBound();

        if (hasEntries)
        {
            bind.SyncNow(syncPhysics: true);
            return;
        }

        bool okScan = RailNodeSnapBinder.TryAttachAllNearestNodesBySnapPoints(
            po, railFollowRadius, railNodeMask, rails, railFollowDebug
        );

        po.GetComponent<RailNodeFollowBinding2D>()?.SyncNow(syncPhysics: true);

        if (!okScan)
            StartCoroutine(CoRetryAttachRailFollow(po));
    }

    // [RAIL] CoRetryAttachRailFollow
    IEnumerator CoRetryAttachRailFollow(PlacementObject po)

    {

        yield return null;
        if (po == null) yield break;
        if (!po.AutoRailAttach) yield break;
        EnsureRailGraphUpToDate();
        var bind = po.GetComponent<RailNodeFollowBinding2D>();

        // 바인딩이 있고 조건도 같으면 KEEP

        if (bind != null && bind.Entries != null && bind.Entries.Count > 0)

        {

            bool okKeep = RailNodeSnapBinder.EnsureAttachedOrKeepExisting(
                po, railFollowRadius, railNodeMask, railFollowDebug
            );
            if (railFollowDebug) Debug.Log($"[AttachRailFollow][Retry1][KEEP] {po.name} ok={okKeep}", po);
            bind.SyncNow(syncPhysics: true);
            yield break;

        }

        // 없으면 SCAN(rails 캐시 넘김)

        var rails = GetAllRailsCached();
        bool ok = RailNodeSnapBinder.TryAttachAllNearestNodesBySnapPoints(
            po, railFollowRadius, railNodeMask, rails, railFollowDebug
        );
        if (railFollowDebug) Debug.Log($"[AttachRailFollow][Retry1][SCAN] {po.name} ok={ok}", po);
        po.GetComponent<RailNodeFollowBinding2D>()?.SyncNow(syncPhysics: true);

    }

    // ===========================

    // FOLLOWED RAIL VALIDATION

    // ===========================

    RailSpan2D[] _railsCache;
    RailNodeFollowBinding2D[] _bindCache;
    int _cacheFrame = -1;
    // [RAIL] GetAllRailsCached
    public RailSpan2D[] GetAllRailsCached()

    {

        if (_cacheFrame == Time.frameCount && _railsCache != null) return _railsCache;

#if UNITY_2022_2_OR_NEWER

        _railsCache = FindObjectsByType<RailSpan2D>(FindObjectsSortMode.None);

#else

        _railsCache = FindObjectsOfType<RailSpan2D>();

#endif

        _cacheFrame = Time.frameCount;
        return _railsCache;

    }

    // [RAIL] CanMoveAttachedRails
    bool CanMoveAttachedRails(PlacementObject po, out RailSpan2D blockedRail)
    {
        return CanMoveAttachedRailsAtPos_Virtual(po, po.transform.position, out blockedRail);
    }


    // =========================================================
    #endregion

    #region SnapPoint helpers (NoAlloc)
    // SnapPoint helpers (NoAlloc)
    // =========================================================
    // [SNAP] TryPickNearestSnapPoint
    bool TryPickNearestSnapPoint(Vector2 world, out SnapPoint snap)
    {
        snap = null;
        if (snapPointMask.value == 0) return false;

        int cnt = Physics2D.OverlapCircleNonAlloc(world, snapPointPickRadius, _spHitsTmp, snapPointMask);
        if (cnt <= 0) return false;

        float best = float.PositiveInfinity;
        SnapPoint bestSp = null;

        for (int i = 0; i < cnt; i++)
        {
            var col = _spHitsTmp[i];
            if (col == null) continue;
            var sp = col.GetComponentInParent<SnapPoint>();
            if (sp == null) continue;

            float d = ((Vector2)sp.transform.position - world).sqrMagnitude;
            if (d < best)
            {
                best = d;
                bestSp = sp;
            }
        }

        snap = bestSp;
        return snap != null;
    }

    // [SNAP] IsSnapPointConnectedToOwner
    static bool IsSnapPointConnectedToOwner(PlacementObject owner, Transform snapPointTr)
    {
        if (owner == null || snapPointTr == null) return false;
        var bind = owner.GetComponent<RailNodeFollowBinding2D>();
        if (bind == null) return false;

        var entries = bind.Entries;
        if (entries == null) return false;
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            if (e.anchorPoint == snapPointTr && e.node != null)
                return true;
        }
        return false;
    }

    // =========================================================
    #endregion

    #region Occupancy segment check (with Snap exceptions)
    // Occupancy segment check (with Snap exceptions) - for PO drag rail validity
    // - Uses GridOccupancy2D baked map: ownerId==0 empty, ownerId==-1 wall.
    // - If endpoint is on a SnapPoint owner's cell, we can allow:
    //    A) endpointCellOnly: only the endpoint cell (within ~0.5 cell) is exempt
    //    B) ignoreRadiusWorld: exempt within radius from the endpoint
    // =========================================================
    // [SNAP] SegmentHitsOccupied_WithSnapExceptions_NoSync - 점유 맵 기반 세그먼트 충돌 검사(스냅 예외 포함)



    // =========================================================
    // [VIRTUAL RAIL CHECK] (no transform move / no Refresh / no Sync)
    // - "연결된 레일 예외/스냅 예외" 전부 무시하고,
    //   가상으로 계산한 레일(선분+두께) 점유 셀이 기존 점유(PO/벽/레일 baked)에 닿는지만 검사.
    // - 드래그 중 렉 확인/분리용으로 쓰기 좋음.
    // =========================================================

    static readonly HashSet<Vector2Int> _tmpVirtualRailCellsSet = new HashSet<Vector2Int>(1024);
    static readonly List<Vector2Int> _tmpVirtualRailCellsList = new List<Vector2Int>(1024);

    // [VIRTUAL] 레일 선분(a-b) + 반경(rWorld)이 닿는 셀을 (정확) 계산
    static List<Vector2Int> ComputeVirtualRailCellsPrecise(GridManager grid, Vector2 a, Vector2 b, float rWorld)
    {
        _tmpVirtualRailCellsSet.Clear();
        _tmpVirtualRailCellsList.Clear();

        if (grid == null) return _tmpVirtualRailCellsList;

        float cellSize = Mathf.Max(0.0001f, grid.cellSize);
        float half = cellSize * 0.5f;
        float r2 = rWorld * rWorld;

        // 후보 범위: 레일 AABB + rWorld
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

                // ✅ RailCellMap2D 규약과 동일하게 "CellToWorld가 셀 중심"이라고 가정
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

    // [VIRTUAL] 가상 레일 셀이 "점유(PO/벽/기타)"에 닿으면 true
    static bool VirtualRailHitsOccupiedCells(
        GridManager grid,
        Vector2 a,
        Vector2 b,
        float thickness,
        int ignoreOwnerId
    )
    {
        var occ = GridOccupancy2D.Instance;
        if (occ == null || grid == null) return false;

        occ.EnsureBaked();

        float rWorld = Mathf.Max(0f, thickness * 0.5f);
        var cells = ComputeVirtualRailCellsPrecise(grid, a, b, rWorld);

        for (int i = 0; i < cells.Count; i++)
        {
            int ownerId = occ.GetOwnerIdAtCell(cells[i]);
            if (ownerId == 0) continue;       // empty
            if (ownerId == -1) return true;   // wall always blocks
            if (ignoreOwnerId != 0 && ownerId == ignoreOwnerId) continue; // 내 PO 점유는 무시(드래그 hide와 동일한 의미)
            return true; // other occupancy blocks
        }

        return false;
    }

    /// <summary>
    /// (가상 검사) PO가 poWorldPos로 이동했을 때, 같이 끌려가는 레일들이 점유(벽/다른 PO)에 닿는지 검사.
    /// - 실제로 노드를 움직이지 않는다.
    /// - r.Refresh / Physics2D.SyncTransforms 호출이 없다.
    /// - "스냅 예외"는 여기서는 고려하지 않는다(요청대로).
    /// </summary>
    public bool CanMoveAttachedRailsAtPos_Virtual(
        PlacementObject po,
        Vector2 poWorldPos,
        out RailSpan2D blockedRail
    )
    {
        blockedRail = null;
        if (po == null) return true;

        var bind = po.GetComponent<RailNodeFollowBinding2D>();
        if (bind == null) return true;

        var entries = GetEntriesNoAlloc(bind, po.transform);
        if (entries == null || entries.Count == 0) return true;

        // 이동되는 노드(=anchor 제외)들의 "예상 위치" 맵을 만든다.
        _movedNodesBuf.Clear();
        _oldPosBuf.Clear(); // 여기서는 oldPosBuf를 "predictedPos" 저장용으로 재사용

        Quaternion poRot = po.transform.rotation;
        Vector3 poScale = po.transform.localScale;

        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            var node = e.node;
            if (node == null) continue;

            _movedNodesBuf.Add(node);

            Vector3 want = CalcAnchorWorld(poWorldPos, poRot, poScale, e.localOffset);
            want.z = 0f;
            _oldPosBuf[node] = want;
        }

        if (_movedNodesBuf.Count == 0) return true;

        var rails = GetAllRailsCached();
        if (rails == null || rails.Length == 0) return true;

        int ignoreOwnerId = po.GetInstanceID();

        for (int i = 0; i < rails.Length; i++)
        {
            var r = rails[i];
            if (r == null) continue;

            // 영향 레일만 (start/end 중 하나라도 이동 노드에 연결)
            bool touchesMoved =
                (r.startNode != null && _movedNodesBuf.Contains(r.startNode)) ||
                (r.endNode != null && _movedNodesBuf.Contains(r.endNode));
            if (!touchesMoved) continue;

            // 가상 start/end 계산 (이동 노드면 predicted, 아니면 현재 노드 위치)
            Vector2 a = (r.startNode != null)
                ? (_movedNodesBuf.Contains(r.startNode) ? (Vector2)_oldPosBuf[r.startNode] : (Vector2)r.startNode.transform.position)
                : r.start;

            Vector2 b = (r.endNode != null)
                ? (_movedNodesBuf.Contains(r.endNode) ? (Vector2)_oldPosBuf[r.endNode] : (Vector2)r.endNode.transform.position)
                : r.end;

            var g = (r.grid != null) ? r.grid : grid;

            if (VirtualRailHitsOccupiedCells(g, a, b, r.thickness, ignoreOwnerId))
            {
                blockedRail = r;
                return false;
            }
        }

        return true;
    }

    // =========================================================
    // [VIRTUAL COMBINED CHECK]
    // - PO + attached rails를 "동시에" 가상 점유 셀로 합쳐서 1번에 판정한다.
    // - 목적: 힌트/최종 판정 불일치(따로따로 검사해서 교차 겹침이 빠지는 문제) 제거.
    // - 전제: PO가 UseManualOccupancy=true일 때 정확함. (콜라이더 기반은 기존 정책 fallback)
    // =========================================================

    static readonly HashSet<Vector2Int> _tmpCombinedCellsSet = new HashSet<Vector2Int>(2048);
    static readonly List<Vector2Int> _tmpCombinedCellsList = new List<Vector2Int>(2048);
    static readonly HashSet<Vector2Int> _tmpPoCellsSet = new HashSet<Vector2Int>(1024);
    static readonly List<Vector2Int> _tmpPoCellsList = new List<Vector2Int>(1024);

    /// <summary>
    /// (가상 검사/동시 판정)
    /// po가 poWorldPos로 이동했을 때:
    /// - PO manual 점유셀 + 부착 레일(가상) 점유셀을 합쳐서
    ///   1) 내부 겹침(PO셀 vs 레일셀) 금지
    ///   2) 점유(벽/다른 PO) 금지
    ///   3) 기존 레일 셀맵(다른 레일) 금지
    /// 을 한 번에 판정한다.
    /// </summary>
    public bool CanMovePOWithAttachedRails_CombinedVirtual(PlacementObject po, Vector2 poWorldPos)
    {
        if (po == null) return true;
        if (grid == null) return true;

        var occ = GridOccupancy2D.Instance;
        if (occ == null) return true;

        occ.EnsureBaked();

        var cellMap = RailCellMap2D.Instance;

        int selfId = po.GetInstanceID();

        // -----------------------
        // 1) PO 셀(가상) 수집
        // -----------------------
        _tmpPoCellsSet.Clear();
        _tmpPoCellsList.Clear();

        po.GetManualOccupiedCellsAtWorld(grid, poWorldPos, _tmpPoCellsList);
        for (int i = 0; i < _tmpPoCellsList.Count; i++)
            _tmpPoCellsSet.Add(_tmpPoCellsList[i]);

        // -----------------------
        // 2) 레일 셀(가상) + Combined union
        // -----------------------
        _tmpCombinedCellsSet.Clear();
        _tmpCombinedCellsList.Clear();

        // add PO first
        for (int i = 0; i < _tmpPoCellsList.Count; i++)
        {
            var c = _tmpPoCellsList[i];
            if (_tmpCombinedCellsSet.Add(c))
                _tmpCombinedCellsList.Add(c);
        }

        // attached rails virtual cells
        if (!TryAddVirtualAttachedRailCellsToCombined(po, poWorldPos, _tmpPoCellsSet, _tmpCombinedCellsSet, _tmpCombinedCellsList))
            return false;

        // -----------------------
        // 3) Combined vs Occupancy(벽/다른 PO)
        // -----------------------
        for (int i = 0; i < _tmpCombinedCellsList.Count; i++)
        {
            var cell = _tmpCombinedCellsList[i];
            int ownerId = occ.GetOwnerIdAtCell(cell);
            if (ownerId == 0) continue;          // empty
            if (ownerId == -1) return false;     // wall blocks always
            if (ownerId == selfId) continue;     // 내 점유는 무시(가상 이동/드래그와 동일)
            return false;                         // other occupancy blocks
        }

        if (cellMap != null)
        {
            for (int i = 0; i < _tmpPoCellsList.Count; i++)
                if (cellMap.HasRailAtCell(_tmpPoCellsList[i]))
                    return false;
        }

        // ✅ SnapPoint 자리의 '다른 레일 endpoint handle' 존재 여부 체크
        if (!CanMovePO_SnapPointHandleNoForeign(po, poWorldPos))
            return false;

        return true;
    }

    /// <summary>
    /// po의 부착 레일들을 poWorldPos 기준으로 "가상" 선분 셀로 계산해서 Combined에 추가.
    /// - 반환 false: (4번 핵심) PO셀과 레일셀이 내부적으로 겹침(=동시 이동 시 충돌) 발생
    /// </summary>
    bool TryAddVirtualAttachedRailCellsToCombined(
        PlacementObject po,
        Vector2 poWorldPos,
        HashSet<Vector2Int> poCellsSet,
        HashSet<Vector2Int> combinedSet,
        List<Vector2Int> combinedList
    )
    {
        var bind = po.GetComponent<RailNodeFollowBinding2D>();
        if (bind == null) return true;

        var entries = GetEntriesNoAlloc(bind, po.transform);
        if (entries == null || entries.Count == 0) return true;

        // 이동되는 노드들의 predictedPos
        _movedNodesBuf.Clear();
        _oldPosBuf.Clear(); // predictedPos 저장으로 재사용

        Quaternion poRot = po.transform.rotation;
        Vector3 poScale = po.transform.localScale;

        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            var node = e.node;
            if (node == null) continue;

            _movedNodesBuf.Add(node);

            Vector3 want = CalcAnchorWorld(poWorldPos, poRot, poScale, e.localOffset);
            want.z = 0f;
            _oldPosBuf[node] = want;
        }

        if (_movedNodesBuf.Count == 0) return true;

        var rails = GetAllRailsCached();
        if (rails == null || rails.Length == 0) return true;

        // 레일별로 가상 셀 계산 후 combined에 누적
        for (int i = 0; i < rails.Length; i++)
        {
            var r = rails[i];
            if (r == null) continue;

            bool touchesMoved =
                (r.startNode != null && _movedNodesBuf.Contains(r.startNode)) ||
                (r.endNode != null && _movedNodesBuf.Contains(r.endNode));
            if (!touchesMoved) continue;

            Vector2 a = (r.startNode != null)
                ? (_movedNodesBuf.Contains(r.startNode) ? (Vector2)_oldPosBuf[r.startNode] : (Vector2)r.startNode.transform.position)
                : r.start;

            Vector2 b = (r.endNode != null)
                ? (_movedNodesBuf.Contains(r.endNode) ? (Vector2)_oldPosBuf[r.endNode] : (Vector2)r.endNode.transform.position)
                : r.end;

            var g = (r.grid != null) ? r.grid : grid;
            float rWorld = Mathf.Max(0f, r.thickness * 0.5f);

            var cells = ComputeVirtualRailCellsPrecise(g, a, b, rWorld);

            for (int c = 0; c < cells.Count; c++)
            {
                var cell = cells[c];

                // ✅ (4번) 내부 겹침: 동시에 옮긴 상태에서 PO셀과 레일셀이 겹치면 실패
                if (poCellsSet != null && poCellsSet.Contains(cell))
                    return false;

                if (combinedSet.Add(cell))
                    combinedList.Add(cell);
            }
        }

        return true;
    }

    // -----------------------
    // Geometry helpers (copied minimal from RailCellMap2D)
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

    static bool SegmentsIntersect(Vector2 p1, Vector2 p2, Vector2 q1, Vector2 q2)
    {
        float o1 = Orient(p1, p2, q1);
        float o2 = Orient(p1, p2, q2);
        float o3 = Orient(q1, q2, p1);
        float o4 = Orient(q1, q2, p2);

        if (o1 * o2 < 0f && o3 * o4 < 0f) return true;

        if (Mathf.Approximately(o1, 0f) && OnSegment(p1, p2, q1)) return true;
        if (Mathf.Approximately(o2, 0f) && OnSegment(p1, p2, q2)) return true;
        if (Mathf.Approximately(o3, 0f) && OnSegment(q1, q2, p1)) return true;
        if (Mathf.Approximately(o4, 0f) && OnSegment(q1, q2, p2)) return true;

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

    // ✅ 여러 노드 중 하나라도 터치하는 레일만 Refresh

    // [RAIL] RefreshRailsTouchingAnyNodes
    static void RefreshRailsTouchingAnyNodes(HashSet<RailSnapNode2D> nodes, RailSpan2D[] rails)

    {

        if (nodes == null || rails == null) return;
        for (int i = 0; i < rails.Length; i++)

        {

            var r = rails[i];
            if (r == null) continue;
            if ((r.startNode != null && nodes.Contains(r.startNode)) ||
                (r.endNode != null && nodes.Contains(r.endNode)))

            {

                r.Refresh(syncFromNodes: true);

            }

        }

    }

    // [PLACE] SetBlockedRailPreview
    void SetBlockedRailPreview(RailSpan2D blockedRail, bool blocked)
    {
        if (blockedRail == null)
        {
            if (!blocked) ClearBlockedRailPreview();
            return;
        }

        if (blocked)
        {
            if (_blockedPreviewRails.Add(blockedRail))
                blockedRail.SetBlockedPreview(true);
        }
        else
        {
            if (_blockedPreviewRails.Remove(blockedRail))
                blockedRail.SetBlockedPreview(false);
        }
    }

    void SetBlockedRailPreview(IEnumerable<RailSpan2D> rails, bool blocked)
    {
        if (rails == null)
        {
            if (!blocked) ClearBlockedRailPreview();
            return;
        }

        if (!blocked)
        {
            ClearBlockedRailPreview();
            return;
        }

        HashSet<RailSpan2D> newSet = new HashSet<RailSpan2D>();

        foreach (var rail in rails)
        {
            if (rail == null) continue;
            newSet.Add(rail);

            if (_blockedPreviewRails.Add(rail))
                rail.SetBlockedPreview(true);
        }

        _tmpPreviewRails.Clear();
        foreach (var oldRail in _blockedPreviewRails)
        {
            if (oldRail == null) continue;
            if (!newSet.Contains(oldRail))
                _tmpPreviewRails.Add(oldRail);
        }

        for (int i = 0; i < _tmpPreviewRails.Count; i++)
        {
            var rail = _tmpPreviewRails[i];
            if (rail == null) continue;

            rail.SetBlockedPreview(false);
            _blockedPreviewRails.Remove(rail);
        }

        _tmpPreviewRails.Clear();
    }

    void ClearBlockedRailPreview()
    {
        if (_blockedPreviewRails.Count == 0)
            return;

        _tmpPreviewRails.Clear();
        foreach (var rail in _blockedPreviewRails)
        {
            if (rail != null)
                _tmpPreviewRails.Add(rail);
        }

        for (int i = 0; i < _tmpPreviewRails.Count; i++)
        {
            var rail = _tmpPreviewRails[i];
            if (rail != null)
                rail.SetBlockedPreview(false);
        }

        _blockedPreviewRails.Clear();
        _tmpPreviewRails.Clear();
    }

    void GetAllBoundRails(PlacementObject po, List<RailSpan2D> outRails)
    {
        outRails.Clear();
        if (po == null) return;

        var bind = po.GetComponent<RailNodeFollowBinding2D>();
        if (bind == null) return;

        var entries = GetEntriesNoAlloc(bind, po.transform);
        if (entries == null || entries.Count == 0) return;

        var rails = GetAllRailsCached();
        if (rails == null || rails.Length == 0) return;

        HashSet<RailSpan2D> added = new HashSet<RailSpan2D>();

        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            if (e.node == null) continue;
            if (e.node.GetConnectedRailCount() <= 0) continue;

            for (int r = 0; r < rails.Length; r++)
            {
                var rail = rails[r];
                if (rail == null) continue;

                if (rail.startNode == e.node || rail.endNode == e.node)
                {
                    if (added.Add(rail))
                        outRails.Add(rail);
                }
            }
        }
    }


    bool IsRailBound(PlacementObject po)
    {
        if (po == null)
            return false;

        var bind = po.GetComponent<RailNodeFollowBinding2D>();
        if (bind != null)
        {
            var entries = bind.Entries;
            if (entries != null)
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    var e = entries[i];
                    if (e.node == null) continue;

                    // 노드가 살아있고 실제 레일이 하나라도 연결돼 있으면 bound
                    if (e.node.GetConnectedRailCount() > 0)
                        return true;

                    // connected count가 아직 늦게 올라오는 경우 대비:
                    // 엔트리 자체가 살아 있으면 일단 bound로 취급하고 싶으면 이 줄 사용
                    // return true;
                }
            }

            if (bind.node != null && bind.node.GetConnectedRailCount() > 0)
                return true;
        }

        var nodes = po.GetComponentsInChildren<RailSnapNode2D>(true);
        for (int i = 0; i < nodes.Length; i++)
        {
            var node = nodes[i];
            if (node == null) continue;

            if (node.GetConnectedRailCount() > 0)
                return true;
        }

        return false;
    }

    bool HasRailBoundToInactiveStrengthTarget(PlacementObject po, int targetLevel)
    {
        if (po == null) return false;

        var strength = po.GetComponent<StrengthBasedOccupancyCells>();
        if (strength == null) return false;

        var bind = po.GetComponent<RailNodeFollowBinding2D>();
        if (bind == null) return false;

        var entries = GetEntriesNoAlloc(bind, po.transform);
        if (entries == null || entries.Count == 0)
            return false;

        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            if (e.node == null) continue;
            if (e.anchorPoint == null) continue;

            // 실제로 연결된 레일이 없는 엔트리는 무시
            if (e.node.GetConnectedRailCount() <= 0)
                continue;

            // 목표 레벨에서 이 anchorPoint(또는 부모)가 활성 대상이 아니면 막음
            if (!strength.IsTargetOrParentActiveAtLevel(e.anchorPoint, targetLevel))
                return true;
        }

        return false;
    }
    // (Removed) OwnerRailPenetrationCalc (Physics2D.Distance) for performance.

    // [UTIL] CalcAnchorWorld
    static Vector3 CalcAnchorWorld(Vector2 poPos, Quaternion poRot, Vector3 poScale, Vector2 localOffset)

    {

        var m = Matrix4x4.TRS(new Vector3(poPos.x, poPos.y, 0f), poRot, poScale);
        return m.MultiplyPoint3x4(new Vector3(localOffset.x, localOffset.y, 0f));

    }

    IReadOnlyList<RailNodeFollowBinding2D.Entry> GetEntriesNoAlloc(RailNodeFollowBinding2D bind, Transform poTr)

    {

        if (bind == null) return null;

        // 멀티 엔트리 우선

        if (bind.Entries != null && bind.Entries.Count > 0)
            return bind.Entries;

        // 구버전 호환

        if (bind.node == null) return null;
        _tmpLegacyEntries.Clear();
        Vector2 localOffset = Vector2.zero;
        if (bind.anchorPoint != null)
            localOffset = (Vector2)poTr.InverseTransformPoint(bind.anchorPoint.position);
        _tmpLegacyEntries.Add(new RailNodeFollowBinding2D.Entry

        {

            node = bind.node,
            anchorPoint = (bind.anchorPoint != null) ? bind.anchorPoint.transform : poTr,
            localOffset = localOffset
        });
        return _tmpLegacyEntries;

    }

    // [UTIL] RestoreMovedNodesNoAlloc - 스냅 상태 백업/복구
    void RestoreMovedNodesNoAlloc(
        Dictionary<RailSnapNode2D, Vector3> backup,
        HashSet<RailSnapNode2D> movedNodes,
        RailSpan2D[] rails
    )

    {

        foreach (var kv in backup)

        {

            if (kv.Key == null) continue;
            kv.Key.transform.position = kv.Value;

        }

        Physics2D.SyncTransforms();
        RefreshRailsTouchingAnyNodes(movedNodes, rails);
        Physics2D.SyncTransforms();

    }

    // [RAIL] DetachRailFollowIfAnyAndRefreshRails
    void DetachRailFollowIfAnyAndRefreshRails(PlacementObject po)

    {

        if (po == null) return;

        // 1) 삭제 전, 내가 붙잡고 있던 노드 목록 확보

        _movedNodesBuf.Clear();
        var bind = po.GetComponent<RailNodeFollowBinding2D>();
        if (bind != null)

        {

            var es = bind.Entries;
            if (es != null && es.Count > 0)

            {

                for (int i = 0; i < es.Count; i++)

                {

                    var n = es[i].node;
                    if (n != null) _movedNodesBuf.Add(n);

                }

            }

            else

            {

                if (bind.node != null) _movedNodesBuf.Add(bind.node);

            }

        }

        // 2) Detach

        RailNodeSnapBinder.Detach(po);
        Physics2D.SyncTransforms();

        // 3) 영향 레일 Refresh (stale 방지)

        var rails = GetAllRailsCached();
        RefreshRailsTouchingAnyNodes(_movedNodesBuf, rails);
        Physics2D.SyncTransforms();

    }

    // [RAIL] InvalidateRailCaches
    void InvalidateRailCaches()

    {

        // FindObjects 캐시 무효화

        _cacheFrame = -1;
        _railsCache = null;
        _bindCache = null;

        // Node->Owner 캐시 무효화 (중요)

        _nodeOwnerCacheFrame = -1;
        _nodeOwnerCache.Clear();
        _nodeOwnerCache2.Clear(); // ✅ 이거 추가 추천

    }

    // [SNAP] CleanupDeadSnapConnectionsScene
    void CleanupDeadSnapConnectionsScene()

    {

#if UNITY_2022_2_OR_NEWER

        var all = FindObjectsByType<PlacementObject>(FindObjectsSortMode.None);

#else

        var all = FindObjectsOfType<PlacementObject>();

#endif

        for (int i = 0; i < all.Length; i++)

        {

            var po = all[i];
            if (po == null) continue;
            var list = po.connections;
            if (list == null || list.Count == 0) continue;

            // 뒤에서 앞으로 제거 (인덱스 안전)

            for (int k = list.Count - 1; k >= 0; k--)

            {

                var c = list[k];

                // 연결 요소가 하나라도 깨졌으면 제거

                if (c.myRoot == null || c.otherRoot == null ||
                    c.myPoint == null || c.otherPoint == null ||
                    c.otherRoot.owner == null)

                {

                    list.RemoveAt(k);
                    continue;

                }

                // other owner가 비활성/파괴 예정이면 제거

                if (!c.otherRoot.owner.gameObject.activeInHierarchy)

                {

                    list.RemoveAt(k);
                    continue;

                }

            }

        }

    }

    // [SELECT/DRAG] PrepareRailFollowForDrag
    void PrepareRailFollowForDrag(PlacementObject po)
    {
        if (po == null) return;

        if (!po.AutoRailAttach) return;

        int id = po.GetInstanceID();
        if (_railFollowPreparedThisDrag && _railFollowPreparedDragOwnerId == id)
            return;

        _railFollowPreparedThisDrag = true;
        _railFollowPreparedDragOwnerId = id;

        EnsureRailGraphUpToDate();

        var bind = po.GetComponent<RailNodeFollowBinding2D>();

        if (bind != null)
        {
            bool hasLive = bind.CleanupInvalidEntriesAndHasAnyBound();

            if (hasLive)
            {
                bind.SyncNow(syncPhysics: true, broadcastMoved: false);
                return;
            }
        }

        var rails = GetAllRailsCached();
        RailNodeSnapBinder.TryAttachAllNearestNodesBySnapPoints(
            po, railFollowRadius, railNodeMask, rails, railFollowDebug
        );

        po.GetComponent<RailNodeFollowBinding2D>()?.SyncNow(syncPhysics: true, broadcastMoved: false);
    }

    // [SELECT/DRAG] ClearRailFollowDragPrepared
    void ClearRailFollowDragPrepared()

    {

        _railFollowPreparedThisDrag = false;
        _railFollowPreparedDragOwnerId = 0;

    }

    // [RAIL] RefreshRailsBoundTo
    void RefreshRailsBoundTo(PlacementObject po)

    {

        if (po == null) return;
        _movedNodesBuf.Clear();
        var bind = po.GetComponent<RailNodeFollowBinding2D>();
        if (bind == null) return;

        // 멀티 엔트리

        if (bind.Entries != null && bind.Entries.Count > 0)

        {

            for (int i = 0; i < bind.Entries.Count; i++)

            {

                var n = bind.Entries[i].node;
                if (n != null) _movedNodesBuf.Add(n);

            }

        }

        else

        {

            // 레거시 단일

            if (bind.node != null) _movedNodesBuf.Add(bind.node);

        }

        if (_movedNodesBuf.Count == 0) return;
        var rails = GetAllRailsCached();
        RefreshRailsTouchingAnyNodes(_movedNodesBuf, rails);
        Physics2D.SyncTransforms();

    }

    // [UTIL] BumpOcc
    void BumpOcc()

    {

        var occ = GridOccupancy2D.Instance;
        if (occ == null) return;
        occ.MarkDirty();

    }

    // [RAIL] EnsureRailGraphUpToDate
    void EnsureRailGraphUpToDate()

    {

        // RailGraphDirty가 없으면 컴파일 에러 나니까,

        // 너 프로젝트에 RailGraphDirty.cs가 반드시 있어야 함.

        if (!RailGraphDirty.dirty) return;

        // ✅ 지금 프레임에서 바로 정리해서 Update 로직이 최신 그래프를 보게 함

        RailGraphDirty.dirty = false;
        RailGraphCleanup2D.Cleanup();

        // ✅ FindObjects 캐시 / NodeOwner 캐시까지 같이 무효화 (안전빵)

        InvalidateRailCaches();

        // ✅ 레일 구조가 변했다 = 설치가능성 변함
        BumpWorldRevAndClearPlacementCaches();

    }

    void CollectBoundRailsForDrag(PlacementObject po, RailNodeFollowBinding2D bind, List<RailSpan2D> outRails)
    {
        outRails.Clear();
        _dragExcludedRailsSet.Clear();

        if (po == null || bind == null) return;

        // ✅ 멀티/레거시 모두 커버
        var entries = GetEntriesNoAlloc(bind, po.transform);
        if (entries == null || entries.Count == 0) return;

        HashSet<RailSnapNode2D> nodeSet = new HashSet<RailSnapNode2D>();
        for (int i = 0; i < entries.Count; i++)
        {
            var n = entries[i].node;
            if (n != null) nodeSet.Add(n);
        }
        if (nodeSet.Count == 0) return;

        var rails = GetAllRailsCached();
        if (rails == null) return;

        for (int i = 0; i < rails.Length; i++)
        {
            var r = rails[i];
            if (r == null) continue;

            if ((r.startNode != null && nodeSet.Contains(r.startNode)) ||
                (r.endNode != null && nodeSet.Contains(r.endNode)))
            {
                if (_dragExcludedRailsSet.Add(r))
                    outRails.Add(r);
            }
        }
    }




    // ✅ PO 드래그 후보 위치에서:
    // - PO의 SnapPoint(앵커) 위치에 "다른 레일의 endpoint handle"이 존재하면 이동 불가
    // - 단, 해당 SnapPoint에 이미 연결된 레일(= entry.node에 붙어있는 레일)의 handle은 허용
    bool CanMovePO_SnapPointHandleNoForeign(PlacementObject po, Vector2 poWorldPos)
    {
        if (po == null) return true;
        if (grid == null) return true;

        // snapPointMask가 비어있으면 규칙을 끔
        if (snapPointMask.value == 0) return true;

        var bind = po.GetComponent<RailNodeFollowBinding2D>();
        if (bind == null) return true;

        var entries = GetEntriesNoAlloc(bind, po.transform);
        if (entries == null || entries.Count == 0) return true;

        var railsAll = GetAllRailsCached();

        float r = (snapPointPickRadius > 0.0001f) ? snapPointPickRadius : 0.25f;

        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            if (e.node == null) continue;

            // anchorPoint가 SnapPoint 레이어일 때만 검사
            if (e.anchorPoint == null) continue;
            if (((1 << e.anchorPoint.gameObject.layer) & snapPointMask.value) == 0) continue;

            // 후보 위치에서의 SnapPoint 월드 좌표
            Vector2 offsetWorld = (Vector2)po.transform.TransformVector((Vector3)e.localOffset);
            Vector2 snapWorld = poWorldPos + offsetWorld;

            // 이 node에 연결된 레일들은 허용
            _tmpAllowedRailsForSnap.Clear();
            if (railsAll != null)
            {
                for (int rIdx = 0; rIdx < railsAll.Length; rIdx++)
                {
                    var rail = railsAll[rIdx];
                    if (rail == null) continue;
                    if (rail.startNode == e.node || rail.endNode == e.node)
                        _tmpAllowedRailsForSnap.Add(rail);
                }
            }

            int hitCount = Physics2D.OverlapCircleNonAlloc(snapWorld, r, _tmpSnapHandleCols);
            for (int hIdx = 0; hIdx < hitCount; hIdx++)
            {
                var col = _tmpSnapHandleCols[hIdx];
                if (col == null) continue;

                var handle = col.GetComponent<RailEndpointHandle2D>() ?? col.GetComponentInParent<RailEndpointHandle2D>();
                if (handle == null) continue;

                // 핸들이 속한 레일 찾기 (필드 접근 X)
                var handleRail = handle.GetComponentInParent<RailSpan2D>();
                if (handleRail == null) continue;

                // 허용 레일이면 통과
                if (_tmpAllowedRailsForSnap.Contains(handleRail))
                    continue;

                // 다른 레일 handle 발견 -> 후보 탈락
                return false;
            }
        }

        return true;
    }

    static readonly List<Vector2Int> _tmpPlacementCells = new List<Vector2Int>(128);
    static readonly List<Vector3> _tmpPlacementWorldPoints = new List<Vector3>(128);

    bool IsInsidePlacementFrame(PlacementObject po, Vector2 worldPos)
    {
        if (placementFrame == null) return true;
        if (po == null || grid == null) return false;

        // manual occupancy 쓰는 PO는 점유 셀 전체가 hole 안에 있어야 함
        if (po.UseManualOccupancy)
        {
            _tmpPlacementCells.Clear();
            _tmpPlacementWorldPoints.Clear();

            po.GetManualOccupiedCellsAtWorld(grid, worldPos, _tmpPlacementCells);
            if (_tmpPlacementCells.Count == 0)
                return false;

            for (int i = 0; i < _tmpPlacementCells.Count; i++)
            {
                var cell = _tmpPlacementCells[i];
                Vector3 world = grid.CellToWorld(cell); // 현재 프로젝트 규약 사용
                _tmpPlacementWorldPoints.Add(world);
            }

            return placementFrame.ContainsAllWorldPointsInHole(_tmpPlacementWorldPoints, placementFrameMargin);
        }

        // manual occupancy 안 쓰면 중심점 기준 fallback
        return placementFrame.ContainsWorldPointInHole(worldPos, placementFrameMargin);
    }

    // =========================================================
    #endregion

    #region Rail Bind: Bake local offsets
    // Rail Bind: Bake local offsets (so future SyncNow won't pull rails back)
    // =========================================================

    // [RAIL] BakeRailBindingLocalOffsetsFromCurrent
    void BakeRailBindingLocalOffsetsFromCurrent(PlacementObject po)
    {
        if (po == null) return;
        var bind = po.GetComponent<RailNodeFollowBinding2D>();
        if (bind == null) return;

        // ✅ Entries는 IReadOnlyList라 GridPlacer에서 직접 수정하지 말고,
        //    바인딩 컴포넌트 내부 메서드로 localOffset을 베이크한다.
        bind.BakeLocalOffsetsFromCurrent();
    }

    // [RAIL] FinalizeRailBindingNow
    void FinalizeRailBindingNow(PlacementObject po)
    {
        if (po == null) return;

        if (po.AutoRailAttach)
            AttachRailFollowIfPossible(po, isDragging: false);

        var bind = po.GetComponent<RailNodeFollowBinding2D>();
        bool okNow = (bind != null && bind.Entries != null && bind.Entries.Count > 0);

        FinalizeRuntimeRailBinding(po);

        if (!okNow && po.AutoRailAttach)
            StartCoroutine(CoRetryAttachRailFollow(po));
    }

    public void FinalizeRuntimeRailBinding(PlacementObject po)
    {
        if (po == null) return;

        var bind = po.GetComponent<RailNodeFollowBinding2D>();
        if (bind == null) return;

        // 노드 -> 앵커 위치 강제 동기화
        bind.SyncNow(syncPhysics: true, broadcastMoved: true);

        // 현재 붙은 상태를 localOffset으로 다시 베이크
        bind.BakeLocalOffsetsFromCurrent();

        // 베이크 후 다시 한 번 동기화
        bind.SyncNow(syncPhysics: true, broadcastMoved: true);

        // 이 PO에 묶인 레일들 강제 refresh
        RefreshRailsBoundTo(po);

        // 캐시 무효화
        InvalidateRailCaches();

        Physics2D.SyncTransforms();
    }

    // [RAIL] HasMovableRailBinding
    bool HasMovableRailBinding(PlacementObject po)
    {
        if (po == null) return false;

        var bind = po.GetComponent<RailNodeFollowBinding2D>();
        if (bind == null) return false;

        var entries = bind.Entries;
        if (entries == null || entries.Count == 0) return false;

        for (int i = entries.Count - 1; i >= 0; i--)
        {
            var e = entries[i];
            if (e.node == null)
                continue;

            // 실제로 레일이 연결된 노드만 movable binding으로 인정
            if (e.node.GetConnectedRailCount() > 0)
                return true;
        }

        return false;
    }


    static readonly Collider2D[] _tmpRailHits = new Collider2D[96];
    static readonly List<Vector2Int> _tmpCells = new List<Vector2Int>(128);


    // ✅ RailCellMap 기반: PO manual 점유 셀에 레일 셀이 하나라도 있으면 true
    public bool HasRailOverManualCells_CellMap(PlacementObject po, Vector2 worldPos)
    {

        if (po == null || grid == null) return false;
        if (!po.UseManualOccupancy) return false;

        var map = RailCellMap2D.Instance;
        if (map == null)
        {
            return false;
        }

        _tmpCells.Clear();
        po.GetManualOccupiedCellsAtWorld(grid, worldPos, _tmpCells);
        if (_tmpCells.Count == 0) return false;

        for (int i = 0; i < _tmpCells.Count; i++)
        {
            if (map.HasRailAtCell(_tmpCells[i]))
                return true;
        }
        return false;
    }


    // ===========================
    // Placement Validity Cache (NO re-check on same spot)
    // ===========================
    int _worldRev = 0; // 월드가 바뀌면 증가(점유/레일 변경 등)

    struct PlaceCacheKey : IEquatable<PlaceCacheKey>
    {
        public int worldRev;
        public int prefabId;      // placementData/prefab 구분
        public Vector2Int cell;   // snappedPos의 셀
        public int rotZ10;        // 회전(0.1도 단위로 정수화)
        public bool flipX;

        public bool Equals(PlaceCacheKey o) =>
            worldRev == o.worldRev &&
            prefabId == o.prefabId &&
            cell.Equals(o.cell) &&
            rotZ10 == o.rotZ10 &&
            flipX == o.flipX;

        public override bool Equals(object obj) =>
            obj is PlaceCacheKey other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int h = worldRev;
                h = h * 31 + prefabId;
                h = h * 31 + cell.GetHashCode();
                h = h * 31 + rotZ10;
                h = h * 31 + (flipX ? 1 : 0);
                return h;
            }
        }
    }


    readonly Dictionary<PlaceCacheKey, bool> _placeCanCache = new Dictionary<PlaceCacheKey, bool>(2048);

    struct DragCacheKey : IEquatable<DragCacheKey>
    {
        public int worldRev;
        public int poId;
        public Vector2Int cell;     // finalPos 셀
        public int rotZ10;
        public int scaleSignX;      // flip/scale 영향(보통 +1/-1)
        public int snapTargetId;
        public int myPointId;
        public int otherPointId;

        public bool Equals(DragCacheKey o) =>
            worldRev == o.worldRev && poId == o.poId && cell.Equals(o.cell) &&
            rotZ10 == o.rotZ10 && scaleSignX == o.scaleSignX &&
            snapTargetId == o.snapTargetId && myPointId == o.myPointId && otherPointId == o.otherPointId;

        public override int GetHashCode()
        {
            unchecked
            {
                int h = worldRev;
                h = h * 31 + poId;
                h = h * 31 + cell.GetHashCode();
                h = h * 31 + rotZ10;
                h = h * 31 + scaleSignX;
                h = h * 31 + snapTargetId;
                h = h * 31 + myPointId;
                h = h * 31 + otherPointId;
                return h;
            }
        }
    }

    readonly Dictionary<DragCacheKey, bool> _dragCanCache = new Dictionary<DragCacheKey, bool>(2048);

    // [PLACE] BumpWorldRevAndClearPlacementCaches
    void BumpWorldRevAndClearPlacementCaches()
    {
        _worldRev++;
        _placeCanCache.Clear();
        _dragCanCache.Clear();
    }

#if UNITY_EDITOR

    // [RAIL] DumpRailBind
    void DumpRailBind(PlacementObject po, string tag)

    {

        if (!debugRailBindDump) return;
        if (po == null) return;
        var bind = po.GetComponent<RailNodeFollowBinding2D>();
        if (bind == null)

        {

            Debug.Log($"[{tag}] bind=NULL po={po.name}");
            return;

        }

        var entries = bind.Entries;
        Debug.Log($"[{tag}] po={po.name} poPos={po.transform.position} rotZ={po.transform.eulerAngles.z} scale={po.transform.localScale} entries={(entries != null ? entries.Count : -1)}");
        if (entries == null || entries.Count == 0)

        {

            Debug.Log($"[{tag}] (legacy) bind.node={bind.node?.name} anchor={bind.anchorPoint?.name}");
            return;

        }

        Quaternion rot = po.transform.rotation;
        Vector3 scale = po.transform.localScale;
        Vector2 poPos = po.transform.position;
        for (int i = 0; i < entries.Count; i++)

        {

            var e = entries[i];
            if (e.node == null) continue;
            Vector3 wantNow = CalcAnchorWorld(poPos, rot, scale, e.localOffset);
            Vector3 nodeNow = e.node.transform.position;
            string anchorName = e.anchorPoint ? e.anchorPoint.name : "NULL";
            Vector3 anchorWorld = e.anchorPoint ? e.anchorPoint.position : Vector3.zero;
            Debug.Log(
                $"[{tag}]  #{i} node={e.node.name} isAnchor={e.node.IsAnchor} " +
                $"anchor={anchorName} anchorWorld={anchorWorld} localOffset={e.localOffset} " +
                $"wantNow={wantNow} nodeNow={nodeNow} diff={(wantNow - nodeNow)}"
            );

            // ✅ 씬뷰 선: "예상 위치(wantNow)" 확인용

            Debug.DrawLine(poPos, wantNow, Color.cyan, 1.5f);
            Debug.DrawLine(nodeNow, wantNow, Color.magenta, 1.5f);
            if (e.anchorPoint) Debug.DrawLine(anchorWorld, wantNow, Color.yellow, 1.5f);

        }

    }

#endif


    #endregion

    #region EXTENSION HOOKS
    // EXTENSION HOOKS

    // =========================================================

    // [UTIL] NotifyStageChanged
    void NotifyStageChanged()
    {
        if (_stageSaveManager == null)
            _stageSaveManager = FindFirstObjectByType<StageSaveManager>();

        if (_stageSaveManager != null)
            _stageSaveManager.NotifyStageChanged();

        SendMessage("OnStageChanged", SendMessageOptions.DontRequireReceiver);
    }

    // =========================================================

    #endregion

    // =========================
    // [SELECT API] Selection expose + event
    // =========================
    public event Action<PlacementObject> SelectedChanged;
    public PlacementObject SelectedPO => selected;

    void SetSelectedPO(PlacementObject po, bool applyVisual = true, bool locked = false)
    {
        if (selected == po)
        {
            // 같은 걸 다시 찍어도 잠금 상태는 갱신될 수 있게
            IsSelectedLocked = locked;
            SelectedChanged?.Invoke(selected);
            return;
        }


        selected = po;
        IsSelectedLocked = (selected != null) ? locked : false;


        SelectedChanged?.Invoke(selected);
    }

    void SetDragPhase(SelectedDragPhase phase)
    {
        if (CurrentDragPhase == phase) return;

        CurrentDragPhase = phase;
        SelectedDragPhaseChanged?.Invoke(selected, CurrentDragPhase);
    }

    void NotifyDragStarted(PlacementObject po)
    {
        if (po == null) return;

        var handlers = po.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < handlers.Length; i++)
        {
            if (handlers[i] is IDragStateHandler dragHandler)
                dragHandler.BeginDragState();
        }

        SelectedDragStarted?.Invoke(po);
    }

    void NotifyDragEnded(PlacementObject po, bool committed)
    {
        if (po == null) return;

        var handlers = po.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < handlers.Length; i++)
        {
            if (handlers[i] is IDragStateHandler dragHandler)
                dragHandler.EndDragState(committed);
        }

        SelectedDragEnded?.Invoke(po, committed);
    }



    // =========================

    // =========================
    // [UI ACTIONS] called by UI buttons
    // =========================
    public void UI_DeleteSelectedPO()
    {
        if (selected == null) return;
        if (IsSelectedFixed()) return; // fixedRoot 금지 유지
        TryDeleteSelectedObject();
    }

    public void UI_RotateSelectedPO(float deltaDegrees)
    {
        if (selected == null) return;
        if (!CanTransformSelected()) return;
        RotateSelectedBy(deltaDegrees);
    }

    public void UI_FlipSelectedPO_X()
    {
        if (selected == null) return;
        if (!CanTransformSelected()) return;
        FlipSelectedX();
    }

    public void UI_FlipSelectedPO_Y()
    {
        if (selected == null) return;
        if (!CanTransformSelected()) return;
        FlipSelectedY();
    }

    void SetRuntimeFollowEnabledForPO(PlacementObject po, bool enabled)
    {
        if (po == null) return;

        var bind = po.GetComponent<RailNodeFollowBinding2D>();
        if (bind == null) return;

        var entries = GetEntriesNoAlloc(bind, po.transform);
        if (entries == null) return;

        int myId = po.GetInstanceID();

        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            var node = e.node;
            if (node == null) continue;
            if (node.IsAnchor) continue;

            var follow = node.GetComponent<RailNodeFollow2D>();
            if (follow == null)
                follow = node.gameObject.AddComponent<RailNodeFollow2D>();

            Transform anchor = e.anchorPoint != null ? e.anchorPoint : po.transform;

            // ✅ 언도 직후 stale follow 보정
            if (follow.target != anchor || follow.ownerId != myId)
                follow.Attach(anchor, myId);

            follow.runtimeFollowEnabled = enabled;
        }
    }

    void SyncBoundNodesNow(PlacementObject po, bool broadcastMoved)
    {
        if (po == null) return;

        var bind = po.GetComponent<RailNodeFollowBinding2D>();
        if (bind == null) return;

        bind.SyncNow(syncPhysics: true, broadcastMoved: broadcastMoved);
        RefreshRailsBoundTo(po);
        Physics2D.SyncTransforms();
    }


    void ApplyPlacementDefaultsToPO(PlacementObject po)
    {
        if (po == null) return;
        if (po.placementData == null) return;

        var strength = po.GetComponent<StrengthBasedOccupancyCells>();
        if (strength != null)
            strength.ApplyDefaultFromPlacementData(po.placementData);
    }

    // =========================
    // [STRENGTH] helpers
    // =========================
    StrengthBasedOccupancyCells GetSelectedStrengthComp()
    {
        if (selected == null) return null;

        var data = selected.placementData;
        if (data == null || !data.allowStrengthControl)
            return null;

        return selected.GetComponent<StrengthBasedOccupancyCells>();
    }

    public bool CanIncreaseSelectedStrengthNow()
    {
        return CanChangeSelectedStrengthNow(+1);
    }

    public bool CanDecreaseSelectedStrengthNow()
    {
        return CanChangeSelectedStrengthNow(-1);
    }

    bool CanChangeSelectedStrengthNow(int delta)
    {
        if (!CanTransformSelectedBase())
        {
            return false;
        }

        var comp = GetSelectedStrengthComp();
        if (comp == null)
        {
            return false;
        }

        int before = comp.CurrentLevel;
        int target = before + delta;

        if (target < comp.MinLevel || target > comp.MaxLevel)
        {
            return false;
        }

        if (TryGetBlockingStrengthRails(selected, target, _tmpPreviewRails))
        {
            return false;
        }


        bool changed = comp.SetLevel(target);
        if (!changed)
            return false;

        Physics2D.SyncTransforms();

        bool canPlace = IsInsidePlacementFrame(selected, selected.transform.position);

        if (canPlace)
            canPlace = selected.CanPlaceByRuleAtWorld((Vector2)selected.transform.position, grid: grid);

        if (canPlace)
        {
            bool railOverlap = HasRailOverManualCells_CellMap(selected, selected.transform.position);
            canPlace &= !railOverlap;
        }

        if (canPlace && selected.UseManualOccupancy)
        {
            _tmpCells.Clear();
            selected.GetManualOccupiedCellsAtWorld(grid, selected.transform.position, _tmpCells);

            var occ = GridOccupancy2D.Instance;
            if (occ != null && occ.WouldOverlapPOBlockedCells(_tmpCells))
                canPlace = false;
        }

        comp.SetLevel(before);
        Physics2D.SyncTransforms();

        return canPlace;
    }

    public void UI_IncreaseSelectedStrength()
    {
        ChangeSelectedStrength(+1);
    }

    public void UI_DecreaseSelectedStrength()
    {
        ChangeSelectedStrength(-1);
    }

    void ChangeSelectedStrength(int delta)
    {
        if (!CanTransformSelectedBase()) return;

        var comp = GetSelectedStrengthComp();
        if (comp == null) return;

        int target = comp.CurrentLevel + delta;
        if (target < comp.MinLevel || target > comp.MaxLevel)
            return;

        if (TryGetBlockingStrengthRails(selected, target, _tmpPreviewRails))
        {
            return;
        }

        var demoLinks = selected.GetComponentsInChildren<PoDemoLink>(true);
        for (int i = 0; i < demoLinks.Length; i++)
        {
            if (demoLinks[i] != null)
                demoLinks[i].StopDemoIfPlaying();
        }

        bool changed = comp.SetLevel(target);
        if (!changed)
            return;

        Physics2D.SyncTransforms();

        bool canPlace = IsInsidePlacementFrame(selected, selected.transform.position);

        if (canPlace)
            canPlace = selected.CanPlaceByRuleAtWorld((Vector2)selected.transform.position, grid: grid);

        if (canPlace)
        {
            bool railOverlap = HasRailOverManualCells_CellMap(selected, selected.transform.position);
            canPlace &= !railOverlap;
        }

        if (canPlace && selected.UseManualOccupancy)
        {
            _tmpCells.Clear();
            selected.GetManualOccupiedCellsAtWorld(grid, selected.transform.position, _tmpCells);

            var occ = GridOccupancy2D.Instance;
            if (occ != null && occ.WouldOverlapPOBlockedCells(_tmpCells))
                canPlace = false;
        }

        if (!canPlace)
        {
            comp.SetLevel(comp.CurrentLevel - delta);
            Physics2D.SyncTransforms();
            return;
        }

        MarkOccupancyDirty();
        FinalizeRailBindingNow(selected);
        NotifyStageChanged();
    }

    public bool IsSelectedPORailBound()
    {
        return selected != null && IsRailBound(selected);
    }

    bool TryGetBlockingStrengthRails(PlacementObject po, int targetLevel, List<RailSpan2D> outRails)
    {
        outRails.Clear();
        if (po == null) return false;

        var strength = po.GetComponent<StrengthBasedOccupancyCells>();
        if (strength == null) return false;

        var bind = po.GetComponent<RailNodeFollowBinding2D>();
        if (bind == null) return false;

        var entries = GetEntriesNoAlloc(bind, po.transform);
        if (entries == null || entries.Count == 0)
            return false;

        var rails = GetAllRailsCached();
        if (rails == null || rails.Length == 0)
            return false;

        HashSet<RailSpan2D> added = new HashSet<RailSpan2D>();
        bool found = false;

        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            if (e.node == null) continue;
            if (e.anchorPoint == null) continue;
            if (e.node.GetConnectedRailCount() <= 0) continue;

            if (strength.IsTargetOrParentActiveAtLevel(e.anchorPoint, targetLevel))
                continue;

            found = true;

            for (int r = 0; r < rails.Length; r++)
            {
                var rail = rails[r];
                if (rail == null) continue;

                if (rail.startNode == e.node || rail.endNode == e.node)
                {
                    if (added.Add(rail))
                        outRails.Add(rail);
                }
            }
        }

        return found;
    }

    public void PreviewBlockedRailsForRotateHover()
    {
        ClearBlockedRailPreview();

        if (selected == null) return;
        if (!IsRailBound(selected)) return;

        GetAllBoundRails(selected, _tmpPreviewRails);
        SetBlockedRailPreview(_tmpPreviewRails, true);
    }

    public void PreviewBlockedRailsForFlipXHover()
    {
        ClearBlockedRailPreview();

        if (selected == null) return;
        if (!IsRailBound(selected)) return;

        GetAllBoundRails(selected, _tmpPreviewRails);
        SetBlockedRailPreview(_tmpPreviewRails, true);
    }

    public void PreviewBlockedRailsForFlipYHover()
    {
        ClearBlockedRailPreview();

        if (selected == null) return;
        if (!IsRailBound(selected)) return;

        GetAllBoundRails(selected, _tmpPreviewRails);
        SetBlockedRailPreview(_tmpPreviewRails, true);
    }

    public void PreviewBlockedRailsForStrengthHover(int delta)
    {
        ClearBlockedRailPreview();

        if (selected == null) return;

        var comp = GetSelectedStrengthComp();
        if (comp == null) return;

        int target = comp.CurrentLevel + delta;
        if (target < comp.MinLevel || target > comp.MaxLevel)
            return;

        if (TryGetBlockingStrengthRails(selected, target, _tmpPreviewRails))
            SetBlockedRailPreview(_tmpPreviewRails, true);
    }

    public void ClearBlockedRailHoverPreview()
    {
        ClearBlockedRailPreview();
    }

    public bool TryCheckFlipXHoverPreview(out RailSpan2D blockedRail)
    {
        blockedRail = null;

        if (!CanTransformSelectedBase())
            return false;

        if (selected == null)
            return false;

        if (selected.placementData != null && !selected.placementData.allowFlipX)
            return false;

        // 실제 실행 정책은 그대로 유지
        // 레일 바운드면 액션은 금지지만,
        // 호버에서는 별도 PreviewBlockedRailsForFlipXHover()로 처리할 예정
        if (IsRailBound(selected))
            return false;

        Vector3 testScale = selected.transform.localScale;
        testScale.x *= -1f;

        return CheckSelectedTransformPossible(
            selected.transform.position,
            selected.transform.rotation,
            testScale,
            out blockedRail
        );
    }

    public void PreviewBlockedRailForTransformHover(RailSpan2D blockedRail)
    {
        ClearBlockedRailPreview();

        if (blockedRail == null)
            return;

        SetBlockedRailPreview(blockedRail, true);
    }

    public void ResetTransientStateForSceneChange()
    {
        // ✅ 드래그 중이던 undo begin 상태 정리
        if (_stageSaveManager == null)
            _stageSaveManager = FindFirstObjectByType<StageSaveManager>();

        if (_stageSaveManager != null && _poDragUndoBeginNotified)
        {
            _stageSaveManager.NotifyStageChangeBeginCanceled();
            _stageSaveManager.EndDeferredStageChanged(false);
        }

        // ✅ 코루틴 정리
        if (_poDragDeferredCommitCo != null)
        {
            StopCoroutine(_poDragDeferredCommitCo);
            _poDragDeferredCommitCo = null;
        }

        // ✅ 내부 플래그 정리
        _poDragUndoBeginNotified = false;

        // ✅ 선택/드래그/프리뷰/블록 표시 정리
        ClearBlockedRailPreview();
        ResetDragState();
        ClearSelection(forceFinalize: false);
        ClearPlacePreviewObjects();

        // ✅ 선택 잠금 상태도 초기화
        IsSelectedLocked = false;
        SetDragPhase(SelectedDragPhase.None);
    }

    void OnDisable()
    {
        // 씬 전환 / 오브젝트 비활성화 시 잔여 입력 상태 정리
        ResetTransientStateForSceneChange();

        if (Instance == this)
            Instance = null;
    }
}
