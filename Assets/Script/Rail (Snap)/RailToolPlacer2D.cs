using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;

[System.Flags]
public enum RailHoverAction
{
    None = 0,
    CanPlace = 1 << 0,
    CanDrag = 1 << 1,
    NoRailBudget = 1 << 2
}

public class RailToolPlacer2D : MonoBehaviour, IBuildModeTool
{

    #region Inspector
    // =========================================================
    // Refs
    // =========================================================
    [Header("Refs")]
    public GridManager grid;
    public Camera cam;
    public RailSpan2D railPrefab;
    // =========================================================
    // Limits
    // =========================================================
    [Header("Length Limits")]
    public float minLength = 0.5f;
    public float maxLength = 3.0f;
    // =========================================================
    // Preview
    // =========================================================
    [Header("Preview")]
    public LineRenderer preview;
    [SerializeField] Material railPreviewMaterial;
    [SerializeField] string previewSortingLayerName = "Default";
    [SerializeField] int previewSortingOrder = 1900;
    // =========================================================
    // Pick / Selection Masks
    // =========================================================
    [Header("Pick Masks")]
    public LayerMask railMask;
    public LayerMask handleMask;
    // =========================================================
    // Start Node (must)
    // =========================================================
    [Header("Start Node (must)")]
    public LayerMask nodeMask;
    public float nodePickRadius = 0.25f;
    // =========================================================
    // Placement Options
    // =========================================================
    [Header("Placement Option")]
    [Tooltip("연속 설치 동작 자체의 기본값(옵션/세팅이 켜져 있어야 실제로 연속 설치가 활성화됩니다).")]
    public bool continuousFromEnd = true;

    [Header("Placement Option - Continuous (Setting)")]
    [Tooltip("옵션(세부설정)으로 연속 설치를 켜고 끌 수 있게 합니다. UI에서 SetContinuousPlacementOption() 호출로 제어하세요.")]
    [SerializeField] bool useContinuousPlacementOption = true;

    [Tooltip("옵션 기본값(첫 실행/저장값 없을 때)")]
    [SerializeField] bool defaultContinuousPlacementOption = true;

    [Tooltip("PlayerPrefs Key (연속 설치 옵션 저장 키)")]
    [SerializeField] string continuousPlacementPrefsKey = "opt_rail_continuous";

    bool _continuousPlacementOptionCached = true;

    // =========================================================
    // Blocking / Collisions
    // =========================================================
    [Header("Wall Blocking")]
    public LayerMask wallMask;
    public float endpointBlockRadius = 0.12f;
    [Header("Wall Blocking (Segment)")]
    public bool blockIfSegmentHitsWall = true;
    [Header("Wall Ignore Near THIS Connection Nodes")]
    public int ignoreWallNearThisNodeCells = 1;
    [Header("Block With Objects")]
    public LayerMask placedMask;
    // =========================================================
    // Node Capacity
    // =========================================================
    [Header("Node Capacity")]
    public int maxRailsPerNode = 3;
    [Header("Anti Loop / Duplicate")]
    [SerializeField] bool forbidEndOnExistingRailNode = true;
    // =========================================================
    // Drag / Input
    // =========================================================
    [Header("Drag Threshold (Select click vs real drag)")]
    [SerializeField] float dragStartThreshold = 0.2f;
    // =========================================================
    // SnapPoint Attach (Object Connector)
    // =========================================================
    [Header("SnapPoint Attach (Object Connector)")]
    public LayerMask snapPointMask;
    public float snapPointPickRadius = 0.25f;
    public bool allowSnapPointAsRailNode = true;
    // =========================================================
    // Follow Attach (SnapPoint -> Node follow)
    // =========================================================
    // =========================================================
    // Hints
    // =========================================================
    [SerializeField] bool showHintsOnRailPlacement = true;
    [Tooltip("레일 설치 중 end 위치를 '힌트 점'으로만 제한할지")]
    [SerializeField] bool restrictEndToHintDots = true;
    [Tooltip("마우스(그리드 스냅) 위치가 힌트 점에 이 셀 반경 이내면 그 힌트로 클램프. (0이면 항상 가장 가까운 힌트로 클램프)")]
    [SerializeField, Min(0f)] float hintPickRadiusCells = 0f;
    [SerializeField] int railPlacementHintRadiusCells = 10;
    readonly List<Vector2> _hintPts = new List<Vector2>(800);
    [Header("Hints")]
    [SerializeField, Min(1)] int maxHintDots = 800;
    // =========================================================
    // Debug / Perf
    // =========================================================
    [Header("Perf")]
    [SerializeField] bool syncTransformsEachFrame = true;
    [Header("Perf (Occupancy Fast-Path)")]
    [SerializeField] bool useOccupancyForWalls = true;
    [SerializeField] bool useOccupancyForPlaced = true;
    [SerializeField] int fastScanMaxCells = 1200;
    [Header("Auto Bind PO after rail commit")]
    [SerializeField] LayerMask railNodeMask;
    [SerializeField] float railFollowRadius = 0.25f;
    [Header("Rail Snap Local Allow (like PO rule)")]
    [SerializeField] float railSnapLocalAllowCells = 1.0f;
    [SerializeField] float railSnapLocalMaxPenCells = 0.5f;
    [SerializeField] float railAllowedSnapPenetration = 0.0f; // 월드 단위
    [Header("Preview - Endpoint Nodes")]
    [SerializeField] bool showPreviewEndpointNodes = true;
    [SerializeField] float previewNodeDiameter = 0.35f;   // 월드 단위(원하는 크기로 조절)
    [SerializeField] int previewNodeTexSize = 64;         // 원 텍스처 해상도
    [SerializeField] int previewNodeSortingOrder = 2000;  // 레일 프리뷰 위로
    [SerializeField] Color previewNodeOkColor = new Color(0f, 1f, 0f, 0.35f);
    [SerializeField] Color previewNodeBlockedColor = new Color(1f, 0f, 0f, 0.35f);
    [SerializeField] Sprite previewEndpointSprite;
    [Header("Placement Rules (Unified with Handle)")]
    [SerializeField] RailPlacementRuleProfile2D ruleProfile;
    SpriteRenderer _previewStartDot;
    SpriteRenderer _previewEndDot;
    Sprite _previewDotSprite;
    // RailToolPlacer2D 필드에 추가
    readonly Dictionary<int, Coroutine> _followRetryMap = new();

    // =========================================================
    // Pick (Pixel-radius, zoom invariant)
    // =========================================================
    [Header("Pick (Click Ease)")]
    [Tooltip("마우스 주변 '핸들'을 잡는 반경(px). 카메라 줌이 변해도 체감 클릭 크기가 유지됩니다.")]
    [SerializeField, Range(6f, 40f)] float handlePickRadiusPx = 14f;

    [Tooltip("마우스 주변 '레일 본체'를 잡는 반경(px). 카메라 줌이 변해도 체감 클릭 크기가 유지됩니다.")]
    [SerializeField, Range(4f, 30f)] float railPickRadiusPx = 10f;

    RailSpanVisual2D _previewVisual;

    #endregion

    #region Global Busy Flag
    // =========================================================
    // GridPlacer가 PO 선택/드래그 로직을 돌릴 때,
    // 레일툴이 입력을 '먹고 있는지'를 알려주기 위한 플래그.
    // (레일/PO가 동시에 선택되는 UX를 막는 용도)
    // =========================================================
    public static RailToolPlacer2D Instance { get; private set; }

    /// <summary>
    /// 레일툴이 현재 입력(드래그/설치/핸들 조작 등)을 진행 중이면 true.
    /// GridPlacer에서 PO 선택 로직을 잠깐 양보할 때 사용.
    /// </summary>
    public static bool IsInputBusyNow => Instance != null && Instance._IsBusyNow();

    bool _IsBusyNow()
    {
        if (!enabled || !gameObject.activeInHierarchy) return false;
        // 설치 시작점 잡은 상태 / 핸들 드래그 / 노드 클릭 추적 등은 "레일 입력 진행 중"으로 본다.
        if (isDraggingHandle) return true;
        if (handleDragBegun) return true;
        if (isTrackingNodeClick) return true;
        if (hasStart) return true;
        if (activeHandle != null) return true;
        return false;
    }

    void OnEnable()
    {
        Instance = this;
    }

    void OnDisable()
    {
        ResetTransientStateForSceneChange();

        if (Instance == this) Instance = null;

        foreach (var kv in _followRetryMap)
            if (kv.Value != null) StopCoroutine(kv.Value);
        _followRetryMap.Clear();
    }
    #endregion

    #region State
    // placement state
    RailSnapNode2D startNode;
    bool hasStart;
    // snap attach owners (placed overlap exception)
    PlacementObject startAttachOwner;
    PlacementObject endAttachOwner;
    // selection state
    RailSpan2D selectedRail;
    BuildTool lastTool = BuildTool.None;
    RailEndpointHandle2D activeHandle;
    bool isDraggingHandle;
    Vector2 dragMouseDownWorld;
    bool dragMovedEnough;
    bool handleDragBegun;
    float dragStartThresholdSq;

    // ✅ Select 모드: RailSnapNode 클릭(드래그/클릭 분기용)
    bool isTrackingNodeClick;
    RailSnapNode2D trackedNode;
    Vector2 trackedNodeDownWorld;

    Vector2 dragBackupStart, dragBackupEnd;
    bool hasDragBackup;
    Vector2 dragLastValidStart, dragLastValidEnd;
    bool hasDragLastValid;
    bool startNodeCreatedNew; // ✅ start SnapPoint로 "새로 생성된 노드" 임시 여부

    #endregion

    #region PERF Caches
    readonly Dictionary<RailSnapNode2D, int> nodeRailCount = new();
    int nodeCountFrame = -1;
    readonly HashSet<ulong> edgeSet = new();
    int edgeSetFrame = -1;
    readonly Dictionary<Vector2Int, bool> wallCache = new();
    int wallCacheFrame = -1;
    GridOccupancy2D occ;
    int occEnsureFrame = -1;
    int occMaskSyncFrame = -1;
    // ✅ FindObjectsByType 캐시(프레임 단위)
    RailSpan2D[] _railsCache;
    int _railsCacheFrame = -1;
    // ✅ SnapPoint 캐시(프레임 단위)
    // - 설치 힌트에서 OverlapCircle로 SnapPoint를 못 잡는(콜라이더 없음/레이어 불일치) 케이스 보정
    SnapPoint[] _snapPointsCache;
    int _snapPointsCacheFrame = -1;
    // ✅ ignoreOwners 재사용 버퍼 (GC 방지)
    readonly List<PlacementObject> _tmpIgnoreOwners = new List<PlacementObject>(2);
    static readonly Collider2D[] _nodeHitsAny = new Collider2D[32];
    SnapPoint startSnapPoint;
    SnapPoint endSnapPoint;
    // 드래그 시작 때 "연결 끊은 owner"는 이번 드래그 동안 자동 Follow 재결합 금지
    readonly HashSet<int> _suppressFollowOwnerIds = new HashSet<int>();
    // ✅ 드래그 판정 순간(Threshold 통과) Detach를 위한 pending
    bool _pendingDetach;
    bool _pendingDetachIsStart;
    RailSpan2D _pendingDetachRail;
    // ✅ 실제 Detach가 일어난 경우, 실패/취소 시 원복용
    PlacementObject _dragDetachedOwner;
    bool _dragDetachedPrevAuto;

    // ✅ 레일 배치 힌트 1회 생성 캐시
    bool _railPlacementHintsBuilt = false;
    int _railPlacementHintsStartNodeId = 0;
    Vector2 _railPlacementHintsStartPos = default;

    // ✅ Preview end를 커밋에서 그대로 쓰기 위한 캐시
    Vector2 _cachedPreviewEndPos;
    bool _cachedPreviewEndUsable; // 프리뷰가 "설치 가능" 상태일 때만 true

    #endregion

    #region Unity
    void Awake()
    {
        if (cam == null) cam = Camera.main;
        dragStartThresholdSq = dragStartThreshold * dragStartThreshold;
        EnsurePreview();
        EnsureOccRef();
        LoadContinuousPlacementOption();
    }

    #region Sound
    void PlayRailPlaceSound()
    {
        if (UISoundManager.I == null) return;
        UISoundManager.I.PlayRailPlace();
    }

    void PlayRailSelectSound()
    {
        if (UISoundManager.I == null) return;
        UISoundManager.I.PlayRailSelect();
    }

    void PlayRailDeselectSound()
    {
        if (UISoundManager.I == null) return;
        UISoundManager.I.PlayRailDeselect();
    }

    void PlayRailDeleteSound()
    {
        if (PuzzleSnapshotManager.SuppressDeleteSfx) return;
        if (UISoundManager.I == null) return;
        UISoundManager.I.PlayRailDelete();
    }
    #endregion

    // ======================
    // Options (Continuous Placement)
    // ======================
    void LoadContinuousPlacementOption()
    {
        if (!useContinuousPlacementOption)
        {
            _continuousPlacementOptionCached = true; // 옵션 미사용이면 항상 ON 취급
            return;
        }
        _continuousPlacementOptionCached = PlayerPrefs.GetInt(continuousPlacementPrefsKey, defaultContinuousPlacementOption ? 1 : 0) == 1;
    }

    void SaveContinuousPlacementOption()
    {
        if (!useContinuousPlacementOption) return;
        PlayerPrefs.SetInt(continuousPlacementPrefsKey, _continuousPlacementOptionCached ? 1 : 0);
        PlayerPrefs.Save();
    }

    /// <summary>UI/옵션 메뉴에서 호출: 연속 설치 옵션을 켜고 끕니다.</summary>
    public void SetContinuousPlacementOption(bool on, bool save = true)
    {
        _continuousPlacementOptionCached = on;
        if (save) SaveContinuousPlacementOption();
    }

