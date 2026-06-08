using System.Collections.Generic;
using UnityEngine;

public class PlacementCellOverlay2D : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] GridPlacer gridPlacer;
    [SerializeField] GridManager grid;
    [SerializeField] HollowRectSpriteFrame placementFrame;

    [Header("Cell Visual")]
    [SerializeField] Sprite cellSprite;
    [SerializeField] Material cellMaterial;
    [SerializeField] string sortingLayerName = "Default";
    [SerializeField] int sortingOrder = 200;

    [Header("Colors")]
    [SerializeField] Color validAllCellColor = new Color(0.2f, 1f, 0.2f, 0.28f);
    [SerializeField] Color invalidBaseCellColor = new Color(1f, 0.35f, 0.35f, 0.10f);
    [SerializeField] Color invalidBlockedCellColor = new Color(1f, 0.2f, 0.2f, 0.35f);

    [Header("Options")]
    [SerializeField] bool showOnlyWhileDraggingSelected = true;
    [SerializeField] bool showDuringPlacementPreview = true;
    [SerializeField] bool hideInPlayMode = true;

    [Header("Collision Rules")]
    [SerializeField] bool blockByRailCells = true;
    [SerializeField] bool blockByPOBlockedCells = true;   // ✅ 추가

    readonly List<SpriteRenderer> _pool = new List<SpriteRenderer>(64);
    readonly List<Vector2Int> _cells = new List<Vector2Int>(128);
    readonly List<bool> _blocked = new List<bool>(128);

    readonly List<SpriteRenderer> _externalPool = new List<SpriteRenderer>(64);
    readonly List<Vector2Int> _externalCells = new List<Vector2Int>(128);
    readonly List<bool> _externalBlocked = new List<bool>(128);

    bool _showExternalPreview = false;
    bool _externalHasAnyBlocked = false;

    void Awake()
    {
        if (gridPlacer == null)
            gridPlacer = FindFirstObjectByType<GridPlacer>();

        if (grid == null)
            grid = FindFirstObjectByType<GridManager>();

        if (placementFrame == null)
            placementFrame = FindFirstObjectByType<HollowRectSpriteFrame>();

        HideAll();
    }

    void LateUpdate()
    {
        if (_showExternalPreview)
        {
            HideAll();              // 현재 점유영역 숨김
            DrawExternalPreview();
            return;
        }

        if (gridPlacer == null || grid == null)
        {
            HideAll();
            return;
        }

        if (hideInPlayMode && GameModeManager.Instance != null && !GameModeManager.Instance.IsBuildMode)
        {
            HideAll();
            return;
        }

        var occ = GridOccupancy2D.Instance;
        if (occ == null)
        {
            HideAll();
            return;
        }

        PlacementObject targetPO = null;
        bool isPlacementPreview = false;

        // 1) 설치 프리뷰 우선
        if (showDuringPlacementPreview && gridPlacer.HasPlacementPreview && gridPlacer.PreviewPlacementObject != null)
        {
            targetPO = gridPlacer.PreviewPlacementObject;
            isPlacementPreview = true;
        }
        else
        {
            // 2) 선택 드래그
            targetPO = gridPlacer.SelectedPO;

            if (targetPO == null)
            {
                HideAll();
                return;
            }

            bool dragging = gridPlacer.IsDraggingSelectedPO || gridPlacer.IsDragCandidateSelectedPO;
            if (showOnlyWhileDraggingSelected && !dragging)
            {
                HideAll();
                return;
            }
        }

        if (targetPO == null || !targetPO.UseManualOccupancy)
        {
            HideAll();
            return;
        }

        _cells.Clear();
        targetPO.GetManualOccupiedCellsAtWorld(grid, targetPO.transform.position, _cells);

        if (_cells.Count == 0)
        {
            HideAll();
            return;
        }

        EnsurePool(_cells.Count);

        // 선택 드래그는 자기 자신 owner 무시
        // 설치 프리뷰는 아직 실제 배치 owner가 아니므로 무시 ID 0
        int ignoreOwnerId = isPlacementPreview ? 0 : targetPO.GetInstanceID();

        RailCellMap2D railMap = RailCellMap2D.Instance;

        _blocked.Clear();
        bool hasAnyBlocked = false;

        for (int i = 0; i < _cells.Count; i++)
        {
            Vector2Int cell = _cells[i];

            bool occupied = occ.IsOccupiedCell(cell, ignoreOwnerId: ignoreOwnerId);

            bool outsideFrame = false;
            if (placementFrame != null)
            {
                Vector3 cellWorld = grid.CellToWorld(cell);
                outsideFrame = !placementFrame.ContainsWorldPointInHole(cellWorld);
            }

            bool railBlocked = false;
            if (blockByRailCells && railMap != null)
            {
                railBlocked = railMap.HasRailAtCell(cell);
            }

            // ✅ 추가: 맵 앵커 기준 PO 전용 금지 셀
            bool poBlocked = false;
            if (blockByPOBlockedCells)
            {
                poBlocked = occ.IsPOBlockedCell(cell);
            }

            bool blocked = occupied || outsideFrame || railBlocked || poBlocked;
            _blocked.Add(blocked);

            if (blocked)
                hasAnyBlocked = true;
        }

        float step = grid.cellSize;
        Vector3 cellScale = new Vector3(step, step, 1f);

        for (int i = 0; i < _pool.Count; i++)
        {
            bool active = i < _cells.Count;
            if (_pool[i].gameObject.activeSelf != active)
                _pool[i].gameObject.SetActive(active);

            if (!active)
                continue;

            Vector2Int cell = _cells[i];
            Vector3 pos = grid.CellToWorld(cell);
            pos.z = 0f;

            var sr = _pool[i];
            sr.transform.position = pos;
            sr.transform.localScale = cellScale;

            if (!hasAnyBlocked)
            {
                sr.color = validAllCellColor;
            }
            else
            {
                sr.color = _blocked[i] ? invalidBlockedCellColor : invalidBaseCellColor;
            }
        }
    }

    void EnsurePool(int count)
    {
        while (_pool.Count < count)
        {
            GameObject go = new GameObject($"CellOverlay_{_pool.Count}");
            go.transform.SetParent(transform, false);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = cellSprite;

            if (cellMaterial != null)
                sr.material = cellMaterial;

            sr.sortingLayerName = sortingLayerName;
            sr.sortingOrder = sortingOrder;

            _pool.Add(sr);
        }
    }

    void HideAll()
    {
        for (int i = 0; i < _pool.Count; i++)
        {
            if (_pool[i] != null && _pool[i].gameObject.activeSelf)
                _pool[i].gameObject.SetActive(false);
        }
    }

    public bool ShowStrengthHoverPreview(
    PlacementObject targetPO,
    StrengthBasedOccupancyCells strength,
    int targetLevel)
    {
        if (targetPO == null || strength == null || grid == null)
        {
            ClearExternalPreview();
            return false;
        }

        var offsets = strength.GetOffsetsForLevel(targetLevel);
        if (offsets == null || offsets.Length == 0)
        {
            ClearExternalPreview();
            return false;
        }

        _externalCells.Clear();
        _externalBlocked.Clear();

        Vector2Int anchor = grid.WorldToCell(targetPO.transform.position);

        bool flipX = targetPO.transform.localScale.x < 0f;

        float z = targetPO.transform.eulerAngles.z;
        int rotSteps = Mathf.RoundToInt(z / 90f) % 4;
        if (rotSteps < 0) rotSteps += 4;

        var occ = GridOccupancy2D.Instance;
        var railMap = RailCellMap2D.Instance;

        bool hasAnyBlocked = false;
        int ignoreOwnerId = targetPO.GetInstanceID();

        for (int i = 0; i < offsets.Length; i++)
        {
            int x = offsets[i].x;
            int y = offsets[i].y;

            // PlacementObject.GetOccupiedCellsAtWorld 와 동일한 규칙 적용
            if (flipX) x = -x;

            switch (rotSteps)
            {
                default:
                case 0: break;
                case 1: (x, y) = (-y, x); break;
                case 2: (x, y) = (-x, -y); break;
                case 3: (x, y) = (y, -x); break;
            }

            Vector2Int cell = new Vector2Int(anchor.x + x, anchor.y + y);
            _externalCells.Add(cell);

            bool occupied = occ != null && occ.IsOccupiedCell(cell, ignoreOwnerId: ignoreOwnerId);

            bool outsideFrame = false;
            if (placementFrame != null)
            {
                Vector3 cellWorld = grid.CellToWorld(cell);
                outsideFrame = !placementFrame.ContainsWorldPointInHole(cellWorld);
            }

            bool railBlocked = blockByRailCells && railMap != null && railMap.HasRailAtCell(cell);
            bool poBlocked = blockByPOBlockedCells && occ != null && occ.IsPOBlockedCell(cell);

            bool blocked = occupied || outsideFrame || railBlocked || poBlocked;
            _externalBlocked.Add(blocked);

            if (blocked)
                hasAnyBlocked = true;
        }

        _externalHasAnyBlocked = hasAnyBlocked;
        _showExternalPreview = true;
        return !hasAnyBlocked;
    }

    public void ClearExternalPreview()
    {
        _showExternalPreview = false;

        for (int i = 0; i < _externalPool.Count; i++)
        {
            if (_externalPool[i] != null && _externalPool[i].gameObject.activeSelf)
                _externalPool[i].gameObject.SetActive(false);
        }
    }

    void DrawExternalPreview()
    {
        if (_externalCells.Count == 0)
        {
            ClearExternalPreview();
            return;
        }

        EnsureExternalPool(_externalCells.Count);

        float step = grid.cellSize;
        Vector3 cellScale = new Vector3(step, step, 1f);

        for (int i = 0; i < _externalPool.Count; i++)
        {
            bool active = i < _externalCells.Count;
            if (_externalPool[i].gameObject.activeSelf != active)
                _externalPool[i].gameObject.SetActive(active);

            if (!active)
                continue;

            Vector2Int cell = _externalCells[i];
            Vector3 pos = grid.CellToWorld(cell);
            pos.z = 0f;

            var sr = _externalPool[i];
            sr.transform.position = pos;
            sr.transform.localScale = cellScale;

            if (!_externalHasAnyBlocked)
            {
                sr.color = validAllCellColor;
            }
            else
            {
                sr.color = _externalBlocked[i]
                    ? invalidBlockedCellColor   // 실제로 막힌 셀
                    : invalidBaseCellColor;     // 설치는 불가하지만 이 셀 자체는 직접 충돌 아님
            }
        }
    }

    void EnsureExternalPool(int count)
    {
        while (_externalPool.Count < count)
        {
            GameObject go = new GameObject($"StrengthHoverCell_{_externalPool.Count}");
            go.transform.SetParent(transform, false);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = cellSprite;

            if (cellMaterial != null)
                sr.material = cellMaterial;

            sr.sortingLayerName = sortingLayerName;
            sr.sortingOrder = sortingOrder; // 기존보다 살짝 위
            _externalPool.Add(sr);
        }
    }
}