using UnityEngine;
using UnityEngine.EventSystems;

public class TooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public enum XDir { Left, Right }
    public enum YDir { Up, Down }

    [Header("Localization Key")]
    public string tooltipKey;

    [Header("Direction")]
    public XDir xDir = XDir.Right;
    public YDir yDir = YDir.Down;

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (string.IsNullOrEmpty(tooltipKey)) return;

        TooltipManager.I?.RequestShowKey(tooltipKey, xDir, yDir);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        TooltipManager.I?.Cancel();
    }

    void OnDisable()
    {
        TooltipManager.I?.Cancel();
    }
}