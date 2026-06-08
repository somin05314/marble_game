using UnityEngine;
using UnityEngine.EventSystems;

public class CursorStateDriver : MonoBehaviour
{
    #region Inspector

    [Header("Auto Camera")]
    [SerializeField] Camera cam;
    [SerializeField] float cameraRetryInterval = 0.25f;

    [Header("PO Hover")]
    [SerializeField] LayerMask poMask;
    [SerializeField] float poPickRadiusPx = 10f;

    [Header("Rail Span Hover")]
    [SerializeField] LayerMask railSpanMask;
    [SerializeField] float railSpanPickRadiusPx = 10f;

    [Header("UI Block")]
    [Tooltip("마우스가 UI 위에 있으면 월드 오브젝트 hover 판정을 막음")]
    [SerializeField] bool blockWorldHoverWhenPointerOverUI = true;

    [Header("Drag Lock")]
    [Tooltip("PO 드래그 중에는 커서/힌트를 고정합니다.")]
    [SerializeField] bool lockCursorWhileDragging = true;

    [Header("PO Drag")]
    [SerializeField] GridPlacer gridPlacer;

    #endregion

    #region Constants

    const string KEY_PLACE_CONFIRM_CANCEL = "hint_place_confirm_cancel";
    const string KEY_RAIL_PLACE = "hint_rail_place";
    const string KEY_RAIL_DRAG = "hint_rail_drag";
    const string KEY_RAIL_PLACE_AND_DRAG = "hint_rail_place_and_drag";
    const string KEY_PO_SELECT_AND_MOVE = "hint_po_select_and_move";
    const string KEY_RAIL_SELECT = "hint_rail_select";
    const string KEY_PO_DRAG_CANCEL = "hint_po_drag_cancel";

    const string KEY_RAIL_LIMIT = "hint_rail_limit";

    #endregion

    #region Runtime

    float _nextCamRetryTime;
    readonly Collider2D[] _hits = new Collider2D[32];

    struct CursorDisplayInfo
    {
        public CursorManager.CursorState cursor;
        public string hint;

        public CursorDisplayInfo(CursorManager.CursorState cursor, string hint)
        {
            this.cursor = cursor;
            this.hint = hint;
        }
    }

    #endregion

    #region Unity

    void Awake()
    {
        Physics2D.queriesHitTriggers = true;

        if (gridPlacer == null)
            gridPlacer = FindFirstObjectByType<GridPlacer>();
    }

    void Update()
    {
        EnsureCamera();

        CursorDisplayInfo display = EvaluateDisplay();
        ApplyDisplay(display);
    }

    #endregion

    #region Main Flow

    CursorDisplayInfo EvaluateDisplay()
    {
        if (cam == null)
            return DefaultDisplay();

        // 1. 설치 프리뷰
        if (GridPlacer.IsPlacementPreviewActive)
        {
            bool hasStrengthControl =
                gridPlacer != null &&
                gridPlacer.placementData != null &&
                gridPlacer.placementData.allowStrengthControl;

            string guideKey = hasStrengthControl
                ? "hint_po_place_guide_with_strength"
                : "hint_po_place_guide_no_strength";

            PlacementGuideUI.I?.SetGuide(Localize(guideKey));

            return new CursorDisplayInfo(
                CursorManager.CursorState.Cross,
                Localize(KEY_PLACE_CONFIRM_CANCEL));
        }

        if (RailToolPlacer2D.IsPlacementPreviewActive)
        {
            PlacementGuideUI.I?.SetGuide(null);

            return new CursorDisplayInfo(
                CursorManager.CursorState.Cross,
                Localize(KEY_PLACE_CONFIRM_CANCEL));
        }

        PlacementGuideUI.I?.SetGuide(null);

        // 🔥 2. 드래그 잠금 (최우선)
        if (lockCursorWhileDragging)
        {
            if (IsRailDraggingNow())
            {
                return new CursorDisplayInfo(
                    CursorManager.CursorState.Hand,
                    Localize(KEY_RAIL_DRAG));
            }

            if (IsPODraggingNow())
            {
                return new CursorDisplayInfo(
                    CursorManager.CursorState.Hand,
                    Localize(KEY_PO_DRAG_CANCEL));
            }
        }

        // 3. UI 위
        if (blockWorldHoverWhenPointerOverUI && IsPointerOverUI())
            return DefaultDisplay();

        // 4. hover
        if (TryGetWorldHoverDisplay(out var hoverDisplay))
            return hoverDisplay;

        return DefaultDisplay();
    }

    bool IsRailDraggingNow()
    {
        // 실제 마우스 왼쪽 버튼을 누르고 있을 때만
        // "드래그 중" 힌트로 취급
        if (!Input.GetMouseButton(0))
            return false;

        // 레일 설치 프리뷰 중이면 위쪽 로직에서
        // hint_place_confirm_cancel을 띄워야 하므로 제외
        if (RailToolPlacer2D.IsPlacementPreviewActive)
            return false;

        return RailToolPlacer2D.IsInputBusyNow;
    }
    void ApplyDisplay(CursorDisplayInfo display)
    {
        CursorManager.I?.Set(display.cursor);
        InteractionHintUI.I?.SetHint(display.hint);
    }

    CursorDisplayInfo DefaultDisplay()
    {
        return new CursorDisplayInfo(CursorManager.CursorState.Default, null);
    }

    #endregion

    #region Hover Evaluation

