using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class RailAnchorHoverHighlight2D : MonoBehaviour
{
    [Header("Target Camera")]
    [SerializeField] Camera cam;
    [SerializeField] float cameraRetryInterval = 0.25f;
    float _nextCamRetryTime;

    [Header("Detection")]
    [Tooltip("마우스와 가장 가까운 Anchor를 찾을 최대 반경(px)")]
    [SerializeField, Min(1f)] float highlightRadiusPx = 18f;

    [Tooltip("앵커 목록을 다시 스캔하는 주기(초)")]
    [SerializeField, Min(0.1f)] float refreshAnchorInterval = 1f;

    [Header("Show Condition")]
    [Tooltip("빌드 모드에서만 하이라이트 표시")]
    [SerializeField] bool buildModeOnly = true;

    [Tooltip("UI 위에 마우스가 있으면 하이라이트 숨김")]
    [SerializeField] bool hideWhenPointerOverUI = true;

    [Tooltip("레일 배치 미리보기 중일 때도 표시")]
    [SerializeField] bool showDuringRailPreview = true;

    [Header("Visual")]
    [Tooltip("비워두면 자기 자신(transform)을 이동시킴")]
    [SerializeField] Transform visualRoot;

    [Tooltip("비워두면 자기 자신 하위의 모든 Renderer를 자동으로 찾음")]
    [SerializeField] Renderer[] targetRenderers;

    readonly List<RailSnapNode2D> _anchors = new List<RailSnapNode2D>(128);

    float _nextRefreshTime;
    RailSnapNode2D _currentAnchor;
    bool _visible;

    void Awake()
    {
        if (visualRoot == null)
            visualRoot = transform;

        if (targetRenderers == null || targetRenderers.Length == 0)
            targetRenderers = GetComponentsInChildren<Renderer>(true);

        SetVisible(false);
        RefreshAnchors();
    }

    void OnEnable()
    {
        RefreshAnchors();
        SetVisible(false);
    }

    void Update()
    {
        EnsureCamera();

        if (cam == null)
        {
            SetVisible(false);
            return;
        }

        if (Time.unscaledTime >= _nextRefreshTime)
        {
            _nextRefreshTime = Time.unscaledTime + refreshAnchorInterval;
            RefreshAnchors();
        }

        if (!CanShowNow())
        {
            ClearCurrent();
            return;
        }

        Vector3 mouseScreen = Input.mousePosition;
        Vector2 mouseWorld = cam.ScreenToWorldPoint(mouseScreen);

        float worldPerPixel = (cam.orthographicSize * 2f) / Screen.height;
        float radiusWorld = worldPerPixel * highlightRadiusPx;
        float radiusWorldSqr = radiusWorld * radiusWorld;

        RailSnapNode2D best = null;
        float bestSqr = float.PositiveInfinity;

        for (int i = _anchors.Count - 1; i >= 0; i--)
        {
            var node = _anchors[i];

            if (node == null)
            {
                _anchors.RemoveAt(i);
                continue;
            }

            if (!node.isActiveAndEnabled || !node.gameObject.activeInHierarchy)
                continue;

            if (!node.IsAnchor)
                continue;

            Vector2 diff = (Vector2)node.transform.position - mouseWorld;
            float sqr = diff.sqrMagnitude;

            if (sqr > radiusWorldSqr)
                continue;

            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                best = node;
            }
        }

        if (best == null)
        {
            ClearCurrent();
            return;
        }

        _currentAnchor = best;
        visualRoot.position = best.transform.position;
        SetVisible(true);
    }

    bool CanShowNow()
    {
        if (buildModeOnly)
        {
            if (GameModeManager.Instance != null && !GameModeManager.Instance.IsBuildMode)
                return false;
        }

        if (hideWhenPointerOverUI && IsPointerOverUI())
            return false;

        if (!showDuringRailPreview && RailToolPlacer2D.IsPlacementPreviewActive)
            return false;

        return true;
    }

    bool IsPointerOverUI()
    {
        if (EventSystem.current == null)
            return false;

        return EventSystem.current.IsPointerOverGameObject();
    }

    void ClearCurrent()
    {
        _currentAnchor = null;
        SetVisible(false);
    }

    void SetVisible(bool visible)
    {
        if (_visible == visible)
            return;

        _visible = visible;

        if (targetRenderers == null) return;

        for (int i = 0; i < targetRenderers.Length; i++)
        {
            if (targetRenderers[i] != null)
                targetRenderers[i].enabled = visible;
        }
    }

    void RefreshAnchors()
    {
#if UNITY_2023_1_OR_NEWER
        var found = FindObjectsByType<RailSnapNode2D>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
#else
        var found = FindObjectsOfType<RailSnapNode2D>(false);
#endif

        _anchors.Clear();

        for (int i = 0; i < found.Length; i++)
        {
            var node = found[i];
            if (node == null) continue;
            if (!node.IsAnchor) continue;

            _anchors.Add(node);
        }
    }

    void EnsureCamera()
    {
        if (cam != null && cam.isActiveAndEnabled) return;

        if (Time.unscaledTime < _nextCamRetryTime) return;
        _nextCamRetryTime = Time.unscaledTime + cameraRetryInterval;

        cam = Camera.main;
        if (cam != null && cam.isActiveAndEnabled) return;

        Camera[] cams = GameObject.FindObjectsOfType<Camera>(false);
        Camera best = null;
        float bestDepth = float.NegativeInfinity;

        for (int i = 0; i < cams.Length; i++)
        {
            var c = cams[i];
            if (c == null) continue;
            if (!c.isActiveAndEnabled) continue;

            if (best == null || c.depth > bestDepth)
            {
                best = c;
                bestDepth = c.depth;
            }
        }

        cam = best;
    }

    /// <summary>
    /// 외부에서 앵커 구조가 바뀐 뒤 수동 갱신할 때 호출
    /// </summary>
    public void ForceRefresh()
    {
        RefreshAnchors();
    }
}