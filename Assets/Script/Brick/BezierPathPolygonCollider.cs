using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(LineRenderer))]
[RequireComponent(typeof(PolygonCollider2D))]
public class BezierPathPolygonCollider : MonoBehaviour
{
    [Header("Points Root")]
    public Transform pointsRoot; // Points ºÎ¸ð

    [Range(10, 100)]
    public int resolutionPerSegment = 20;

    [Range(0.01f, 0.5f)]
    public float width = 0.1f;

    LineRenderer line;
    PolygonCollider2D polygon;

    void Awake()
    {
        Cache();
        UpdateAll();
    }

    void OnValidate()
    {
        Cache();
        UpdateAll();
    }

    void Update()
    {
        if (!Application.isPlaying)
            UpdateAll();
    }

    void Cache()
    {
        if (line == null) line = GetComponent<LineRenderer>();
        if (polygon == null) polygon = GetComponent<PolygonCollider2D>();
    }

    void UpdateAll()
    {
        if (pointsRoot == null) return;
        if (pointsRoot.childCount < 3) return;

        List<Vector3> renderPoints = new List<Vector3>();
        List<Vector2> top = new List<Vector2>();
        List<Vector2> bottom = new List<Vector2>();

        List<Transform> pts = new List<Transform>();
        for (int i = 0; i < pointsRoot.childCount; i++)
            pts.Add(pointsRoot.GetChild(i));

        for (int i = 0; i <= pts.Count - 3; i += 2)
        {
            Transform p0 = pts[i];
            Transform p1 = pts[i + 1];
            Transform p2 = pts[i + 2];

            for (int j = 0; j <= resolutionPerSegment; j++)
            {
                float t = j / (float)resolutionPerSegment;

                Vector2 p = Bezier(p0.position, p1.position, p2.position, t);
                Vector2 d = BezierDerivative(p0.position, p1.position, p2.position, t).normalized;
                Vector2 n = new Vector2(-d.y, d.x);

                if (renderPoints.Count == 0 || renderPoints[^1] != (Vector3)p)
                    renderPoints.Add(p);

                top.Add(transform.InverseTransformPoint(p + n * width));
                bottom.Add(transform.InverseTransformPoint(p - n * width));
            }
        }

        // Line
        line.positionCount = renderPoints.Count;
        line.SetPositions(renderPoints.ToArray());

        // Polygon
        bottom.Reverse();
        List<Vector2> poly = new List<Vector2>();
        poly.AddRange(top);
        poly.AddRange(bottom);

        polygon.SetPath(0, poly.ToArray());
    }

    Vector2 Bezier(Vector2 a, Vector2 b, Vector2 c, float t)
    {
        return
            (1 - t) * (1 - t) * a +
            2 * (1 - t) * t * b +
            t * t * c;
    }

    Vector2 BezierDerivative(Vector2 a, Vector2 b, Vector2 c, float t)
    {
        return
            2 * (1 - t) * (b - a) +
            2 * t * (c - b);
    }
}
