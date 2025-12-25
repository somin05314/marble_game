using System.Linq;
using UnityEngine;

public class GridPlacer : MonoBehaviour
{
    [Header("Refs")]
    public GridManager grid;
    public PlacementData placementData;

    [Header("Runtime")]
    GameObject ghost;
    PlacementObject previewPO;
    PlacementObject selected;
    PlacementObject placeSnapTarget;

    // -----------------------------
    // States
    // -----------------------------
    bool isDragging;
    bool dragCandidate;

    bool isRotating;
    Quaternion rotationBefore;
    Vector3 positionBefore;
    Vector3 rotationPivot;

    Vector3 dragStartPos;
    Vector3 mouseDownWorld;

    const float DRAG_START_DISTANCE = 0.08f;

    // Place preview snap cache
    bool placeHasSnap;
    SnapPreviewPair placeSnap;

    BuildTool lastTool;

    // Masks (cache)
    int placedMask;
    int wallMask;

    void Awake()
    {
        placedMask = LayerMask.GetMask("PlacedObject");
        wallMask = LayerMask.GetMask("Wall");
    }

    void Update()
    {
        if (GameModeManager.Instance.currentMode != GameMode.Build)
        {
            ClearSelection(forceFinalize: true);
            ClearPlacePreviewObjects();
            return;
        }

        var tool = BuildToolManager.Instance.currentTool;

        if (tool != lastTool)
        {
            OnToolChanged(tool);
            lastTool = tool;
        }

        // -----------------------------
        // Tool: Place
        // -----------------------------
        if (tool == BuildTool.Place)
        {
            UpdatePlacePreview();

            if (Input.GetMouseButtonDown(0))
                ApplyPlace();
        }

        // -----------------------------
        // Tool: Select
        // -----------------------------
        if (tool == BuildTool.Select)
        {
            HandleSelection();
            UpdateDrag();
            EndDrag();
            HandleRotation();

            if (Input.GetKeyDown(KeyCode.Delete))
                TryDeleteSelected();
        }
    }

    void OnToolChanged(BuildTool tool)
    {
        if (tool != BuildTool.Select)
            ClearSelection(forceFinalize: true);

        if (tool == BuildTool.Place)
            EnsurePlacePreviewObjects();
        else
            ClearPlacePreviewObjects();
    }

    // =========================================================
    // Helpers
    // =========================================================
    static float GetAllowedPenetration(in SnapPreviewPair snap)
    {
        float a = snap.myPoint != null ? snap.myPoint.allowedPenetration : 0f;
        float b = snap.otherPoint != null ? snap.otherPoint.allowedPenetration : 0f;
        return Mathf.Min(a, b);
    }

    // =========================================================
    // PLACE TOOL
    // =========================================================
    void UpdatePlacePreview()
    {
        EnsurePlacePreviewObjects();

        if (!MouseUtil.TryGetMouseWorld(Camera.main, out var mouse))
            return;

        Vector3 freePos = grid.CellToWorld(grid.WorldToCell(mouse));

        // 1) 프리뷰를 freePos에 두고 스냅 계산
        previewPO.transform.position = freePos;

        placeHasSnap = SnapManager.TryGetSnapPreview(previewPO, out placeSnap);
        placeSnapTarget = placeHasSnap ? placeSnap.otherPoint.root.owner : null;

        // 2) 실제 표시/판정 위치(스냅이면 스냅 위치)
        Vector3 checkPos = placeHasSnap ? placeSnap.previewObjectPos : freePos;

        ghost.transform.position = checkPos;

        // ✅ 충돌 판정도 checkPos 기준으로 해야 함
        previewPO.transform.position = checkPos;

        float allowedPen = placeHasSnap ? GetAllowedPenetration(placeSnap) : 0f;

        bool canPlace = previewPO.CanPlaceByRule(
            wallMask,
            placeSnapTarget,
            allowedPen
        );

        SetGhostColor(canPlace);
    }

    void ApplyPlace()
    {
        if (previewPO == null)
            return;

        float allowedPen = placeHasSnap ? GetAllowedPenetration(placeSnap) : 0f;

        bool canPlace = previewPO.CanPlaceByRule(
            wallMask,
            placeSnapTarget,
            allowedPen
        );

        if (!canPlace)
            return;

        Vector3 finalPos = placeHasSnap
            ? placeSnap.previewObjectPos
            : previewPO.transform.position;

        GameObject obj = Instantiate(
            placementData.prefab,
            finalPos,
            Quaternion.identity
        );

        var po = obj.GetComponent<PlacementObject>();
        po.placementData = placementData;

        if (placeHasSnap && placeSnapTarget != null)
        {
            var myPoint = po.GetComponentsInChildren<SnapPoint>()
                .First(p => p.snapId == placeSnap.myPoint.snapId);

            var otherPoint = placeSnapTarget.GetComponentsInChildren<SnapPoint>()
                .First(p => p.snapId == placeSnap.otherPoint.snapId);

            SnapManager.CommitSnapDirect(
                po,
                myPoint.root,
                myPoint,
                placeSnapTarget,
                otherPoint.root,
                otherPoint
            );
        }

        po.SetPlaced();
    }

