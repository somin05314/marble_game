using UnityEngine;

public class GridRenderer : MonoBehaviour
{
    public GridManager grid;
    public int range = 50;

    [Header("Line Style")]
    public Color normalColor = new Color(1f, 1f, 1f, 0.15f);
    public Color majorColor = new Color(1f, 1f, 1f, 0.4f);

    public float normalWidth = 0.02f;
    public float majorWidth = 0.04f;

    void Start()
    {
        DrawGrid();
    }

    void DrawGrid()
    {
        // 세로선
        for (int x = -range; x <= range; x++)
        {
            bool isMajor = (x % 10 == 0);

            CreateLine(
                grid.origin + new Vector2(x * grid.cellSize, -range * grid.cellSize),
                grid.origin + new Vector2(x * grid.cellSize, range * grid.cellSize),
                isMajor
            );
        }

        // 가로선
        for (int y = -range; y <= range; y++)
        {
            bool isMajor = (y % 10 == 0);

            CreateLine(
                grid.origin + new Vector2(-range * grid.cellSize, y * grid.cellSize),
                grid.origin + new Vector2(range * grid.cellSize, y * grid.cellSize),
                isMajor
            );
        }
    }

    void CreateLine(Vector2 start, Vector2 end, bool isMajor)
    {
        GameObject lineObj = new GameObject(isMajor ? "GridLine_Major" : "GridLine");
        lineObj.transform.parent = transform;

        LineRenderer lr = lineObj.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.positionCount = 2;
        lr.SetPosition(0, start);
        lr.SetPosition(1, end);

        lr.material = new Material(Shader.Find("Sprites/Default"));

        lr.startColor = isMajor ? majorColor : normalColor;
        lr.endColor = isMajor ? majorColor : normalColor;

        lr.startWidth = isMajor ? majorWidth : normalWidth;
        lr.endWidth = isMajor ? majorWidth : normalWidth;
    }
}
