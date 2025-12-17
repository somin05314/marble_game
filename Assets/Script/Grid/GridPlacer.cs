using UnityEngine;

public class GridPlacer : MonoBehaviour
{
    public GridManager grid;
    public PlacementData placementData;

    GameObject ghost;
    PlacementObject ghostPlacement;

    LayerMask placedLayer;

    bool isDragging;
    Vector3 dragOffset;

    Vector3 originalPosition;
    bool canDropHere;

    Quaternion originalRotation;



    void Start()
    {
        placedLayer = LayerMask.GetMask("PlacedObject");
    }

    void Update()
    {
        if (GameModeManager.Instance.currentMode != GameMode.Build)
        {
            ClearGhost();
            return;
        }

        HandleRotation();

        var tool = BuildToolManager.Instance.currentTool;

        if (tool == BuildTool.Place)
        {
            HandlePlaceTool();
        }
        else if (tool == BuildTool.Select)
        {
            HandleSelectTool();
        }
    }

    // =========================
    // Place Tool
    // =========================
    void HandlePlaceTool()
    {
        if (ghost == null)
            CreateGhost();

        UpdateGhost();
        HandleRotation();

        if (Input.GetMouseButtonDown(0))
            TryPlace();
    }

    // =========================
    // Select Tool
    // =========================
    void HandleSelectTool()
    {
        ClearGhost();

        if (Input.GetMouseButtonDown(0))
        {
            TrySelect();
            StartDrag();
        }

        if (Input.GetMouseButton(0))
            DragSelected();

        if (Input.GetMouseButtonUp(0))
            EndDrag();

        if (Input.GetKeyDown(KeyCode.Delete))
            DeleteSelected();
    }

    // =========================
    // Ghost
    // =========================
    void CreateGhost()
    {
        ghost = Instantiate(placementData.prefab);
        ghost.name = "GhostPreview";
        ghost.layer = LayerMask.NameToLayer("Ghost");

        ghostPlacement = ghost.GetComponent<PlacementObject>();

        foreach (var col in ghost.GetComponentsInChildren<Collider2D>())
        {
            col.enabled = true;
            col.isTrigger = true;
        }

        SetGhostColor(Color.green);
    }

    void ClearGhost()
    {
        if (ghost != null)
        {
            Destroy(ghost);
            ghost = null;
            ghostPlacement = null;
        }
    }

    void UpdateGhost()
    {
        Vector2 mouseWorld = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        Vector2Int cell = grid.WorldToCell(mouseWorld);

        ghost.transform.position = grid.CellToWorld(cell);

        bool canPlace = ghostPlacement.CanPlace(placedLayer);
        SetGhostColor(canPlace ? Color.green : Color.red);
    }

    void TryPlace()
    {
        if (!ghostPlacement.CanPlace(placedLayer))
            return;

        GameObject obj = Instantiate(
            placementData.prefab,
            ghost.transform.position,
            ghost.transform.rotation
        );

        obj.layer = LayerMask.NameToLayer("PlacedObject");

        foreach (var col in obj.GetComponentsInChildren<Collider2D>())
            col.isTrigger = false;
    }

    // =========================
    // Selection / Drag
    // =========================
    void TrySelect()
    {
        Vector2 mouseWorld = Camera.main.ScreenToWorldPoint(Input.mousePosition);

        RaycastHit2D hit = Physics2D.Raycast(
            mouseWorld,
            Vector2.zero,
            0f,
            Physics2D.AllLayers
        );

        if (hit.collider == null)
        {
            SelectionManager.Instance.Deselect();
            return;
        }

        if (hit.collider.gameObject.layer == LayerMask.NameToLayer("Ghost"))
            return;

        var po = hit.collider.GetComponentInParent<PlacementObject>();
        if (po != null)
            SelectionManager.Instance.Select(po);
    }

