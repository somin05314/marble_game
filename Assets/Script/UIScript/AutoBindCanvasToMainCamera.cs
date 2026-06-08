using UnityEngine;

[RequireComponent(typeof(Canvas))]
public class AutoBindCanvasToMainCamera : MonoBehaviour
{
    [SerializeField] bool setScreenSpaceCamera = true;
    [SerializeField] float planeDistance = 100f;

    Canvas _canvas;

    void Awake()
    {
        _canvas = GetComponent<Canvas>();
        BindCamera();
    }

    void Start()
    {
        // Awake 타이밍에 아직 카메라가 준비 안 됐을 수도 있어서 한 번 더
        BindCamera();
    }

    void BindCamera()
    {
        if (_canvas == null)
            return;

        Camera cam = Camera.main;
        if (cam == null)
        {
            Debug.LogWarning($"[AutoBindCanvasToMainCamera] MainCamera not found: {name}");
            return;
        }

        if (setScreenSpaceCamera)
            _canvas.renderMode = RenderMode.ScreenSpaceCamera;

        _canvas.worldCamera = cam;
        _canvas.planeDistance = planeDistance;
    }
}