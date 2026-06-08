using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CoreCameraRegister : MonoBehaviour
{
    Camera _cam;

    void Awake()
    {
        _cam = GetComponent<Camera>();

        if (UICameraRouter.I != null)
            UICameraRouter.I.SetCoreCamera(_cam);
    }

    void Start()
    {
        if (UICameraRouter.I != null)
        {
            UICameraRouter.I.SetCoreCamera(_cam);

            // 현재 기준이 없으면 core 사용
            if (UICameraRouter.I.CurrentCamera == null)
                UICameraRouter.I.UseCoreCamera();
        }
    }
}