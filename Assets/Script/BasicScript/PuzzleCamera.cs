using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PuzzleCamera : MonoBehaviour
{
    public Camera cam;

    [Header("Camera Binding")]
    [SerializeField] string coreCameraName = "CoreCamera";
    [SerializeField] bool preferCoreCamera = true;
    [SerializeField] bool rebindCameraOnSceneChanged = true;

    [Header("Pan")]
    [SerializeField] float panSpeed = 1f;

    [Header("Zoom Feel")]
    [SerializeField, Range(0.01f, 0.3f)]
    float zoomPercentPerWheel = 0.08f;

    [Header("Runtime Limits")]
    [SerializeField] float minZoom = 4f;
    [SerializeField] float maxZoom = 30f;
    const float DEFAULT_MAX_ZOOM_IF_STAGE_ZERO = 30f;

    [Header("Bounds")]
    [SerializeField] Vector2 boundsMin = new Vector2(-10, -10);
    [SerializeField] Vector2 boundsMax = new Vector2(10, 10);

    [Header("Zoom In Pose (Work View)")]
    [SerializeField] Vector2 resetPos = Vector2.zero;
    [SerializeField] float resetZoom = 8f;

    [Header("Intro Start Pose (1 sec hold)")]
    [SerializeField] Vector2 introStartPos;
    [SerializeField] float introStartZoom = 10f;

    [Header("Zoom Out Pose (Overview View)")]
    [SerializeField] Vector2 zoomOutPos;
    [SerializeField] float zoomOutZoom = 10f;

    [Header("Stage Intro (Overview)")]
    [SerializeField] bool showOverviewOnEnter = true;
    [SerializeField] float overviewMoveTime = 0.35f;
    [SerializeField] float holdAtResetBeforeMove = 1.0f;

    [Header("UI Hide During Overview")]
    [SerializeField] Canvas[] hideCanvasesDuringOverview;

    [Header("Zoom Toggle (UI)")]
    [SerializeField] float zoomToggleTime = 0.25f;

    [Header("UI Sprites (Toggle Icons)")]
    [SerializeField] Image overviewToggleButtonImage;
    [SerializeField] Sprite spriteOverviewOff;
    [SerializeField] Sprite spriteOverviewOn;
    [SerializeField] bool dimWhenLocked = true;
    [SerializeField, Range(0.2f, 1f)] float lockedAlpha = 0.6f;

    [Header("UI - Overview Toggle Tooltip")]
    [SerializeField] Button overviewToggleButton;
    [SerializeField] TooltipTrigger overviewToggleTooltip;
    [SerializeField] string tooltipKeyOverview = "tooltip_overview";
    [SerializeField] string tooltipKeyReturnToWorkView = "tooltip_return_to_workview";

    [Header("Goal Feedback")]
    [SerializeField] float goalHitZoomMultiplier = 0.92f;
    [SerializeField] float goalHitDuration = 0.12f;
    [SerializeField] bool ignoreGoalFeedbackDuringOverview = true;

    Coroutine _goalHitCo;

    bool _isZoomedOut;
    bool _isOverviewLocked;
    bool _isZoomAnimating;
    Coroutine _zoomToggleCo;

    Vector3 _zoomSavedPos;
    float _zoomSavedSize;
    bool _hasZoomSaved;

    bool _inOverview;
    Coroutine _overviewCo;

    Vector3 _lastMouseScreen;
    bool _isRightPanning;

    StageConfigHolder _holder;

    float _lastAspect = -1f;
    int _lastPixelH = -1;

    Transform CamTr => cam != null ? cam.transform : transform;

    void Awake()
    {
        ResolveCamera();

        if (overviewToggleTooltip == null && overviewToggleButton != null)
            overviewToggleTooltip = overviewToggleButton.GetComponent<TooltipTrigger>();

        SceneManager.activeSceneChanged += OnActiveSceneChanged;
        RefreshHolderFromActiveScene();
    }

    void OnDestroy()
    {
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
    }

    void OnDisable()
    {
        StopOverview();
    }

    void Start()
    {
        ResolveCamera();

        if (cam == null)
        {
            Debug.LogWarning("[PuzzleCamera] Camera not found.");
            enabled = false;
            return;
        }

        ApplyStageConfigIfExists();
        ResetView();
        CacheAspect();

        if (showOverviewOnEnter)
            StartOverview();
    }

    void OnActiveSceneChanged(Scene oldScene, Scene newScene)
    {
        StopOverview();

        if (rebindCameraOnSceneChanged)
            ResolveCamera();

        if (cam == null)
        {
            Debug.LogWarning("[PuzzleCamera] Camera not found after scene change.");
            return;
        }

        RefreshHolderFromActiveScene();
        ApplyStageConfigIfExists();

        ResetView();
        CacheAspect();

        if (showOverviewOnEnter)
            StartOverview();
    }

    void Update()
    {
        if (cam == null)
        {
            ResolveCamera();
            if (cam == null) return;
        }

        bool allowInput = !_inOverview && !_isZoomAnimating;

        if (allowInput)
        {
            HandlePan();
            HandleZoom();

            if (GameModeManager.Instance != null &&
                GameModeManager.Instance.currentMode == GameMode.Build)
            {
                HandleReset();
            }
        }

        if (HasAspectChanged())
        {
            if (!_inOverview && !_isZoomAnimating)
            {
                ApplyStageConfigIfExists();
                ClampNow();
            }
            CacheAspect();
        }
    }

    void ResolveCamera()
    {
        if (preferCoreCamera)
        {
            var foundCore = FindCameraByName(coreCameraName);
            if (foundCore != null)
            {
                cam = foundCore;
                return;
            }
        }

        if (cam != null)
            return;

        cam = Camera.main;
    }

    Camera FindCameraByName(string targetName)
    {
        if (string.IsNullOrWhiteSpace(targetName))
            return null;

        Camera[] all = FindObjectsOfType<Camera>(true);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].name == targetName)
                return all[i];
        }

        return null;
    }

    public void SetCamera(Camera target)
    {
        cam = target;
        CacheAspect();
    }

    public void ResetView()
    {
        if (cam == null) return;

        CamTr.position = new Vector3(resetPos.x, resetPos.y, CamTr.position.z);
        cam.orthographicSize = resetZoom;
        ClampNow();

        _isZoomedOut = false;
        _hasZoomSaved = false;
        _isOverviewLocked = false;

        RefreshToggleIcons();
    }

    public void FocusToBounds()
    {
        if (cam == null) return;

        Vector2 center = (boundsMin + boundsMax) * 0.5f;
        CamTr.position = new Vector3(center.x, center.y, CamTr.position.z);

        float allowedMax = CalcAllowedMaxZoom(boundsMin, boundsMax, minZoom);
        cam.orthographicSize = Mathf.Clamp(allowedMax, minZoom, maxZoom);

        ClampNow();
    }

    public void UI_ToggleOverviewMap()
    {
        if (cam == null) return;
        if (_inOverview) return;
        if (_isZoomAnimating) return;

        if (_isZoomedOut)
        {
            if (UISoundManager.I != null)
                UISoundManager.I.PlayRelease();

            ZoomBackToSavedOrReset();
        }
        else
        {
            if (UISoundManager.I != null)
                UISoundManager.I.PlayApply();

            ZoomOutToPose();
        }
    }

    public void UI_ResetToWorkView()
    {
        if (cam == null) return;

        StopOverview();

        if (_zoomToggleCo != null)
        {
            StopCoroutine(_zoomToggleCo);
            _zoomToggleCo = null;
        }

        _isZoomAnimating = false;
        _isZoomedOut = false;
        _isOverviewLocked = false;

        cam.orthographicSize = resetZoom;
        ClampNow();

        RefreshToggleIcons();
    }

    bool IsLimitsDisabled()
    {
        return _inOverview || _isZoomedOut || _isOverviewLocked;
    }

    void ZoomOutToPose()
    {
        _zoomSavedPos = CamTr.position;
        _zoomSavedSize = cam.orthographicSize;
        _hasZoomSaved = true;

        _isZoomedOut = true;
        _isOverviewLocked = true;

        RefreshToggleIcons();

        Vector3 toPos = new Vector3(zoomOutPos.x, zoomOutPos.y, CamTr.position.z);
        float toZoom = zoomOutZoom;

        StartZoomToggle(toPos, toZoom, zoomToggleTime, true);
    }

    void ZoomBackToSavedOrReset()
    {
        _isZoomedOut = false;
        _isOverviewLocked = false;

        RefreshToggleIcons();

        Vector3 toPos;
        float toZoom;

        if (_hasZoomSaved)
        {
            toPos = _zoomSavedPos;
            toZoom = _zoomSavedSize;
        }
        else
        {
            toPos = CamTr.position;
            toZoom = resetZoom;
        }

        StartZoomToggle(toPos, toZoom, zoomToggleTime, false);
    }

    void StartZoomToggle(Vector3 toPos, float toZoom, float time, bool makeZoomedOut)
    {
        if (_zoomToggleCo != null)
        {
            StopCoroutine(_zoomToggleCo);
            _zoomToggleCo = null;
        }

        _zoomToggleCo = StartCoroutine(CoZoomToggle(toPos, toZoom, time, makeZoomedOut));
    }

    IEnumerator CoZoomToggle(Vector3 toPos, float toZoom, float time, bool makeZoomedOut)
    {
        _isZoomAnimating = true;

        Vector3 fromPos = CamTr.position;
        float fromZoom = cam.orthographicSize;
        float t = Mathf.Max(0f, time);

        if (t <= 0f)
        {
            CamTr.position = toPos;
            cam.orthographicSize = toZoom;
        }
        else
        {
            float elapsed = 0f;
            while (elapsed < t)
            {
                elapsed += Time.unscaledDeltaTime;
                float a = Mathf.Clamp01(elapsed / t);
                a = a * a * (3f - 2f * a);

                CamTr.position = Vector3.Lerp(fromPos, toPos, a);
                cam.orthographicSize = Mathf.Lerp(fromZoom, toZoom, a);

                ClampPosition();
                yield return null;
            }
        }

        ClampNow();

        _isZoomAnimating = false;
        _zoomToggleCo = null;
    }

    void ApplyStageConfigIfExists()
    {
        if (_holder == null || _holder.gameObject.scene != SceneManager.GetActiveScene())
            RefreshHolderFromActiveScene();

        if (_holder == null || _holder.config == null)
            return;

        var lim = _holder.config.cameraLimits;

        maxZoom = (lim.MaxZoomCap <= 0f) ? DEFAULT_MAX_ZOOM_IF_STAGE_ZERO : lim.MaxZoomCap;
        maxZoom = Mathf.Max(minZoom, maxZoom);

        resetPos = lim.zoomInPos;
        resetZoom = ClampZoom(lim.zoomInZoom);

        boundsMin = lim.BoundsMin;
        boundsMax = lim.BoundsMax;

        if (boundsMin.x > boundsMax.x) (boundsMin.x, boundsMax.x) = (boundsMax.x, boundsMin.x);
        if (boundsMin.y > boundsMax.y) (boundsMin.y, boundsMax.y) = (boundsMax.y, boundsMin.y);

        introStartPos = lim.introStartPose.pos;
        introStartZoom = Mathf.Max(0.01f, lim.introStartPose.zoom);

        zoomOutPos = lim.zoomOutPose.pos;
        zoomOutZoom = Mathf.Max(0.01f, lim.zoomOutPose.zoom);

        zoomOutZoom = Mathf.Min(zoomOutZoom, maxZoom);
    }

    void RefreshHolderFromActiveScene()
    {
        _holder = null;

        var s = SceneManager.GetActiveScene();
        if (!s.IsValid()) return;

        var roots = s.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            var h = roots[i].GetComponentInChildren<StageConfigHolder>(true);
            if (h != null)
            {
                _holder = h;
                break;
            }
        }
    }

    Vector2 ClampToBounds(Vector2 p)
    {
        return new Vector2(
            Mathf.Clamp(p.x, boundsMin.x, boundsMax.x),
            Mathf.Clamp(p.y, boundsMin.y, boundsMax.y)
        );
    }

    float ClampZoom(float z)
    {
        z = Mathf.Max(0.01f, z);
        return Mathf.Clamp(z, minZoom, maxZoom);
    }

    float CalcAllowedMaxZoom(Vector2 bMin, Vector2 bMax, float minZoomValue)
    {
        float aspect = (cam != null) ? cam.aspect : (16f / 9f);

        float width = Mathf.Max(0.01f, bMax.x - bMin.x);
        float height = Mathf.Max(0.01f, bMax.y - bMin.y);

        float allowedByHeight = height * 0.5f;
        float allowedByWidth = (width * 0.5f) / Mathf.Max(0.0001f, aspect);

        return Mathf.Max(minZoomValue, Mathf.Min(allowedByHeight, allowedByWidth));
    }

    void StartOverview()
    {
        StopOverview();
        SetIntroUIVisible(false);
        RefreshToggleIcons();
        _overviewCo = StartCoroutine(CoEnterOverview());
    }

    void StopOverview()
    {
        _inOverview = false;
        SetIntroUIVisible(true);

        if (_overviewCo != null)
        {
            StopCoroutine(_overviewCo);
            _overviewCo = null;
        }
    }

    IEnumerator CoEnterOverview()
    {
        if (cam == null) yield break;

        _inOverview = true;
        _isZoomedOut = false;
        _isOverviewLocked = false;

        Vector3 zPos = new Vector3(0, 0, CamTr.position.z);

        Vector3 startPos = new Vector3(introStartPos.x, introStartPos.y, zPos.z);
        float startZoom = introStartZoom;

        CamTr.position = startPos;
        cam.orthographicSize = startZoom;
        ClampPosition();

        yield return new WaitForSecondsRealtime(Mathf.Max(0f, holdAtResetBeforeMove));

        Vector3 endPos = new Vector3(resetPos.x, resetPos.y, zPos.z);
        float endZoom = resetZoom;

        yield return MoveZoom(
            CamTr.position, endPos,
            cam.orthographicSize, endZoom,
            overviewMoveTime
        );

        ClampNow();

        _inOverview = false;
        _overviewCo = null;

        SetIntroUIVisible(true);
        RefreshToggleIcons();
    }

    IEnumerator MoveZoom(Vector3 fromPos, Vector3 toPos, float fromZoom, float toZoom, float t)
    {
        if (cam == null) yield break;

        if (t <= 0f)
        {
            CamTr.position = toPos;
            cam.orthographicSize = toZoom;
            ClampPosition();
            yield break;
        }

        float time = 0f;
        while (time < t)
        {
            time += Time.unscaledDeltaTime;
            float a = Mathf.Clamp01(time / t);

            CamTr.position = Vector3.Lerp(fromPos, toPos, a);
            cam.orthographicSize = Mathf.Lerp(fromZoom, toZoom, a);

            ClampPosition();
            yield return null;
        }
    }

    bool IsMousePositionValid()
    {
        Vector3 mouse = Input.mousePosition;
        return mouse.x >= 0 && mouse.y >= 0 &&
               mouse.x <= Screen.width &&
               mouse.y <= Screen.height;
    }

    void HandlePan()
    {
        if (_isOverviewLocked) return;

        if (Input.GetMouseButtonDown(1))
        {
            _isRightPanning = true;
            _lastMouseScreen = Input.mousePosition;
        }

        if (Input.GetMouseButtonUp(1))
            _isRightPanning = false;

        if (!_isRightPanning) return;

        if (!IsMousePositionValid())
        {
            _lastMouseScreen = Input.mousePosition;
            return;
        }

        Vector3 cur = Input.mousePosition;
        Vector3 deltaPx = cur - _lastMouseScreen;

        if (deltaPx.sqrMagnitude < 0.25f)
        {
            _lastMouseScreen = cur;
            return;
        }

        float worldPerPixelY = (cam.orthographicSize * 2f) / Mathf.Max(1, cam.pixelHeight);
        float worldPerPixelX = worldPerPixelY * cam.aspect;

        Vector3 moveWorld = new Vector3(
            -deltaPx.x * worldPerPixelX,
            -deltaPx.y * worldPerPixelY,
            0f
        );

        CamTr.position += moveWorld * panSpeed;
        ClampPosition();

        _lastMouseScreen = cur;
    }

    void HandleZoom()
    {
        float scroll = Input.mouseScrollDelta.y;
        if (scroll == 0) return;

        if (_isOverviewLocked || _isZoomedOut)
        {
            _isZoomedOut = false;
            _isOverviewLocked = false;

            RefreshToggleIcons();
        }

        float factor = 1f - scroll * zoomPercentPerWheel;
        factor = Mathf.Clamp(factor, 0.2f, 5f);

        cam.orthographicSize *= factor;
        cam.orthographicSize = Mathf.Clamp(cam.orthographicSize, minZoom, maxZoom);

        ClampPosition();
    }

    void HandleReset()
    {
    }

    void ClampNow()
    {
        if (cam == null) return;
        if (IsLimitsDisabled()) return;

        cam.orthographicSize = Mathf.Clamp(cam.orthographicSize, minZoom, maxZoom);
        ClampPosition();
    }

    void ClampPosition()
    {
        if (cam == null) return;
        if (IsLimitsDisabled()) return;

        Vector3 p = CamTr.position;

        float halfH = cam.orthographicSize;
        float halfW = halfH * cam.aspect;

        float minX = boundsMin.x + halfW;
        float maxX = boundsMax.x - halfW;
        float minY = boundsMin.y + halfH;
        float maxY = boundsMax.y - halfH;

        if (minX > maxX) p.x = (boundsMin.x + boundsMax.x) * 0.5f;
        else p.x = Mathf.Clamp(p.x, minX, maxX);

        if (minY > maxY) p.y = (boundsMin.y + boundsMax.y) * 0.5f;
        else p.y = Mathf.Clamp(p.y, minY, maxY);

        CamTr.position = p;
    }

    bool HasAspectChanged()
    {
        if (cam == null) return false;
        if (_lastPixelH != cam.pixelHeight) return true;
        if (Mathf.Abs(_lastAspect - cam.aspect) > 0.0001f) return true;
        return false;
    }

    void CacheAspect()
    {
        if (cam == null) return;
        _lastAspect = cam.aspect;
        _lastPixelH = cam.pixelHeight;
    }

    void SetIntroUIVisible(bool visible)
    {
        if (hideCanvasesDuringOverview == null) return;

        for (int i = 0; i < hideCanvasesDuringOverview.Length; i++)
        {
            var c = hideCanvasesDuringOverview[i];
            if (c != null) c.enabled = visible;
        }
    }

    void RefreshToggleIcons()
    {
        if (overviewToggleButtonImage != null)
        {
            var sp = _isZoomedOut ? spriteOverviewOn : spriteOverviewOff;
            if (sp != null) overviewToggleButtonImage.sprite = sp;
        }

        if (overviewToggleTooltip != null)
        {
            overviewToggleTooltip.tooltipKey = _isZoomedOut
                ? tooltipKeyReturnToWorkView
                : tooltipKeyOverview;

            if (!_inOverview && IsPointerOverOverviewToggleButton())
            {
                TooltipManager.I?.RestartShowKey(
                    overviewToggleTooltip.tooltipKey,
                    overviewToggleTooltip.xDir,
                    overviewToggleTooltip.yDir
                );
            }
        }
    }

    bool IsPointerOverOverviewToggleButton()
    {
        if (overviewToggleButton == null)
            return false;

        var rt = overviewToggleButton.transform as RectTransform;
        if (rt == null)
            return false;

        Canvas canvas = overviewToggleButton.GetComponentInParent<Canvas>();
        Camera eventCam = null;

        if (canvas != null &&
            (canvas.renderMode == RenderMode.ScreenSpaceCamera || canvas.renderMode == RenderMode.WorldSpace))
        {
            eventCam = canvas.worldCamera;
        }

        return RectTransformUtility.RectangleContainsScreenPoint(rt, Input.mousePosition, eventCam);
    }

    public void PlayGoalHitFeedback()
    {
        if (cam == null) return;
        if (_isZoomAnimating) return;
        if (ignoreGoalFeedbackDuringOverview && _inOverview) return;
        if (!GoalHitFeedbackOption.Enabled) return;

        if (_goalHitCo != null)
            StopCoroutine(_goalHitCo);

        _goalHitCo = StartCoroutine(CoGoalHitFeedback());
    }

    IEnumerator CoGoalHitFeedback()
    {
        float baseZoom = cam.orthographicSize;
        float targetZoom = Mathf.Max(0.01f, baseZoom * goalHitZoomMultiplier);

        float half = Mathf.Max(0.01f, goalHitDuration * 0.5f);

        float t = 0f;
        while (t < half)
        {
            t += Time.unscaledDeltaTime;
            float a = Mathf.Clamp01(t / half);
            a = 1f - Mathf.Pow(1f - a, 3f);

            cam.orthographicSize = Mathf.Lerp(baseZoom, targetZoom, a);
            ClampPosition();
            yield return null;
        }

        t = 0f;
        while (t < half)
        {
            t += Time.unscaledDeltaTime;
            float a = Mathf.Clamp01(t / half);
            a = a * a * (3f - 2f * a);

            cam.orthographicSize = Mathf.Lerp(targetZoom, baseZoom, a);
            ClampPosition();
            yield return null;
        }

        cam.orthographicSize = baseZoom;
        ClampPosition();

        _goalHitCo = null;
    }

}