using UnityEngine;
using UnityEngine.UI;

public class RailDeleteButtonPresenter : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] RailToolPlacer2D railTool;
    [SerializeField] Canvas canvas;
    [SerializeField] Button deleteButton;

    [Header("Follow")]
    [SerializeField] Camera worldCamera;

    [SerializeField] RectTransform visualPanel;

    [Header("World Offset")]
    [SerializeField] GridManager grid;
    [SerializeField] float cellSize = 1f;
    [SerializeField] float aboveCellCount = 3f;
    [SerializeField] float fallbackCellSize = 1f;

    [Header("Scale")]
    [SerializeField] bool scaleWithZoom = true;
    [SerializeField] float referenceOrthoSize = 5f;
    [SerializeField] float minScale = 0.6f;
    [SerializeField] float maxScale = 1.6f;

    [SerializeField] GameKeyBindingConfig keyConfig;

    RectTransform _btnRt;
    RectTransform _canvasRt;
    bool _hasSelection;

    bool _isDeleteKeyHolding;

    void Awake()
    {
        if (worldCamera == null) worldCamera = Camera.main;

        _btnRt = GetComponent<RectTransform>();
        _canvasRt = canvas.GetComponent<RectTransform>();

        deleteButton.onClick.AddListener(OnClickDelete);
        deleteButton.gameObject.SetActive(false);
    }

    void OnEnable()
    {
        if (railTool != null)
            railTool.SelectedRailChanged += OnSelectedRailChanged;

        OnSelectedRailChanged(railTool != null ? railTool.SelectedRail : null);
    }

    void OnDisable()
    {
        if (railTool != null)
            railTool.SelectedRailChanged -= OnSelectedRailChanged;

        _isDeleteKeyHolding = false;
    }

    void Update()
    {
        if (!_hasSelection || railTool == null) return;

        var rail = railTool.SelectedRail;
        if (rail == null) { Hide(); return; }

        HandleKeyboardDelete();

        if (railTool.SelectedRail == null)
            return;

        float cellSize = fallbackCellSize;

        Vector3 worldPos = rail.transform.position + Vector3.up * (cellSize * aboveCellCount);

        Vector3 sp = worldCamera.WorldToScreenPoint(worldPos);
        if (sp.z < 0f) { Hide(); return; }

        Camera eventCam = null;
        if (canvas.renderMode == RenderMode.ScreenSpaceCamera || canvas.renderMode == RenderMode.WorldSpace)
            eventCam = (canvas.worldCamera != null) ? canvas.worldCamera : worldCamera;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRt, sp, eventCam, out var localPos))
        {
            Vector2 correction = GetPanelEdgeCorrectionAbove();
            _btnRt.anchoredPosition = localPos + correction;
        }

        if (scaleWithZoom && worldCamera != null && worldCamera.orthographic)
        {
            float s = referenceOrthoSize / Mathf.Max(0.0001f, worldCamera.orthographicSize);
            s = Mathf.Clamp(s, minScale, maxScale);
            _btnRt.localScale = Vector3.one * s;
        }
        else
        {
            _btnRt.localScale = Vector3.one;
        }
    }

    void HandleKeyboardDelete()
    {
        if (deleteButton == null || !deleteButton.gameObject.activeSelf || !deleteButton.interactable)
            return;

        if (keyConfig != null ? keyConfig.GetKeyDown(keyConfig.railDelete) : Input.GetKeyDown(KeyCode.B))
        {
            _isDeleteKeyHolding = true;
        }

        if (_isDeleteKeyHolding && (keyConfig != null ? keyConfig.GetKeyUp(keyConfig.railDelete) : Input.GetKeyUp(KeyCode.B)))
        {
            _isDeleteKeyHolding = false;
            OnClickDelete();
        }
    }

    Vector2 GetPanelEdgeCorrectionAbove()
    {
        if (_btnRt == null)
            return Vector2.zero;

        RectTransform rt = visualPanel != null ? visualPanel : _btnRt;

        Bounds b = RectTransformUtility.CalculateRelativeRectTransformBounds(_btnRt, rt);

        float scaleY = _btnRt.localScale.y;

        return Vector2.up * (-b.min.y * scaleY);
    }

    void OnSelectedRailChanged(RailSpan2D rail)
    {
        _hasSelection = (rail != null);
        _isDeleteKeyHolding = false;

        deleteButton.gameObject.SetActive(_hasSelection);
    }

    void OnClickDelete()
    {
        if (railTool == null) return;
        railTool.DeleteSelectedRail();

        _isDeleteKeyHolding = false;
    }

    void Hide()
    {
        _hasSelection = false;
        _isDeleteKeyHolding = false;

        deleteButton.gameObject.SetActive(false);
    }
}