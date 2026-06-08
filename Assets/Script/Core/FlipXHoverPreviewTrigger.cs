using UnityEngine;
using UnityEngine.EventSystems;

public class FlipXHoverPreviewTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] PlacementObjectActionButtonsPresenter presenter;
    [SerializeField] GridPlacer gridPlacer;

    void Awake()
    {
        if (gridPlacer == null)
            gridPlacer = FindFirstObjectByType<GridPlacer>();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (presenter == null) return;

        presenter.BeginButtonHoldPreview(
            PlacementObjectActionButtonsPresenter.KeyboardAction.FlipX
        );
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (presenter == null) return;
        presenter.NotifyHoverExit();
    }

    void OnDisable()
    {
        if (presenter != null)
            presenter.EndTransformHoverPreview();
    }
}