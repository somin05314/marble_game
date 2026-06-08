using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIHideOptionUI : MonoBehaviour
{
    [Header("Hide Target")]
    [SerializeField] GameObject uiRoot;
    [SerializeField] CanvasGroup uiCanvasGroup;

    [Header("Optional UI")]
    public Button button;

    [Header("Tooltip")]
    [SerializeField] TooltipTrigger tooltipTrigger;
    [SerializeField] string tooltipKeyOn = "tooltip_ui_visible";
    [SerializeField] string tooltipKeyOff = "tooltip_ui_hidden";

    [Header("Button Visual")]
    [SerializeField] Graphic targetGraphic;
    [SerializeField] Color onColor = Color.white;
    [SerializeField] Color offColor = new Color(1f, 1f, 1f, 0.45f);

    [Header("Optional Text")]
    [SerializeField] TMP_Text labelText;
    [SerializeField] string labelOn = "UI ON";
    [SerializeField] string labelOff = "UI OFF";

    bool isVisible = true;
    public bool IsVisible => isVisible;

    void Awake()
    {
        if (tooltipTrigger == null)
            tooltipTrigger = GetComponent<TooltipTrigger>();

        if (tooltipTrigger == null && button != null)
            tooltipTrigger = button.GetComponent<TooltipTrigger>();

        if (targetGraphic == null)
        {
            if (button != null)
                targetGraphic = button.targetGraphic;

            if (targetGraphic == null)
                targetGraphic = GetComponent<Graphic>();
        }

        if (labelText == null)
            labelText = GetComponentInChildren<TMP_Text>(true);

        if (uiCanvasGroup == null && uiRoot != null)
            uiCanvasGroup = uiRoot.GetComponent<CanvasGroup>();

        if (uiCanvasGroup == null && uiRoot != null)
            uiCanvasGroup = uiRoot.AddComponent<CanvasGroup>();
    }

    void OnEnable()
    {
        if (uiCanvasGroup != null)
            isVisible = uiCanvasGroup.alpha > 0.001f;
        else
            isVisible = (uiRoot == null) ? true : uiRoot.activeSelf;

        SyncUI(isVisible);
        ApplyVisibility(isVisible);
    }

    void SyncUI(bool on)
    {
        if (targetGraphic != null)
            targetGraphic.color = on ? onColor : offColor;

        if (labelText != null)
            labelText.text = on ? labelOn : labelOff;

        if (tooltipTrigger != null)
        {
            tooltipTrigger.tooltipKey = on ? tooltipKeyOn : tooltipKeyOff;

            if (IsPointerOverButton())
            {
                TooltipManager.I?.RestartShowKey(
                    tooltipTrigger.tooltipKey,
                    tooltipTrigger.xDir,
                    tooltipTrigger.yDir
                );
            }
        }
    }

    void ApplyVisibility(bool visible)
    {
        if (uiCanvasGroup != null)
        {
            uiCanvasGroup.alpha = visible ? 1f : 0f;
            uiCanvasGroup.interactable = visible;
            uiCanvasGroup.blocksRaycasts = visible;
            return;
        }

        // CanvasGroup이 없는 경우 fallback
        if (uiRoot != null)
            uiRoot.SetActive(visible);
    }

    public void ToggleUIVisibility()
    {
        isVisible = !isVisible;
        ApplyVisibility(isVisible);
        SyncUI(isVisible);
    }

    public void ShowUI()
    {
        isVisible = true;
        ApplyVisibility(true);
        SyncUI(true);
    }

    public void HideUI()
    {
        isVisible = false;
        ApplyVisibility(false);
        SyncUI(false);
    }

    bool IsPointerOverButton()
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
}