    bool IsContinuousPlacementEnabled()
    {
        // 기본 동작(continuousFromEnd) AND 옵션이 켜져 있을 때만
        return continuousFromEnd && (!useContinuousPlacementOption || _continuousPlacementOptionCached);
    }
    void Update()
    {
        if (!IsBuildMode())
        {
            ResetAllStatesOnExitBuildMode();
            lastTool = BuildTool.None;
            return;
        }
        if (cam == null) cam = Camera.main;
        if (cam == null || grid == null) return;
        if (syncTransformsEachFrame)
            Physics2D.SyncTransforms();
        EnsureFrameCaches();
        EnsureOccBakedAndMasks();
        BuildTool tool = BuildToolManager.Instance != null
            ? BuildToolManager.Instance.currentTool
            : BuildTool.None;
        if (tool != lastTool)
            OnToolChanged(tool);
        switch (tool)
        {
            case BuildTool.Select:
                HandleRailSelection();
                break;
            default:
                CancelPlacementPreview("NotRailTool");
                break;
        }
    }


    #endregion

    #region BuildMode / Tool
    bool IsBuildMode()
    {
        if (GameModeManager.Instance == null) return true;
        return GameModeManager.Instance.currentMode == GameMode.Build;
    }



    // =========================================================
    // Rail Budget Gate
    // =========================================================
    bool CanStartNewRailPlacement()
    {
        var budget = RailBudget2D.Instance;
        if (budget == null) return true;
        if (!budget.IsLimited) return true;
        return budget.CanSpend(1);
    }

    public void OnEnterBuildMode() { }

    public void OnExitBuildMode()
    {
        ResetAllStatesOnExitBuildMode();
    }
    void ResetAllStatesOnExitBuildMode()
    {
        CancelPlacementPreview();
        CancelDraggingHandle();
        DeselectRail();
        SetAllRailsEditVisible(false);
        SetPreviewVisible(false);
    }
    void OnToolChanged(BuildTool newTool)
    {
        CancelPlacementPreview($"ToolChanged {lastTool}->{newTool}");
        CancelDraggingHandle();
        bool isSelect = (newTool == BuildTool.Select);
        SetAllRailsEditVisible(isSelect);
        if (!isSelect)
            DeselectRail();
        lastTool = newTool;
    }
    void SetAllRailsEditVisible(bool visible)
    {
        var rails = GetAllRailsCached();
        foreach (var r in rails)
        {
            if (r == null) continue;
            r.SetEditCamera(cam);
            r.SetEditModeVisible(visible);
            ApplyRailConfig(r);
        }
    }
    void ApplyRailConfig(RailSpan2D r)
    {
        r.wallMask = wallMask;
        r.endpointBlockRadius = endpointBlockRadius;
        r.blockIfSegmentHitsWall = blockIfSegmentHitsWall;
        r.ignoreWallNearThisNodeCells = ignoreWallNearThisNodeCells;
        r.maxRailsPerNode = maxRailsPerNode;
        r.placedMask = placedMask; // ✅ 추가
        r.railMask = railMask;     // ✅ 추가
    }

    #endregion

    void EnsureRailPlacementHintsBuiltOnce(Vector2 startPos)
    {
        if (!showHintsOnRailPlacement || !restrictEndToHintDots) return;

        if (hasStart && startNode == null)
        {
            CancelPlacementPreview("StartNodeDestroyedBeforeHints");
            return;
        }

        if (!hasStart || startNode == null) return;

        int sid = startNode.GetInstanceID();

        // start가 같고 이미 만들어졌으면 재생성 금지
        if (_railPlacementHintsBuilt && _railPlacementHintsStartNodeId == sid && _railPlacementHintsStartPos == startPos)
            return;

        BuildRailPlacementHintPoints(startPos);

        _railPlacementHintsBuilt = true;
        _railPlacementHintsStartNodeId = sid;
        _railPlacementHintsStartPos = startPos;
    }


    #region Placement (Rail Tool)
    void HandleRailPlacement()
    {
        if (hasStart && startNode == null)
        {
            CancelPlacementPreview("StartNodeDestroyed");
            return;
        }

        // ✅ 레일 설치 중 UI를 클릭하면 설치 취소
        if (Input.GetMouseButtonDown(0) && IsPointerOverUI())
        {
            CancelPlacementPreview("ClickedUIWhilePlacing");

            isTrackingNodeClick = false;
            trackedNode = null;

            return;
        }

        // ✅ Rail budget이 꽉 찼으면, 설치 시도 자체를 막는다(프리뷰/힌트도 안 뜨게)
        if (!CanStartNewRailPlacement())
        {
            CancelPlacementPreview("BudgetFull");
            return;
        }


        Vector2 mouse = cam.ScreenToWorldPoint(Input.mousePosition);

        // ✅ UpdatePreview는 이제 힌트 재빌드 안 함(표시만)
        UpdatePreview(mouse);

        if (Input.GetMouseButtonDown(1))
        {
            CancelPlacementPreview("RightClick");

            // ✅ Rail 툴에서의 설치 취소는 Select로 전환.
            //    (Select 모드에서 hasStart로 설치 중일 때는 툴 전환 없이 그냥 취소)
            if (BuildToolManager.Instance != null)
                BuildToolManager.Instance.SetTool(BuildTool.Select);
            // ✅ 취소 시 힌트 캐시 초기화
            _railPlacementHintsBuilt = false;
            _railPlacementHintsStartNodeId = 0;
            return;
        }

        if (!Input.GetMouseButtonDown(0))
            return;

        // 1) choose start
        if (!hasStart)
        {

            // ✅ 레일 설치 시작(첫 클릭) 시, 기존에 선택되어 있던 레일이 있다면 해제
            if (selectedRail != null)
                DeselectRail();

            if (TryPickStartNode(mouse, out var pickedNode, out var pickedOwner, out bool createdNew, out var sp))
            {
                // ✅ 노드 용량 체크: 꽉 찼으면 start 확정 금지 + 고스트/힌트 정리
                EnsureFrameCaches();
                if (!CanAddRailToNode(pickedNode))
                {
                    CancelPlacementPreview("NodeFullStart");
                    hasStart = false;
                    startNode = null;

                    _railPlacementHintsBuilt = false;
                    _railPlacementHintsStartNodeId = 0;
                    _hintPts.Clear();
                    MoveHintBroker2D.Instance?.Clear(this);
                    return;
                }

                startNode = pickedNode;
                startAttachOwner = pickedOwner;
                startSnapPoint = sp;
                startNodeCreatedNew = createdNew;
                hasStart = true;

                EnsureRailPlacementHintsBuiltOnce(startNode.WorldPos);
                MoveHintBroker2D.Instance?.Request(this, priority: 200, _hintPts);
            }
            return;
        }

        if (startNode == null)
        {
            CancelPlacementPreview("StartNodeDestroyed");
            return;
        }

        Vector2 startPos = startNode.WorldPos;

        // 2) choose end
        // 2) choose end
        // ✅ 커밋은 프리뷰에서 확정된 END만 사용 (프리뷰=커밋 100% 일치)
        if (!_cachedPreviewEndUsable)
        {
            CancelPlacementPreview("ClickedEmptyOrInvalidWhilePlacing");
            DeselectRail();
            return;
        }

        Vector2 endMouse = _cachedPreviewEndPos;


        // ✅ 추가: 이전 프레임/이전 프리뷰 SnapPoint 잔존 방지
        endSnapPoint = null;
        endAttachOwner = null;

        // ✅ 여기서 힌트 클램프/그리드 스냅을 다시 하지 않는다(프리뷰와 달라지는 원인 제거)
        // if (showHintsOnRailPlacement && restrictEndToHintDots) { ... }  <-- 삭제/비활성화

        if (!TryResolveEndForPlacement(
            startPos, endMouse,
            out RailSnapNode2D endNode,
            out Vector2 endPos,
            out bool endAllowedByExistingNodeInWall,
            out bool endNodeCreatedNew,
            out PlacementObject endOwner))
        {
            return;
        }

        endAttachOwner = endOwner;

        if (allowSnapPointAsRailNode && TryPickSnapPoint(endPos, snapPointPickRadius, out var spEnd))
        {
            endSnapPoint = spEnd;
            endAttachOwner = GetSnapOwner(spEnd);
            if (endAttachOwner != null)
                endAttachOwner.AutoRailAttach = true;
        }

        // --- (이하 설치/커밋 로직은 너 기존 코드 그대로 유지) ---

        var railNew = Instantiate(railPrefab);
        ApplyRailConfig(railNew);
        RailGraphDirty.MarkDirty();

        railNew.minLength = minLength;
        railNew.maxLength = maxLength;
        railNew.maxRailsPerNode = maxRailsPerNode;
        railNew.SetEditCamera(cam);
        railNew.Initialize(grid, startPos, endPos);

        railNew.startNode = startNode;
        railNew.endNode = endNode;
        railNew.Refresh(syncFromNodes: true);

        railNew.startNode?.RegisterRail(railNew);
        railNew.endNode?.RegisterRail(railNew);

        RailEdgeRegistry2D.Register(railNew.gameObject.GetInstanceID(), railNew.StartWorld, railNew.EndWorld);

        railNew.SetEditModeVisible(true);

        // 설치 직후 선택
        SelectRail(railNew);

        AddRailCount(startNode);
        AddRailCount(endNode);

        var budget = RailBudget2D.Instance;
        if (budget != null)
            budget.TrySpend(1);

        // ✅ 설치 완료 소리
        PlayRailPlaceSound();

        RailGraphRevision.Bump("RailPlaced");

        if (RailGraphDirty.dirty)
        {
            RailGraphDirty.dirty = false;
            RailGraphCleanup2D.Cleanup();
            RebuildNodeRailCountAndEdges();
            nodeCountFrame = Time.frameCount;
            edgeSetFrame = Time.frameCount;
        }

        endNode = railNew.endNode;
        startNode = railNew.startNode;

        if (startAttachOwner != null)
            _suppressFollowOwnerIds.Remove(startAttachOwner.GetInstanceID());
        if (endAttachOwner != null)
            _suppressFollowOwnerIds.Remove(endAttachOwner.GetInstanceID());

        // ✅ "이번 커밋에서" SnapPoint를 집어서 레일 노드를 만든 경우
        //    스캔 기반 자동 Attach(EnsureFollow)만 믿지 말고, 해당 endpoint 1개만 확정 바인딩을 먼저 박는다.
        //    (그래야 "PO랑 연결되었는지"가 커밋 직후 바로 확실해짐)
        if (startAttachOwner != null && startSnapPoint != null && railNew.startNode != null)
            TryAttachEndpointToSnapPoint_Single(startAttachOwner, railNew.startNode, startSnapPoint.transform);

        if (endAttachOwner != null && endSnapPoint != null && railNew.endNode != null)
            TryAttachEndpointToSnapPoint_Single(endAttachOwner, railNew.endNode, endSnapPoint.transform);

        EnsureFollowForOwner(startAttachOwner, force: true);
        EnsureFollowForOwner(endAttachOwner, force: true);
        RebindNearbyPOs(startPos);
        RebindNearbyPOs(endPos);

        var gp = GridPlacer.Instance;
        if (gp != null)
        {
            if (startAttachOwner != null)
                gp.FinalizeRuntimeRailBinding(startAttachOwner);

            if (endAttachOwner != null && endAttachOwner != startAttachOwner)
                gp.FinalizeRuntimeRailBinding(endAttachOwner);
        }

        railNew.Refresh(syncFromNodes: true);
        Physics2D.SyncTransforms();

        startNode = railNew.startNode;
        endNode = railNew.endNode;

        startNodeCreatedNew = false;

        CommitStageChanged();

        var save = FindFirstObjectByType<StageSaveManager>();
        if (save != null)
            save.ForceSaveNow(save.GetCurrentStageIdForUndo());

        if (edgeSetFrame == Time.frameCount && startNode != null && endNode != null)
            edgeSet.Add(EdgeKey(startNode, endNode));

        // 8) continuous
        // ✅ SnapPoint(=PO의 스냅포인트)로 연결된 경우에는, 옵션이 켜져 있어도 연속 설치를 강제로 끈다.
        bool usedPoSnapPointThisCommit =
            (startAttachOwner != null && startSnapPoint != null) ||
            (endAttachOwner != null && endSnapPoint != null);

        bool doContinuous = IsContinuousPlacementEnabled() && !usedPoSnapPointThisCommit;

        if (doContinuous)
        {
            if (selectedRail != null)
                DeselectRail();

            startNode = railNew.endNode;
            startAttachOwner = endAttachOwner;
            startNodeCreatedNew = false;

            hasStart = (startNode != null && CanAddRailToNode(startNode));

            if (!hasStart)
            {
                startNode = null;
                startAttachOwner = null;
            }

            endAttachOwner = null;

            // ✅ 연속 배치로 start가 바뀌었으니, 힌트도 새 start 기준으로 "1회" 다시 생성
            if (hasStart && startNode != null)
            {
                _railPlacementHintsBuilt = false; // 새 start이므로 강제로 재생성 허용
                EnsureRailPlacementHintsBuiltOnce(startNode.WorldPos);
                MoveHintBroker2D.Instance?.Request(this, priority: 200, _hintPts);
            }
            else
            {
                _railPlacementHintsBuilt = false;
                _railPlacementHintsStartNodeId = 0;
                _hintPts.Clear();
                MoveHintBroker2D.Instance?.Clear(this);
            }
        }
        else
        {
            startNodeCreatedNew = false;

            // ✅ 배치 종료면 힌트 캐시 초기화
            _railPlacementHintsBuilt = false;
            _railPlacementHintsStartNodeId = 0;

            CancelPlacementPreview();
        }
    }

