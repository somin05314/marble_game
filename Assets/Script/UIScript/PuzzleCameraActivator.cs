using UnityEngine;

[RequireComponent(typeof(Camera))]
public class PuzzleCameraActivator : MonoBehaviour
{
    Camera _cam;

    void Awake()
    {
        _cam = GetComponent<Camera>();
    }

    void OnEnable()
    {
        if (UICameraRouter.I != null)
            UICameraRouter.I.SetCurrentCamera(_cam);
    }

    void Start()
    {
        if (UICameraRouter.I != null)
            UICameraRouter.I.SetCurrentCamera(_cam);
    }

    void OnDisable()
    {
        if (UICameraRouter.I != null && UICameraRouter.I.CurrentCamera == _cam)
            UICameraRouter.I.UseCoreCamera();
    }

    void OnDestroy()
    {
        if (UICameraRouter.I != null && UICameraRouter.I.CurrentCamera == _cam)
            UICameraRouter.I.UseCoreCamera();
    }
}