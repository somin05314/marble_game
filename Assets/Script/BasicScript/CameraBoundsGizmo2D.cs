using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class CameraBoundsGizmo2D : MonoBehaviour
{
    [Header("Source")]
    [Tooltip("비워두면 씬에서 StageConfigHolder를 찾아서 사용")]
    public StageConfigHolder holder;

    [Header("Camera View (optional)")]
    [Tooltip("비워두면 Camera.main 사용")]
    public Camera targetCamera;

    [Header("Gizmo")]
    public bool drawAlways = true;
    public bool drawWhenSelectedOnly = false;
    public float z = 0f;

    [Header("Draw Options")]
    public bool drawBounds = true;

    [Tooltip("ZoomIn(작업 화면) 포즈 십자를 그립니다.")]
    public bool drawZoomInCross = true;

    [Tooltip("현재 카메라가 실제로 보는 사각형(orthographic view)을 그립니다.")]
    public bool drawCurrentCameraViewRect = true;

    [Tooltip("Pose 십자/라벨을 그립니다. (ZoomIn / IntroStart / Landing=ZoomOut)")]
    public bool drawPoseMarkers = true;

    [Tooltip("Pose들의 zoom 기준 '보이는 사각형'도 같이 그립니다.")]
    public bool drawPoseViewRects = true;

    [Tooltip("Bounds 안으로 clamp된 pose view rect를 추가로 그립니다.")]
    public bool drawClampedPoseViewRects = false;

    [Header("Colors")]
    public Color boundsColor = new Color(1f, 0.9f, 0f, 0.9f);

    public Color zoomInColor = new Color(1f, 0.3f, 0.3f, 0.95f);
    public Color introStartColor = new Color(1f, 0.5f, 0.1f, 0.95f);
    public Color landingColor = new Color(0.2f, 1f, 0.2f, 0.95f);

    public Color currentCamViewColor = new Color(0f, 1f, 1f, 0.9f);
    public Color clampedViewColor = new Color(1f, 1f, 1f, 0.25f);

    [Header("Marker")]
    public float crossSize = 0.35f;

    void OnDrawGizmos()
    {
        if (!drawAlways || drawWhenSelectedOnly) return;
        Draw();
    }

    void OnDrawGizmosSelected()
    {
        if (!drawWhenSelectedOnly) return;
        Draw();
    }

    void Draw()
    {
        if (holder == null) holder = FindFirstObjectByType<StageConfigHolder>();
        if (holder == null || holder.config == null) return;

        var lim = holder.config.cameraLimits;

        Vector2 bMin = lim.BoundsMin;
        Vector2 bMax = lim.BoundsMax;

        if (bMin.x > bMax.x) (bMin.x, bMax.x) = (bMax.x, bMin.x);
        if (bMin.y > bMax.y) (bMin.y, bMax.y) = (bMax.y, bMin.y);

        // pose data
        Vector2 zoomInPos = lim.zoomInPos;
        float zoomInZoom = Mathf.Max(0.01f, lim.zoomInZoom);

        Vector2 introStartPos = lim.introStartPose.pos;
        float introStartZoom = Mathf.Max(0.01f, lim.introStartPose.zoom);

        Vector2 landingPos = lim.zoomOutPose.pos;
        float landingZoom = Mathf.Max(0.01f, lim.zoomOutPose.zoom);

        // 카메라는 없어도 되는 것부터 그림
        if (drawBounds)
        {
            Gizmos.color = boundsColor;
            DrawRect(bMin, bMax, z);
        }

        if (drawZoomInCross)
        {
            Gizmos.color = zoomInColor;
            DrawCross(zoomInPos, crossSize, z);
            DrawLabel(zoomInPos, $"ZoomInPos ({zoomInPos.x:F2}, {zoomInPos.y:F2})", zoomInColor);
        }

        if (drawPoseMarkers)
        {
            DrawPoseMarker("ZoomIn (Work View)", zoomInPos, zoomInZoom, zoomInColor);
            DrawPoseMarker("IntroStart (Hold 1s)", introStartPos, introStartZoom, introStartColor);
            DrawPoseMarker("Landing / ZoomOut (Same)", landingPos, landingZoom, landingColor);
        }

        // 카메라 resolve
        Camera cam = ResolveCamera();
        if (cam == null || !cam.orthographic)
            return;

        if (drawCurrentCameraViewRect)
        {
            Gizmos.color = currentCamViewColor;
            DrawOrthoViewRect(cam.transform.position, cam.orthographicSize, cam.aspect, z);
        }

        if (drawPoseViewRects)
        {
            DrawPoseRect("ZoomInRect", zoomInPos, zoomInZoom, cam.aspect, zoomInColor, bMin, bMax);
            DrawPoseRect("IntroStartRect", introStartPos, introStartZoom, cam.aspect, introStartColor, bMin, bMax);
            DrawPoseRect("LandingRect", landingPos, landingZoom, cam.aspect, landingColor, bMin, bMax);
        }
    }

    Camera ResolveCamera()
    {
        if (targetCamera != null)
            return targetCamera;

        if (Camera.main != null)
            return Camera.main;

#if UNITY_EDITOR
        if (SceneView.lastActiveSceneView != null)
            return SceneView.lastActiveSceneView.camera;
#endif

        return null;
    }

    void DrawPoseMarker(string name, Vector2 pos, float zoom, Color color)
    {
        Gizmos.color = color;
        DrawCross(pos, crossSize * 0.9f, z);
        DrawLabel(pos, $"{name}\npos=({pos.x:F2},{pos.y:F2})  zoom={zoom:F2}", color);
    }

    void DrawPoseRect(string name, Vector2 center, float zoom, float aspect, Color color, Vector2 bMin, Vector2 bMax)
    {
        Gizmos.color = color;
        DrawOrthoViewRect(new Vector3(center.x, center.y, 0f), zoom, aspect, z);

        if (drawClampedPoseViewRects)
        {
            var clamped = CalcClampedViewRect(center, zoom, aspect, bMin, bMax);
            Gizmos.color = new Color(clampedViewColor.r, clampedViewColor.g, clampedViewColor.b, clampedViewColor.a);
            DrawRect(clamped.min, clamped.max, z);
        }
    }

    static void DrawCross(Vector2 center, float size, float z)
    {
        Vector3 c = new Vector3(center.x, center.y, z);
        Gizmos.DrawLine(c + Vector3.left * size, c + Vector3.right * size);
        Gizmos.DrawLine(c + Vector3.down * size, c + Vector3.up * size);
    }

    static void DrawOrthoViewRect(Vector3 center, float orthoSize, float aspect, float z)
    {
        float halfH = Mathf.Max(0.01f, orthoSize);
        float halfW = halfH * aspect;

        Vector2 min = new Vector2(center.x - halfW, center.y - halfH);
        Vector2 max = new Vector2(center.x + halfW, center.y + halfH);

        DrawRect(min, max, z);
    }

    static void DrawRect(Vector2 min, Vector2 max, float z)
    {
        Vector3 a = new Vector3(min.x, min.y, z);
        Vector3 b = new Vector3(max.x, min.y, z);
        Vector3 c = new Vector3(max.x, max.y, z);
        Vector3 d = new Vector3(min.x, max.y, z);

        Gizmos.DrawLine(a, b);
        Gizmos.DrawLine(b, c);
        Gizmos.DrawLine(c, d);
        Gizmos.DrawLine(d, a);
    }

    struct Rect2
    {
        public Vector2 min;
        public Vector2 max;
    }

    static Rect2 CalcClampedViewRect(Vector2 center, float zoom, float aspect, Vector2 bMin, Vector2 bMax)
    {
        float halfH = Mathf.Max(0.01f, zoom);
        float halfW = halfH * aspect;

        float minX = bMin.x + halfW;
        float maxX = bMax.x - halfW;
        float minY = bMin.y + halfH;
        float maxY = bMax.y - halfH;

        float cx = (minX > maxX) ? (bMin.x + bMax.x) * 0.5f : Mathf.Clamp(center.x, minX, maxX);
        float cy = (minY > maxY) ? (bMin.y + bMax.y) * 0.5f : Mathf.Clamp(center.y, minY, maxY);

        return new Rect2
        {
            min = new Vector2(cx - halfW, cy - halfH),
            max = new Vector2(cx + halfW, cy + halfH)
        };
    }

    static void DrawLabel(Vector2 pos, string text, Color color)
    {
#if UNITY_EDITOR
        Handles.color = color;
        Handles.Label(new Vector3(pos.x, pos.y, 0f), text);
#endif
    }
}