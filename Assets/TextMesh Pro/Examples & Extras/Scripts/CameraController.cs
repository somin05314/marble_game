using UnityEngine;

namespace TMPro.Examples
{
    public class CameraController : MonoBehaviour
    {
        private Transform cameraTransform;
        public Transform CameraTarget;

        [Header("Pan")]
        public float MoveSensitivity = 1.0f;

        [Header("Zoom (OrthographicSize)")]
        public float FollowDistance = 8.0f;
        public float MaxFollowDistance = 14.0f;
        public float MinFollowDistance = 4.0f;

        [Header("Bounds (World, 2D)")]
        public Vector2 boundsMin = new Vector2(-10, -10);
        public Vector2 boundsMax = new Vector2(10, 10);

        Camera cam;

        // ✅ 버튼별로 분리 (공유하면 튐 원인)
        Vector3 lastMouseScreenMiddle;
        Vector3 lastMouseScreenRight;

        bool isMiddlePanning;
        bool isRightPanning;

        void Awake()
        {
            cameraTransform = transform;
            cam = GetComponent<Camera>();
            if (cam == null) cam = Camera.main;

            if (cam != null)
            {
                cam.orthographic = true;
                cam.orthographicSize = FollowDistance;
            }

            if (CameraTarget == null)
            {
                var go = new GameObject("Camera Target (2D)");
                CameraTarget = go.transform;
                CameraTarget.position = Vector3.zero;
            }

            if (boundsMin.x > boundsMax.x) (boundsMin.x, boundsMax.x) = (boundsMax.x, boundsMin.x);
            if (boundsMin.y > boundsMax.y) (boundsMin.y, boundsMax.y) = (boundsMax.y, boundsMin.y);

            ClampNow();
        }

        void Update()
        {
            if (cam == null) return;

            // ✅ 둘 다 눌리면 한쪽만 적용(중복 이동 방지)
            HandleRightPan2D();
            if (!isRightPanning) HandleMiddlePan2D();

            HandleZoom2D();
        }

        // =========================
        // Pan (Middle Mouse Drag)
        // =========================
        void HandleMiddlePan2D()
        {
            if (Input.GetMouseButtonDown(2))
            {
                isMiddlePanning = true;
                lastMouseScreenMiddle = Input.mousePosition;
            }

            if (Input.GetMouseButtonUp(2))
                isMiddlePanning = false;

            if (!isMiddlePanning) return;

            PanByScreenDelta(ref lastMouseScreenMiddle);
        }

        // =========================
        // Pan (Right Mouse Drag)
        // =========================
        void HandleRightPan2D()
        {
            if (Input.GetMouseButtonDown(1))
            {
                isRightPanning = true;
                lastMouseScreenRight = Input.mousePosition;
            }

            if (Input.GetMouseButtonUp(1))
                isRightPanning = false;

            if (!isRightPanning) return;

            PanByScreenDelta(ref lastMouseScreenRight);
        }

        // ✅ 공통 패닝 로직: 경계에서 “델타 누적” 방지 포함
        void PanByScreenDelta(ref Vector3 lastScreen)
        {
            Vector3 cur = Input.mousePosition;
            Vector3 deltaPx = cur - lastScreen;

            // 데드존: 0.5px
            if (deltaPx.sqrMagnitude < 0.25f)
            {
                lastScreen = cur;
                return;
            }

            // 픽셀 -> 월드 변환
            float worldPerPixelY = (cam.orthographicSize * 2f) / Mathf.Max(1, cam.pixelHeight);
            float worldPerPixelX = worldPerPixelY * cam.aspect;

            Vector3 moveWorld = new Vector3(-deltaPx.x * worldPerPixelX, -deltaPx.y * worldPerPixelY, 0f) * MoveSensitivity;

            Vector3 p0 = cameraTransform.position;
            Vector3 p1 = p0 + moveWorld;

            // 클램프 후 실제 적용된 이동량
            Vector3 clamped = ClampPositionToBounds(p1);
            cameraTransform.position = clamped;

            Vector3 appliedWorld = clamped - p0;

            // ✅ 핵심: 경계에 막혀서 appliedWorld가 작아지면,
            // lastScreen도 그만큼만 “따라오게” 보정해서 델타가 쌓이지 않게 함
            float appliedPxX = (worldPerPixelX > 0f) ? (-appliedWorld.x / (worldPerPixelX * MoveSensitivity)) : 0f;
            float appliedPxY = (worldPerPixelY > 0f) ? (-appliedWorld.y / (worldPerPixelY * MoveSensitivity)) : 0f;

            lastScreen = lastScreen + new Vector3(appliedPxX, appliedPxY, 0f);
        }

        // =========================
        // Zoom (Mouse Wheel)
        // =========================
        void HandleZoom2D()
        {
            float wheel = Input.GetAxis("Mouse ScrollWheel");
            if (wheel > -0.0001f && wheel < 0.0001f) return;

            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                wheel *= 10f;

            FollowDistance -= wheel * 5.0f;
            FollowDistance = Mathf.Clamp(FollowDistance, MinFollowDistance, MaxFollowDistance);

            cam.orthographicSize = FollowDistance;
            cameraTransform.position = ClampPositionToBounds(cameraTransform.position);
        }

        // =========================
        // Helpers
        // =========================
        void ClampNow()
        {
            FollowDistance = Mathf.Clamp(FollowDistance, MinFollowDistance, MaxFollowDistance);
            cam.orthographicSize = FollowDistance;
            cameraTransform.position = ClampPositionToBounds(cameraTransform.position);
        }

        Vector3 ClampPositionToBounds(Vector3 pos)
        {
            float halfH = cam.orthographicSize;
            float halfW = halfH * cam.aspect;

            float minX = boundsMin.x + halfW;
            float maxX = boundsMax.x - halfW;
            float minY = boundsMin.y + halfH;
            float maxY = boundsMax.y - halfH;

            if (minX > maxX) pos.x = (boundsMin.x + boundsMax.x) * 0.5f;
            else pos.x = Mathf.Clamp(pos.x, minX, maxX);

            if (minY > maxY) pos.y = (boundsMin.y + boundsMax.y) * 0.5f;
            else pos.y = Mathf.Clamp(pos.y, minY, maxY);

            return pos;
        }
    }
}
