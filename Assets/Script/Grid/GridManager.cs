using System.Collections.Generic;
using UnityEngine;

public class GridManager : MonoBehaviour
{
    [Header("Grid Settings")]
    public float cellSize = 1f;
    public Vector2 origin = Vector2.zero;

    private Vector2Int debugCell;
    private HashSet<Vector2Int> occupiedCells = new HashSet<Vector2Int>();

    [Header("Scene Gizmos (Editor)")]
    public bool drawSceneGrid = true;
    [Range(5, 200)] public int gizmoRange = 40;

    [Tooltip("10칸마다 Major 라인")]
    public int majorStep = 10;

    [Range(0f, 1f)] public float normalAlpha = 0.06f; // ✅ 훨씬 연하게
    [Range(0f, 1f)] public float majorAlpha = 0.18f; // ✅ 10칸마다만 조금 진하게

    public bool drawMouseCell = true;
    public Color mouseCellColor = new Color(1f, 0.2f, 0.2f, 0.9f);

    public Vector2Int WorldToCell(Vector2 worldPos)
    {
        float gx = (worldPos.x - origin.x) / cellSize;
        float gy = (worldPos.y - origin.y) / cellSize;
        int x = Mathf.RoundToInt(gx);
        int y = Mathf.RoundToInt(gy);
        return new Vector2Int(x, y);
    }

    public Vector2 CellToWorld(Vector2Int cell)
    {
        return origin + ((Vector2)cell) * cellSize;
    }

    public bool IsCellOccupied(Vector2Int cell) => occupiedCells.Contains(cell);
    public void OccupyCell(Vector2Int cell) => occupiedCells.Add(cell);

    private void Update()
    {
        // Scene에서만 의미 있는 디버그니까, 카메라 없으면 조용히 리턴
        if (!MouseUtil.TryGetMouseWorld(Camera.main, out Vector3 mouseWorld))
            return;

        debugCell = WorldToCell(mouseWorld);
    }

    void OnDrawGizmos()
    {
        if (!drawSceneGrid) return;

        int range = gizmoRange;

        // 세로선
        for (int x = -range; x <= range; x++)
        {
            bool isMajor = (majorStep > 0) && (x % majorStep == 0);
            float a = isMajor ? majorAlpha : normalAlpha;

            Gizmos.color = new Color(1f, 1f, 1f, a);
            Vector2 start = origin + new Vector2(x * cellSize, -range * cellSize);
            Vector2 end = origin + new Vector2(x * cellSize, range * cellSize);
            Gizmos.DrawLine(start, end);
        }

        // 가로선
        for (int y = -range; y <= range; y++)
        {
            bool isMajor = (majorStep > 0) && (y % majorStep == 0);
            float a = isMajor ? majorAlpha : normalAlpha;

            Gizmos.color = new Color(1f, 1f, 1f, a);
            Vector2 start = origin + new Vector2(-range * cellSize, y * cellSize);
            Vector2 end = origin + new Vector2(range * cellSize, y * cellSize);
            Gizmos.DrawLine(start, end);
        }

        // 마우스 셀 표시
        if (drawMouseCell)
        {
            Gizmos.color = mouseCellColor;
            Vector2 center = CellToWorld(debugCell);
            Gizmos.DrawWireCube(center, Vector3.one * cellSize);
        }
    }
}