    bool TryPickStartNode(
    Vector2 mouseWorld,
    out RailSnapNode2D node,
    out PlacementObject owner,
    out bool createdNew,
    out SnapPoint pickedSnapPoint
)
    {
        node = null;
        owner = null;
        createdNew = false;
        pickedSnapPoint = null; // ✅ 무조건 초기화(컴파일 필수)
        // ✅ START도 SnapPoint면: 설치용으로 노드 GetOrCreate + owner 확보
        // fallback: Node 픽
        if (RailSnapNodeUtil.TryPickNode(mouseWorld, nodePickRadius, nodeMask, out var picked))
        {
            node = picked;
            createdNew = false;
            // ✅ Node로 시작했더라도, 근처 SnapPoint가 있으면 owner + pickedSnapPoint 잡아주기
            if (allowSnapPointAsRailNode && TryPickSnapPoint(node.WorldPos, snapPointPickRadius, out var sp2))
            {
                pickedSnapPoint = sp2;            // ✅ 추가
                owner = GetSnapOwner(sp2);
            }
            return true;
        }
        return false;
    }
    void CancelPlacementPreview(string reason = "")
    {
        bool hasAnythingToCancel =
            hasStart ||
            startNode != null ||
            startNodeCreatedNew ||
            startAttachOwner != null ||
            (preview != null && preview.enabled);
        if (!hasAnythingToCancel)
            return;
        if (_previewStartDot != null) _previewStartDot.enabled = false;
        if (_previewEndDot != null) _previewEndDot.enabled = false;
        if (startNodeCreatedNew && startNode != null)
            Destroy(startNode.gameObject);
        startNodeCreatedNew = false;
        hasStart = false;
        startNode = null;
        startAttachOwner = null;
        startSnapPoint = null;
        endSnapPoint = null;
        SetPreviewVisible(false);

        if (_previewVisual != null)
        {
            _previewVisual.ApplyTint(Color.clear);
            _previewVisual.MarkAllDirty();
        }
        MoveHintBroker2D.Instance?.Clear(this);
    }
    // ======================
    // Unified Rule Probe
    // ======================
    RailSpan2D _ruleProbeRail;
    RailSpan2D GetRuleProbeRail()
    {
        if (_ruleProbeRail != null) return _ruleProbeRail;
        _ruleProbeRail = Instantiate(railPrefab);
        _ruleProbeRail.name = "RailRuleProbe";
        _ruleProbeRail.gameObject.SetActive(false);
        ApplyRailConfig(_ruleProbeRail);
        _ruleProbeRail.minLength = minLength;
        _ruleProbeRail.maxLength = maxLength;
        _ruleProbeRail.maxRailsPerNode = maxRailsPerNode;
        _ruleProbeRail.SetEditCamera(cam);
        _ruleProbeRail.grid = grid;
        _ruleProbeRail.blockIfSegmentHitsWall = blockIfSegmentHitsWall;
        return _ruleProbeRail;
    }

    bool ValidatePlacementSpanCandidate(Vector2 startPos, Vector2 endPos)
    {
        if (grid == null) return false;

        var probe = GetRuleProbeRail();
        var prof = ruleProfile;

        LayerMask wMask = (prof != null && prof.wallMask.value != 0) ? prof.wallMask : wallMask;
        LayerMask pMask = (prof != null && prof.placedMask.value != 0) ? prof.placedMask : placedMask;
        LayerMask rMask = (prof != null && prof.railMask.value != 0) ? prof.railMask : railMask;

        float allowRad = (prof != null && prof.endpointAllowRadius > 0f) ? prof.endpointAllowRadius : endpointBlockRadius;

        return RailPlacementRules2D.CanPlaceRailSpan_WithMasks(
            grid, probe, startPos, endPos,
            wMask, pMask, rMask,
            allowStartInsideWall: false,
            allowEndInsideWall: false,
            endpointAllowRadius: allowRad,
            ignoreOwners: null,
            ignoreRail: null,
            placedOwnerAllowPenetration: 0f,
            useSegmentWallCheck: blockIfSegmentHitsWall,
            ignoreOwnerRelaxTotalCells: 1f,
            endpointCellOnlyA: false,
            endpointCellOnlyB: false
        );
    }

    /// <summary>
    /// Handle/Preview/Hint 공통: 후보 위치가 기존 RailSnapNode2D에 충분히 가깝다면 그 노드로 병합(스냅)한다.
    /// (규칙/충돌 판정은 Span 검증에서 처리하고, 여기서는 "병합 대상 결정"만 담당)
    /// </summary>
    void ResolveMergeTarget_NoSync(
        Vector2 rawCandidate,
        RailSnapNode2D excludeNode,
        out Vector2 resolvedPos,
        out RailSnapNode2D mergeTarget
    )
    {
        mergeTarget = null;
        resolvedPos = rawCandidate;
        if (RailSnapNodeUtil.TryPickNode(rawCandidate, nodePickRadius, nodeMask, out var picked))
        {
            if (picked != null && picked != excludeNode)
            {
                mergeTarget = picked;
                resolvedPos = picked.WorldPos;
            }
        }
    }
    static Color PreviewGreen(float a) => new Color(0f, 1f, 0f, a);
    static Color PreviewRed(float a) => new Color(1f, 0f, 0f, a);

    #endregion

    #region Preview
    void UpdatePreview(Vector2 mouseWorld)
    {
        if (preview == null) EnsurePreview();
        if (preview == null) return;

        if (!hasStart || startNode == null)
        {
            if (hasStart && startNode == null)
                CancelPlacementPreview("StartNodeDestroyedInPreview");

            SetPreviewVisible(false);
            MoveHintBroker2D.Instance?.Clear(this);
            UpdatePreviewEndpointDots(Vector2.zero, Vector2.zero, blocked: true);
            _cachedPreviewEndUsable = false;
            return;
        }


        Vector2 startPos = startNode.WorldPos;

        // ✅ 여기서는 힌트 재빌드 금지: 이미 만들어진 _hintPts만 표시
        if (showHintsOnRailPlacement && hasStart && startNode != null)
        {
            MoveHintBroker2D.Instance?.Request(this, priority: 200, _hintPts);
        }
        else
        {
            _hintPts.Clear();
            MoveHintBroker2D.Instance?.Clear(this);
        }

        // end candidate (grid clamp)
        Vector2 endCandidateRaw = RailGridUtil.GetSnappedClampedEnd(
            grid, startPos, mouseWorld, minLen: 0f, maxLen: maxLength
        );

        // SnapPoint 우선 (마우스가 스냅포인트 위면 그 좌표)
        if (allowSnapPointAsRailNode && TryPickSnapPoint(mouseWorld, snapPointPickRadius, out var sp))
            endCandidateRaw = sp.transform.position;

        bool hintFail = false;
        Vector2 endCandidate = endCandidateRaw;

        if (showHintsOnRailPlacement && restrictEndToHintDots)
        {
            Vector2 desiredForClamp = endCandidateRaw;
            if (!(allowSnapPointAsRailNode && TryPickSnapPoint(endCandidateRaw, snapPointPickRadius, out var _)))
                desiredForClamp = SnapWorldToGrid(endCandidateRaw);

            if (TryClampToNearestHint(desiredForClamp, out var clamped))
                endCandidate = clamped;
            else
                hintFail = true;
        }

        bool startFull = !CanAddRailToNode(startNode);

        bool okResolve = TryResolveEndForPreview(
            endCandidate,
            out RailSnapNode2D previewEndNode,
            out Vector2 endPreview,
            out bool endAllowed,
            out bool endAllowedByExistingNodeInWall
        );

        if (!okResolve)
        {
            SetPreviewVisible(false);
            MoveHintBroker2D.Instance?.Clear(this);
            UpdatePreviewEndpointDots(Vector2.zero, Vector2.zero, blocked: true);

            _cachedPreviewEndUsable = false;   // ✅ 추가
            return;
        }

        // (정리) MergeTarget 검색에 Collider 쿼리가 걸릴 수 있어, 필요할 때만 SyncTransforms
        if (!syncTransformsEachFrame) Physics2D.SyncTransforms();

        ResolveMergeTarget_NoSync(endPreview, excludeNode: null, out var resolvedPreviewEnd, out var mergeTargetPrev);
        if (mergeTargetPrev != null)
        {
            previewEndNode = mergeTargetPrev;
            endPreview = mergeTargetPrev.WorldPos;
        }
        else
        {
            endPreview = resolvedPreviewEnd;
        }
        // (정리) 설치 가능/불가 판정에 영향을 주지 않는 probe/endpointCellOnly 계산은 제거

        bool blockedRules = !endAllowed || hintFail;

        bool blockedLen = !IsLengthValid(startPos, endPreview);
        // ✅ Duplicate (position-based, O(1))
        bool duplicate = RailEdgeRegistry2D.Contains(startPos, endPreview);
        bool blockedSpan = false;
        if (!blockedRules && !blockedLen && !duplicate)
            blockedSpan = !ValidatePlacementSpanCandidate(startPos, endPreview);

        bool blockedBudget = false;
        var budget = RailBudget2D.Instance;
        if (budget != null && budget.IsLimited)
            blockedBudget = !budget.CanSpend(1);

        bool blocked = startFull || blockedRules || duplicate || blockedLen || blockedSpan || blockedBudget;

        _cachedPreviewEndUsable = !blocked;     // ✅ "설치 가능"일 때만 커밋 허용
        _cachedPreviewEndPos = endPreview;      // ✅ 프리뷰에서 확정된 END를 그대로 저장

        Color okC = (railPrefab != null) ? railPrefab.NormalColor : Color.white;
        Color badC = (railPrefab != null) ? railPrefab.BlockedColor : Color.red;

        preview.startColor = blocked ? badC : okC;
        preview.endColor = blocked ? badC : okC;

        SetPreviewVisible(true);
        preview.SetPosition(0, startPos);
        preview.SetPosition(1, endPreview);

        if (_previewVisual != null)
        {
            _previewVisual.ApplyTint(blocked ? badC : okC);
            _previewVisual.MarkAllDirty();
        }


        UpdatePreviewEndpointDots(startPos, endPreview, blocked);
    }

