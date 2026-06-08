using System;
using System.Collections.Generic;
using UnityEngine;

public enum PhysicsType
{
    Static,
    DynamicNoGravity,
    DynamicGravity
}

public enum ActionButtonsAttachDirection
{
    Above,
    Below
}

public class PlacementObject : MonoBehaviour
{
    [Header("Interaction")]
    [SerializeField] bool selectable = true;

    public bool Selectable => selectable;

    [Header("Action Buttons Display (Optional)")]
    [Tooltip("PO 제어 버튼 표시의 기준 중심. 비워두면 PO의 transform.position을 사용")]
    [SerializeField] Transform actionButtonsAnchor = null;

    [Tooltip("버튼 위치를 추가로 이동시키는 오프셋")]
    [SerializeField] Vector2 actionButtonsOffset = new Vector2(0f, 0f);

    [Header("Action Buttons Attach")]
    [SerializeField] ActionButtonsAttachDirection actionButtonsAttachDirection = ActionButtonsAttachDirection.Above;

    public ActionButtonsAttachDirection ActionButtonsAttachDirection => actionButtonsAttachDirection;


    [Tooltip("true면 actionButtonsOffset을 PO 로컬 기준으로 해석(회전/플립 반영). false면 월드 오프셋")]
    [SerializeField] bool actionButtonsOffsetIsLocal = true;

    [Header("Hint Display (Optional)")]
    [Tooltip("힌트 점 표시의 기준 중심. 비워두면 PO의 transform.position을 사용")]
    [SerializeField] public Transform hintCenter = null;

    SpriteRenderer _hintCenterSr;
    bool _hintCenterSrOriginalEnabled = true;

    [Tooltip("true면 hintCenter를 PO 드래그 힌트가 활성화될 때만 보이게 제어합니다.")]
    [SerializeField] bool controlHintCenterVisibility = true;

    [Tooltip("controlHintCenterVisibility가 true일 때, 기본 상태(드래그 아닐 때)를 숨김으로 시작합니다.")]
    [SerializeField] bool hideHintCenterWhenIdle = true;

    [Tooltip("힌트 점 표시를 추가로 이동시키는 오프셋")]
    [SerializeField] Vector2 hintOffset = Vector2.zero;

    [Tooltip("true면 hintOffset을 PO 로컬 기준으로 해석(회전/플립 반영). false면 월드 오프셋")]
    [SerializeField] bool hintOffsetIsLocal = true;

    [Header("Build Mode Only Visuals")]
    [Tooltip("배치 모드(Build)일 때만 보이고, 플레이 모드(Play)일 때는 숨길 SpriteRenderer들")]
    [SerializeField] SpriteRenderer[] buildModeOnlyRenderers = Array.Empty<SpriteRenderer>();

    [Tooltip("비어 있으면 자식 SpriteRenderer 중에서 자동 수집할지 여부")]
    [SerializeField] bool autoCollectBuildModeOnlyRenderers = false;

    [HideInInspector] public List<SnapConnection> connections = new();
    public PlacementData placementData;

    Collider2D[] colliders;


    [Header("Physics")]
    public PhysicsType physicsType = PhysicsType.Static;

    [SerializeField, HideInInspector]
    string persistentId;

    [SerializeField] bool autoRailAttach = true;
    public bool AutoRailAttach
    {
        get => autoRailAttach;
        set => autoRailAttach = value;
    }

    public string PersistentId => persistentId;
    public bool HasMultipleConnections => connections.Count >= 2;

    static readonly List<Vector2Int> _tmpManualCells = new List<Vector2Int>(128);
    static readonly HashSet<int> _tmpIgnoreOwners = new HashSet<int>(16);
    static readonly List<Vector2Int> _tmpOccupancyOffsets = new List<Vector2Int>(128);

    [Serializable]
    public struct ColliderState
    {
        public Collider2D col;
        public bool enabled;
        public bool isTrigger;
    }

    ColliderState[] originalColliderStates;

    [Serializable]
    public struct Rigidbody2DState
    {
        public Rigidbody2D rb;
        public bool simulated;
    }

