using UnityEngine;
using UnityEngine.UI;

public class StageUndoRedoUI : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] StageUndoHistoryManager historyManager;

    [Header("Optional UI")]
    [SerializeField] Button undoButton;
    [SerializeField] Button redoButton;

    [Header("Tooltip")]
    [SerializeField] TooltipTrigger undoTooltipTrigger;
    [SerializeField] TooltipTrigger redoTooltipTrigger;

    [SerializeField] string tooltipKeyEnabledUndo = "tooltip_undo";
    [SerializeField] string tooltipKeyDisabledUndo = "tooltip_undo_disabled";
    [SerializeField] string tooltipKeyEnabledRedo = "tooltip_redo";
    [SerializeField] string tooltipKeyDisabledRedo = "tooltip_redo_disabled";

    [Header("Button Visual")]
    [SerializeField] Graphic undoTargetGraphic;
    [SerializeField] Graphic redoTargetGraphic;
    [SerializeField] Color enabledColor = Color.white;
    [SerializeField] Color disabledColor = new Color(1f, 1f, 1f, 0.45f);

    bool _isBuildMode = true;

    string _lastUndoTooltipKey;
    string _lastRedoTooltipKey;

    void Awake()
    {
        if (historyManager == null)
            historyManager = FindFirstObjectByType<StageUndoHistoryManager>();

        if (undoTooltipTrigger == null && undoButton != null)
            undoTooltipTrigger = undoButton.GetComponent<TooltipTrigger>();

        if (redoTooltipTrigger == null && redoButton != null)
            redoTooltipTrigger = redoButton.GetComponent<TooltipTrigger>();

        if (undoTargetGraphic == null && undoButton != null)
            undoTargetGraphic = undoButton.targetGraphic;

        if (redoTargetGraphic == null && redoButton != null)
            redoTargetGraphic = redoButton.targetGraphic;

        var gmm = GameModeManager.Instance;
        _isBuildMode = (gmm == null || gmm.currentMode == GameMode.Build);
    }

    void OnEnable()
    {
        GameModeManager.OnModeChanged += HandleModeChanged;

        var gmm = GameModeManager.Instance;
        _isBuildMode = (gmm == null || gmm.currentMode == GameMode.Build);

        RefreshUI();
    }

    void OnDisable()
    {
        GameModeManager.OnModeChanged -= HandleModeChanged;
    }

    void Update()
    {
        RefreshUI();
    }

    void HandleModeChanged(GameMode mode)
    {
        _isBuildMode = (mode == GameMode.Build);
        RefreshUI();
    }

    void RefreshUI()
    {
        bool canUndo = _isBuildMode && historyManager != null && historyManager.CanUndo;
        bool canRedo = _isBuildMode && historyManager != null && historyManager.CanRedo;

        RefreshSingleButton(
            undoButton,
            undoTargetGraphic,
            undoTooltipTrigger,
            canUndo,
            tooltipKeyEnabledUndo,
            tooltipKeyDisabledUndo,
            ref _lastUndoTooltipKey
        );

        RefreshSingleButton(
            redoButton,
            redoTargetGraphic,
            redoTooltipTrigger,
            canRedo,
            tooltipKeyEnabledRedo,
            tooltipKeyDisabledRedo,
            ref _lastRedoTooltipKey
        );
    }

    void RefreshSingleButton(
        Button button,
        Graphic targetGraphic,
        TooltipTrigger tooltipTrigger,
        bool interactable,
        string enabledTooltipKey,
        string disabledTooltipKey,
        ref string lastTooltipKey)
    {
        if (button != null)
            button.interactable = interactable;

        if (targetGraphic != null)
            targetGraphic.color = interactable ? enabledColor : disabledColor;

        if (tooltipTrigger == null)
            return;

        string newKey = interactable ? enabledTooltipKey : disabledTooltipKey;
        tooltipTrigger.tooltipKey = newKey;

        if (IsPointerOverButton(button) && newKey != lastTooltipKey)
        {
            TooltipManager.I?.RestartShowKey(
                newKey,
                tooltipTrigger.xDir,
                tooltipTrigger.yDir
            );
        }

        lastTooltipKey = newKey;
    }

    public void ExecuteUndo()
    {
        if (!_isBuildMode) return;
        if (historyManager == null) return;
        if (!historyManager.CanUndo) return;

        historyManager.Undo();
        RefreshUI();
    }

    public void ExecuteRedo()
    {
        if (!_isBuildMode) return;
        if (historyManager == null) return;
        if (!historyManager.CanRedo) return;

        historyManager.Redo();
        RefreshUI();
    }

    bool IsPointerOverButton(Button button)
    {
        if (button == null) return false;

        var rt = button.transform as RectTransform;
        if (rt == null) return false;

        Canvas canvas = button.GetComponentInParent<Canvas>();
        Camera eventCam = null;

        if (canvas != null &&
            (canvas.renderMode == RenderMode.ScreenSpaceCamera || canvas.renderMode == RenderMode.WorldSpace))
        {
            eventCam = canvas.worldCamera;
        }

        return RectTransformUtility.RectangleContainsScreenPoint(rt, Input.mousePosition, eventCam);
    }
}