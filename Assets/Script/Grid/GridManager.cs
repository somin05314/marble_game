using System.Collections.Generic;
using UnityEngine;

public class GridManager : MonoBehaviour
{
    [Header("Grid Settings")]
    public float cellSize = 1f;
    public Vector2 origin = Vector2.zero;
    private Vector2Int debugCell;
    private HashSet<Vector2Int> occupiedCells = new HashSet<Vector2Int>();

    public Vector2Int WorldToCell(Vector2 worldPos)
    {
        int x = Mathf.FloorToInt((worldPos.x - origin.x) / cellSize);
        int y = Mathf.FloorToInt((worldPos.y - origin.y) / cellSize);
        return new Vector2Int(x, y);
    }

    public Vector2 CellToWorld(Vector2Int cell)
    {
        return origin + ((Vector2)cell + Vector2.one * 0.5f) * cellSize;
    }

    public bool IsCellOccupied(Vector2Int cell)
    {
        return occupiedCells.Contains(cell);
    }

    public void OccupyCell(Vector2Int cell)
    {
        occupiedCells.Add(cell);
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.gray;

        int range = 100; // 임시로 ±10칸만 그리기

        for (int x = -range; x <= range; x++)
        {
            Vector2 start = origin + new Vector2(x * cellSize, -range * cellSize);
            Vector2 end = origin + new Vector2(x * cellSize, range * cellSize);
            Gizmos.DrawLine(start, end);
        }

        for (int y = -range; y <= range; y++)
        {
            Vector2 start = origin + new Vector2(-range * cellSize, y * cellSize);
            Vector2 end = origin + new Vector2(range * cellSize, y * cellSize);
            Gizmos.DrawLine(start, end);
        }

        // 현재 마우스가 있는 셀 표시
        Gizmos.color = Color.red;

        Vector2 center = CellToWorld(debugCell);
        Gizmos.DrawWireCube(center, Vector3.one * cellSize);
    }

    private void Update()
    {
        if (!MouseUtil.TryGetMouseWorld(Camera.main, out Vector3 mouseWorld))
            return;

        debugCell = WorldToCell(mouseWorld);
    }


}