    [Serializable]
    public struct Rigidbody3DState
    {
        public Rigidbody rb;
        public bool isKinematic;
        public bool detectCollisions;
    }

    [Header("Ghost/Preview Physics")]
    [Tooltip("고스트(프리뷰/드래그) 상태에서는 하위 Rigidbody들을 잠깐 멈춥니다. (프리뷰에서 떨어지는 문제 방지)")]
    [SerializeField] bool disableRigidbodiesWhenGhost = true;

    Rigidbody2DState[] originalRb2DStates;
    Rigidbody3DState[] originalRb3DStates;

    [Header("Occupancy (Manual)")]
    [SerializeField] bool useManualOccupancy = false;



#if UNITY_EDITOR
    [Header("Manual Occupancy Preview (Editor)")]
    [SerializeField] bool drawManualOccupancyInEditMode = true;

    [Tooltip("씬에 GridManager가 없을 때(프리팹 편집 모드 등) 사용할 셀 크기")]
    [SerializeField] float previewCellSize = 1f;

    [SerializeField] Color manualOccupancyPreviewFillColor = new Color(0f, 1f, 1f, 0.35f);
    [SerializeField] Color manualOccupancyPreviewOutlineColor = new Color(0f, 1f, 1f, 0.9f);
#endif

    [Tooltip("피벗 셀 기준 오프셋 셀 목록")]
    [SerializeField] Vector2Int[] manualCellOffsets = Array.Empty<Vector2Int>();

    public bool UseManualOccupancy => useManualOccupancy;

    /// <summary>
    /// 현재 PO가 사용할 점유 셀 오프셋(피벗 기준)을 가져온다.
    /// - 상태형 공급자(IOccupancyCellProvider)가 있으면 그 값을 우선 사용
    /// - 없으면 기존 manualCellOffsets를 사용
    /// </summary>
    protected virtual void GetCurrentOccupancyCellOffsets(List<Vector2Int> outOffsets)
    {
        outOffsets.Clear();

        if (!useManualOccupancy)
            return;

        var provider = GetComponent<IOccupancyCellProvider>();
        if (provider != null && provider.TryGetOccupancyCellOffsets(outOffsets))
            return;

        if (manualCellOffsets == null || manualCellOffsets.Length == 0)
            return;

        for (int i = 0; i < manualCellOffsets.Length; i++)
            outOffsets.Add(manualCellOffsets[i]);
    }

    /// <summary>
    /// 현재 상태 기준 점유 셀(월드 기준)을 가져온다.
    /// 기존 manual occupancy의 확장 버전.
    /// </summary>
    public void GetOccupiedCells(GridManager grid, List<Vector2Int> outCells)
    {
        GetOccupiedCellsAtWorld(grid, transform.position, outCells);
    }

    /// <summary>
    /// 현재 상태 기준 점유 셀(월드 기준)을 계산한다.
    /// 회전/플립 반영은 공통 처리한다.
    /// </summary>
    public void GetOccupiedCellsAtWorld(GridManager grid, Vector2 worldPos, List<Vector2Int> outCells)
    {
        outCells.Clear();
        if (!useManualOccupancy) return;
        if (grid == null) return;

        _tmpOccupancyOffsets.Clear();
        GetCurrentOccupancyCellOffsets(_tmpOccupancyOffsets);
        if (_tmpOccupancyOffsets.Count == 0) return;

        Vector2Int anchor = (Vector2Int)grid.WorldToCell(worldPos);

        bool flipX = transform.localScale.x < 0f;

        float z = transform.eulerAngles.z;
        int rotSteps = Mathf.RoundToInt(z / 90f) % 4;
        if (rotSteps < 0) rotSteps += 4;

        for (int i = 0; i < _tmpOccupancyOffsets.Count; i++)
        {
            Vector2Int o = _tmpOccupancyOffsets[i];
            int x = o.x;
            int y = o.y;

            if (flipX) x = -x;

            switch (rotSteps)
            {
                default:
                case 0: break;
                case 1: (x, y) = (-y, x); break;
                case 2: (x, y) = (-x, -y); break;
                case 3: (x, y) = (y, -x); break;
            }

            outCells.Add(new Vector2Int(anchor.x + x, anchor.y + y));
        }
    }