    // =========================================================
    // SELECT
    // =========================================================
    void HandleSelection()
    {
        if (!Input.GetMouseButtonDown(0))
            return;

        if (!MouseUtil.TryGetMouseWorld(Camera.main, out var mouse))
            return;

        var po = PickPlacementObject(mouse);

        if (po == null)
        {
            ClearSelection();
            return;
        }

        if (selected != null && selected != po)
            selected.SetPlacedVisual();

        selected = po;
        selected.SetSelectedVisual();

        dragCandidate = !selected.HasMultipleConnections;
        mouseDownWorld = mouse;
        dragStartPos = selected.transform.position;
        isDragging = false;

        if (selected.HasMultipleConnections)
        {
            isDragging = false;
            return;
        }
    }

    void ClearSelection(bool forceFinalize = false)
    {
        if (selected == null)
            return;

        if (forceFinalize)
        {
            selected.SetPlaced();
            RestorePhysics();
        }

        selected.SetPlacedVisual();

        selected = null;
        isDragging = false;
        dragCandidate = false;
        isRotating = false;
    }

    // =========================================================
    // DRAG
    // =========================================================
    void UpdateDrag()
    {
        if (selected == null)
            return;

        if (!Input.GetMouseButton(0))
            return;

        if (!MouseUtil.TryGetMouseWorld(Camera.main, out var mouse))
            return;

        if (!isDragging)
        {
            if (!dragCandidate)
                return;

            float dist = Vector2.Distance(mouse, mouseDownWorld);
            if (dist < DRAG_START_DISTANCE)
                return;

            isDragging = true;

            var rb = selected.GetComponent<Rigidbody2D>();
            if (rb != null)
                rb.simulated = false;

            selected.SetGhost();
            selected.BreakAllSnaps();
        }

        Vector3 freePos = grid.CellToWorld(grid.WorldToCell(mouse));

        selected.transform.position = freePos;

        bool hasSnap = SnapManager.TryGetSnapPreview(selected, out var snap);
        PlacementObject snapTarget = hasSnap ? snap.otherPoint.root.owner : null;

        Vector3 checkPos = hasSnap ? snap.previewObjectPos : freePos;
        selected.transform.position = checkPos;

        float allowedPen = hasSnap ? GetAllowedPenetration(snap) : 0f;

        bool canPlace = selected.CanPlaceByRule(
            wallMask,
            snapTarget,
            allowedPen
        );

        selected.SetGhostVisual(canPlace);
    }

    void EndDrag()
    {
        if (Input.GetMouseButtonUp(0))
            dragCandidate = false;

        if (!isDragging || !Input.GetMouseButtonUp(0))
            return;

        isDragging = false;

        bool hasSnap = SnapManager.TryGetSnapPreview(selected, out var snap);
        PlacementObject snapTarget = hasSnap ? snap.otherPoint.root.owner : null;

        float allowedPen = hasSnap ? GetAllowedPenetration(snap) : 0f;

        bool canPlace = selected.CanPlaceByRule(
            wallMask,
            snapTarget,
            allowedPen
        );

        if (!canPlace)
        {
            selected.transform.position = dragStartPos;
            selected.SetPlaced();
            selected.SetPlacedVisual();
            RestorePhysics();
            return;
        }

        if (hasSnap)
        {
            selected.transform.position = snap.previewObjectPos;

            var myPoint = selected.GetComponentsInChildren<SnapPoint>()
                .First(p => p.snapId == snap.myPoint.snapId);

            var otherPoint = snapTarget.GetComponentsInChildren<SnapPoint>()
                .First(p => p.snapId == snap.otherPoint.snapId);

            SnapManager.CommitSnapDirect(
                selected,
                myPoint.root,
                myPoint,
                snapTarget,
                otherPoint.root,
                otherPoint
            );
        }

        selected.SetPlaced();
        selected.SetSelectedVisual();
        RestorePhysics();
    }

    void RestorePhysics()
    {
        if (selected == null)
            return;

        var rb = selected.GetComponent<Rigidbody2D>();
        if (rb != null)
            rb.simulated = true;
    }

