using UnityEngine;
using UnityEngine.UI;

public class PlacementObjectActionButtonsPresenter : MonoBehaviour
{
    enum HoverPreviewButtonType
    {
        None,
        Rotate,
        FlipX,
        FlipY,
        StrengthDown,
        StrengthUp
    }

    [Header("Refs")]
    [SerializeField] GridPlacer gridPlacer;
    [SerializeField] Canvas canvas;
    [SerializeField] RectTransform panelRoot;
    [SerializeField] Button deleteButton;
    [SerializeField] Button rotateButton;
    [SerializeField] Button flipXButton;
    [SerializeField] Button flipYButton;
    [SerializeField] Button strengthDownButton;
    [SerializeField] Button strengthUpButton;
    [SerializeField] Button demoButton;

    [Header("Follow")]
    [SerializeField] Camera worldCamera;
    [Tooltip("Canvas 로컬(UI) 단위 오프셋. CanvasScaler/해상도/창 크기 달라도 체감 일정")]
    [SerializeField] Vector2 canvasLocalOffset = new Vector2(0f, 60f);

    [SerializeField] RectTransform visualPanel;

    [Header("Rotate")]
    [SerializeField] float rotateDeltaDegrees = -90f;

    [Header("Scale")]
    [SerializeField] bool scaleWithZoom = true;
    [SerializeField] float referenceOrthoSize = 5f;
    [SerializeField] float minScale = 0.6f;
    [SerializeField] float maxScale = 1.6f;

    [Header("Groups")]
    [SerializeField] GameObject buttonsGroup;
    [SerializeField] GameObject lockIconObject;

    [Header("Strength Hover Preview")]
    [SerializeField] PlacementCellOverlay2D placementCellOverlay;

    [Header("Hover Preview Cooldown")]
    [SerializeField] float actionButtonsHideDuration = 0.6f;
    [SerializeField] float keyboardInputCooldown = 0.1f;

    [SerializeField] GameKeyBindingConfig keyConfig;

    float _buttonsHiddenUntil = -1f;
    float _keyboardBlockedUntil = -1f;

    PlacementObject _lastPreviewPO;
    StrengthBasedOccupancyCells _lastPreviewStrength;

    PlacementObject _transformPreviewPO;
    Quaternion _transformPreviewOriginalRot;
    Vector3 _transformPreviewOriginalScale;
    bool _isTransformHoverPreviewing;

    int _strengthHoverDelta = 0; // -1, 0, +1

    HoverPreviewButtonType _currentHoveredButton = HoverPreviewButtonType.None;
    float _hoverPreviewBlockedUntil = -1f;

    public bool IsSelectedLocked { get; private set; }

    RectTransform _canvasRt;

    bool _flipXHoverCachedCanExecute;
    bool _flipYHoverCachedCanExecute;
    bool _rotateHoverCachedCanExecute;

    public enum KeyboardAction
    {
        None,
        Demo,
        FlipX,
        StrengthDown,
        StrengthUp,
        Delete
    }

    KeyboardAction _activeKeyboardAction = KeyboardAction.None;
    KeyCode _activeKeyboardKey = KeyCode.None;

    void Awake()
    {
        if (worldCamera == null) worldCamera = Camera.main;

        if (canvas != null)
            _canvasRt = canvas.GetComponent<RectTransform>();

        if (deleteButton != null) deleteButton.onClick.AddListener(OnClickDelete);
        if (rotateButton != null) rotateButton.onClick.AddListener(OnClickRotate);
        if (flipXButton != null) flipXButton.onClick.AddListener(OnClickFlipX);
        if (flipYButton != null) flipYButton.onClick.AddListener(OnClickFlipY);

        if (strengthDownButton != null) strengthDownButton.onClick.AddListener(OnClickStrengthDown);
        if (strengthUpButton != null) strengthUpButton.onClick.AddListener(OnClickStrengthUp);

        if (demoButton != null) demoButton.onClick.AddListener(OnClickDemo);

        if (buttonsGroup != null)
            buttonsGroup.SetActive(CanShowButtonsNow());

        if (panelRoot != null)
            panelRoot.gameObject.SetActive(false);
    }

