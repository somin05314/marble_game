using UnityEngine;

public class UICameraRouter : MonoBehaviour
{
    public static UICameraRouter I { get; private set; }

    [Header("Default / Core")]
    [SerializeField] Camera coreCamera;

    [Header("Runtime Current")]
    [SerializeField] Camera currentCamera;

    public Camera CurrentCamera => currentCamera != null ? currentCamera : coreCamera;
    public Camera CoreCamera => coreCamera;

    void Awake()
    {
        if (I != null && I != this)
        {
            Destroy(gameObject);
            return;
        }

        I = this;
        DontDestroyOnLoad(gameObject);

        if (coreCamera == null)
            coreCamera = Camera.main;

        if (currentCamera == null)
            currentCamera = coreCamera;
    }

    public void SetCoreCamera(Camera cam)
    {
        if (cam == null) return;

        coreCamera = cam;

        // 현재 카메라가 비어있거나 core를 쓰는 상태면 같이 갱신
        if (currentCamera == null)
            currentCamera = coreCamera;
    }

    public void SetCurrentCamera(Camera cam)
    {
        if (cam == null)
            currentCamera = coreCamera;
        else
            currentCamera = cam;
    }

    public void UseCoreCamera()
    {
        currentCamera = coreCamera;
    }
}