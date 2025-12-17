using UnityEngine;

public class GridPlacer : MonoBehaviour
{
    public GridManager grid;
    public PlacementData placementData;

    GameObject ghost;   //  고스트 오브젝트
    void Start()
    {
        CreateGhost();
    }
    void Update()
    {
        UpdateGhost();

        if (Input.GetMouseButtonDown(0))
        {
            TryPlace();
        }
    }
    void CreateGhost()
    {
        ghost = Instantiate(placementData.prefab);
        ghost.name = "GhostPreview";

        // 물리/충돌 비활성화
        foreach (var col in ghost.GetComponentsInChildren<Collider2D>())
            col.enabled = false;

        // 반투명 처리
        SetGhostColor(Color.green);
    }

    void SetGhostColor(Color color)
    {
        foreach (var sr in ghost.GetComponentsInChildren<SpriteRenderer>())
        {
            sr.color = new Color(color.r, color.g, color.b, 0.4f);
        }
    }

    void UpdateGhost()
    {
        Vector2 mouseWorld =
            Camera.main.ScreenToWorldPoint(Input.mousePosition);

        Vector2Int originCell = grid.WorldToCell(mouseWorld);
        Vector2 worldPos = grid.CellToWorld(originCell);

        ghost.transform.position = worldPos;

        bool canPlace = CanPlace(originCell);
        SetGhostColor(canPlace ? Color.green : Color.red);
    }


    void TryPlace()
    {
        Vector2 mouseWorld =
            Camera.main.ScreenToWorldPoint(Input.mousePosition);

        Vector2Int originCell = grid.WorldToCell(mouseWorld);

        if (!CanPlace(originCell))
            return;

        Vector2 spawnPos = grid.CellToWorld(originCell);

        Instantiate(placementData.prefab, spawnPos, Quaternion.identity);

        Occupy(originCell);
    }

    bool CanPlace(Vector2Int originCell)
    {
        for (int x = 0; x < placementData.size.x; x++)
        {
            for (int y = 0; y < placementData.size.y; y++)
            {
                Vector2Int cell = originCell + new Vector2Int(x, y);

                if (grid.IsCellOccupied(cell))
                    return false;
            }
        }
        return true;
    }

    void Occupy(Vector2Int originCell)
    {
        for (int x = 0; x < placementData.size.x; x++)
        {
            for (int y = 0; y < placementData.size.y; y++)
            {
                Vector2Int cell = originCell + new Vector2Int(x, y);
                grid.OccupyCell(cell);
            }
        }
    }
}

