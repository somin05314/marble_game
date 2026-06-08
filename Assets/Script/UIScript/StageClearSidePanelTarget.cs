using UnityEngine;

public class StageClearSidePanelTarget : MonoBehaviour
{
    public enum Side
    {
        Left,
        Right
    }

    [Header("Target")]
    [SerializeField] Side side = Side.Left;
    public Side PanelSide => side;

    [Header("Optional")]
    [Tooltip("비워두면 자기 자신의 RectTransform을 사용")]
    [SerializeField] RectTransform targetRect;
    public RectTransform TargetRect
    {
        get
        {
            if (targetRect == null)
                targetRect = transform as RectTransform;
            return targetRect;
        }
    }

    [Tooltip("비워두면 부모/자기 자신에서 CanvasGroup 자동 탐색")]
    [SerializeField] CanvasGroup canvasGroup;
    public CanvasGroup TargetCanvasGroup
    {
        get
        {
            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = GetComponentInChildren<CanvasGroup>(true);
            return canvasGroup;
        }
    }
}