using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public class GlobalOverlayStackRouter : MonoBehaviour
{
    public static GlobalOverlayStackRouter I { get; private set; }

    [SerializeField] Camera overlayCamera;

    void Awake()
    {
        if (I != null && I != this)
        {
            Destroy(gameObject);
            return;
        }

        I = this;
        DontDestroyOnLoad(gameObject);

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        if (I == this)
            SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RebindToCurrentBaseCamera();
    }

    public void RebindToCurrentBaseCamera()
    {
        if (overlayCamera == null) return;

        Camera targetBase = FindBaseCameraInLoadedScenes();
        if (targetBase == null) return;

        var baseData = targetBase.GetUniversalAdditionalCameraData();
        var overlayData = overlayCamera.GetUniversalAdditionalCameraData();

        if (baseData == null || overlayData == null) return;

        baseData.renderType = CameraRenderType.Base;
        overlayData.renderType = CameraRenderType.Overlay;

        baseData.cameraStack.Clear();
        baseData.cameraStack.Add(overlayCamera);

        Debug.Log($"[GlobalOverlayStackRouter] {overlayCamera.name} -> {targetBase.name}");
    }

    Camera FindBaseCameraInLoadedScenes()
    {
        Camera[] cams = Camera.allCameras;
        foreach (var cam in cams)
        {
            var data = cam.GetUniversalAdditionalCameraData();
            if (data != null && data.renderType == CameraRenderType.Base)
            {
                // 필요하면 태그나 이름으로 PuzzleCamera만 고르도록 강화 가능
                if (cam != overlayCamera)
                    return cam;
            }
        }
        return null;
    }
}