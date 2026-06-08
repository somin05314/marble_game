using UnityEngine;

[RequireComponent(typeof(Canvas))]
public class AutoBindCanvasToActiveCamera : MonoBehaviour
{
    [SerializeField] bool forceScreenSpaceCamera = true;
    [SerializeField] float planeDistance = 100f;

    Canvas _canvas;
    Camera _lastBound;

    void Awake()
    {
        _canvas = GetComponent<Canvas>();
    }

    void Start()
    {
        ApplyCamera();
    }

    void LateUpdate()
    {
        ApplyCamera();
    }

    void ApplyCamera()
    {
        if (_canvas == null) return;
        if (UICameraRouter.I == null) return;

        Camera target = UICameraRouter.I.CurrentCamera;
        if (target == null) return;

        if (_lastBound == target && _canvas.worldCamera == target)
            return;

        if (forceScreenSpaceCamera)
            _canvas.renderMode = RenderMode.ScreenSpaceCamera;

        _canvas.worldCamera = target;
        _canvas.planeDistance = planeDistance;
        _lastBound = target;
    }
}