    void Awake()
    {
        EnsureId();

        if (hintCenter != null)
        {
            _hintCenterSr = hintCenter.GetComponent<SpriteRenderer>();
            if (_hintCenterSr == null)
                _hintCenterSr = hintCenter.GetComponentInChildren<SpriteRenderer>(true);

            if (_hintCenterSr != null)
                _hintCenterSrOriginalEnabled = _hintCenterSr.enabled;
        }

        if (controlHintCenterVisibility && hideHintCenterWhenIdle)
            SetHintCenterDragVisible(false);

        colliders = GetComponentsInChildren<Collider2D>(true);

        originalColliderStates = new ColliderState[colliders.Length];
        for (int i = 0; i < colliders.Length; i++)
        {
            var c = colliders[i];
            originalColliderStates[i] = new ColliderState
            {
                col = c,
                enabled = c ? c.enabled : false,
                isTrigger = c ? c.isTrigger : false
            };
        }

        var rbs2d = GetComponentsInChildren<Rigidbody2D>(true);
        originalRb2DStates = new Rigidbody2DState[rbs2d.Length];
        for (int i = 0; i < rbs2d.Length; i++)
        {
            var rb2d = rbs2d[i];
            originalRb2DStates[i] = new Rigidbody2DState
            {
                rb = rb2d,
                simulated = rb2d ? rb2d.simulated : false
            };
        }

        var rbs3d = GetComponentsInChildren<Rigidbody>(true);
        originalRb3DStates = new Rigidbody3DState[rbs3d.Length];
        for (int i = 0; i < rbs3d.Length; i++)
        {
            var rb = rbs3d[i];
            originalRb3DStates[i] = new Rigidbody3DState
            {
                rb = rb,
                isKinematic = rb ? rb.isKinematic : false,
                detectCollisions = rb ? rb.detectCollisions : false
            };
        }

        var roots = GetComponentsInChildren<SnapRoot>(true);
        foreach (var root in roots)
        {
            root.owner = this;
            foreach (var p in root.GetComponentsInChildren<SnapPoint>(true))
                p.root = root;
        }
    }

    void OnEnable()
    {
        GameModeManager.OnModeChanged += HandleModeChanged;

        if (autoCollectBuildModeOnlyRenderers &&
            (buildModeOnlyRenderers == null || buildModeOnlyRenderers.Length == 0))
        {
            buildModeOnlyRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        }

        RefreshBuildModeOnlyVisuals();
    }

    void OnDisable()
    {
        GameModeManager.OnModeChanged -= HandleModeChanged;
    }

    void Start()
    {
        RefreshBuildModeOnlyVisuals();
    }

    void HandleModeChanged(GameMode mode)
    {
        RefreshBuildModeOnlyVisuals();
    }

    void RefreshBuildModeOnlyVisuals()
    {
        bool visible = true;

        if (GameModeManager.Instance != null)
            visible = GameModeManager.Instance.IsBuildMode;

        if (buildModeOnlyRenderers == null) return;

        for (int i = 0; i < buildModeOnlyRenderers.Length; i++)
        {
            var sr = buildModeOnlyRenderers[i];
            if (sr == null) continue;
            sr.enabled = visible;
        }
    }

    public void EnsureId()
    {
        if (string.IsNullOrEmpty(persistentId))
            persistentId = Guid.NewGuid().ToString();
    }

    public void SetPersistentId(string id) => persistentId = id;

    /// <summary>
    /// 힌트 점(표시용) 이동량(월드)을 반환.
    /// - 실제 '가능 위치' 계산에는 영향을 주지 않고, 표시만 PO 기준으로 옮길 때 사용.
    /// </summary>
    public Vector2 GetHintShiftWorld()
    {
        Vector2 poPos = (Vector2)transform.position;
        Vector2 basePos = (hintCenter != null) ? (Vector2)hintCenter.position : poPos;

        Vector2 offsetWorld = hintOffsetIsLocal
            ? (Vector2)transform.TransformVector(new Vector3(hintOffset.x, hintOffset.y, 0f))
            : hintOffset;

        return (basePos - poPos) + offsetWorld;
    }

