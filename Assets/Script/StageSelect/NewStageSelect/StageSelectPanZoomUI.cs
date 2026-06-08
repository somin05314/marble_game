using UnityEngine;
using UnityEngine.EventSystems;

public class StageSelectPanZoomUI_Update : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] RectTransform viewport;
    [SerializeField] RectTransform content;
    [SerializeField] Canvas canvas; // Screen Space Overlay면 없어도 되지만 넣는게 안전

    [Header("Pan (Right Mouse Drag)")]
    [SerializeField] float dragSpeed = 1.0f;
    [SerializeField] bool clampToBounds = true;

    [Header("Zoom (Mouse Wheel)")]
    [SerializeField] float zoomStep = 0.12f;
    [SerializeField] float minZoom = 0.6f;
    [SerializeField] float maxZoom = 1.8f;
    [SerializeField] bool zoomToMouse = true;

    bool _panning;
    Vector2 _lastVpLocal;

    void Start()
    {
        if (content != null) content.anchoredPosition = Vector2.zero;
        if (clampToBounds) ClampContentToViewport();
    }

    void Update()
    {
        if (viewport == null || content == null) return;

        // ✅ 우클릭 누르기 시작
        if (Input.GetMouseButtonDown(1))
        {
            // (선택) UI 위든 아니든 패닝 시작. 필요하면 여기서 조건 걸어도 됨.
            _panning = TryGetViewportLocal(Input.mousePosition, out _lastVpLocal);
        }

        // ✅ 우클릭 드래그 중
        if (_panning && Input.GetMouseButton(1))
        {
            Vector2 nowVpLocal;
            if (TryGetViewportLocal(Input.mousePosition, out nowVpLocal))
            {
                Vector2 delta = (nowVpLocal - _lastVpLocal) * dragSpeed;
                _lastVpLocal = nowVpLocal;

                content.anchoredPosition += delta;

                if (clampToBounds) ClampContentToViewport();
                Debug.Log("이동중");
            }
            
        }

        // ✅ 우클릭 떼기
        if (Input.GetMouseButtonUp(1))
            _panning = false;

        // ✅ 휠 줌
        float wheel = Input.mouseScrollDelta.y;
        if (!Mathf.Approximately(wheel, 0f))
        {
            Zoom(wheel);
        }
    }

    void Zoom(float wheelDeltaY)
    {
        float current = content.localScale.x;
        float target = current * (1f + Mathf.Sign(wheelDeltaY) * zoomStep);
        target = Mathf.Clamp(target, minZoom, maxZoom);
        if (Mathf.Approximately(target, current)) return;

        if (zoomToMouse && TryGetViewportLocal(Input.mousePosition, out var vpLocal))
        {
            Vector2 contentLocalBefore = (vpLocal - content.anchoredPosition) / current;

            SetUniformScale(target);

            content.anchoredPosition = vpLocal - contentLocalBefore * target;
        }
        else
        {
            SetUniformScale(target);
        }

        if (clampToBounds) ClampContentToViewport();
    }

    bool TryGetViewportLocal(Vector3 screenPos, out Vector2 vpLocal)
    {
        Camera cam = null;
        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            cam = canvas.worldCamera;

        return RectTransformUtility.ScreenPointToLocalPointInRectangle(
            viewport, screenPos, cam, out vpLocal);
    }

    void SetUniformScale(float s)
    {
        content.localScale = new Vector3(s, s, 1f);
    }

    void ClampContentToViewport()
    {
        Vector2 vp = viewport.rect.size;
        Vector2 ct = content.rect.size * content.localScale.x;

        float minX, maxX, minY, maxY;

        if (ct.x <= vp.x) minX = maxX = 0f;
        else
        {
            float halfDiffX = (ct.x - vp.x) * 0.5f;
            minX = -halfDiffX; maxX = +halfDiffX;
        }

        if (ct.y <= vp.y) minY = maxY = 0f;
        else
        {
            float halfDiffY = (ct.y - vp.y) * 0.5f;
            minY = -halfDiffY; maxY = +halfDiffY;
        }

        Vector2 p = content.anchoredPosition;
        p.x = Mathf.Clamp(p.x, minX, maxX);
        p.y = Mathf.Clamp(p.y, minY, maxY);
        content.anchoredPosition = p;
    }
}