    // =========================================================
    // ROTATION (Select tool only)
    // =========================================================
    void HandleRotation()
    {
        if (selected == null || isDragging)
            return;

        int connectionCount = selected.connections.Count;

        if (connectionCount >= 2)
            return;

        PlacementObject snapTarget = null;
        bool hasSingleSnap = connectionCount == 1;

        float allowedPen = 0f;

        if (hasSingleSnap)
        {
            var c = selected.connections[0];

            if (c.otherRoot != null)
                snapTarget = c.otherRoot.owner;

            float a = (c.myPoint != null) ? c.myPoint.allowedPenetration : 0f;
            float b = (c.otherPoint != null) ? c.otherPoint.allowedPenetration : 0f;
            allowedPen = Mathf.Min(a, b);
        }

        if (!isRotating && (Input.GetKeyDown(KeyCode.Q) || Input.GetKeyDown(KeyCode.E)))
        {
            isRotating = true;

            rotationBefore = selected.transform.rotation;
            positionBefore = selected.transform.position;

            rotationPivot = hasSingleSnap && selected.connections[0].myPoint != null
                ? selected.connections[0].myPoint.transform.position
                : selected.transform.position;

            selected.SetGhost();

            bool canPlaceStart = selected.CanPlaceByRule(wallMask, snapTarget, allowedPen);
            selected.SetGhostVisual(canPlaceStart);
        }

        if (!isRotating)
            return;

        float angle = 0f;
        if (Input.GetKey(KeyCode.Q)) angle += 90f * Time.deltaTime;
        if (Input.GetKey(KeyCode.E)) angle -= 90f * Time.deltaTime;

        if (angle != 0f)
        {
            selected.transform.RotateAround(rotationPivot, Vector3.forward, angle);

            bool canPlacePreview = selected.CanPlaceByRule(
                wallMask,
                snapTarget,
                allowedPen
            );

            selected.SetGhostVisual(canPlacePreview);
        }

        if (Input.GetKeyUp(KeyCode.Q) || Input.GetKeyUp(KeyCode.E))
        {
            isRotating = false;

            bool canPlaceFinal = selected.CanPlaceByRule(
                wallMask,
                snapTarget,
                allowedPen
            );

            if (!canPlaceFinal)
            {
                selected.transform.rotation = rotationBefore;
                selected.transform.position = positionBefore;
            }

            selected.SetPlaced();
            selected.SetSelectedVisual();
            RestorePhysics();
        }
    }

    // =========================================================
    // PREVIEW OBJECTS
    // =========================================================
    void EnsurePlacePreviewObjects()
    {
        if (ghost == null) CreateGhost();
        if (previewPO == null) CreatePreviewObject();
    }

    void ClearPlacePreviewObjects()
    {
        if (ghost) Destroy(ghost);
        if (previewPO) Destroy(previewPO.gameObject);

        ghost = null;
        previewPO = null;
        placeSnapTarget = null;
        placeHasSnap = false;
        placeSnap = default;
    }

    void CreateGhost()
    {
        ghost = Instantiate(placementData.prefab);
        ghost.name = "Ghost";
        SetLayerRecursively(ghost, LayerMask.NameToLayer("Ghost"));

        Destroy(ghost.GetComponent<PlacementObject>());

        foreach (var col in ghost.GetComponentsInChildren<Collider2D>())
            col.enabled = false;

        SetGhostAlpha(0.4f);
    }

    void CreatePreviewObject()
    {
        var go = Instantiate(placementData.prefab);
        SetLayerRecursively(go, LayerMask.NameToLayer("Ghost"));

        previewPO = go.GetComponent<PlacementObject>();
        previewPO.placementData = placementData;

        foreach (var sr in go.GetComponentsInChildren<SpriteRenderer>())
            sr.enabled = false;

        foreach (var col in go.GetComponentsInChildren<Collider2D>())
        {
            col.enabled = true;
            col.isTrigger = true;
        }
    }

    void SetLayerRecursively(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform)
            SetLayerRecursively(child.gameObject, layer);
    }

    void SetGhostAlpha(float alpha)
    {
        foreach (var sr in ghost.GetComponentsInChildren<SpriteRenderer>())
        {
            var c = sr.color;
            c.a = alpha;
            sr.color = c;
        }
    }

    void SetGhostColor(bool canPlace)
    {
        Color c = canPlace ? Color.green : Color.red;
        foreach (var sr in ghost.GetComponentsInChildren<SpriteRenderer>())
            sr.color = new Color(c.r, c.g, c.b, 0.4f);
    }

    // =========================================================
    // DELETE
    // =========================================================
    void TryDeleteSelected()
    {
        if (selected == null)
            return;

        if (isDragging)
            return;

        selected.BreakAllSnaps();
        Destroy(selected.gameObject);

        selected = null;
        isRotating = false;
        dragCandidate = false;
        isDragging = false;
    }

    // =========================================================
    // PICK (접점 오선택 방지)
    // =========================================================
    PlacementObject PickPlacementObject(Vector2 mouseWorld)
    {
        const float pickRadius = 0.08f;

        var cols = Physics2D.OverlapCircleAll(mouseWorld, pickRadius, placedMask);
        if (cols == null || cols.Length == 0)
            return null;

        PlacementObject best = null;
        float bestScore = float.PositiveInfinity;

        for (int i = 0; i < cols.Length; i++)
        {
            var col = cols[i];
            if (col == null) continue;

            var po = col.GetComponentInParent<PlacementObject>();
            if (po == null) continue;

            Vector2 closest = col.ClosestPoint(mouseWorld);
            float score = (closest - mouseWorld).sqrMagnitude;

            if (selected != null && po == selected)
                score *= 0.25f;

            if (score < bestScore)
            {
                bestScore = score;
                best = po;
            }
        }

        return best;
    }
}