    /// <summary>
    /// hintCenter에 연결된 오브젝트를 드래그 중에만 보이게(또는 원상복구) 합니다.
    /// </summary>
    public void SetHintCenterDragVisible(bool visible)
    {
        if (!controlHintCenterVisibility) return;
        if (_hintCenterSr == null) return;

        if (visible)
        {
            _hintCenterSr.enabled = true;
        }
        else
        {
            _hintCenterSr.enabled = hideHintCenterWhenIdle ? false : _hintCenterSrOriginalEnabled;
        }
    }

    public Vector2 GetActionButtonsWorldPos()
    {
        Vector2 poPos = (Vector2)transform.position;

        Vector2 basePos = (actionButtonsAnchor != null)
            ? (Vector2)actionButtonsAnchor.position
            : poPos;

        Vector2 offset = actionButtonsOffset;

        // 아래에 붙는 타입이면 Y 오프셋을 반대로
        if (actionButtonsAttachDirection == ActionButtonsAttachDirection.Below)
            offset.y = -Mathf.Abs(offset.y);
        else
            offset.y = Mathf.Abs(offset.y);

        Vector2 offsetWorld = actionButtonsOffsetIsLocal
            ? (Vector2)transform.TransformVector(new Vector3(offset.x, offset.y, 0f))
            : offset;

        return basePos + offsetWorld;
    }

    // =========================
    // Snap
    // =========================
    public void BreakAllSnaps()
    {
        var snapshot = new List<SnapConnection>(connections);

        foreach (var c in snapshot)
        {
            if (c.otherRoot != null && c.otherRoot.owner != null)
            {
                c.otherRoot.owner.connections.RemoveAll(x => x.otherRoot == c.myRoot);
            }
        }

        connections.Clear();
    }

    void ApplyGhostPhysicsState(bool ghost)
    {
        if (!disableRigidbodiesWhenGhost) return;

        if (originalRb2DStates != null)
        {
            for (int i = 0; i < originalRb2DStates.Length; i++)
            {
                var st = originalRb2DStates[i];
                if (!st.rb) continue;

                if (ghost)
                {
                    if (st.rb.bodyType != RigidbodyType2D.Static)
                    {
                        st.rb.velocity = Vector2.zero;
                        st.rb.angularVelocity = 0f;
                    }
                    st.rb.simulated = false;
                }
                else
                {
                    st.rb.simulated = st.simulated;
                }
            }
        }

        if (originalRb3DStates != null)
        {
            for (int i = 0; i < originalRb3DStates.Length; i++)
            {
                var st = originalRb3DStates[i];
                if (!st.rb) continue;

                if (ghost)
                {
                    st.rb.velocity = Vector3.zero;
                    st.rb.angularVelocity = Vector3.zero;
                    st.rb.detectCollisions = false;
                    st.rb.isKinematic = true;
                }
                else
                {
                    st.rb.isKinematic = st.isKinematic;
                    st.rb.detectCollisions = st.detectCollisions;
                }
            }
        }
    }

    // =========================
    // Modes
    // =========================
    public void SetGhost()
    {
        ApplyGhostPhysicsState(true);

        foreach (var st in originalColliderStates)
        {
            if (!st.col) continue;
            st.col.enabled = true;
            st.col.isTrigger = true;
        }

    }

