using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 옵션 UI에서 레일 연속 설치 옵션을 제어.
/// - OnEnable: UI Sync + 이벤트 구독
/// - Start: 저장된 현재 옵션을 실제 씬에 적용
/// - Button 클릭: 옵션을 토글하고 실제 씬에 적용
/// </summary>
public class RailContinuousOptionUI : MonoBehaviour
{
    [Header("Optional UI")]
    public Button button;

    [Header("Tooltip")]
    [SerializeField] TooltipTrigger tooltipTrigger;
    [SerializeField] string tooltipKeyOn = "tooltip_rail_continue_on";
    [SerializeField] string tooltipKeyOff = "tooltip_rail_continue_off";

    [Header("Button Visual")]
    [SerializeField] Graphic targetGraphic;
    [SerializeField] Color onColor = Color.white;
    [SerializeField] Color offColor = new Color(1f, 1f, 1f, 0.45f);

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
    }

    void OnEnable()
    {
        SyncUI(RailBuildOptions.ContinuousPlacement);
        RailBuildOptions.OnContinuousChanged += SyncUI;
    }

    void Start()
    {
        OptionsApplier.TryApplyAll();
    }

    void OnDisable()
    {
        RailBuildOptions.OnContinuousChanged -= SyncUI;
    }

    void SyncUI(bool on)
    {
        if (targetGraphic != null)
            targetGraphic.color = on ? onColor : offColor;

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

    // Button의 OnClick()에 연결
    public void ToggleContinuous()
    {
        RailBuildOptions.ContinuousPlacement = !RailBuildOptions.ContinuousPlacement;
        OptionsApplier.TryApplyAll();
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