using UnityEngine;
using UnityEngine.EventSystems;

public class RotateHoverPreviewTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] PlacementObjectActionButtonsPresenter presenter;
    [SerializeField] GridPlacer gridPlacer;
    [SerializeField] float rotateDeltaDegrees = -90f;

    void Awake()
    {
        if (gridPlacer == null)
            gridPlacer = FindFirstObjectByType<GridPlacer>();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (presenter == null) return;
        presenter.BeginRotateHoverPreview(rotateDeltaDegrees);
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