    public void SetPlaced()
    {
        ApplyGhostPhysicsState(false);

        foreach (var st in originalColliderStates)
        {
            if (!st.col) continue;
            st.col.enabled = st.enabled;
            st.col.isTrigger = st.isTrigger;
        }

        var rb = GetComponent<Rigidbody2D>();
        if (rb != null && rb.bodyType != RigidbodyType2D.Static)
        {
            rb.velocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

    }

    // =========================
    // Visual
    // =========================

    // =========================
    // Occupancy
    // =========================
    public void GetManualOccupiedCells(GridManager grid, List<Vector2Int> outCells)
    {
        GetOccupiedCells(grid, outCells);
    }

    /// <summary>
    /// 이 월드 좌표에 PO가 있었다면 점유맵 기준으로 설치 가능 여부를 판단.
    /// </summary>
    public bool CanPlaceByRuleAtWorld(
        Vector2 worldPos,
        GridManager grid = null,
        PlacementObject snapTarget = null,
        IReadOnlyList<PlacementObject> extraAllowedTargets = null,
        HashSet<int> ignoreOwnersOverride = null
    )
    {
        var occ = GridOccupancy2D.Instance;
        if (occ == null) return true;

        occ.EnsureBaked();

        int selfId = GetInstanceID();

        HashSet<int> ignoreOwners = ignoreOwnersOverride;
        if (ignoreOwners == null)
        {
            _tmpIgnoreOwners.Clear();

            if (snapTarget != null)
                _tmpIgnoreOwners.Add(snapTarget.GetInstanceID());

            if (extraAllowedTargets != null)
            {
                for (int i = 0; i < extraAllowedTargets.Count; i++)
                {
                    var t = extraAllowedTargets[i];
                    if (t != null)
                        _tmpIgnoreOwners.Add(t.GetInstanceID());
                }
            }

            ignoreOwners = (_tmpIgnoreOwners.Count > 0) ? _tmpIgnoreOwners : null;
        }

        if (UseManualOccupancy)
        {
            var g = (occ.grid != null) ? occ.grid : grid;
            if (g == null) return true;

            _tmpManualCells.Clear();
            GetManualOccupiedCellsAtWorld(g, worldPos, _tmpManualCells);
            if (_tmpManualCells.Count == 0) return true;

            if (occ.WouldOverlapOccupiedCells(_tmpManualCells, selfId))
                return false;
        }
        else
        {
            if (occ.WouldOverlapOccupied(colliders, selfId, ignoreOwners))
                return false;
        }

        return true;
    }

    public void GetManualOccupiedCellsAtWorld(GridManager grid, Vector2 worldPos, List<Vector2Int> outCells)
    {
        GetOccupiedCellsAtWorld(grid, worldPos, outCells);
    }

#if UNITY_EDITOR
    void GetTransformedManualCellOffsets(List<Vector2Int> outOffsets)
    {
        outOffsets.Clear();
        if (!useManualOccupancy) return;
        if (manualCellOffsets == null || manualCellOffsets.Length == 0) return;

        bool flipX = transform.localScale.x < 0f;

        float z = transform.eulerAngles.z;
        int rotSteps = Mathf.RoundToInt(z / 90f) % 4;
        if (rotSteps < 0) rotSteps += 4;

        for (int i = 0; i < manualCellOffsets.Length; i++)
        {
            Vector2Int o = manualCellOffsets[i];
            int x = o.x;
            int y = o.y;

            if (flipX) x = -x;

            switch (rotSteps)
            {
                default:
                case 0: break;
                case 1: (x, y) = (-y, x); break;
                case 2: (x, y) = (-x, -y); break;
                case 3: (x, y) = (y, -x); break;
            }

            outOffsets.Add(new Vector2Int(x, y));
        }
    }
#endif

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (!drawManualOccupancyInEditMode) return;
        if (!useManualOccupancy) return;

        var grid = FindFirstObjectByType<GridManager>();

        // 1) 씬에 GridManager가 있으면 실제 grid 기준으로 그림
        if (grid != null)
        {
            Vector2 cellSize2 = Vector2.one * grid.cellSize;

            _tmpManualCells.Clear();
            GetOccupiedCellsAtWorld(grid, transform.position, _tmpManualCells);

            Gizmos.color = manualOccupancyPreviewFillColor;

            for (int i = 0; i < _tmpManualCells.Count; i++)
            {
                Vector2Int cell = _tmpManualCells[i];
                Vector2 center = grid.CellToWorld(cell);

                Gizmos.DrawCube(
                    new Vector3(center.x, center.y, 0f),
                    new Vector3(cellSize2.x, cellSize2.y, 0.01f)
                );

                Gizmos.color = manualOccupancyPreviewOutlineColor;
                Gizmos.DrawWireCube(
                    new Vector3(center.x, center.y, 0f),
                    new Vector3(cellSize2.x, cellSize2.y, 0.01f)
                );

                Gizmos.color = manualOccupancyPreviewFillColor;
            }

            return;
        }

        // 2) GridManager가 없으면 프리팹 편집 모드용 로컬 프리뷰
        var transformedOffsets = new List<Vector2Int>();
        GetTransformedManualCellOffsets(transformedOffsets);

        if (transformedOffsets.Count == 0) return;

        float step = Mathf.Max(0.0001f, previewCellSize);
        Vector3 size = new Vector3(step, step, 0.01f);

        Gizmos.color = manualOccupancyPreviewFillColor;

        for (int i = 0; i < transformedOffsets.Count; i++)
        {
            Vector2Int o = transformedOffsets[i];

            Vector3 center =
                transform.position +
                transform.right * (o.x * step) +
                transform.up * (o.y * step);

            Gizmos.DrawCube(center, size);

            Gizmos.color = manualOccupancyPreviewOutlineColor;
            Gizmos.DrawWireCube(center, size);

            Gizmos.color = manualOccupancyPreviewFillColor;
        }
    }

