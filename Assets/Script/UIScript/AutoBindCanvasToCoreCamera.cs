using UnityEngine;

[RequireComponent(typeof(Canvas))]
public class AutoBindCanvasToCoreCamera : MonoBehaviour
{
    [Header("Optional Direct Reference")]
    [SerializeField] Camera coreCamera;

    [Header("Auto Find")]
    [Tooltip("직접 참조가 없을 때 찾을 카메라 이름")]
    [SerializeField] string coreCameraName = "CoreUICamera";

    [Tooltip("true면 Screen Space - Camera 로 강제")]
    [SerializeField] bool forceScreenSpaceCamera = true;

    [SerializeField] float planeDistance = 100f;

    [Header("Rebind")]
    [Tooltip("씬 전환이나 카메라 재생성에 대비해 계속 확인")]
    [SerializeField] bool keepRebinding = true;

    [Tooltip("카메라 재탐색 주기")]
    [SerializeField] float retryInterval = 0.25f;

    Canvas _canvas;
    Camera _lastBound;
    float _nextRetryTime;

    void Awake()
    {
        _canvas = GetComponent<Canvas>();
    }

    void OnEnable()
    {
        TryBind();
    }

    void Start()
    {
        TryBind();
    }

    void LateUpdate()
    {
        if (!keepRebinding) return;
        if (Time.unscaledTime < _nextRetryTime) return;

        _nextRetryTime = Time.unscaledTime + retryInterval;

        if (_canvas == null)
            _canvas = GetComponent<Canvas>();

        // 이미 정상 연결되어 있으면 스킵
        if (_canvas != null &&
            _canvas.worldCamera != null &&
            _canvas.worldCamera == _lastBound)
            return;

        TryBind();
    }

    [ContextMenu("Bind Now")]
    public void TryBind()
    {
        if (_canvas == null)
            _canvas = GetComponent<Canvas>();

        if (_canvas == null)
            return;

        Camera target = ResolveCoreCamera();
        if (target == null)
            return;

        if (forceScreenSpaceCamera)
            _canvas.renderMode = RenderMode.ScreenSpaceCamera;

        _canvas.worldCamera = target;
        _canvas.planeDistance = planeDistance;
        _lastBound = target;
    }

    Camera ResolveCoreCamera()
    {
        if (coreCamera != null)
            return coreCamera;

        // 1. 이름으로 정확히 찾기
        Camera[] all = Camera.allCameras;
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].name == coreCameraName)
            {
                coreCamera = all[i];
                return coreCamera;
            }
        }

        // 2. 혹시 비활성/지연 생성 대비로 FindObjectsOfType
        Camera[] found = FindObjectsOfType<Camera>(true);
        for (int i = 0; i < found.Length; i++)
        {
            if (found[i] != null && found[i].name == coreCameraName)
            {
                coreCamera = found[i];
                return coreCamera;
            }
        }

        return null;
    }

    public void SetCoreCamera(Camera cam)
    {
        coreCamera = cam;
        TryBind();
    }
}