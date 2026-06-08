using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class HintButtonUI : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] StageHintController hintController;

    [Header("Buttons")]
    [SerializeField] Button nextHintButton;
    [SerializeField] Button toggleVisibleButton;

    [Header("Optional Labels")]
    [SerializeField] TMP_Text nextHintButtonLabel;
    [SerializeField] TMP_Text toggleVisibleButtonLabel;

    [Header("Hint Step Text")]
    [SerializeField] TMP_Text hintStepText;   // 예: 1 / 2

    [Header("Hint Visible Image Swap")]
    [SerializeField] Image toggleVisibleButtonImage;
    [SerializeField] Sprite hintShowSprite;
    [SerializeField] Sprite hintHideSprite;

    [Header("Optional Hint Root Visual")]
    [Tooltip("힌트 관련 UI 루트. 플레이 전환 시 숨기고 싶으면 넣어주세요.")]
    [SerializeField] GameObject hintVisualRoot;

    [Header("Tooltips")]
    [SerializeField] TooltipTrigger nextHintTooltipTrigger;
    [SerializeField] TooltipTrigger toggleVisibleTooltipTrigger;

    [SerializeField] string tooltipKeyHintNext = "tooltip_hint_next";
    [SerializeField] string tooltipKeyHintUnavailable = "tooltip_hint_unavailable";

    [SerializeField] string tooltipKeyHintShow = "tooltip_hint_show";
    [SerializeField] string tooltipKeyHintHide = "tooltip_hint_hide";

    bool _canGoNext;
    bool _canToggleVisible;

    bool isInPlayMode = false;

    void Awake()
    {
        if (hintController == null)
            hintController = FindObjectOfType<StageHintController>();

        if (nextHintButton != null)
            nextHintButton.onClick.AddListener(OnClickNextHint);

        if (toggleVisibleButton != null)
            toggleVisibleButton.onClick.AddListener(OnClickToggleVisible);

        if (nextHintTooltipTrigger == null && nextHintButton != null)
            nextHintTooltipTrigger = nextHintButton.GetComponent<TooltipTrigger>();

        if (toggleVisibleTooltipTrigger == null && toggleVisibleButton != null)
            toggleVisibleTooltipTrigger = toggleVisibleButton.GetComponent<TooltipTrigger>();

        RefreshUI();
    }

    void OnEnable()
    {
        GameModeManager.OnModeChanged += HandleModeChanged;

        if (GameModeManager.Instance != null)
            HandleModeChanged(GameModeManager.Instance.currentMode);
        else
            RefreshUI();
    }

    void OnDisable()
    {
        GameModeManager.OnModeChanged -= HandleModeChanged;
    }

    void HandleModeChanged(GameMode mode)
    {
        if (mode == GameMode.Play)
        {
            OnEnterPlayMode();
        }
        else if (mode == GameMode.Build)
        {
            OnEnterBuildMode();
        }
    }

    void OnDestroy()
    {
        if (nextHintButton != null)
            nextHintButton.onClick.RemoveListener(OnClickNextHint);

        if (toggleVisibleButton != null)
            toggleVisibleButton.onClick.RemoveListener(OnClickToggleVisible);
    }

    void OnClickNextHint()
    {
        if (!_canGoNext) return;
        if (hintController == null) return;

        hintController.NextHintStep();
        RefreshUI();
    }

    void OnClickToggleVisible()
    {
        if (!_canToggleVisible) return;
        if (hintController == null) return;

        hintController.ToggleHintVisible();
        RefreshUI();
    }

    public void RefreshUI()
    {
        bool hasController = hintController != null;
        bool hasHint = hasController && hintController.StepCount > 0;
        bool hasShownHintStep = hasController && hintController.CurrentStepIndex >= 0;

        bool canGoNext = !isInPlayMode &&
                         hasHint &&
                         hintController.CurrentStepIndex < hintController.StepCount - 1;

        bool canToggleVisible = !isInPlayMode &&
                                hasHint &&
                                hasShownHintStep;

        if (nextHintButtonLabel != null)
            nextHintButtonLabel.text = GetNextHintButtonText();

        if (toggleVisibleButtonLabel != null)
            toggleVisibleButtonLabel.text = GetVisibleToggleButtonText();

        if (hintStepText != null)
            hintStepText.text = GetHintStepText();

        _canGoNext = canGoNext;
        _canToggleVisible = canToggleVisible;

        if (toggleVisibleButton != null)
            toggleVisibleButton.interactable = true;

        if (nextHintButton != null)
            nextHintButton.interactable = true;

        RefreshVisibleButtonImage();
        RefreshTooltips();
    }

    void RefreshVisibleButtonImage()
    {
        if (toggleVisibleButtonImage == null || hintController == null)
            return;

        if (hintController.CurrentStepIndex < 0)
        {
            if (hintShowSprite != null)
                toggleVisibleButtonImage.sprite = hintShowSprite;
            return;
        }

        if (hintController.IsVisible)
        {
            if (hintHideSprite != null)
                toggleVisibleButtonImage.sprite = hintHideSprite;
        }
        else
        {
            if (hintShowSprite != null)
                toggleVisibleButtonImage.sprite = hintShowSprite;
        }
    }

    void RefreshTooltips()
    {
        if (isInPlayMode)
        {
            if (nextHintTooltipTrigger != null)
                nextHintTooltipTrigger.tooltipKey = "";

            if (toggleVisibleTooltipTrigger != null)
                toggleVisibleTooltipTrigger.tooltipKey = "";

            TooltipManager.I?.Cancel();

            return;
        }

        if (nextHintTooltipTrigger != null)
            nextHintTooltipTrigger.tooltipKey = GetNextHintTooltipKey();

        if (toggleVisibleTooltipTrigger != null)
            toggleVisibleTooltipTrigger.tooltipKey = GetToggleVisibleTooltipKey();

        RestartTooltipIfHovering(nextHintButton, nextHintTooltipTrigger);
        RestartTooltipIfHovering(toggleVisibleButton, toggleVisibleTooltipTrigger);
    }

    string GetNextHintButtonText()
    {
        if (hintController == null || hintController.StepCount <= 0)
            return "Hint";

        int nextIndex = hintController.CurrentStepIndex + 1;

        if (nextIndex >= hintController.StepCount)
            return "Hint Off";

        return $"Hint {nextIndex + 1}";
    }

    string GetVisibleToggleButtonText()
    {
        if (hintController == null || hintController.StepCount <= 0)
            return "Hide Hint";

        if (hintController.CurrentStepIndex < 0)
            return "Hide Hint";

        return hintController.IsVisible ? "Hide Hint" : "Show Hint";
    }

    string GetHintStepText()
    {
        if (hintController == null || hintController.StepCount <= 0)
            return "0 / 0";

        if (hintController.CurrentStepIndex < 0)
            return $"0 / {hintController.StepCount}";

        return $"{hintController.CurrentStepIndex + 1} / {hintController.StepCount}";
    }

    string GetNextHintTooltipKey()
    {
        if (isInPlayMode)
            return tooltipKeyHintUnavailable;

        if (hintController == null || hintController.StepCount <= 0)
            return tooltipKeyHintUnavailable;

        int nextIndex = hintController.CurrentStepIndex + 1;

        if (nextIndex >= hintController.StepCount)
            return tooltipKeyHintUnavailable;

        return tooltipKeyHintNext;
    }

    string GetToggleVisibleTooltipKey()
    {
        if (isInPlayMode)
            return tooltipKeyHintUnavailable;

        if (hintController == null || hintController.StepCount <= 0)
            return tooltipKeyHintUnavailable;

        // 아직 힌트를 한 번도 안 봤어도
        // 보기/숨기기 버튼은 "힌트 숨기기" 툴팁으로 보여준다
        if (hintController.CurrentStepIndex < 0)
            return tooltipKeyHintHide;

        return hintController.IsVisible ? tooltipKeyHintHide : tooltipKeyHintShow;
    }

    void RestartTooltipIfHovering(Button button, TooltipTrigger trigger)
    {
        if (button == null || trigger == null)
            return;

        if (!IsPointerOverButton(button))
            return;

        TooltipManager.I?.RestartShowKey(
            trigger.tooltipKey,
            trigger.xDir,
            trigger.yDir
        );
    }

    bool IsPointerOverButton(Button button)
    {
        if (button == null)
            return false;

        var rt = button.transform as RectTransform;
        if (rt == null)
            return false;

        Canvas canvas = button.GetComponentInParent<Canvas>();
        Camera eventCam = null;

        if (canvas != null &&
            (canvas.renderMode == RenderMode.ScreenSpaceCamera || canvas.renderMode == RenderMode.WorldSpace))
        {
            eventCam = canvas.worldCamera;
        }

        return RectTransformUtility.RectangleContainsScreenPoint(rt, Input.mousePosition, eventCam);
    }

    public void OnEnterPlayMode()
    {
        isInPlayMode = true;

        if (hintController != null)
            hintController.HideHints();

        if (hintVisualRoot != null)
            hintVisualRoot.SetActive(false);

        TooltipManager.I?.Cancel();

        RefreshUI();
    }

    public void OnEnterBuildMode()
    {
        isInPlayMode = false;

        if (hintController != null)
            hintController.ShowHints();

        if (hintVisualRoot != null)
            hintVisualRoot.SetActive(true);

        RefreshUI();
    }
}