    [Header("Debug Drag Occupancy Gizmos")]
    [SerializeField] bool drawDragOccupancyGizmos = true;
    [SerializeField] Color dragFreeCellColor = new Color(0.2f, 1f, 0.2f, 0.28f);
    [SerializeField] Color dragBlockedCellColor = new Color(1f, 0.2f, 0.2f, 0.35f);
    [SerializeField] Color dragCellOutlineColor = new Color(1f, 1f, 1f, 0.15f);

    void OnDrawGizmos()
    {
        if (!drawDragOccupancyGizmos) return;
        if (!Application.isPlaying) return;
        if (!useManualOccupancy) return;

        var grid = FindFirstObjectByType<GridManager>();
        var occ = GridOccupancy2D.Instance;
        var gridPlacer = FindFirstObjectByType<GridPlacer>();

        if (grid == null || occ == null || gridPlacer == null)
            return;

        if (gridPlacer.SelectedPO != this)
            return;

        if (!gridPlacer.IsDraggingSelectedPO && !gridPlacer.IsDragCandidateSelectedPO)
            return;

        _tmpManualCells.Clear();
        GetOccupiedCellsAtWorld(grid, transform.position, _tmpManualCells);

        if (_tmpManualCells.Count == 0)
            return;

        float step = grid.cellSize;
        Vector3 size = new Vector3(step, step, 0.01f);

        int selfId = GetInstanceID();

        for (int i = 0; i < _tmpManualCells.Count; i++)
        {
            Vector2Int cell = _tmpManualCells[i];
            Vector3 center = (Vector3)grid.CellToWorld(cell);

            bool blocked = occ.IsOccupiedCell(cell, ignoreOwnerId: selfId);

            Gizmos.color = blocked ? dragBlockedCellColor : dragFreeCellColor;
            Gizmos.DrawCube(center, size);

            Gizmos.color = dragCellOutlineColor;
            Gizmos.DrawWireCube(center, size);
        }
    }
#endif

#if UNITY_EDITOR
    public Vector2Int[] EditorGetManualCellOffsetsCopy()
    {
        if (manualCellOffsets == null || manualCellOffsets.Length == 0)
            return Array.Empty<Vector2Int>();

        var copy = new Vector2Int[manualCellOffsets.Length];
        Array.Copy(manualCellOffsets, copy, manualCellOffsets.Length);
        return copy;
    }

    public void EditorSetManualCellOffsets(IReadOnlyList<Vector2Int> src)
    {
        if (src == null || src.Count == 0)
        {
            manualCellOffsets = Array.Empty<Vector2Int>();
            return;
        }

        manualCellOffsets = new Vector2Int[src.Count];
        for (int i = 0; i < src.Count; i++)
            manualCellOffsets[i] = src[i];
    }
#endif
}