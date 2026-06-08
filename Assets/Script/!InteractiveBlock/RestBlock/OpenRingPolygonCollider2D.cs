using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(PolygonCollider2D))]
public class OpenRingPolygonCollider2D : MonoBehaviour
{
    [Header("Shape")]
    public float outerRadius = 2.0f;       // 바깥 반지름
    public float thickness = 0.5f;         // 링 두께
    [Range(1f, 180f)]
    public float gapAngle = 30f;           // "위쪽 틈" 크기(각도 기반이지만 수직 컷으로 변환)
    [Min(8)]
    public int segments = 64;              // 부드러움

    PolygonCollider2D _poly;

    void Reset() { _poly = GetComponent<PolygonCollider2D>(); Rebuild(); }
    void OnValidate() { if (_poly == null) _poly = GetComponent<PolygonCollider2D>(); Rebuild(); }

    public void Rebuild()
    {
        if (_poly == null) return;

        float innerRadius = Mathf.Max(0.01f, outerRadius - thickness);

        // ✅ gapAngle을 "수직 컷의 x폭"으로 변환
        // x = outerR * sin(halfGap)
        float halfGap = gapAngle * 0.5f;

        // inner 원에서도 같은 x로 잘려야 수직벽이 생김
        // 조건: |x| <= innerRadius  =>  sin(halfGap) <= innerR/outerR
        float maxHalfGapRad = Mathf.Asin(Mathf.Clamp(innerRadius / outerRadius, 0f, 1f));
        float halfGapRad = Mathf.Min(halfGap * Mathf.Deg2Rad, maxHalfGapRad);

        float xRight = outerRadius * Mathf.Sin(halfGapRad);
        float xLeft = -xRight;

        // ✅ outer/inner에서 "같은 x"로 교차하는 y(위쪽 교차점)
        float yOuter = Mathf.Sqrt(Mathf.Max(0f, outerRadius * outerRadius - xRight * xRight));
        float yInner = Mathf.Sqrt(Mathf.Max(0f, innerRadius * innerRadius - xRight * xRight));

        // 교차점(위쪽)
        Vector2 outerRight = new Vector2(xRight, yOuter);
        Vector2 outerLeft = new Vector2(xLeft, yOuter);
        Vector2 innerLeft = new Vector2(xLeft, yInner);
        Vector2 innerRight = new Vector2(xRight, yInner);

        // 교차점 각도(도)
        float angOuterRight = Mathf.Atan2(outerRight.y, outerRight.x) * Mathf.Rad2Deg;
        float angOuterLeft = Mathf.Atan2(outerLeft.y, outerLeft.x) * Mathf.Rad2Deg;
        float angInnerLeft = Mathf.Atan2(innerLeft.y, innerLeft.x) * Mathf.Rad2Deg;
        float angInnerRight = Mathf.Atan2(innerRight.y, innerRight.x) * Mathf.Rad2Deg;

        // ✅ 바깥 원호: 오른쪽 컷점 -> 왼쪽 컷점 (긴 방향으로, 아래쪽으로 돌아감)
        var outerArc = BuildArcPoints(outerRadius, angOuterRight, angOuterLeft, segments, useLongWay: true);

        // ✅ 안쪽 원호: 왼쪽 컷점 -> 오른쪽 컷점 (긴 방향으로, 아래쪽으로 돌아감)
        var innerArc = BuildArcPoints(innerRadius, angInnerLeft, angInnerRight, segments, useLongWay: true);

        // ✅ 경로 구성
        // outerArc(오른->왼) 끝에서 innerLeft로 "수직 내려가기"
        // innerArc(왼->오른) 끝에서 outerRight로 "수직 올라가기"
        // (폴리곤은 자동으로 닫힘)
        int total = outerArc.Length + 1 + (innerArc.Length - 1) + 1;
        Vector2[] path = new Vector2[total];

        int idx = 0;

        // 1) 바깥 원호
        for (int i = 0; i < outerArc.Length; i++)
            path[idx++] = outerArc[i];

        // 2) 왼쪽 수직 벽: outerLeft -> innerLeft
        path[idx++] = innerLeft;

        // 3) 안쪽 원호 (첫 점은 innerLeft와 겹치므로 제외)
        for (int i = 1; i < innerArc.Length; i++)
            path[idx++] = innerArc[i];

        // 4) 오른쪽 수직 벽: innerRight -> outerRight
        path[idx++] = outerRight;

        _poly.pathCount = 1;
        _poly.SetPath(0, path);
    }

    Vector2[] BuildArcPoints(float r, float fromDeg, float toDeg, int seg, bool useLongWay)
    {
        float delta = Mathf.DeltaAngle(fromDeg, toDeg); // 짧은 방향(-180~180)

        if (useLongWay)
        {
            // 긴 방향으로 돌기
            if (delta > 0) delta -= 360f;
            else delta += 360f;
        }

        int count = Mathf.Max(2, seg + 1);
        var pts = new Vector2[count];

        for (int i = 0; i < count; i++)
        {
            float t = (float)i / (count - 1);
            float deg = fromDeg + delta * t;
            float rad = deg * Mathf.Deg2Rad;
            pts[i] = new Vector2(Mathf.Cos(rad) * r, Mathf.Sin(rad) * r);
        }
        return pts;
    }
}