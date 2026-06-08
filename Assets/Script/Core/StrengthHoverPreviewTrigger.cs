using UnityEngine;
using UnityEngine.EventSystems;

public class StrengthHoverPreviewTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public enum HoverType
    {
        Increase,
        Decrease
    }

    [SerializeField] PlacementObjectActionButtonsPresenter presenter;
    [SerializeField] GridPlacer gridPlacer;
    [SerializeField] HoverType hoverType;

    void Awake()
    {
        if (gridPlacer == null)
            gridPlacer = FindFirstObjectByType<GridPlacer>();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (presenter == null) return;

        if (hoverType == HoverType.Increase)
        {
            presenter.BeginButtonHoldPreview(
                PlacementObjectActionButtonsPresenter.KeyboardAction.StrengthUp
            );
        }
        else
        {
            presenter.BeginButtonHoldPreview(
                PlacementObjectActionButtonsPresenter.KeyboardAction.StrengthDown
            );
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (presenter == null) return;
        presenter.NotifyHoverExit();
    }

    void OnDisable()
    {
        if (presenter != null)
            presenter.EndStrengthHoverPreview();
    }
}