using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(PolygonCollider2D))]
public class BezierPolygonCollider : MonoBehaviour
{
    public Transform p0;
    public Transform p1;
    public Transform p2;

    [Range(10, 100)]
    public int resolution = 40;

    [Range(0.01f, 0.5f)]
    public float width = 0.1f; // 절반 두께

    PolygonCollider2D polygon;
    LineRenderer line;

    void Awake()
    {
        polygon = GetComponent<PolygonCollider2D>();
        line = GetComponent<LineRenderer>();

        if (Application.isPlaying)
            UpdatePolygonCollider();
    }

    void OnValidate()
    {
        polygon = GetComponent<PolygonCollider2D>();
        line = GetComponent<LineRenderer>();
        UpdateLinePreview(); // 편집 중엔 라인만
    }

    void Update()
    {
        if (!Application.isPlaying)
        {
            UpdateLinePreview(); // 점 이동 즉시 반영
        }
    }

    void Start()
    {
        UpdatePolygonCollider(); // 게임 시작 시 1회
    }

    // -------------------------
    // 편집용 미리보기 (선)
    // -------------------------
    void UpdateLinePreview()
    {
        if (line == null) return;
        if (p0 == null || p1 == null || p2 == null) return;

        List<Vector3> points = new List<Vector3>();

        for (int i = 0; i <= resolution; i++)
        {
            float t = i / (float)resolution;
            Vector2 p = Bezier(p0.position, p1.position, p2.position, t);
            points.Add(p);
        }

        line.positionCount = points.Count;
        line.SetPositions(points.ToArray());
    }

    // -------------------------
    // 플레이용 폴리곤 콜라이더
    // -------------------------
    void UpdatePolygonCollider()
    {
        if (polygon == null) return;
        if (p0 == null || p1 == null || p2 == null) return;

        List<Vector2> top = new List<Vector2>();
        List<Vector2> bottom = new List<Vector2>();

        for (int i = 0; i <= resolution; i++)
        {
            float t = i / (float)resolution;

            Vector2 point = Bezier(p0.position, p1.position, p2.position, t);
            Vector2 tangent = BezierDerivative(p0.position, p1.position, p2.position, t).normalized;
            Vector2 normal = new Vector2(-tangent.y, tangent.x);

            top.Add(transform.InverseTransformPoint(point + normal * width));
            bottom.Add(transform.InverseTransformPoint(point - normal * width));
        }

        bottom.Reverse();

        List<Vector2> polygonPoints = new List<Vector2>();
        polygonPoints.AddRange(top);
        polygonPoints.AddRange(bottom);

        polygon.SetPath(0, polygonPoints.ToArray());
    }

    // -------------------------
    // Bezier math
    // -------------------------
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