    void StartDrag()
    {
        var selected = SelectionManager.Instance.selected;
        if (selected == null)
            return;

        isDragging = true;

        originalPosition = selected.transform.position;

        Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mouseWorld.z = 0;

        dragOffset = selected.transform.position - mouseWorld;
    }

    void DragSelected()
    {
        if (!isDragging)
            return;

        var selected = SelectionManager.Instance.selected;
        if (selected == null)
            return;

        Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mouseWorld.z = 0;

        Vector3 targetPos = mouseWorld + dragOffset;
        Vector2Int cell = grid.WorldToCell(targetPos);
        selected.transform.position = grid.CellToWorld(cell);

        var po = selected.GetComponent<PlacementObject>();
        canDropHere = po.CanPlace(placedLayer, po);

        // 불가능할 때만 빨간색
        if (!canDropHere)
            SetTempColor(po, Color.red);
        else
            RestoreSelectedColor(po);
    }


    void SetSelectedColor(PlacementObject obj, Color color)
    {
        foreach (var sr in obj.GetComponentsInChildren<SpriteRenderer>())
        {
            sr.color = color;
        }
    }

    void EndDrag()
    {
        if (!isDragging)
            return;

        isDragging = false;

        var selected = SelectionManager.Instance.selected;
        if (selected == null)
            return;

        if (!canDropHere)
            selected.transform.position = originalPosition;

        RestoreSelectedColor(selected);
    }



    void DeleteSelected()
    {
        var selected = SelectionManager.Instance.selected;
        if (selected == null)
            return;

        Destroy(selected.gameObject);
        SelectionManager.Instance.Deselect();
    }

    // =========================
    // Visual / Rotation
    // =========================
    void SetGhostColor(Color color)
    {
        foreach (var sr in ghost.GetComponentsInChildren<SpriteRenderer>())
            sr.color = new Color(color.r, color.g, color.b, 0.4f);
    }

    void HandleRotation()
    {
        Transform target = GetRotateTarget();
        if (target == null)
            return;

        float step = 5f;
        bool rotated = false;

        if (Input.GetKeyDown(KeyCode.Q))
        {
            SaveOriginalRotation(target);
            target.Rotate(0, 0, step);
            rotated = true;
        }

        if (Input.GetKeyDown(KeyCode.E))
        {
            SaveOriginalRotation(target);
            target.Rotate(0, 0, -step);
            rotated = true;
        }

        if (!rotated)
            return;

        // Select 툴일 때만 판정
        if (BuildToolManager.Instance.currentTool == BuildTool.Select)
        {
            var po = target.GetComponent<PlacementObject>();
            bool canRotate = po.CanPlace(placedLayer, po);

            if (!canRotate)
            {
                // 회전 불가 → 원래 각도로 복귀
                target.rotation = originalRotation;
                SetTempColor(po, Color.red);
            }
            else
            {
                // 회전 가능 → 선택 색 유지
                SelectionManager.Instance.RefreshSelectedColor();
            }
        }
    }


    void SaveOriginalRotation(Transform target)
    {
        // 이미 저장돼 있으면 덮어쓰지 않음
        if (originalRotation == target.rotation)
            return;

        originalRotation = target.rotation;
    }




    Transform GetRotateTarget()
    {
        var tool = BuildToolManager.Instance.currentTool;

        if (tool == BuildTool.Place && ghost != null)
            return ghost.transform;

        if (tool == BuildTool.Select && SelectionManager.Instance.selected != null)
            return SelectionManager.Instance.selected.transform;

        return null;
    }

    void SetTempColor(PlacementObject obj, Color color)
    {
        foreach (var sr in obj.GetComponentsInChildren<SpriteRenderer>())
            sr.color = color;
    }

    void RestoreSelectedColor(PlacementObject obj)
    {
        // 선택 중이면 노란색, 아니면 기본색
        if (SelectionManager.Instance.selected == obj)
            SelectionManager.Instance.Select(obj);
        else
            SetTempColor(obj, Color.white);
    }


}