    bool TryGetWorldHoverDisplay(out CursorDisplayInfo display)
    {
        display = DefaultDisplay();

        if (cam == null)
            return false;

        Vector2 mouseWorld = cam.ScreenToWorldPoint(Input.mousePosition);

        // 1. 레일 툴 우선
        if (TryGetRailHoverDisplay(mouseWorld, out display))
            return true;

        // 2. PO hover
        if (IsOverSelectablePO())
        {
            display = new CursorDisplayInfo(
                CursorManager.CursorState.Hand,
                Localize(KEY_PO_SELECT_AND_MOVE));
            return true;
        }

        // 3. RailSpan hover
        if (IsOverWorldCircle(railSpanMask, railSpanPickRadiusPx))
        {
            display = new CursorDisplayInfo(
                CursorManager.CursorState.Hand,
                Localize(KEY_RAIL_SELECT));
            return true;
        }

        return false;
    }

    bool IsOverSelectablePO()
    {
        if (cam == null)
            return false;

        Vector2 mouseWorld = cam.ScreenToWorldPoint(Input.mousePosition);

        float worldPerPixel = (cam.orthographicSize * 2f) / Screen.height;
        float radiusWorld = worldPerPixel * poPickRadiusPx;

        var filter = new ContactFilter2D();
        filter.SetLayerMask(poMask);
        filter.useTriggers = true;

        int count = Physics2D.OverlapCircle(mouseWorld, radiusWorld, filter, _hits);

        for (int i = 0; i < count; i++)
        {
            var c = _hits[i];
            if (c == null) continue;

            var po = c.GetComponentInParent<PlacementObject>();
            if (po == null) continue;

            // 선택 불가 PO는 완전히 무시하고 다음 hit 검사
            if (!po.Selectable)
                continue;

            return true;
        }

        return false;
    }

    bool TryGetRailHoverDisplay(Vector2 mouseWorld, out CursorDisplayInfo display)
    {
        display = DefaultDisplay();

        var railTool = RailToolPlacer2D.Instance;
        if (railTool == null)
            return false;

        RailHoverAction action = railTool.GetHoverActionAtMouse(mouseWorld);

        bool canPlace = (action & RailHoverAction.CanPlace) != 0;
        bool canDrag = (action & RailHoverAction.CanDrag) != 0;
        bool noRailBudget = (action & RailHoverAction.NoRailBudget) != 0;

        if (!canPlace && !canDrag && !noRailBudget)
            return false;

        CursorManager.CursorState cursor =
            canPlace ? CursorManager.CursorState.Cross : CursorManager.CursorState.Hand;

        string hint;

        if (noRailBudget && canDrag)
        {
            hint = Localize(KEY_RAIL_LIMIT) + " / " + Localize(KEY_RAIL_DRAG);
        }
        else if (noRailBudget)
        {
            hint = Localize(KEY_RAIL_LIMIT);
        }
        else if (canPlace && canDrag)
        {
            hint = Localize(KEY_RAIL_PLACE_AND_DRAG);
        }
        else if (canPlace)
        {
            hint = Localize(KEY_RAIL_PLACE);
        }
        else
        {
            hint = Localize(KEY_RAIL_DRAG);
        }

        display = new CursorDisplayInfo(cursor, hint);
        return true;
    }

    #endregion

    #region Helpers

    string Localize(string key)
    {
        if (LocalizationManager.I != null &&
            LocalizationManager.I.TryGet(key, out var text) &&
            !string.IsNullOrEmpty(text))
        {
            return text;
        }

        return $"[{key}]";
    }

    bool IsPointerOverUI()
    {
        if (EventSystem.current == null)
            return false;

        return EventSystem.current.IsPointerOverGameObject();
    }

    bool IsPODraggingNow()
    {
        if (gridPlacer == null)
            gridPlacer = FindFirstObjectByType<GridPlacer>();

        if (gridPlacer == null)
            return false;

        return gridPlacer.IsDraggingSelectedPO || gridPlacer.IsDragCandidateSelectedPO;
    }

    bool IsOverWorldCircle(LayerMask mask, float radiusPx)
    {
        if (cam == null)
            return false;

        Vector2 mouseWorld = cam.ScreenToWorldPoint(Input.mousePosition);

        float worldPerPixel = (cam.orthographicSize * 2f) / Screen.height;
        float radiusWorld = worldPerPixel * radiusPx;

        var filter = new ContactFilter2D();
        filter.SetLayerMask(mask);
        filter.useTriggers = true;

        int count = Physics2D.OverlapCircle(mouseWorld, radiusWorld, filter, _hits);
        return count > 0;
    }

    void EnsureCamera()
    {
        if (cam != null && cam.isActiveAndEnabled)
            return;

        if (Time.unscaledTime < _nextCamRetryTime)
            return;

        _nextCamRetryTime = Time.unscaledTime + cameraRetryInterval;

        cam = Camera.main;
        if (cam != null && cam.isActiveAndEnabled)
            return;

        cam = FindBestCamera();
    }

    Camera FindBestCamera()
    {
        Camera[] cams = GameObject.FindObjectsOfType<Camera>(false);

        Camera best = null;
        float bestDepth = float.NegativeInfinity;

        for (int i = 0; i < cams.Length; i++)
        {
            Camera c = cams[i];
            if (c == null || !c.isActiveAndEnabled)
                continue;

            if (best == null || c.depth > bestDepth)
            {
                best = c;
                bestDepth = c.depth;
            }
        }

        return best;
    }

    #endregion
}