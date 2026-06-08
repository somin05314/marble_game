using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Build-mode helper overlay:
/// - occupied(wall/placed) cells are always visible (if enabled)
/// - PO-blocked cells are shown only during placement preview / PO drag
/// </summary>
[DefaultExecutionOrder(300)]
public class OccupancyHintOverlay2D : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] GridOccupancy2D occupancy;
    [SerializeField] GridManager grid;
    [SerializeField] GridPlacer gridPlacer;
    [SerializeField] Camera renderCamera; // 비우면 Camera.main

    [Header("Render")]
    [SerializeField] Material dotMaterial;
    [SerializeField] Color occupiedColor = new Color(1f, 0.25f, 0.25f, 0.25f);
    [SerializeField] Color poBlockedColor = new Color(1f, 0.1f, 0.1f, 0.42f);
    [SerializeField, Min(0.0001f)] float dotSize = 0.14f;

    [Header("Render Order (Behind Sprites)")]
    [SerializeField] bool overrideRenderQueue = true;
    [SerializeField] int renderQueue = 2900;

    [Header("Scan")]
    [SerializeField] bool scanOnlyCameraView = true;
    [SerializeField] int marginCells = 2;
    [SerializeField] int maxDots = 5000;
    [SerializeField] float updateInterval = 0.08f;

    [Header("Always Visible")]
    [SerializeField] bool visible = true;
    [SerializeField] bool includeWalls = true;
    [SerializeField] bool includePlaced = true;

    [Header("PO Block Visibility")]
    [SerializeField] bool includePOBlockedCells = true;
    [SerializeField] bool showPOBlockedDuringPlacementPreview = true;
    [SerializeField] bool showPOBlockedDuringSelectedPODrag = true;
    [SerializeField] bool showPOBlockedDuringDragCandidate = true;

    [Header("Hide In Play Mode")]
    [SerializeField] bool hideInPlayMode = true;
    [SerializeField] GameModeManager gameMode;

    [Header("Hide During Drag")]
    [SerializeField] int hideOwnerId = 0; // 0이면 숨김 없음

    readonly List<Matrix4x4> _occupiedMatrices = new(8192);
    readonly List<Matrix4x4> _poBlockedMatrices = new(2048);

    Mesh _quad;
    MaterialPropertyBlock _mpb;
    float _nextUpdateT;

    Material _matInst;
    bool _matInstOwned;

    public void SetHideOwnerId(int ownerId) => hideOwnerId = ownerId;
    public void ClearHideOwnerId() => hideOwnerId = 0;
    public void SetVisible(bool on) => visible = on;

    void Awake()
    {
        if (occupancy == null) occupancy = GridOccupancy2D.Instance;
        if (grid == null && occupancy != null) grid = occupancy.grid;
        if (gridPlacer == null) gridPlacer = FindFirstObjectByType<GridPlacer>();

        EnsureQuad();
        if (_mpb == null) _mpb = new MaterialPropertyBlock();
        EnsureMaterialInstance();
    }

    void OnEnable()
    {
        if (occupancy == null) occupancy = GridOccupancy2D.Instance;
        if (grid == null && occupancy != null) grid = occupancy.grid;
        if (gridPlacer == null) gridPlacer = FindFirstObjectByType<GridPlacer>();

        _nextUpdateT = 0f;
        EnsureMaterialInstance();
    }

    void OnDisable()
    {
        ReleaseMaterialInstance();
    }

    void EnsureMaterialInstance()
    {
        if (dotMaterial == null)
        {
            _matInst = null;
            return;
        }

        if (_matInst != null) return;

        if (!overrideRenderQueue)
        {
            _matInst = dotMaterial;
            _matInstOwned = false;
            return;
        }

        _matInst = Instantiate(dotMaterial);
        _matInst.name = $"{dotMaterial.name}_OccHintInst";
        _matInst.renderQueue = renderQueue;
        _matInstOwned = true;
    }

    void ReleaseMaterialInstance()
    {
        if (_matInstOwned && _matInst != null)
            Destroy(_matInst);

        _matInst = null;
        _matInstOwned = false;
    }

    void EnsureQuad()
    {
        if (_quad != null) return;

        _quad = new Mesh { name = "OccupancyHintQuad" };
        _quad.vertices = new[]
        {
            new Vector3(-0.5f, -0.5f, 0),
            new Vector3( 0.5f, -0.5f, 0),
            new Vector3( 0.5f,  0.5f, 0),
            new Vector3(-0.5f,  0.5f, 0),
        };
        _quad.uv = new[]
        {
            new Vector2(0,0),
            new Vector2(1,0),
            new Vector2(1,1),
            new Vector2(0,1),
        };
        _quad.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        _quad.RecalculateNormals();
        _quad.RecalculateBounds();
    }

    bool IsPlayModeNow()
    {
        if (!hideInPlayMode) return false;
        if (!Application.isPlaying) return false;

        if (gameMode == null)
            gameMode = GameModeManager.Instance ?? FindFirstObjectByType<GameModeManager>();

        if (gameMode == null) return false;

        return gameMode.currentMode == GameMode.Play;
    }

    bool ShouldShowPOBlockedCellsNow()
    {
        if (!includePOBlockedCells)
            return false;

        if (gridPlacer == null)
            gridPlacer = FindFirstObjectByType<GridPlacer>();

        if (gridPlacer == null)
            return false;

        bool showForPlacement =
            showPOBlockedDuringPlacementPreview &&
            gridPlacer.HasPlacementPreview &&
            gridPlacer.PreviewPlacementObject != null;

        bool showForDrag =
            showPOBlockedDuringSelectedPODrag &&
            gridPlacer.SelectedPO != null &&
            gridPlacer.IsDraggingSelectedPO;

        bool showForDragCandidate =
            showPOBlockedDuringDragCandidate &&
            gridPlacer.SelectedPO != null &&
            gridPlacer.IsDragCandidateSelectedPO;

        return showForPlacement || showForDrag || showForDragCandidate;
    }

    void LateUpdate()
    {
        if (!visible)
            return;

        if (IsPlayModeNow())
            return;

        if (dotMaterial == null || occupancy == null || grid == null)
            return;

        if (_matInst == null)
            EnsureMaterialInstance();

        if (_matInst == null)
            return;

        if (renderCamera == null)
            renderCamera = Camera.main;

        if (renderCamera == null)
            return;

        if (updateInterval <= 0f || Time.unscaledTime >= _nextUpdateT)
        {
            RebuildPoints();
            _nextUpdateT = Time.unscaledTime + Mathf.Max(0f, updateInterval);
        }

        DrawMatrices(_occupiedMatrices, occupiedColor);
        DrawMatrices(_poBlockedMatrices, poBlockedColor);
    }

    void RebuildPoints()
    {
        _occupiedMatrices.Clear();
        _poBlockedMatrices.Clear();

        occupancy.EnsureBaked();

        if (!scanOnlyCameraView)
            return;

        Vector3 bl = renderCamera.ViewportToWorldPoint(new Vector3(0f, 0f, 0f));
        Vector3 tr = renderCamera.ViewportToWorldPoint(new Vector3(1f, 1f, 0f));

        Vector2 minW = new Vector2(Mathf.Min(bl.x, tr.x), Mathf.Min(bl.y, tr.y));
        Vector2 maxW = new Vector2(Mathf.Max(bl.x, tr.x), Mathf.Max(bl.y, tr.y));

        Vector2Int cMin = grid.WorldToCell(minW);
        Vector2Int cMax = grid.WorldToCell(maxW);

        cMin -= new Vector2Int(marginCells, marginCells);
        cMax += new Vector2Int(marginCells, marginCells);

        Vector2 step = grid.CellToWorld(Vector2Int.right) - grid.CellToWorld(Vector2Int.zero);
        float size = (dotSize > 0f) ? dotSize : Mathf.Max(0.01f, Mathf.Abs(step.x));

        bool showPOBlockedNow = ShouldShowPOBlockedCellsNow();

        int totalCount = 0;

        for (int y = cMin.y; y <= cMax.y; y++)
        {
            for (int x = cMin.x; x <= cMax.x; x++)
            {
                if (totalCount >= maxDots)
                    return;

                var cell = new Vector2Int(x, y);
                Vector2 center = grid.CellToWorld(cell);
                var m = Matrix4x4.TRS(
                    new Vector3(center.x, center.y, 0f),
                    Quaternion.identity,
                    new Vector3(size, size, 1f)
                );

                bool occupied = occupancy.IsOccupiedCell(cell, ignoreOwnerId: hideOwnerId);
                if (occupied)
                {
                    bool isWall = occupancy.IsWallCell(cell);
                    bool isPlaced = occupancy.IsPlacedCell(cell);

                    bool keepOccupied = true;

                    if (isWall && !includeWalls)
                        keepOccupied = false;

                    if (isPlaced && !includePlaced)
                        keepOccupied = false;

                    if (keepOccupied)
                    {
                        _occupiedMatrices.Add(m);
                        totalCount++;
                    }
                }

                if (showPOBlockedNow && occupancy.IsPOBlockedCell(cell))
                {
                    // occupied와 겹쳐도 괜찮지만, 완전히 중복이면 생략
                    if (!occupied)
                    {
                        _poBlockedMatrices.Add(m);
                        totalCount++;
                    }
                }
            }
        }
    }

    void DrawMatrices(List<Matrix4x4> matrices, Color drawColor)
    {
        if (matrices == null || matrices.Count == 0)
            return;

        _mpb.SetColor("_Color", drawColor);

        const int BATCH = 1023;
        int total = matrices.Count;

        for (int i = 0; i < total; i += BATCH)
        {
            int cnt = Mathf.Min(BATCH, total - i);

            var arr = new Matrix4x4[cnt];
            matrices.CopyTo(i, arr, 0, cnt);

            Graphics.DrawMeshInstanced(
                _quad,
                0,
                _matInst,
                arr,
                cnt,
                _mpb,
                UnityEngine.Rendering.ShadowCastingMode.Off,
                receiveShadows: false,
                layer: gameObject.layer,
                camera: renderCamera
            );
        }
    }
}