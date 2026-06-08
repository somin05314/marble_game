using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 옵션 UI에서 그리드 표시 옵션을 제어.
/// - OnEnable: UI Sync + 이벤트 구독
/// - Start: 저장된 현재 옵션을 실제 씬에 적용
/// - Button 클릭: 옵션을 토글하고 실제 씬에 적용
/// </summary>
public class GridViewOptionUI : MonoBehaviour
{
    [Header("Optional UI")]
    public Button button;

    [Header("Tooltip")]
    [SerializeField] TooltipTrigger tooltipTrigger;
    [SerializeField] string tooltipKeyOn = "tooltip_grid_on";
    [SerializeField] string tooltipKeyOff = "tooltip_grid_off";

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
        GridViewOptions.EnsureLoaded();
        SyncUI(GridViewOptions.GridVisible);
        GridViewOptions.OnGridVisibleChanged += SyncUI;
    }

    void Start()
    {
        GridViewOptions.ApplyToSceneGridRenderer();
    }

    void OnDisable()
    {
        GridViewOptions.OnGridVisibleChanged -= SyncUI;
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
    public void ToggleGridView()
    {
        GridViewOptions.GridVisible = !GridViewOptions.GridVisible;
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