    void EnsurePreview()
    {
        if (preview != null) return;

        var go = new GameObject("RailPreview");
        go.transform.SetParent(transform, worldPositionStays: false);

        preview = go.AddComponent<LineRenderer>();
        preview.positionCount = 2;
        preview.useWorldSpace = true;
        preview.enabled = false;

        float w = (railPrefab != null ? railPrefab.thickness : 0.12f);
        preview.startWidth = w;
        preview.endWidth = w;
        preview.numCapVertices = 6;
        preview.numCornerVertices = 0;

        if (railPreviewMaterial != null)
            preview.sharedMaterial = railPreviewMaterial;

        preview.textureMode = LineTextureMode.Tile;
        preview.alignment = LineAlignment.View;
        preview.startColor = preview.endColor = Color.white;

        preview.sortingLayerName = previewSortingLayerName;
        preview.sortingOrder = previewSortingOrder;

        // 실제 레일 비주얼 복제
        if (railPrefab != null)
        {
            var prefabVisual = railPrefab.GetComponent<RailSpanVisual2D>();
            if (prefabVisual != null)
            {
                _previewVisual = go.AddComponent<RailSpanVisual2D>();
                _previewVisual.CopySettingsFrom(
                    prefabVisual,
                    preview,
                    previewSortingLayerName,
                    0
                );
                _previewVisual.SyncNow();
            }
        }

        EnsurePreviewEndpointDots();
        SetPreviewVisible(false);
    }
    void UpdatePreviewEndpointDots(Vector2 startPos, Vector2 endPos, bool blocked)
    {
        if (!showPreviewEndpointNodes) { SetPreviewDotsEnabled(false); return; }
        EnsurePreviewEndpointDots();
        if (_previewStartDot == null || _previewEndDot == null)
            return;

        if (preview == null || !preview.enabled)
        {
            SetPreviewDotsEnabled(false);
            return;
        }

        SetPreviewDotsEnabled(true);

        Color c = blocked ? previewNodeBlockedColor : previewNodeOkColor;
        _previewStartDot.color = c;
        _previewEndDot.color = c;

        _previewStartDot.transform.position = new Vector3(startPos.x, startPos.y, 0f);
        _previewEndDot.transform.position = new Vector3(endPos.x, endPos.y, 0f);

        var s = new Vector3(previewNodeDiameter, previewNodeDiameter, 1f);
        _previewStartDot.transform.localScale = s;
        _previewEndDot.transform.localScale = s;
    }
    void SetPreviewDotsEnabled(bool on)
    {
        if (_previewStartDot != null) _previewStartDot.enabled = on;
        if (_previewEndDot != null) _previewEndDot.enabled = on;
    }
    void EnsurePreviewEndpointDots()
    {
        if (_previewStartDot != null && _previewEndDot != null) return;

        // 1) 스프라이트 준비
        if (_previewDotSprite == null)
        {
            if (previewEndpointSprite != null)
                _previewDotSprite = previewEndpointSprite;
            else
                _previewDotSprite = CreateCircleSprite(previewNodeTexSize);
        }

        // 2) Start Dot
        if (_previewStartDot == null)
        {
            var goA = new GameObject("RailPreview_StartDot");
            goA.transform.SetParent(preview.transform, worldPositionStays: true);
            _previewStartDot = goA.AddComponent<SpriteRenderer>();
            _previewStartDot.sprite = _previewDotSprite;
            _previewStartDot.sortingLayerName = preview.sortingLayerName;
            _previewStartDot.sortingOrder = previewNodeSortingOrder;
            _previewStartDot.enabled = false;
        }

        // 3) End Dot
        if (_previewEndDot == null)
        {
            var goB = new GameObject("RailPreview_EndDot");
            goB.transform.SetParent(preview.transform, worldPositionStays: true);
            _previewEndDot = goB.AddComponent<SpriteRenderer>();
            _previewEndDot.sprite = _previewDotSprite;
            _previewEndDot.sortingLayerName = preview.sortingLayerName;
            _previewEndDot.sortingOrder = previewNodeSortingOrder;
            _previewEndDot.enabled = false;
        }
    }
    // 텍스처에서 원(Alpha) 만들어 Sprite로 변환
    Sprite CreateCircleSprite(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        float r = (size - 1) * 0.5f;
        float r2 = r * r;
        float cx = r, cy = r;
        var cols = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - cx;
                float dy = y - cy;
                float d2 = dx * dx + dy * dy;
                // 부드러운 가장자리(안티앨리어싱 느낌)
                float t = Mathf.Clamp01((r2 - d2) / (r * 1.5f));
                byte a = (byte)Mathf.RoundToInt(255f * t);
                cols[y * size + x] = new Color32(255, 255, 255, a);
            }
        }
        tex.SetPixels32(cols);
        tex.Apply(false, true);
        // pixelsPerUnit = size 로 하면 스프라이트 한 변이 "1 유닛"이 됨
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), pixelsPerUnit: size);
    }

    #endregion

    #region Selection / Handle Drag
    // =========================================================
    // Select mode: Click vs Drag branching helpers
    // =========================================================
    // =========================================================
    // Select mode에서도 툴 전환 없이 레일 설치를 시작한다.
    // - node 클릭 / handle 클릭(임계 미만)에서 호출
    // - 이후 Select 루프에서 hasStart==true면 HandleRailPlacement()를 돌려서
    //   프리뷰/커밋 로직을 그대로 재사용한다.
    // =========================================================
    void BeginPlacementFromNode_Select(RailSnapNode2D node)
    {
        if (node == null) return;

        // ✅ Rail budget이 꽉 찼으면 Select에서 설치 시작도 막는다
        if (!CanStartNewRailPlacement())
            return;

        EnsureFrameCaches(); // nodeRailCount 최신화

        // ✅ NEW RULE: 스냅포인트가 있고 + 이미 연결된 레일이 1개 이상이면 시작 금지
        SnapPoint sp = null;
        if (allowSnapPointAsRailNode && TryPickSnapPoint(node.WorldPos, snapPointPickRadius, out var sp2))
            sp = sp2;

        if (sp != null)
        {
            nodeRailCount.TryGetValue(node, out int connected);
            if (connected >= 1)
            {
                CancelPlacementPreview("StartBlocked_SnapAndHasRail");
                hasStart = false;
                startNode = null;

                _railPlacementHintsBuilt = false;
                _railPlacementHintsStartNodeId = 0;
                _hintPts.Clear();
                MoveHintBroker2D.Instance?.Clear(this);
                return;
            }
        }

        // ✅ 노드 용량 체크: 꽉 찼으면 시작 자체 금지 + 고스트/힌트 정리
        if (!CanAddRailToNode(node))
        {
            CancelPlacementPreview("NodeFull");
            hasStart = false;
            startNode = null;

            _railPlacementHintsBuilt = false;
            _railPlacementHintsStartNodeId = 0;
            _hintPts.Clear();
            MoveHintBroker2D.Instance?.Clear(this);
            return;
        }

        if (selectedRail != null)
            DeselectRail();

        // ✅ Select에서 시작했어도, 설치는 Rail 툴 플로우(Preview/Commit)를 그대로 재사용
        CancelDraggingHandle();
        CancelPlacementPreview("BeginPlacementFromSelect");

        startNode = node;
        hasStart = true;
        startNodeCreatedNew = false;
        startAttachOwner = null;
        startSnapPoint = null;
        endAttachOwner = null;
        endSnapPoint = null;

        // ✅ 새 start 기준 힌트 1회 구축
        _railPlacementHintsBuilt = false;
        _railPlacementHintsStartNodeId = 0;
        EnsureRailPlacementHintsBuiltOnce(startNode.WorldPos);
        MoveHintBroker2D.Instance?.Request(this, priority: 200, _hintPts);
    }

    void BeginPlacementFromHandleClick_Select(RailEndpointHandle2D handle)
    {
        if (handle == null) return;
        var rail = handle.GetComponentInParent<RailSpan2D>();
        if (rail == null) return;

        // 노드가 없으면 확보(기존 선택 로직과 동일)
        if (RailSnapNodeManager.Instance != null)
        {
            bool created = false;
            if (rail.startNode == null)
            {
                var any = FindNearestAnyNodeIncludingFollow(rail.start);
                rail.startNode = (any != null) ? any : RailSnapNodeManager.Instance.GetOrCreate(rail.start);
                created = true;
            }
            if (rail.endNode == null)
            {
                var any = FindNearestAnyNodeIncludingFollow(rail.end);
                rail.endNode = (any != null) ? any : RailSnapNodeManager.Instance.GetOrCreate(rail.end);
                created = true;
            }
            rail.Refresh(syncFromNodes: created);
        }

        // handle이 어느 쪽인지 판정
        bool wantStart = (handle == rail.GetHandle(true));
        var node = wantStart ? rail.startNode : rail.endNode;

        BeginPlacementFromNode_Select(node);
    }


    // 파일 상단에 필요하면 추가
    // using System.Collections.Generic;

    // using System.Collections.Generic; // 필요하면 파일 상단에 추가

    void HandleRailSelection()
    {
        if (selectedRail == null && isDraggingHandle)
        {
            CancelDraggingHandle();
            return;
        }

        // ✅ Select 상태에서도 레일 설치 중 UI 클릭하면 취소
        if (Input.GetMouseButtonDown(0) && IsPointerOverUI())
        {
            CancelPlacementPreview("ClickedUIInSelect");

            isTrackingNodeClick = false;
            trackedNode = null;

            if (isDraggingHandle)
                CancelDraggingHandle();

            return;
        }

        Vector2 mouse = cam.ScreenToWorldPoint(Input.mousePosition);

        // ✅ Select에서도 설치 진행(hasStart) 중이면, Rail 툴로 전환하지 않고
        //    기존 설치(프리뷰/커밋) 루프를 그대로 돌린다.
        if (hasStart)
        {
            HandleRailPlacement();
            return;
        }

        // Select 일반 상태에서는 프리뷰를 끈다.
        CancelPlacementPreview();

        // ✅ (Select) RailSnapNode 클릭: MouseUp까지 대기해서 클릭/드래그 분기
        if (isTrackingNodeClick)
        {
            if (trackedNode == null)
            {
                isTrackingNodeClick = false;
                trackedNode = null;
                return;
            }

            if (Input.GetMouseButton(0))
            {
                if ((mouse - trackedNodeDownWorld).sqrMagnitude > dragStartThresholdSq)
                {
                    // 드래그로 판단되면 '설치 클릭' 취소
                    isTrackingNodeClick = false;
                    trackedNode = null;
                }
                else
                {
                    return; // 아직 클릭 확정 전
                }
            }

            if (Input.GetMouseButtonUp(0))
            {
                if ((mouse - trackedNodeDownWorld).sqrMagnitude <= dragStartThresholdSq)
                {
                    // ✅ 용량 체크(마우스업에서도 한 번 더)
                    EnsureFrameCaches(); // nodeRailCount 최신화
                    if (trackedNode != null && CanAddRailToNode(trackedNode))
                        BeginPlacementFromNode_Select(trackedNode);
                    // else: 이미 2개(또는 cap) 꽉 참 -> 시작 안 함
                }

                if (isTrackingNodeClick && trackedNode == null)
                {
                    isTrackingNodeClick = false;
                    trackedNode = null;
                    return;
                }
            }
        }

        // --- dragging ---
        if (isDraggingHandle)
        {
            HandleDragging(mouse);
            return;
        }

        if (!Input.GetMouseButtonDown(0))
            return;

        // =========================
        // handle click (OverlapPoint -> CircleCastAll)
        // =========================
        {
            // ✅ 화면 픽셀 기준 반지름 -> 줌아웃해도 체감 클릭 크기 유지
            float worldPerPixel = (cam.orthographicSize * 2f) / Screen.height;
            float handleRadiusWorld = worldPerPixel * handlePickRadiusPx; // ✅ 핸들은 레일보다 살짝 크게(12~18px 추천)

            var hhits = Physics2D.CircleCastAll(mouse, handleRadiusWorld, Vector2.zero, 0f, handleMask);

            RailEndpointHandle2D bestHandle = null;
            float bestScore = float.PositiveInfinity;

            if (hhits != null && hhits.Length > 0)
            {
                var seenHandles = new HashSet<RailEndpointHandle2D>(); // ✅ 같은 핸들 중복 히트 방지

                for (int i = 0; i < hhits.Length; i++)
                {
                    var c = hhits[i].collider;
                    if (c == null) continue;

                    var h = c.GetComponentInParent<RailEndpointHandle2D>();
                    if (h == null) continue;

                    if (!seenHandles.Add(h)) continue;

                    // ✅ hit.point는 콜라이더 모양에 따라 튈 수 있으니 "핸들 위치" 기준이 더 안정적
                    float score = ((Vector2)h.transform.position - mouse).sqrMagnitude;

                    if (score < bestScore)
                    {
                        bestScore = score;
                        bestHandle = h;
                    }
                }
            }

            if (bestHandle != null)
            {
                BeginHandleDragFromRail(bestHandle.GetComponentInParent<RailSpan2D>(), bestHandle, mouse);
                return;
            }
        }

        // ✅ node click (install start)
        if (RailSnapNodeUtil.TryPickNode(mouse, nodePickRadius, nodeMask, out var nodePicked))
        {
            // ✅ 여기서도 미리 용량 체크해서 "추적 시작" 자체를 막기
            EnsureFrameCaches(); // nodeRailCount 최신화
            if (!CanAddRailToNode(nodePicked))
            {
                // (선택) 사운드/팝업/깜빡임 등 UX 처리
                return;
            }

            isTrackingNodeClick = true;
            trackedNode = nodePicked;
            trackedNodeDownWorld = mouse;
            return;
        }

        // =========================
        // rail click (OverlapPoint -> CircleCastAll)
        // =========================

        // ✅ 화면 픽셀 기준 반지름 -> 줌아웃해도 체감 클릭 크기 유지
        float worldPerPixel2 = (cam.orthographicSize * 2f) / Screen.height;
        float radiusWorld = worldPerPixel2 * railPickRadiusPx; // ✅ 10px (원하면 12~18로 올려도 됨)

        var hits = Physics2D.CircleCastAll(mouse, radiusWorld, Vector2.zero, 0f, railMask);

        RailSpan2D railHit = null;

        if (hits != null && hits.Length > 0)
        {
            float bestScore = float.PositiveInfinity;
            var seen = new HashSet<RailSpan2D>(); // ✅ 같은 레일 중복 히트 방지

            for (int i = 0; i < hits.Length; i++)
            {
                var c = hits[i].collider;
                if (c == null) continue;

                var r = c.GetComponentInParent<RailSpan2D>();
                if (r == null) continue;

                if (!seen.Add(r)) continue; // ✅ 동일 레일 여러 콜라이더 중복 제거

                float score = (hits[i].point - mouse).sqrMagnitude; // ✅ 마우스에 가까운 레일 우선
                if (score < bestScore)
                {
                    bestScore = score;
                    railHit = r;
                }
            }
        }

        if (railHit == null)
        {
            DeselectRail();
            return;
        }

        SelectRail(railHit);

        if (RailSnapNodeManager.Instance != null)
        {
            bool created = false;
            if (railHit.startNode == null)
            {
                var any = FindNearestAnyNodeIncludingFollow(railHit.start);
                railHit.startNode = (any != null) ? any : RailSnapNodeManager.Instance.GetOrCreate(railHit.start);
                created = true;
            }
            if (railHit.endNode == null)
            {
                var any = FindNearestAnyNodeIncludingFollow(railHit.end);
                railHit.endNode = (any != null) ? any : RailSnapNodeManager.Instance.GetOrCreate(railHit.end);
                created = true;
            }
            // ✅ 노드를 “새로 만들었을 때만” 노드 기준으로 정렬
            railHit.Refresh(syncFromNodes: created);
        }

        Vector2 s = railHit.StartWorld;
        Vector2 e = railHit.EndWorld;
        bool wantStart = (mouse - s).sqrMagnitude <= (mouse - e).sqrMagnitude;
        var autoHandle = railHit.GetHandle(wantStart);
    }
    void HandleDragging(Vector2 mouse)
    {
        if (isDraggingHandle)
        {
            if (activeHandle == null || selectedRail == null)
            {
                CancelDraggingHandle();
                return;
            }
        }

        if (Input.GetMouseButton(0))
        {
            if (!dragMovedEnough)
            {
                if ((mouse - dragMouseDownWorld).sqrMagnitude < dragStartThresholdSq)
                    return;

                dragMovedEnough = true;

                // ✅ "드래그 판정 순간"에만 Detach
                if (!handleDragBegun)
                {
                    PerformPendingDetachIfNeeded();
                    activeHandle?.BeginDrag();
                    handleDragBegun = true;
                }
            }

            // 드래그 이동만 수행 (판정/검증 없음)
            activeHandle?.DragTo(mouse, commit: false);

            // (선택) last valid 갱신은 그냥 현재 좌표로만 업데이트
            // -> 롤백을 안 쓰면 사실 필요 없음
            if (activeHandle != null)
            {
                var rail = activeHandle.GetComponentInParent<RailSpan2D>();
                if (rail != null)
                {
                    dragLastValidStart = rail.StartWorld;
                    dragLastValidEnd = rail.EndWorld;
                    hasDragLastValid = true;
                }
            }
        }

        if (Input.GetMouseButtonUp(0))
        {
            if (!dragMovedEnough)
            {
                // ✅ 클릭(드래그 임계 미만) = 설치 시작
                BeginPlacementFromHandleClick_Select(activeHandle);
                CancelDraggingHandle();
                return;
            }

            if (selectedRail == null)
            {
                CancelDraggingHandle();
                return;
            }

            // ✅ MouseUp에서는 바로 commit(true)로 처리
            Vector2 commitMouse = mouse;
            SnapPoint spCommit = null;

            // ✅ MouseUp에서는 "현재 움직인 endpoint" 근처 SnapPoint만 스냅 보정
            if (allowSnapPointAsRailNode && snapPointMask.value != 0 && activeHandle != null)
            {
                Vector2 probe = (Vector2)activeHandle.transform.position;

                if (TryPickSnapPoint(probe, snapPointPickRadius, out spCommit) && spCommit != null)
                    commitMouse = spCommit.transform.position;
            }

            activeHandle?.DragTo(commitMouse, commit: true);

            EnsureRailNodes(selectedRail, refresh: false);
            // ✅ 커밋 직후: 양쪽 endpoint 모두 SnapPoint가 가까우면 각각 바인딩 생성
            // - 기존: 움직인 endpoint(spCommit)만 바인딩
            // - 변경: start/end 둘 다 검사해서 스냅포인트가 있으면 연결 생성
            if (allowSnapPointAsRailNode && selectedRail != null)
            {
                // --- START endpoint도 검사 ---
                if (selectedRail.startNode != null &&
                    TryPickSnapPoint(selectedRail.StartWorld, snapPointPickRadius, out var spA) &&
                    spA != null)
                {
                    var ownerA = GetSnapOwner(spA);
                    if (ownerA != null)
                        TryAttachEndpointToSnapPoint_Single(ownerA, selectedRail.startNode, spA.transform);
                }

                // --- END endpoint도 검사 ---
                if (selectedRail.endNode != null &&
                    TryPickSnapPoint(selectedRail.EndWorld, snapPointPickRadius, out var spB) &&
                    spB != null)
                {
                    var ownerB = GetSnapOwner(spB);
                    if (ownerB != null)
                        TryAttachEndpointToSnapPoint_Single(ownerB, selectedRail.endNode, spB.transform);
                }
            }


            // ✅ 이제부터는 "항상 성공 처리" (검증/롤백 없음)
            RailGraphRevision.Bump("RailMoved");

            // ✅ 밖에서 선언
            PlacementObject oa = null;
            PlacementObject ob = null;

            // 커밋된 레일의 엔드포인트가 PO SnapPoint에 붙었으면 Follow 갱신
            if (selectedRail != null)
            {
                if (allowSnapPointAsRailNode)
                {
                    if (TryPickSnapPoint(selectedRail.StartWorld, snapPointPickRadius, out var spA))
                    {
                        oa = GetSnapOwner(spA);
                        if (oa != null && selectedRail.startNode != null)
                            TagNodeOwner(selectedRail.startNode, oa);
                    }

                    if (TryPickSnapPoint(selectedRail.EndWorld, snapPointPickRadius, out var spB))
                    {
                        ob = GetSnapOwner(spB);
                        if (ob != null && selectedRail.endNode != null)
                            TagNodeOwner(selectedRail.endNode, ob);
                    }
                }

                int oaId = (oa != null) ? oa.GetInstanceID() : 0;
                int obId = (ob != null) ? ob.GetInstanceID() : 0;

                if (oa != null)
                {
                    oa.AutoRailAttach = true;
                    _suppressFollowOwnerIds.Remove(oaId);
                    EnsureFollowForOwner(oa, force: true);
                }

                if (ob != null)
                {
                    ob.AutoRailAttach = true;
                    _suppressFollowOwnerIds.Remove(obId);
                    EnsureFollowForOwner(ob, force: true);
                }

                var gp = GridPlacer.Instance;
                if (gp != null)
                {
                    if (oa != null) gp.FinalizeRuntimeRailBinding(oa);
                    if (ob != null) gp.FinalizeRuntimeRailBinding(ob);
                }
            }

            bool hadUndoBegin = _dragUndoBeginNotified;

            if (selectedRail != null)
            {
                selectedRail.Refresh(syncFromNodes: true);
                Physics2D.SyncTransforms();
            }

            var save = FindFirstObjectByType<StageSaveManager>();
            if (save != null && hadUndoBegin)
            {
                save.NotifyStageChanged();
                save.EndDeferredStageChanged(true);
                _dragUndoBeginNotified = false;
            }
            else
            {
                CommitStageChanged();
            }
            save = FindFirstObjectByType<StageSaveManager>();
            if (save != null)
                save.ForceSaveNow(save.GetCurrentStageIdForUndo());

            EndDraggingHandle();
            _railDragDeferredCommitCo = null;
        }
    }

    void PerformPendingDetachIfNeeded()
    {
        if (!_pendingDetach) return;
        _pendingDetach = false;
        var rail = _pendingDetachRail;
        bool isStart = _pendingDetachIsStart;
        _pendingDetachRail = null;
        if (rail == null) return;
        // ✅ Detach + 복원용 상태 저장
        if (TryDetachEndpointFromPO(rail, isStart, out var detachedOwner, out var prevAuto))
        {
            _dragDetachedOwner = detachedOwner;
            _dragDetachedPrevAuto = prevAuto;
        }
    }
    void RestoreDetachedOwnerIfNeeded()
    {
        if (_dragDetachedOwner == null) return;
        var owner = _dragDetachedOwner;
        _dragDetachedOwner = null;
        // ✅ 커밋 성공으로 AutoRailAttach가 true가 됐다면 건드리지 않음
        if (!owner.AutoRailAttach)
            owner.AutoRailAttach = _dragDetachedPrevAuto;
        // AutoRailAttach가 켜져있는 상태라면 팔로우 재결합 보장
        if (owner.AutoRailAttach)
            EnsureFollowForOwner(owner, force: true);
        _dragDetachedPrevAuto = false;
    }
    void SelectRail(RailSpan2D rail)
    {
        if (rail == null) return;

        // 이미 같은 레일이 선택되어 있으면 중복 소리 방지
        if (selectedRail == rail)
            return;

        if (selectedRail != null && selectedRail != rail)
            selectedRail.SetSelected(false);

        selectedRail = rail;
        selectedRail.SetEditCamera(cam);
        selectedRail.SetSelected(true);

        // ✅ 선택 소리
        PlayRailSelectSound();

        SelectedRailChanged?.Invoke(selectedRail);
    }
    void DeselectRail()
    {
        if (selectedRail == null) return;

        selectedRail.SetSelected(false);
        selectedRail = null;

        // ✅ 선택 해제 소리
        PlayRailDeselectSound();

        SelectedRailChanged?.Invoke(selectedRail);
    }

    public static void ClearSelectedRail()
    {
        if (Instance == null) return;
        Instance.DeselectRail();
    }

    bool _dragUndoBeginNotified;

    Coroutine _railDragDeferredCommitCo;

    void BeginHandleDragFromRail(RailSpan2D rail, RailEndpointHandle2D handle, Vector2 mouse)
    {
        if (rail == null || handle == null) return;
        if (isDraggingHandle) return;
        if (handle.IsLockedByAnchor)
        {
            return;
        }
        var save = FindFirstObjectByType<StageSaveManager>();
        if (save != null && !_dragUndoBeginNotified)
        {
            save.NotifyStageChangeBegin();
            save.BeginDeferredStageChanged(); // 추가
            _dragUndoBeginNotified = true;
        }

        // ✅ Detach 예약 (실제 Detach는 threshold 통과 시점에만)
        _pendingDetachRail = rail;
        _pendingDetachIsStart = (handle == rail.GetHandle(true));
        _pendingDetach = true;
        _dragDetachedOwner = null;
        _dragDetachedPrevAuto = false;
        SelectRail(rail);
        activeHandle = handle;
        dragBackupStart = rail.StartWorld;
        dragBackupEnd = rail.EndWorld;
        hasDragBackup = true;
        dragLastValidStart = dragBackupStart;
        dragLastValidEnd = dragBackupEnd;
        hasDragLastValid = true;
        isDraggingHandle = true;
        handleDragBegun = false;
        dragMouseDownWorld = mouse;
        dragMovedEnough = false;
    }
    void EndDraggingHandle()
    {
        // ✅ (추가) pending 상태 정리
        _pendingDetach = false;
        _pendingDetachRail = null;
        _pendingDetachIsStart = false;
        _dragDetachedOwner = null;
        _dragDetachedPrevAuto = false;
        isDraggingHandle = false;
        dragMovedEnough = false;
        handleDragBegun = false;
        activeHandle = null;
        hasDragBackup = false;
        hasDragLastValid = false;
        _suppressFollowOwnerIds.Clear();
    }
    void CancelDraggingHandle()
    {
        isDraggingHandle = false;
        dragMovedEnough = false;
        handleDragBegun = false;
        bool hadUndoBegin = _dragUndoBeginNotified;   // 먼저 백업

        isDraggingHandle = false;
        dragMovedEnough = false;
        handleDragBegun = false;
        if (activeHandle != null)
            activeHandle.CancelDrag();

        RestoreDetachedOwnerIfNeeded();

        _pendingDetach = false;
        _pendingDetachRail = null;
        _pendingDetachIsStart = false;
        activeHandle = null;
        hasDragBackup = false;
        hasDragLastValid = false;
        _suppressFollowOwnerIds.Clear();

        var save = FindFirstObjectByType<StageSaveManager>();
        if (save != null && hadUndoBegin)
        {
            save.NotifyStageChangeBeginCanceled();
            save.EndDeferredStageChanged(false);
        }

        _dragUndoBeginNotified = false;
    }

    #endregion

    #region Validation / Caches / Occupancy
    bool IsLengthValid(Vector2 start, Vector2 end)
    {
        float d = Vector2.Distance(start, end);
        if (d <= 0.000001f) return false;
        if (d < minLength - 0.0001f) return false;
        if (maxLength > 0f && d > maxLength + 0.0001f) return false;
        return true;
    }
    void EnsureOccRef()
    {
        if (occ == null) occ = GridOccupancy2D.Instance;
    }
    void EnsureOccBakedAndMasks()
    {
        EnsureOccRef();
        if (occ == null) return;
        if (occEnsureFrame != Time.frameCount)
        {
            occ.EnsureBaked();
            occEnsureFrame = Time.frameCount;
        }
        if (occMaskSyncFrame != Time.frameCount)
        {
            bool changed = false;
            if (occ.grid == null && grid != null) { occ.grid = grid; changed = true; }
            if (occ.wallMask.value != wallMask.value) { occ.wallMask = wallMask; changed = true; }
            if (occ.placedMask.value != placedMask.value) { occ.placedMask = placedMask; changed = true; }
            if (changed) occ.MarkDirty();
            occMaskSyncFrame = Time.frameCount;
        }
    }
    void EnsureFrameCaches()
    {
        // ✅ rails 캐시는 프레임마다 1회만 갱신
        if (_railsCacheFrame != Time.frameCount)
        {
            _railsCache = null;
            _railsCacheFrame = -1;
        }
        if (nodeCountFrame != Time.frameCount)
        {
            RebuildNodeRailCountAndEdges();
            nodeCountFrame = Time.frameCount;
        }
        if (wallCacheFrame != Time.frameCount)
        {
            wallCache.Clear();
            wallCacheFrame = Time.frameCount;
        }
    }
    void RebuildNodeRailCountAndEdges()
    {
        nodeRailCount.Clear();
        edgeSet.Clear();
        edgeSetFrame = Time.frameCount;
        var rails = GetAllRailsCached();
        for (int i = 0; i < rails.Length; i++)
        {
            var r = rails[i];
            if (r == null) continue;
            EnsureRailNodes(r);
            if (r.startNode != null) AddRailCount(r.startNode);
            if (r.endNode != null) AddRailCount(r.endNode);
            if (r.startNode != null && r.endNode != null)
                edgeSet.Add(EdgeKey(r.startNode, r.endNode));
        }
    }
    static ulong EdgeKey(RailSnapNode2D a, RailSnapNode2D b)
    {
        uint ia = (uint)a.GetInstanceID();
        uint ib = (uint)b.GetInstanceID();
        if (ia > ib) (ia, ib) = (ib, ia);
        return ((ulong)ia << 32) | ib;
    }
    void AddRailCount(RailSnapNode2D node)
    {
        if (node == null) return;
        nodeRailCount.TryGetValue(node, out int c);
        nodeRailCount[node] = c + 1;
    }
    bool CanAddRailToNode(RailSnapNode2D n)
    {
        if (n == null) return false;
        nodeRailCount.TryGetValue(n, out int count);
        // ✅ 노드가 스스로 말하는 용량을 사용 (앵커면 1)
        int cap = n.GetCapacity(maxRailsPerNode);
        return count < cap;
    }

    static readonly Collider2D[] _wallHits = new Collider2D[64];
    bool IsWallAtCached(Vector2 worldPos)
    {
        // ✅ Occupancy-only wall check (통일)
        // - 벽은 GridOccupancy2D(벽 셀)로만 판단
        // - 프레임 캐시(wallCache)로 같은 셀 재검사 방지
        EnsureFrameCaches();
        Vector2Int cellKey = (grid != null) ? grid.WorldToCell(worldPos) : Vector2Int.RoundToInt(worldPos);
        if (wallCache.TryGetValue(cellKey, out bool hit))
            return hit;
        float r = Mathf.Max(0.0001f, endpointBlockRadius);
        hit = RailPlacementRules2D.IsWallAtFiltered_WithOccupancy_NoSync(grid, worldPos, r, wallMask);
        wallCache[cellKey] = hit;
        return hit;
    }

    #endregion

    #region Nodes / SnapPoints / Placement Resolve
    RailSpan2D[] GetAllRails()
    {
#if UNITY_2022_2_OR_NEWER
        return FindObjectsByType<RailSpan2D>(FindObjectsSortMode.None);
#else
        return FindObjectsOfType<RailSpan2D>();
#endif
    }
    RailSpan2D[] GetAllRailsCached()
    {
        if (_railsCacheFrame == Time.frameCount && _railsCache != null) return _railsCache;
        _railsCache = GetAllRails();
        _railsCacheFrame = Time.frameCount;
        return _railsCache;
    }
    SnapPoint[] GetAllSnapPoints()
    {
#if UNITY_2022_2_OR_NEWER
        return FindObjectsByType<SnapPoint>(FindObjectsSortMode.None);
#else
        return FindObjectsOfType<SnapPoint>();
#endif
    }
    SnapPoint[] GetAllSnapPointsCached()
    {
        if (_snapPointsCacheFrame == Time.frameCount && _snapPointsCache != null) return _snapPointsCache;
        _snapPointsCache = GetAllSnapPoints();
        _snapPointsCacheFrame = Time.frameCount;
        return _snapPointsCache;
    }
    void EnsureRailNodes(RailSpan2D r, bool refresh = false)
    {
        if (r == null) return;
        var mgr = RailSnapNodeManager.Instance;
        if (mgr == null) return;
        if (r.startNode == null)
        {
            var any = FindNearestAnyNodeIncludingFollow(r.start);
            r.startNode = (any != null) ? any : mgr.GetOrCreate(r.start);
        }
        if (r.endNode == null)
        {
            var any = FindNearestAnyNodeIncludingFollow(r.end);
            r.endNode = (any != null) ? any : mgr.GetOrCreate(r.end);
        }
        if (refresh) r.Refresh();
    }
    static readonly Collider2D[] _spHits = new Collider2D[32];
    bool TryPickSnapPoint(Vector2 world, float radius, out SnapPoint sp)
    {
        sp = null;

        int count = Physics2D.OverlapCircleNonAlloc(world, radius, _spHits, snapPointMask);

        float best = float.MaxValue;

        for (int i = 0; i < count; i++)
        {
            var c = _spHits[i];
            if (c == null) continue;
            if (!c.gameObject.activeInHierarchy) continue;
            if (!c.enabled) continue;

            var p = c.GetComponentInParent<SnapPoint>();
            if (p == null) continue;
            if (!p.gameObject.activeInHierarchy) continue;
            if (!p.isActiveAndEnabled) continue;

            var owner = GetSnapOwner(p);
            if (owner == null) continue;
            if (!owner.gameObject.activeInHierarchy) continue;

            float d = ((Vector2)p.transform.position - world).sqrMagnitude;
            if (d < best)
            {
                best = d;
                sp = p;
            }
        }

        return sp != null;
    }
    RailSnapNode2D GetOrCreateNodeForSnapPoint(SnapPoint sp, out bool createdNew)
    {
        createdNew = false;
        if (sp == null) return null;
        var mgr = RailSnapNodeManager.Instance;
        if (mgr == null) return null;
        Vector2 pos = sp.transform.position;
        var owner = GetSnapOwner(sp);
        if (RailSnapNodeUtil.TryPickNode(pos, nodePickRadius, nodeMask, out var existing))
        {
            TagNodeOwner(existing, owner); // ✅ 추가
            return existing;
        }
        var node = mgr.GetOrCreate(pos, asAnchorRoot: false);
        if (node == null) return null;
        createdNew = true;
        TagNodeOwner(node, owner); // ✅ 추가
        return node;
    }
    PlacementObject GetSnapOwner(SnapPoint sp)
    {
        if (sp == null) return null;
        if (sp.root != null && sp.root.owner != null)
            return sp.root.owner;
        return sp.GetComponentInParent<PlacementObject>();
    }
    bool TryResolveEndForPreview(
        Vector2 endCandidate,
        out RailSnapNode2D endNode,
        out Vector2 endPos,
        out bool endAllowed,
        out bool endAllowedByExistingNodeInWall)
    {
        endNode = null;
        endPos = endCandidate;
        endAllowed = true;
        endAllowedByExistingNodeInWall = false;

        // ✅ 추가
        endSnapPoint = null;
        endAttachOwner = null;

        // ✅ 정책: endCandidate가 벽 안이면 무조건 불가능
        if (IsWallAtCached(endCandidate))
        {
            endAllowed = false;
            return true;
        }

        // ✅ SnapPoint는 "좌표만" 사용 (노드 생성 금지)
        if (allowSnapPointAsRailNode && TryPickSnapPoint(endCandidate, snapPointPickRadius, out var sp))
        {
            endPos = sp.transform.position;
            endSnapPoint = sp;

            // ✅ 정책: SnapPoint로 스냅된 endPos가 벽 안이면 무조건 불가능
            if (IsWallAtCached(endPos))
            {
                endAllowed = false;
                return true;
            }

            // 벽이 아니면 기존 노드가 있든 없든:
            // - 노드 없으면 OK
            // - 노드 있으면 CanAddRailToNode 통과해야 OK
            if (RailSnapNodeUtil.TryPickNode(endPos, nodePickRadius, nodeMask, out var picked))
            {
                endNode = picked;
                endAllowed = CanAddRailToNode(endNode);
            }
            else
            {
                endAllowed = true;
            }

            return true;
        }

        // 벽이 아닌 일반 좌표에서 기존 노드가 있으면 그 노드로 스냅 + 연결 가능 여부 체크
        if (RailSnapNodeUtil.TryPickNode(endCandidate, nodePickRadius, nodeMask, out var picked2))
        {
            endNode = picked2;
            endPos = picked2.WorldPos;
            endAllowed = CanAddRailToNode(endNode);
        }

        if (forbidEndOnExistingRailNode && endNode != null) endAllowed = false;

        return true;
    }

    bool TryResolveEndForPlacement(
        Vector2 startPos,
        Vector2 mouseWorld,
        out RailSnapNode2D endNode,
        out Vector2 endPos,
        out bool endAllowedByExistingNodeInWall,
        out bool endNodeCreatedNew,
        out PlacementObject endAttachOwner)
    {
        endNode = null;
        endPos = Vector2.zero;
        endAllowedByExistingNodeInWall = false;
        endNodeCreatedNew = false;
        endAttachOwner = null;
        // ✅ 커밋은 프리뷰에서 확정된 end(=mouseWorld)를 그대로 사용한다
        Vector2 endCandidate = mouseWorld;

        // ✅ 길이 유효성은 여기서 확실히 보장
        if (!IsLengthValid(startPos, endCandidate))
            return false;

        bool endInsideWall = IsWallAtCached(endCandidate);
        // ✅ SnapPoint 우선
        if (allowSnapPointAsRailNode && TryPickSnapPoint(endCandidate, snapPointPickRadius, out var sp))
        {
            Vector2 spPos = sp.transform.position;
            float dist = Vector2.Distance(startPos, spPos);
            if (!IsLengthValid(startPos, spPos))
            {
                return false;
            }
            bool spInsideWall = IsWallAtCached(spPos);
            RailSnapNode2D n = null;
            bool createdNew = false;
            if (spInsideWall)
            {
                if (!RailSnapNodeUtil.TryPickNode(spPos, nodePickRadius, nodeMask, out var existing))
                {
                    return false;
                }
                n = existing;
                createdNew = false;
                endAllowedByExistingNodeInWall = true;
            }
            else
            {
                n = GetOrCreateNodeForSnapPoint(sp, out createdNew);
                if (n == null)
                {
                    return false;
                }
            }
            if (!CanAddRailToNode(n))
            {
                if (createdNew) Destroy(n.gameObject);
                return false;
            }
            endNode = n;
            endPos = n.WorldPos;
            endAttachOwner = GetSnapOwner(sp);
            if (endAttachOwner != null)
            {
                endAttachOwner.AutoRailAttach = true;
                _suppressFollowOwnerIds.Remove(endAttachOwner.GetInstanceID());
            }
            TagNodeOwner(endNode, endAttachOwner); // ✅ 추가
            endNodeCreatedNew = createdNew;
            {
                string ownerName = (endAttachOwner != null) ? endAttachOwner.name : "null";
            }
            if (forbidEndOnExistingRailNode && endNode != null && !endNodeCreatedNew)
            {
                if (endNodeCreatedNew) Destroy(endNode.gameObject);
                return false;
            }
            return true;
        }
        // --- 이하 기존 흐름 ---
        if (endInsideWall)
        {
            if (!RailSnapNodeUtil.TryPickNode(endCandidate, nodePickRadius, nodeMask, out var picked))
            {
                return false;
            }
            if (!CanAddRailToNode(picked))
            {
                return false;
            }
            endNode = picked;
            endPos = picked.WorldPos;
            endAllowedByExistingNodeInWall = true;
            endNodeCreatedNew = false;
            if (forbidEndOnExistingRailNode && endNode != null && !endNodeCreatedNew)
            {
                if (endNodeCreatedNew) Destroy(endNode.gameObject);
                return false;
            }
            return true;
        }
        if (RailSnapNodeUtil.TryPickNode(endCandidate, nodePickRadius, nodeMask, out var picked2))
        {
            if (!CanAddRailToNode(picked2))
            {
                return false;
            }
            endNode = picked2;
            endPos = picked2.WorldPos;
            endAllowedByExistingNodeInWall = false;
            endNodeCreatedNew = false;
            // endOwner 복구는 그대로
            if (allowSnapPointAsRailNode && TryPickSnapPoint(endPos, snapPointPickRadius, out var sp2))
            {
                var po2 = GetSnapOwner(sp2);
                if (po2 != null) endAttachOwner = po2;
            }
            if (forbidEndOnExistingRailNode && endNode != null && !endNodeCreatedNew)
            {
                if (endNodeCreatedNew) Destroy(endNode.gameObject);
                return false;
            }
            return true;
        }
        if (RailSnapNodeManager.Instance == null)
        {
            return false;
        }
        endNode = RailSnapNodeManager.Instance.GetOrCreate(endCandidate, out endNodeCreatedNew);
        if (endNode == null)
        {
            return false;
        }
        endPos = endNode.WorldPos;
        if (!CanAddRailToNode(endNode))
        {
            if (endNodeCreatedNew) Destroy(endNode.gameObject);
            return false;
        }
        if (forbidEndOnExistingRailNode && endNode != null && !endNodeCreatedNew)
        {
            if (endNodeCreatedNew) Destroy(endNode.gameObject);
            return false;
        }
        return true;
    }

    #endregion

    #region Delete
    bool TryDeleteRail(RailSpan2D rail)
    {
        if (rail == null) return false;
        if (isDraggingHandle && activeHandle != null)
        {
            var parentRail = activeHandle.GetComponentInParent<RailSpan2D>();
            if (parentRail == rail)
                CancelDraggingHandle();
        }

        if (selectedRail == rail)
            DeselectRail();

        rail.enabled = false;
        rail.gameObject.SetActive(false);

        RailEdgeRegistry2D.Unregister(rail.gameObject.GetInstanceID());

        rail.startNode?.UnregisterRail(rail);
        rail.endNode?.UnregisterRail(rail);

        PruneOwnerBindingByNode(rail.startNode);
        PruneOwnerBindingByNode(rail.endNode);

        // ✅ 삭제 소리
        PlayRailDeleteSound();

        Destroy(rail.gameObject);

        RailGraphCleanup2D.Cleanup();
        RailGraphRevision.Bump("RailDeleted");

        SyncRailBudgetFromScene();

        CommitStageChanged();
        return true;
    }

    public void DeleteRailForReset(RailSpan2D rail)
    {
        TryDeleteRail(rail);
        return;
    }

    public event Action<RailSpan2D> SelectedRailChanged;

    public RailSpan2D SelectedRail => selectedRail; // (네가 이미 쓰는 필드명에 맞춰)

    public bool DeleteSelectedRail()
    {
        if (selectedRail == null) return false;
        return TryDeleteRail(selectedRail); // 기존 함수 재사용
    }

    void PruneOwnerBindingByNode(RailSnapNode2D node)
    {
        if (node == null) return;

        // node에 붙은 Follow가 잡고 있는 anchor(=SnapPoint.transform)로 owner(PO)를 찾는다
        var follow = node.GetComponent<RailNodeFollow2D>();
        if (follow == null || follow.target == null) return;

        var owner = follow.target.GetComponentInParent<PlacementObject>();
        if (owner == null) return;

        int ownerId = owner.GetInstanceID();

        var bind = owner.GetComponent<RailNodeFollowBinding2D>();
        if (bind == null) return;

        // entries를 복사해서 node 항목만 제거 + null 항목도 같이 정리
        var src = bind.Entries;
        if (src == null || src.Count == 0) return;

        List<RailNodeFollowBinding2D.Entry> newEntries = new List<RailNodeFollowBinding2D.Entry>(src.Count);

        for (int i = 0; i < src.Count; i++)
        {
            var e = src[i];

            // ✅ 파괴된 노드 / 이번에 삭제되는 노드 제거
            if (e.node == null) continue;
            if (e.node == node) continue;

            newEntries.Add(e);
        }

        // ✅ follow도 내가 붙인 거면 제거 (이 노드는 레일 삭제로 같이 날아가거나, orphan 될 수 있음)
        if (follow.ownerId == ownerId)
        {
            follow.Detach();
            Destroy(follow);
        }

        if (newEntries.Count == 0)
        {
            // 더 이상 바인딩이 없으면 깔끔하게 제거
            bind.Clear();
            Destroy(bind);
        }
        else
        {
            bind.SetEntries(newEntries);
            // 필요하면 즉시 동기화(보험)
            bind.SyncNow(syncPhysics: true, broadcastMoved: false);
        }
    }


    #endregion

    #region Ignore Attach Owners (Exact Check) - ✅ ignorePlacedOwners 방식


    #endregion

    #region Logging

    #endregion

    #region Follow Attach
    void EnsureFollowForOwner(PlacementObject owner, bool force = false)
    {
        if (owner == null) return;

        // ✅ 바인딩 컴포넌트가 없으면 자동 생성
        // (프리팹에 RailNodeFollowBinding2D가 빠져 있으면 Binder가 바로 false로 리턴해서
        //  "PO랑 연결이 안된 것처럼" 보이는 증상이 생김)
        if (owner.GetComponent<RailNodeFollowBinding2D>() == null)
            owner.gameObject.AddComponent<RailNodeFollowBinding2D>();

        // ✅ 자동 재결합이 꺼져있으면 스킵 (단, force면 무시)
        if (!force && !owner.AutoRailAttach)
            return;
        int id = owner.GetInstanceID();
        // ✅ 추가: 드래그 중 suppress면 재결합 금지 (force면 예외)
        if (!force && _suppressFollowOwnerIds.Contains(id))
            return;
        if (_followRetryMap.TryGetValue(id, out var co) && co != null)
            StopCoroutine(co);
        _followRetryMap.Remove(id);
        bool ok = TryAttachNow_Strict(owner);
        if (!ok)
            _followRetryMap[id] = StartCoroutine(CoRetryAttach_Strict(owner, id));
    }

    #endregion

    #region Hints
    bool HasRailPlacementHints => _hintPts != null && _hintPts.Count > 0;
    float GetGridStepCached()
    {
        if (grid == null) return 1f;
        float step = RailGridUtil.GetGridStep(grid);
        return (step > 0f) ? step : 1f;
    }
    Vector2 SnapWorldToGrid(Vector2 world)
    {
        if (grid == null) return world;
        return grid.CellToWorld(grid.WorldToCell(world));
    }
    /// <summary>
    /// desired(그리드 스냅된 좌표)를 가장 가까운 힌트 점으로 클램프.
    /// hintPickRadiusCells > 0이면 해당 반경(셀 기준) 안에서만 허용.
    /// </summary>
    bool TryClampToNearestHint(Vector2 desiredGridPos, out Vector2 clamped)
    {
        clamped = desiredGridPos;
        if (!HasRailPlacementHints) return false;
        float best = float.MaxValue;
        int bestIdx = -1;
        for (int i = 0; i < _hintPts.Count; i++)
        {
            float d = (_hintPts[i] - desiredGridPos).sqrMagnitude;
            if (d < best) { best = d; bestIdx = i; }
        }
        if (bestIdx < 0) return false;
        float step = GetGridStepCached();
        if (hintPickRadiusCells > 0f)
        {
            float maxDist = step * hintPickRadiusCells;
            if (best > maxDist * maxDist)
                return false; // 너무 멀면 클램프 금지(= 설치 불가)
        }
        clamped = _hintPts[bestIdx];
        return true;
    }



    void BuildRailPlacementHintPoints(Vector2 startPos)
    {
        _hintPts.Clear();
        if (grid == null) return;
        if (maxLength <= 0f) return;

        float step = RailGridUtil.GetGridStep(grid);
        int maxCells = Mathf.CeilToInt(maxLength / Mathf.Max(0.0001f, step));
        int minCells = Mathf.FloorToInt(minLength / Mathf.Max(0.0001f, step));
        maxCells = Mathf.Min(maxCells, railPlacementHintRadiusCells);

        Vector2Int c0 = grid.WorldToCell(startPos);

        // ✅ 스팬 정밀체크에 쓸 ruleProbe
        var probe = GetRuleProbeRail();

        // ✅ 프로파일/마스크/파라미터를 설치와 동일하게
        var prof = ruleProfile;
        LayerMask wMask = (prof != null && prof.wallMask.value != 0) ? prof.wallMask : wallMask;
        LayerMask pMask = (prof != null && prof.placedMask.value != 0) ? prof.placedMask : placedMask;
        LayerMask rMask = (prof != null && prof.railMask.value != 0) ? prof.railMask : railMask;

        float allowRad = (prof != null && prof.endpointAllowRadius > 0f) ? prof.endpointAllowRadius : endpointBlockRadius;
        float pen = 0f; // penetration disabled

        // ✅ Occupancy-only 정책: "벽 안 예외(노드 있으면 허용)" 제거
        bool allowStartInsideWall = false;

        // ✅ 연결되는 PO(ignoreOwners) 무시 정책 제거: 항상 null / false로 고정
        IReadOnlyList<PlacementObject> ignoreOwners = null;

        bool endpointCellOnlyA = false;
        bool endpointCellOnlyB = false;
        float ignoreOwnerRelaxTotalCells = 1f;
        bool allowEndInsideWall = false;

        for (int dy = -maxCells; dy <= maxCells; dy++)
        {
            for (int dx = -maxCells; dx <= maxCells; dx++)
            {
                if (_hintPts.Count >= maxHintDots) return;

                int d2 = dx * dx + dy * dy;
                if (d2 < minCells * minCells) continue;
                if (d2 > maxCells * maxCells) continue;

                Vector2Int cell = new Vector2Int(c0.x + dx, c0.y + dy);
                Vector2 endCandidate = grid.CellToWorld(cell);

                if (!IsLengthValid(startPos, endCandidate)) continue;

                // 1) end 후보 resolve
                if (!TryResolveEndForPreview(
                        endCandidate,
                        out var endNode,
                        out var endPos,
                        out bool endAllowed,
                        out bool endAllowedByExistingNodeInWall))
                    continue;

                if (!endAllowed) continue;

                // 2) ✅ Unified endpoint rules (프리뷰/설치와 동일)
                ResolveMergeTarget_NoSync(endPos, excludeNode: null, out var resolvedEnd, out var mergeTarget);
                if (mergeTarget != null)
                {
                    endNode = mergeTarget;
                    endPos = mergeTarget.WorldPos;
                    endAllowedByExistingNodeInWall = IsWallAtCached(endPos);
                }
                else
                {
                    endPos = resolvedEnd;
                }

                // 3) duplicate
                if (RailEdgeRegistry2D.Contains(startPos, endPos)) continue;

                // 4) ✅ 스팬 정밀 체크
                bool spanOk = RailPlacementRules2D.CanPlaceRailSpan_WithMasks(
                    grid, probe, startPos, endPos,
                    wMask, pMask, rMask,
                    allowStartInsideWall, allowEndInsideWall,
                    allowRad,
                    ignoreOwners,
                    ignoreRail: null,
                    placedOwnerAllowPenetration: pen,
                    useSegmentWallCheck: blockIfSegmentHitsWall,
                    ignoreOwnerRelaxTotalCells: ignoreOwnerRelaxTotalCells,
                    endpointCellOnlyA: endpointCellOnlyA,
                    endpointCellOnlyB: endpointCellOnlyB
                );

                if (!spanOk) continue;

                _hintPts.Add(endPos);
            }
        }
    }




    #endregion

    #region Rebind Nearby
    static readonly Collider2D[] _poHits = new Collider2D[64];
    static readonly HashSet<int> _rebindSeen = new HashSet<int>(64);
    void RebindNearbyPOs(Vector2 worldPos)
    {
        Physics2D.SyncTransforms();
        _rebindSeen.Clear();
        // 1) placedMask 스캔(기존)
        int n = Physics2D.OverlapCircleNonAlloc(
            worldPos, snapPointPickRadius * 2f, _poHits, placedMask
        );
        for (int i = 0; i < n; i++)
        {
            var c = _poHits[i];
            if (c == null) continue;
            var po = c.GetComponentInParent<PlacementObject>();
            if (po == null) continue;
            int id = po.GetInstanceID();
            if (!_rebindSeen.Add(id)) continue;
            EnsureFollowForOwner(po);
        }
        // 2) ✅ snapPointMask 스캔 추가 (placedMask로 PO를 못 잡는 케이스 보정)
        int ns = Physics2D.OverlapCircleNonAlloc(
            worldPos, snapPointPickRadius * 2f, _poHits, snapPointMask
        );
        for (int i = 0; i < ns; i++)
        {
            var c = _poHits[i];
            if (c == null) continue;
            var sp = c.GetComponentInParent<SnapPoint>();
            if (sp == null) continue;
            var po = GetSnapOwner(sp);
            if (po == null) continue;
            int id = po.GetInstanceID();
            if (!_rebindSeen.Add(id)) continue;
            EnsureFollowForOwner(po);
        }
    }

    #endregion
    RailSnapNode2D FindNearestAnyNodeIncludingFollow(Vector2 worldPos)
    {
        var mgr = RailSnapNodeManager.Instance;
        if (mgr == null) return null;
        Physics2D.SyncTransforms();
        int n = Physics2D.OverlapCircleNonAlloc(worldPos, mgr.mergeRadius, _nodeHitsAny, mgr.railNodeMask);
        if (n <= 0) return null;
        RailSnapNode2D best = null;
        float bestDist = float.MaxValue;
        for (int i = 0; i < n; i++)
        {
            var col = _nodeHitsAny[i];
            if (col == null) continue;
            var node = col.GetComponentInParent<RailSnapNode2D>();
            if (node == null) continue;
            float d = ((Vector2)node.transform.position - worldPos).sqrMagnitude;
            if (d < bestDist)
            {
                bestDist = d;
                best = node;
            }
        }
        return best;
    }
    bool TryAttachNow_Strict(PlacementObject owner)
    {
        if (owner == null) return false;
        void StabilizeGraph(string tag)
        {
            // 그래프 더러우면 즉시 정리 + 캐시 리빌드
            if (RailGraphDirty.dirty)
            {
                RailGraphDirty.dirty = false;
                RailGraphCleanup2D.Cleanup();
                SyncRailBudgetFromScene();
                RebuildNodeRailCountAndEdges();
                // ✅ 매우 중요: Cleanup이 노드 병합/정리하면
                // nodeRailCount / edgeSet / capacity 판단이 stale이 될 수 있음
                RebuildNodeRailCountAndEdges();
                nodeCountFrame = Time.frameCount;
                edgeSetFrame = Time.frameCount;
            }
        }
        Physics2D.SyncTransforms();
        // 1) 진입 시점에서 먼저 안정화
        StabilizeGraph("BeforeAttach");
        // ✅ rails 스냅샷 (프레임 캐시 사용)
        var rails = GetAllRailsCached();
        // 2) 바인딩(Attach or Keep)
        bool ok = RailNodeSnapBinder.EnsureAttachedOrKeepExisting(
            owner, railFollowRadius, railNodeMask, rails, debug: false
        );
        var bind = owner.GetComponent<RailNodeFollowBinding2D>();
        if (bind == null) return false;
        // 3) 바인딩이 만든 Follow들이 노드를 target 위치로 즉시 이동시키게 함
        bind.SyncNow(syncPhysics: true);
        // ✅ 핵심: Follow.Attach → RailSpan2D.NotifyNodeMoved → RailGraphDirty.MarkDirty
        // 이게 같은 프레임에 또 dirty를 만들 수 있어서, 여기서 2차 안정화가 필요함
        StabilizeGraph("AfterBindSyncNow");
        // 4) 최종 유효성(엔트리들 node null이면 실패 처리)
        if (bind.Entries != null && bind.Entries.Count > 0)
        {
            for (int i = 0; i < bind.Entries.Count; i++)
                if (bind.Entries[i].node == null)
                    return false;
            return ok;
        }
        return (bind.node != null) && ok;
    }


    // =========================================================
    // ✅ Single-endpoint attach: only bind the moved endpoint to a specific SnapPoint.
    //    This avoids pulling the opposite endpoint when forcing binding.
    // =========================================================
    bool TryAttachEndpointToSnapPoint_Single(PlacementObject owner, RailSnapNode2D node, Transform snapPointTr)
    {
        if (owner == null || node == null || snapPointTr == null) return false;

        // ✅ 추가
        if (!owner.gameObject.activeInHierarchy) return false;
        if (!snapPointTr.gameObject.activeInHierarchy) return false;

        var sp = snapPointTr.GetComponent<SnapPoint>();
        if (sp == null) sp = snapPointTr.GetComponentInParent<SnapPoint>();
        if (sp == null) return false;
        if (!sp.gameObject.activeInHierarchy) return false;
        if (!sp.isActiveAndEnabled) return false;

        var bind = owner.GetComponent<RailNodeFollowBinding2D>();
        if (bind == null)
            bind = owner.gameObject.AddComponent<RailNodeFollowBinding2D>();

        int myId = owner.GetInstanceID();

        var old = bind.Entries;
        var newEntries = new List<RailNodeFollowBinding2D.Entry>((old != null ? old.Count : 0) + 1);

        if (old != null && old.Count > 0)
        {
            for (int i = 0; i < old.Count; i++)
            {
                var e = old[i];
                if (e.node == null) continue;
                if (e.node == node) continue;
                newEntries.Add(e);
            }
        }

        node.EnsurePersistentId();
        TagNodeOwner(node, owner);

        newEntries.Add(new RailNodeFollowBinding2D.Entry
        {
            node = node,
            anchorPoint = snapPointTr,
            localOffset = (Vector2)owner.transform.InverseTransformPoint(node.transform.position),
            ownerId = myId,
            nodeId = node.PersistentId
        });

        bind.SetEntries(newEntries);

        bind.builtRevision = RailGraphRevision.Value;
        bind.builtRadius = railFollowRadius;
        bind.builtMaskValue = railNodeMask.value;

        // ✅ 조건 없이 바로 Follow 부착
        var follow = node.GetComponent<RailNodeFollow2D>();
        if (follow == null)
            follow = node.gameObject.AddComponent<RailNodeFollow2D>();

        follow.Attach(snapPointTr, myId);

        return true;
    }


    IEnumerator CoRetryAttach_Strict(PlacementObject owner, int id)
    {
        yield return null;
        if (owner == null) { _followRetryMap.Remove(id); yield break; }
        if (!owner.AutoRailAttach) { _followRetryMap.Remove(id); yield break; } // ✅ 추가
        if (TryAttachNow_Strict(owner))
        {
            _followRetryMap.Remove(id);
            yield break;
        }
        yield return new WaitForFixedUpdate();
        if (owner == null) { _followRetryMap.Remove(id); yield break; }
        if (!owner.AutoRailAttach) { _followRetryMap.Remove(id); yield break; } // ✅ 추가
        TryAttachNow_Strict(owner);
        _followRetryMap.Remove(id);
    }
    void TagNodeOwner(RailSnapNode2D node, PlacementObject owner)
    {
        if (node == null || owner == null) return;
        var tag = node.GetComponent<RailNodeOwnerTag2D>();
        if (tag == null) tag = node.gameObject.AddComponent<RailNodeOwnerTag2D>();
        // ✅ 덮어쓰기 허용 (stale 방지)
        if (tag.Owner != owner)
            tag.SetOwner(owner);
    }
    bool TryDetachEndpointFromPO(
    RailSpan2D rail,
    bool isStartEndpoint,
    out PlacementObject detachedOwner,
    out bool prevAutoRailAttach
)
    {
        detachedOwner = null;
        prevAutoRailAttach = false;

        if (rail == null) return false;
        if (!allowSnapPointAsRailNode) return false;

        Vector2 p = isStartEndpoint ? rail.StartWorld : rail.EndWorld;

        RailSnapNode2D node = isStartEndpoint ? rail.startNode : rail.endNode;
        if (node == null) return false;

        PlacementObject owner = null;

        // 1) OwnerTag 우선
        var tag = node.GetComponent<RailNodeOwnerTag2D>();
        if (tag != null && tag.Owner != null)
            owner = tag.Owner;

        // 2) fallback: 현재 endpoint 위치의 SnapPoint
        if (owner == null)
        {
            if (!TryPickSnapPoint(p, snapPointPickRadius, out var sp)) return false;
            owner = GetSnapOwner(sp);
            if (owner == null) return false;
        }

        prevAutoRailAttach = owner.AutoRailAttach;

        // ✅ 전체 Detach 금지, 해당 endpoint만 분리
        bool detached = TryDetachEndpointFromPO_Single(owner, node);
        if (!detached) return false;

        // ✅ 드래그 중 자동 재결합 억제는 유지
        int id = owner.GetInstanceID();
        if (_followRetryMap.TryGetValue(id, out var co) && co != null)
            StopCoroutine(co);
        _followRetryMap.Remove(id);

        _suppressFollowOwnerIds.Add(id);

        detachedOwner = owner;
        return true;
    }

    bool TryDetachEndpointFromPO_Single(PlacementObject owner, RailSnapNode2D node)
    {
        if (owner == null || node == null) return false;

        var bind = owner.GetComponent<RailNodeFollowBinding2D>();
        if (bind == null) return false;

        var src = bind.Entries;
        if (src == null || src.Count == 0) return false;

        int ownerId = owner.GetInstanceID();
        var newEntries = new List<RailNodeFollowBinding2D.Entry>(src.Count);

        for (int i = 0; i < src.Count; i++)
        {
            var e = src[i];

            if (e.node == null) continue;

            // ✅ 이번에 떼는 endpoint만 제거
            if (e.node == node) continue;

            newEntries.Add(e);
        }

        // ✅ 이 node에 붙어 있던 follow만 제거
        var follow = node.GetComponent<RailNodeFollow2D>();
        if (follow != null && follow.ownerId == ownerId)
        {
            follow.Detach();
            Destroy(follow);
        }

        // ✅ owner tag도 내가 지우는 endpoint면 정리
        var tag = node.GetComponent<RailNodeOwnerTag2D>();
        if (tag != null && tag.Owner == owner)
            Destroy(tag);

        if (newEntries.Count == 0)
        {
            bind.Clear();
            Destroy(bind);
            owner.AutoRailAttach = false; // 이제 진짜 아무 것도 안 붙어있음
        }
        else
        {
            bind.SetEntries(newEntries);
            bind.SyncNow(syncPhysics: true, broadcastMoved: false);
            owner.AutoRailAttach = true; // 아직 다른 스냅포인트 바인딩 남아있음
        }

        return true;
    }

    static bool IsPointerOverUI()
    {
        if (EventSystem.current == null) return false;
        return EventSystem.current.IsPointerOverGameObject();
    }

    public void SyncRailBudgetFromScene()
    {
        var budget = RailBudget2D.Instance;
        if (budget == null) return;

        // 무제한이면 굳이 쓸 필요 없음(원하면 해도 됨)
        // if (!budget.IsLimited) return;

        int ghost = LayerMask.NameToLayer("Ghost");

        // Destroy는 프레임 끝에 반영되므로, "비활성화된 레일"은 이미 제거된 걸로 취급
        var rails = FindObjectsOfType<RailSpan2D>(true);

        int aliveCount = 0;
        for (int i = 0; i < rails.Length; i++)
        {
            var r = rails[i];
            if (r == null) continue;
            if (r.gameObject.layer == ghost) continue;
            if (!r.gameObject.activeInHierarchy) continue; // TryDeleteRail에서 먼저 SetActive(false) 함
            aliveCount++;
        }

        budget.ResetUsed(aliveCount);
    }

    public static bool HasPriorityAtPointer(Vector2 mouseWorld)
    {
        return Instance != null && Instance.HasPriorityAtPointer_Internal(mouseWorld);
    }

    bool HasPriorityAtPointer_Internal(Vector2 mouseWorld)
    {
        if (!enabled || !gameObject.activeInHierarchy || cam == null)
            return false;

        // 이미 레일 입력 진행 중이면 무조건 레일 우선
        if (_IsBusyNow())
            return true;

        // UI 위는 여기서 처리하지 않음
        // (GridPlacer / RailToolPlacer 각자 기존 UI 가드 유지)

        float worldPerPixel = (cam.orthographicSize * 2f) / Screen.height;

        float handleRadiusWorld = worldPerPixel * handlePickRadiusPx;
        float railRadiusWorld = worldPerPixel * railPickRadiusPx;

        // 1) 핸들 우선
        var handleHits = Physics2D.CircleCastAll(mouseWorld, handleRadiusWorld, Vector2.zero, 0f, handleMask);
        if (handleHits != null)
        {
            for (int i = 0; i < handleHits.Length; i++)
            {
                var c = handleHits[i].collider;
                if (c == null) continue;

                var h = c.GetComponentInParent<RailEndpointHandle2D>();
                if (h != null)
                    return true;
            }
        }

        // 2) RailSnapNode도 레일 설치 시작 입력으로 취급
        if (RailSnapNodeUtil.TryPickNode(mouseWorld, nodePickRadius, nodeMask, out _))
            return true;

        // 3) 레일 본체
        var railHits = Physics2D.CircleCastAll(mouseWorld, railRadiusWorld, Vector2.zero, 0f, railMask);
        if (railHits != null)
        {
            for (int i = 0; i < railHits.Length; i++)
            {
                var c = railHits[i].collider;
                if (c == null) continue;

                var r = c.GetComponentInParent<RailSpan2D>();
                if (r != null)
                    return true;
            }
        }

        return false;
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

        return hasStart;
    }

    public static bool IsShowingSnapPointGuides
    {
        get
        {
            if (Instance == null) return false;
            return Instance._IsShowingSnapPointGuides();
        }
    }

    bool _IsShowingSnapPointGuides()
    {
        if (!enabled || !gameObject.activeInHierarchy) return false;
        if (!IsBuildMode()) return false;

        // 레일 설치 중
        if (hasStart) return true;

        // 레일 핸들 드래그 중
        if (isDraggingHandle) return true;
        if (handleDragBegun) return true;
        if (activeHandle != null) return true;

        return false;
    }

    public RailHoverAction GetHoverActionAtMouse(Vector2 mouseWorld)
    {
        if (!CanEvaluateHover())
            return RailHoverAction.None;

        RailHoverAction result = RailHoverAction.None;

        bool budgetFull = !CanStartNewRailPlacement();

        // 원래 설치 가능한 시작 노드인지 먼저 검사
        if (TryGetStartNodeIgnoringBudget(mouseWorld, out var placeNode, out var snapPoint))
        {
            if (CanUseNodeAsPlaceStart(placeNode, snapPoint))
            {
                if (budgetFull)
                    result |= RailHoverAction.NoRailBudget;
                else
                    result |= RailHoverAction.CanPlace;
            }
        }

        if (TryGetDraggableNodeAt(mouseWorld, out _))
            result |= RailHoverAction.CanDrag;

        return result;
    }

    bool TryGetStartNodeIgnoringBudget(
    Vector2 mouseWorld,
    out RailSnapNode2D node,
    out SnapPoint pickedSnapPoint
)
    {
        node = null;
        pickedSnapPoint = null;

        if (!TryPickStartNode(mouseWorld, out var pickedNode, out var pickedOwner, out bool createdNew, out var sp))
            return false;

        if (pickedNode == null)
            return false;

        node = pickedNode;
        pickedSnapPoint = sp;
        return true;
    }

    bool CanEvaluateHover()
    {
        if (!enabled || !gameObject.activeInHierarchy)
            return false;

        if (!IsBuildMode())
            return false;

        EnsureFrameCaches();
        EnsureOccBakedAndMasks();

        if (hasStart)
            return false;

        return true;
    }

    bool TryGetPlaceableStartNodeAt(Vector2 mouseWorld, out RailSnapNode2D node)
    {
        node = null;

        if (!CanStartNewRailPlacement())
            return false;

        if (!TryPickStartNode(mouseWorld, out var pickedNode, out var pickedOwner, out bool createdNew, out var pickedSnapPoint))
            return false;

        if (pickedNode == null)
            return false;

        if (!CanUseNodeAsPlaceStart(pickedNode, pickedSnapPoint))
            return false;

        node = pickedNode;
        return true;
    }

    bool CanUseNodeAsPlaceStart(RailSnapNode2D node, SnapPoint pickedSnapPoint)
    {
        if (node == null)
            return false;

        // SnapPoint 기반 시작점은 이미 연결된 경우 새 설치 금지
        if (pickedSnapPoint != null && IsNodeAlreadyConnected(node))
            return false;

        if (!CanAddRailToNode(node))
            return false;

        return true;
    }

    bool TryGetDraggableNodeAt(Vector2 mouseWorld, out RailSnapNode2D node)
    {
        node = null;

        if (!RailSnapNodeUtil.TryPickNode(mouseWorld, nodePickRadius, nodeMask, out var picked))
            return false;

        if (!CanUseNodeAsDragTarget(picked))
            return false;

        node = picked;
        return true;
    }

    bool CanUseNodeAsDragTarget(RailSnapNode2D node)
    {
        if (node == null)
            return false;

        // 고정 앵커면 드래그 금지
        if (node.IsAnchor)
            return false;

        // 실제 연결된 레일이 없는 노드는 드래그 의미 없음
        return IsNodeAlreadyConnected(node);
    }

    bool IsNodeAlreadyConnected(RailSnapNode2D node)
    {
        if (node == null)
            return false;

        EnsureFrameCaches();

        nodeRailCount.TryGetValue(node, out int count);
        return count > 0;
    }

    void CommitStageChanged()
    {
        var save = FindFirstObjectByType<StageSaveManager>();
        if (save == null) return;

        save.NotifyStageChanged();
    }

    bool OwnerHasAnyBinding(PlacementObject owner)
    {
        if (owner == null) return false;

        var bind = owner.GetComponent<RailNodeFollowBinding2D>();
        return bind != null && bind.Entries != null && bind.Entries.Count > 0;
    }

    IEnumerator CoCommitRailDragAfterBindingSettle(bool hadUndoBegin)
    {
        yield return null;
        yield return new WaitForEndOfFrame();

        if (selectedRail != null)
        {
            selectedRail.Refresh(syncFromNodes: true);
            Physics2D.SyncTransforms();
        }

        var save = FindFirstObjectByType<StageSaveManager>();
        if (save != null && hadUndoBegin)
        {
            save.NotifyStageChanged();
            save.EndDeferredStageChanged(true);
            _dragUndoBeginNotified = false;
        }
        else
        {
            CommitStageChanged();
        }

        EndDraggingHandle();
        _railDragDeferredCommitCo = null;
    }

    void SetPreviewVisible(bool visible)
    {
        if (preview != null)
            preview.enabled = visible;

        if (_previewVisual != null)
        {
            var lrs = _previewVisual.GetComponentsInChildren<LineRenderer>(true);
            for (int i = 0; i < lrs.Length; i++)
                lrs[i].enabled = visible;
        }

        if (!visible)
            SetPreviewDotsEnabled(false);
    }

    public void ResetTransientStateForSceneChange()
    {
        var save = FindFirstObjectByType<StageSaveManager>();

        if (save != null && _dragUndoBeginNotified)
        {
            save.NotifyStageChangeBeginCanceled();
            save.EndDeferredStageChanged(false);
        }

        _dragUndoBeginNotified = false;

        if (_railDragDeferredCommitCo != null)
        {
            StopCoroutine(_railDragDeferredCommitCo);
            _railDragDeferredCommitCo = null;
        }

        // ✅ 드래그/선택 상태 정리
        CancelDraggingHandle();
        CancelPlacementPreview("SceneChanged");
        DeselectRail();

        // ✅ 노드 클릭 추적 상태 정리
        isTrackingNodeClick = false;
        trackedNode = null;
        trackedNodeDownWorld = default;

        // ✅ 시작점/스냅 상태 정리
        hasStart = false;
        startNode = null;
        startAttachOwner = null;
        endAttachOwner = null;
        startSnapPoint = null;
        endSnapPoint = null;
        startNodeCreatedNew = false;

        // ✅ 힌트/프리뷰 캐시 정리
        _railPlacementHintsBuilt = false;
        _railPlacementHintsStartNodeId = 0;
        _railPlacementHintsStartPos = default;
        _hintPts.Clear();

        _cachedPreviewEndPos = default;
        _cachedPreviewEndUsable = false;

        MoveHintBroker2D.Instance?.Clear(this);
        SetPreviewVisible(false);
    }
}