    void Update()
    {
        if (gridPlacer == null || panelRoot == null || canvas == null) return;

        var po = gridPlacer.SelectedPO;
        bool has = (po != null);

        if (!has)
        {
            NotifyHoverExit();
            RestoreLastStrengthVisualPreview();
            EndTransformHoverPreview();

            if (panelRoot.gameObject.activeSelf)
                panelRoot.gameObject.SetActive(false);

            if (buttonsGroup != null && buttonsGroup.activeSelf)
                buttonsGroup.SetActive(false);

            if (lockIconObject != null && lockIconObject.activeSelf)
                lockIconObject.SetActive(false);

            return;
        }

        // 추가
        if (IsSelectedPODemoPlaying())
        {
            NotifyHoverExit();
            RestoreLastStrengthVisualPreview();
            EndTransformHoverPreview();

            if (panelRoot.gameObject.activeSelf)
                panelRoot.gameObject.SetActive(false);

            if (buttonsGroup != null && buttonsGroup.activeSelf)
                buttonsGroup.SetActive(false);

            if (lockIconObject != null && lockIconObject.activeSelf)
                lockIconObject.SetActive(false);

            return;
        }


        bool hideWhileDragging = gridPlacer.IsDraggingSelectedPO;

        if (hideWhileDragging)
        {
            NotifyHoverExit();
            RestoreLastStrengthVisualPreview();
            EndTransformHoverPreview();

            if (panelRoot.gameObject.activeSelf)
                panelRoot.gameObject.SetActive(false);

            if (buttonsGroup != null && buttonsGroup.activeSelf)
                buttonsGroup.SetActive(false);

            if (lockIconObject != null && lockIconObject.activeSelf)
                lockIconObject.SetActive(false);

            return;
        }

        if (!panelRoot.gameObject.activeSelf)
            panelRoot.gameObject.SetActive(true);

        if (worldCamera == null) worldCamera = Camera.main;
        if (worldCamera == null) return;

        if (_canvasRt == null)
            _canvasRt = canvas.GetComponent<RectTransform>();

        Vector2 worldPos = po.GetActionButtonsWorldPos();
        Vector3 sp = worldCamera.WorldToScreenPoint(worldPos);

        if (sp.z < 0f)
        {
            if (buttonsGroup != null && buttonsGroup.activeSelf)
                buttonsGroup.SetActive(false);

            if (lockIconObject != null && lockIconObject.activeSelf)
                lockIconObject.SetActive(false);
        }

        Camera eventCam = null;
        if (canvas.renderMode == RenderMode.ScreenSpaceCamera || canvas.renderMode == RenderMode.WorldSpace)
            eventCam = canvas.worldCamera != null ? canvas.worldCamera : worldCamera;

        // 1. 먼저 스케일 결정
        if (scaleWithZoom && worldCamera.orthographic)
        {
            float s = referenceOrthoSize / Mathf.Max(0.0001f, worldCamera.orthographicSize);
            s = Mathf.Clamp(s, minScale, maxScale);
            panelRoot.localScale = Vector3.one * s;
        }
        else
        {
            panelRoot.localScale = Vector3.one;
        }

        // 2. 그 다음 위치 계산
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRt, sp, eventCam, out var localPos))
        {
            Vector2 correction = GetPanelEdgeCorrection(po);
            panelRoot.anchoredPosition = localPos + correction;
        }

        bool locked = gridPlacer.IsSelectedLocked;
        IsSelectedLocked = locked;

        if (buttonsGroup != null)
            buttonsGroup.SetActive(CanShowButtonsNow());

        if (lockIconObject != null)
            lockIconObject.SetActive(locked);

        if (!locked)
        {
            ApplyPlacementDataRules(po);
            RefreshStrengthHoverPreview();
            RefreshHoveredButtonPreviewIfNeeded();

            HandleKeyboardShortcuts();
        }
        else
        {
            NotifyHoverExit();
            EndTransformHoverPreview();

            if (placementCellOverlay != null)
                placementCellOverlay.ClearExternalPreview();

            RestoreLastStrengthVisualPreview();

            HideAllActionButtons();

            // ✅ 잠금 오브젝트여도 데모 버튼 키보드 입력은 허용
            HandleKeyboardShortcuts();
        }
    }

    Vector2 GetPanelEdgeCorrection(PlacementObject po)
    {
        if (po == null || panelRoot == null)
            return Vector2.zero;

        RectTransform rt = visualPanel != null ? visualPanel : panelRoot;

        // visualPanel이 panelRoot 기준으로 실제 어디까지 차지하는지 계산
        Bounds b = RectTransformUtility.CalculateRelativeRectTransformBounds(panelRoot, rt);

        float scaleY = panelRoot.localScale.y;

        if (po.ActionButtonsAttachDirection == ActionButtonsAttachDirection.Above)
        {
            // visualPanel의 아래쪽이 기준점에 딱 붙게
            return Vector2.up * (-b.min.y * scaleY);
        }
        else
        {
            // visualPanel의 위쪽이 기준점에 딱 붙게
            return Vector2.down * (b.max.y * scaleY);
        }
    }


    void HideAllActionButtons()
    {
        if (deleteButton != null) deleteButton.gameObject.SetActive(false);
        if (rotateButton != null) rotateButton.gameObject.SetActive(false);
        if (flipXButton != null) flipXButton.gameObject.SetActive(false);
        if (flipYButton != null) flipYButton.gameObject.SetActive(false);
        if (strengthDownButton != null) strengthDownButton.gameObject.SetActive(false);
        if (strengthUpButton != null) strengthUpButton.gameObject.SetActive(false);
    }

    public void NotifyHoverExit()
    {
        _flipXHoverCachedCanExecute = false;
        _flipYHoverCachedCanExecute = false;
        _rotateHoverCachedCanExecute = false;

        _currentHoveredButton = HoverPreviewButtonType.None;
        _strengthHoverDelta = 0;

        // 호버가 풀리면 쿨타임도 같이 초기화
        _hoverPreviewBlockedUntil = -1f;

        // 강도 프리뷰 제거
        if (placementCellOverlay != null)
            placementCellOverlay.ClearExternalPreview();

        RestoreLastStrengthVisualPreview();

        if (gridPlacer != null)
            gridPlacer.ClearBlockedRailHoverPreview();

        // 회전/플립 프리뷰 제거
        EndTransformHoverPreview();
    }

    void BlockCurrentHoverPreviewTemporarily()
    {
        _hoverPreviewBlockedUntil = Time.unscaledTime + keyboardInputCooldown;

        if (placementCellOverlay != null)
            placementCellOverlay.ClearExternalPreview();

        RestoreLastStrengthVisualPreview();
        EndTransformHoverPreview();
    }

    bool IsHoverPreviewBlocked()
    {
        return Time.unscaledTime < _hoverPreviewBlockedUntil;
    }

    void RefreshHoveredButtonPreviewIfNeeded()
    {
        if (IsHoverPreviewBlocked())
            return;

        switch (_currentHoveredButton)
        {
            case HoverPreviewButtonType.Rotate:
                if (!_isTransformHoverPreviewing)
                    BeginRotateHoverPreview(rotateDeltaDegrees);
                break;

            case HoverPreviewButtonType.FlipX:
                if (!_isTransformHoverPreviewing)
                    BeginFlipXHoverPreview();
                break;

            case HoverPreviewButtonType.FlipY:
                if (!_isTransformHoverPreviewing)
                    BeginFlipYHoverPreview();
                break;

            case HoverPreviewButtonType.StrengthDown:
            case HoverPreviewButtonType.StrengthUp:
                if (_strengthHoverDelta != 0)
                    RefreshStrengthHoverPreview();
                break;
        }
    }

    public void OnClickRotate()
    {
        if (gridPlacer == null) return;

        BlockCurrentHoverPreviewTemporarily();
        gridPlacer.UI_RotateSelectedPO(rotateDeltaDegrees);

        HideButtonsTemporarily();
    }

    public void OnClickFlipX()
    {
        if (gridPlacer == null) return;

        ClearAllPreviewState();

        BlockCurrentHoverPreviewTemporarily();
        gridPlacer.UI_FlipSelectedPO_X();

        HideButtonsTemporarily();
    }

    public void OnClickFlipY()
    {
        if (gridPlacer == null) return;

        ClearAllPreviewState();

        BlockCurrentHoverPreviewTemporarily();
        gridPlacer.UI_FlipSelectedPO_Y();

        HideButtonsTemporarily();
    }

    public void OnClickStrengthDown()
    {
        if (gridPlacer == null) return;

        ClearAllPreviewState();

        BlockCurrentHoverPreviewTemporarily();
        gridPlacer.UI_DecreaseSelectedStrength();

        HideButtonsTemporarily();
    }

    public void OnClickStrengthUp()
    {
        if (gridPlacer == null) return;

        ClearAllPreviewState();

        BlockCurrentHoverPreviewTemporarily();
        gridPlacer.UI_IncreaseSelectedStrength();

        HideButtonsTemporarily();
    }

    public void OnClickDelete()
    {
        if (gridPlacer == null) return;

        gridPlacer.UI_DeleteSelectedPO();

        HideButtonsTemporarily();
    }

    void OnClickDemo()
    {
        if (gridPlacer == null)
            return;

        var po = gridPlacer.SelectedPO;
        if (po == null)
            return;

        if (gridPlacer.IsDraggingSelectedPO)
            return;

        var demo = FindDemoLink(po);
        if (demo == null)
            return;

        // 데모 시작 전에 프리뷰/호버 정리
        NotifyHoverExit();
        RestoreLastStrengthVisualPreview();
        EndTransformHoverPreview();

        if (placementCellOverlay != null)
            placementCellOverlay.ClearExternalPreview();

        if (gridPlacer != null)
            gridPlacer.ClearBlockedRailHoverPreview();

        demo.PlayDemo();
    }

    PoDemoLink FindDemoLink(PlacementObject po)
    {
        if (po == null)
            return null;

        var demo = po.GetComponent<PoDemoLink>();
        if (demo != null)
            return demo;

        return po.GetComponentInChildren<PoDemoLink>(true);
    }

    void ApplyPlacementDataRules(PlacementObject po)
    {
        var data = (po != null) ? po.placementData : null;

        bool allowRotateFeature = (data == null) ? true : data.allowRotate;
        bool allowFlipXFeature = (data == null) ? true : data.allowFlipX;
        bool allowFlipYFeature = (data == null) ? true : data.allowFlipY;
        bool allowStrengthFeature = (data != null) && data.allowStrengthControl;

        if (deleteButton != null) deleteButton.gameObject.SetActive(true);
        if (rotateButton != null) rotateButton.gameObject.SetActive(allowRotateFeature);
        if (flipXButton != null) flipXButton.gameObject.SetActive(allowFlipXFeature);
        if (flipYButton != null) flipYButton.gameObject.SetActive(allowFlipYFeature);
        if (strengthDownButton != null) strengthDownButton.gameObject.SetActive(allowStrengthFeature);
        if (strengthUpButton != null) strengthUpButton.gameObject.SetActive(allowStrengthFeature);

        if (allowRotateFeature)
        {
            bool canRotateNow =
                (_isTransformHoverPreviewing &&
                 _currentHoveredButton == HoverPreviewButtonType.Rotate &&
                 _transformPreviewPO == po)
                ? _rotateHoverCachedCanExecute
                : (gridPlacer != null && gridPlacer.CanRotateSelectedNow(rotateDeltaDegrees));

            ApplyButtonState(rotateButton, canRotateNow);
        }

        if (allowFlipXFeature)
        {
            bool canFlipXNow =
                (_isTransformHoverPreviewing &&
                 _currentHoveredButton == HoverPreviewButtonType.FlipX &&
                 _transformPreviewPO == po)
                ? _flipXHoverCachedCanExecute
                : (gridPlacer != null && gridPlacer.CanFlipSelectedXNow());

            ApplyButtonState(flipXButton, canFlipXNow);
        }

        if (allowFlipYFeature)
        {
            bool canFlipYNow =
                (_isTransformHoverPreviewing &&
                 _currentHoveredButton == HoverPreviewButtonType.FlipY &&
                 _transformPreviewPO == po)
                ? _flipYHoverCachedCanExecute
                : (gridPlacer != null && gridPlacer.CanFlipSelectedYNow());

            ApplyButtonState(flipYButton, canFlipYNow);
        }

        if (allowStrengthFeature)
        {
            bool canDownNow = gridPlacer != null && gridPlacer.CanDecreaseSelectedStrengthNow();
            bool canUpNow = gridPlacer != null && gridPlacer.CanIncreaseSelectedStrengthNow();

            if (strengthDownButton != null)
                strengthDownButton.interactable = canDownNow;

            if (strengthUpButton != null)
                strengthUpButton.interactable = canUpNow;
        }

        if (deleteButton != null)
            deleteButton.interactable = true;
    }

    void ApplyButtonState(Button button, bool enabledState)
    {
        if (button != null)
            button.interactable = enabledState;
    }

    public void BeginStrengthHoverPreview(int delta)
    {
        if (delta != -1 && delta != 1)
            return;

        _strengthHoverDelta = delta;
        _currentHoveredButton = (delta < 0)
            ? HoverPreviewButtonType.StrengthDown
            : HoverPreviewButtonType.StrengthUp;

        gridPlacer.ClearBlockedRailHoverPreview();
        gridPlacer.PreviewBlockedRailsForStrengthHover(delta);

        RefreshStrengthHoverPreview();
    }

    public void EndStrengthHoverPreview()
    {
        _strengthHoverDelta = 0;

        if (placementCellOverlay != null)
            placementCellOverlay.ClearExternalPreview();

        if (gridPlacer != null)
            gridPlacer.ClearBlockedRailHoverPreview();

        RestoreLastStrengthVisualPreview();
    }

    void RefreshStrengthHoverPreview()
    {
        if (placementCellOverlay == null || gridPlacer == null)
            return;

        var po = gridPlacer.SelectedPO;
        if (po == null)
        {
            placementCellOverlay.ClearExternalPreview();
            return;
        }

        var strength = po.GetComponent<StrengthBasedOccupancyCells>();
        if (strength == null)
        {
            placementCellOverlay.ClearExternalPreview();
            return;
        }

        if (_strengthHoverDelta == 0)
        {
            placementCellOverlay.ClearExternalPreview();
            RestoreLastStrengthVisualPreview();
            ApplyStrengthButtonSprites(po, strength);
            return;
        }

        if (IsHoverPreviewBlocked())
        {
            placementCellOverlay.ClearExternalPreview();
            RestoreLastStrengthVisualPreview();
            ApplyStrengthButtonSprites(po, strength);
            return;
        }

        int current = strength.CurrentLevel;
        int target = Mathf.Clamp(current + _strengthHoverDelta, strength.MinLevel, strength.MaxLevel);

        if (target == current)
        {
            placementCellOverlay.ClearExternalPreview();
            RestoreLastStrengthVisualPreview();
            ApplyStrengthButtonSprites(po, strength);
            return;
        }

        ApplyStrengthButtonSprites(po, strength, hoveredDelta: _strengthHoverDelta);

        if (_lastPreviewPO != null && _lastPreviewPO != po)
            RestoreLastStrengthVisualPreview();

        strength.PreviewLevelActiveObjects(target);
        _lastPreviewPO = po;
        _lastPreviewStrength = strength;

        placementCellOverlay.ShowStrengthHoverPreview(po, strength, target);
    }

    void ApplyStrengthButtonSprites(PlacementObject po, StrengthBasedOccupancyCells strength, int hoveredDelta = 0)
    {
        bool canDownNow = gridPlacer != null && gridPlacer.CanDecreaseSelectedStrengthNow();
        bool canUpNow = gridPlacer != null && gridPlacer.CanIncreaseSelectedStrengthNow();

        if (strengthDownButton != null)
            strengthDownButton.interactable = canDownNow;

        if (strengthUpButton != null)
            strengthUpButton.interactable = canUpNow;
    }

    void RestoreLastStrengthVisualPreview()
    {
        if (_lastPreviewStrength != null)
            _lastPreviewStrength.RestoreCurrentLevelActiveObjects();

        _lastPreviewPO = null;
        _lastPreviewStrength = null;
    }

    public void BeginRotateHoverPreview(float deltaDegrees)
    {
        _currentHoveredButton = HoverPreviewButtonType.Rotate;

        if (IsHoverPreviewBlocked())
            return;

        if (gridPlacer == null) return;

        var po = gridPlacer.SelectedPO;
        if (po == null) return;

        if (gridPlacer.IsSelectedPORailBound())
        {
            _rotateHoverCachedCanExecute = false;
            gridPlacer.PreviewBlockedRailsForRotateHover();
            return;
        }

        // ✅ 호버 시작 시 1번만 계산
        _rotateHoverCachedCanExecute = gridPlacer.CanRotateSelectedNow(deltaDegrees);

        BeginTransformPreviewCommon(po);

        Quaternion previewRot =
            Quaternion.Euler(0f, 0f, Mathf.Repeat(po.transform.eulerAngles.z + deltaDegrees, 360f));

        po.transform.rotation = previewRot;
        Physics2D.SyncTransforms();

        // ✅ 불가능해도 버튼 상태는 캐시값 유지, 재검사는 안 함
        gridPlacer.ClearBlockedRailHoverPreview();
    }

    public void BeginFlipXHoverPreview()
    {
        _currentHoveredButton = HoverPreviewButtonType.FlipX;

        if (IsHoverPreviewBlocked())
            return;

        if (gridPlacer == null) return;

        var po = gridPlacer.SelectedPO;
        if (po == null) return;

        if (gridPlacer.IsSelectedPORailBound())
        {
            _flipXHoverCachedCanExecute = false;
            gridPlacer.PreviewBlockedRailsForFlipXHover();
            return;
        }

        // ✅ 호버 시작 시 1번만 계산
        _flipXHoverCachedCanExecute = gridPlacer.CanFlipSelectedXNow();

        BeginTransformPreviewCommon(po);

        Vector3 s = po.transform.localScale;
        s.x *= -1f;
        po.transform.localScale = s;
        Physics2D.SyncTransforms();

        // ✅ 불가능하면 버튼은 disabled 상태 유지, 레일 프리뷰는 필요 없으면 지움
        gridPlacer.ClearBlockedRailHoverPreview();
    }

    public void BeginFlipYHoverPreview()
    {
        _currentHoveredButton = HoverPreviewButtonType.FlipY;

        if (IsHoverPreviewBlocked())
            return;

        if (gridPlacer == null) return;

        var po = gridPlacer.SelectedPO;
        if (po == null) return;

        if (gridPlacer.IsSelectedPORailBound())
        {
            _flipYHoverCachedCanExecute = false;
            gridPlacer.PreviewBlockedRailsForFlipYHover();
            return;
        }

        // ✅ 호버 시작 시 1번만 계산
        _flipYHoverCachedCanExecute = gridPlacer.CanFlipSelectedYNow();

        BeginTransformPreviewCommon(po);

        Vector3 s = po.transform.localScale;
        s.y *= -1f;
        po.transform.localScale = s;
        Physics2D.SyncTransforms();

        // ✅ 불가능해도 버튼 상태는 캐시값 유지, 재검사는 안 함
        gridPlacer.ClearBlockedRailHoverPreview();
    }

    void BeginTransformPreviewCommon(PlacementObject po)
    {
        if (po == null) return;

        if (_transformPreviewPO != null && _transformPreviewPO != po)
            EndTransformHoverPreview();

        if (_isTransformHoverPreviewing && _transformPreviewPO == po)
            EndTransformHoverPreview();

        _transformPreviewPO = po;
        _transformPreviewOriginalRot = po.transform.rotation;
        _transformPreviewOriginalScale = po.transform.localScale;
        _isTransformHoverPreviewing = true;
    }

    public void EndTransformHoverPreview()
    {
        if (!_isTransformHoverPreviewing)
            return;

        if (_transformPreviewPO != null)
        {
            _transformPreviewPO.transform.rotation = _transformPreviewOriginalRot;
            _transformPreviewPO.transform.localScale = _transformPreviewOriginalScale;
            Physics2D.SyncTransforms();
        }

        if (gridPlacer != null)
            gridPlacer.ClearBlockedRailHoverPreview();

        _transformPreviewPO = null;
        _isTransformHoverPreviewing = false;
    }


    bool IsSelectedPODemoPlaying()
    {
        if (gridPlacer == null)
            return false;

        var po = gridPlacer.SelectedPO;
        if (po == null)
            return false;

        var demo = FindDemoLink(po);
        return demo != null && demo.IsDemoPlaying;
    }

    void HandleKeyboardShortcuts()
    {
        if (gridPlacer == null) return;
        if (panelRoot == null || !panelRoot.gameObject.activeInHierarchy) return;

        if (buttonsGroup == null) return;
        if (!CanUseKeyboardNow()) return;

        if (gridPlacer.SelectedPO == null) return;
        if (gridPlacer.IsDraggingSelectedPO) return;
        if (IsSelectedPODemoPlaying()) return;

        // 이미 키 하나를 누르고 있으면, 그 키를 뗄 때만 처리
        if (_activeKeyboardAction != KeyboardAction.None)
        {
            if (Input.GetKeyUp(_activeKeyboardKey))
            {
                KeyboardAction action = _activeKeyboardAction;

                ClearKeyboardActionPreview();
                ApplyKeyboardAction(action);
            }

            return;
        }

        if (keyConfig.GetKeyDown(keyConfig.demo))
            BeginKeyboardAction(KeyboardAction.Demo, keyConfig.ToKeyCode(keyConfig.demo));

        else if (keyConfig.GetKeyDown(keyConfig.selectedFlipX))
            BeginKeyboardAction(KeyboardAction.FlipX, keyConfig.ToKeyCode(keyConfig.selectedFlipX));

        else if (keyConfig.GetKeyDown(keyConfig.selectedStrengthDown))
            BeginKeyboardAction(KeyboardAction.StrengthDown, keyConfig.ToKeyCode(keyConfig.selectedStrengthDown));

        else if (keyConfig.GetKeyDown(keyConfig.selectedStrengthUp))
            BeginKeyboardAction(KeyboardAction.StrengthUp, keyConfig.ToKeyCode(keyConfig.selectedStrengthUp));

        else if (keyConfig.GetKeyDown(keyConfig.selectedDelete))
            BeginKeyboardAction(KeyboardAction.Delete, keyConfig.ToKeyCode(keyConfig.selectedDelete));
    }

    void BeginKeyboardAction(KeyboardAction action, KeyCode key)
    {
        // 프리뷰는 interactable이 false여도 실행되어야 함
        if (!CanPreviewKeyboardAction(action))
            return;

        _activeKeyboardAction = action;
        _activeKeyboardKey = key;

        BeginButtonHoldPreview(action);
    }

    bool CanPreviewKeyboardAction(KeyboardAction action)
    {
        switch (action)
        {
            case KeyboardAction.FlipX:
                return flipXButton != null &&
                       flipXButton.gameObject.activeSelf;

            case KeyboardAction.StrengthDown:
                return strengthDownButton != null &&
                       strengthDownButton.gameObject.activeSelf;

            case KeyboardAction.StrengthUp:
                return strengthUpButton != null &&
                       strengthUpButton.gameObject.activeSelf;

            case KeyboardAction.Demo:
                return demoButton != null &&
                       demoButton.gameObject.activeSelf;

            case KeyboardAction.Delete:
                return deleteButton != null &&
                       deleteButton.gameObject.activeSelf;
        }

        return false;
    }

    public void BeginButtonHoldPreview(KeyboardAction action)
    {
        switch (action)
        {
            case KeyboardAction.FlipX:
                if (flipXButton != null && flipXButton.gameObject.activeSelf)
                    BeginFlipXHoverPreview();
                break;

            case KeyboardAction.StrengthDown:
                if (strengthDownButton != null && strengthDownButton.gameObject.activeSelf)
                    BeginStrengthHoverPreview(-1);
                break;

            case KeyboardAction.StrengthUp:
                if (strengthUpButton != null && strengthUpButton.gameObject.activeSelf)
                    BeginStrengthHoverPreview(+1);
                break;
        }
    }



    void ApplyKeyboardAction(KeyboardAction action)
    {
        if (!CanUseKeyboardAction(action))
            return;

        PlayButtonSound(action);

        switch (action)
        {
            case KeyboardAction.Demo:
                OnClickDemo();
                break;

            case KeyboardAction.FlipX:
                OnClickFlipX();
                break;

            case KeyboardAction.StrengthDown:
                OnClickStrengthDown();
                break;

            case KeyboardAction.StrengthUp:
                OnClickStrengthUp();
                break;

            case KeyboardAction.Delete:
                OnClickDelete();
                break;
        }

    }

    bool CanUseKeyboardAction(KeyboardAction action)
    {
        switch (action)
        {
            case KeyboardAction.Demo:
                return demoButton != null &&
                       demoButton.gameObject.activeSelf &&
                       demoButton.interactable;

            case KeyboardAction.FlipX:
                return flipXButton != null &&
                       flipXButton.gameObject.activeSelf &&
                       flipXButton.interactable;

            case KeyboardAction.StrengthDown:
                return strengthDownButton != null &&
                       strengthDownButton.gameObject.activeSelf &&
                       strengthDownButton.interactable;

            case KeyboardAction.StrengthUp:
                return strengthUpButton != null &&
                       strengthUpButton.gameObject.activeSelf &&
                       strengthUpButton.interactable;

            case KeyboardAction.Delete:
                return deleteButton != null &&
                       deleteButton.gameObject.activeSelf &&
                       deleteButton.interactable;
        }

        return false;
    }

    void PlayButtonSound(KeyboardAction action)
    {
        Button button = null;

        switch (action)
        {
            case KeyboardAction.Demo:
                button = demoButton;
                break;

            case KeyboardAction.FlipX:
                button = flipXButton;
                break;

            case KeyboardAction.StrengthDown:
                button = strengthDownButton;
                break;

            case KeyboardAction.StrengthUp:
                button = strengthUpButton;
                break;

            case KeyboardAction.Delete:
                button = deleteButton;
                break;
        }

        if (button == null) return;

        var sound = button.GetComponent<UIButtonSound>();
        if (sound != null)
            sound.Play();
    }

    void ClearKeyboardActionPreview()
    {
        _activeKeyboardAction = KeyboardAction.None;
        _activeKeyboardKey = KeyCode.None;

        _currentHoveredButton = HoverPreviewButtonType.None;
        _strengthHoverDelta = 0;

        _flipXHoverCachedCanExecute = false;
        _flipYHoverCachedCanExecute = false;
        _rotateHoverCachedCanExecute = false;

        EndStrengthHoverPreview();
        EndTransformHoverPreview();

        if (placementCellOverlay != null)
            placementCellOverlay.ClearExternalPreview();

        RestoreLastStrengthVisualPreview();

        if (gridPlacer != null)
            gridPlacer.ClearBlockedRailHoverPreview();
    }

    void HideButtonsTemporarily()
    {
        float now = Time.unscaledTime;

        _buttonsHiddenUntil = now + actionButtonsHideDuration;
        _keyboardBlockedUntil = now + keyboardInputCooldown;

        if (buttonsGroup != null)
            buttonsGroup.SetActive(CanShowButtonsNow());
    }

    bool CanUseKeyboardNow()
    {
        return Time.unscaledTime >= _keyboardBlockedUntil;
    }

    bool CanShowButtonsNow()
    {
        return Time.unscaledTime >= _buttonsHiddenUntil;
    }

    void ClearAllPreviewState()
    {
        _currentHoveredButton = HoverPreviewButtonType.None;
        _strengthHoverDelta = 0;

        _flipXHoverCachedCanExecute = false;
        _flipYHoverCachedCanExecute = false;
        _rotateHoverCachedCanExecute = false;

        if (placementCellOverlay != null)
            placementCellOverlay.ClearExternalPreview();

        RestoreLastStrengthVisualPreview();
        EndTransformHoverPreview();

        if (gridPlacer != null)
            gridPlacer.ClearBlockedRailHoverPreview();
    }
}