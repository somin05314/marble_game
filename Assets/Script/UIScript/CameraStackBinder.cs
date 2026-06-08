using UnityEngine;
using UnityEngine.Rendering.Universal;

public class CameraStackBinder : MonoBehaviour
{
    [Header("Assign or auto-find")]
    [SerializeField] Camera baseCamera;
    [SerializeField] Camera overlayCamera;

    [Header("Options")]
    [SerializeField] bool bindOnStart = true;
    [SerializeField] bool clearBeforeAdd = false;

    void Start()
    {
        if (bindOnStart)
            Bind();
    }

    [ContextMenu("Bind Camera Stack")]
    public void Bind()
    {
        if (baseCamera == null || overlayCamera == null)
        {
            Debug.LogWarning("[CameraStackBinder] Base/Overlay camera is missing.");
            return;
        }

        var baseData = baseCamera.GetUniversalAdditionalCameraData();
        var overlayData = overlayCamera.GetUniversalAdditionalCameraData();

        if (baseData == null || overlayData == null)
        {
            Debug.LogWarning("[CameraStackBinder] URP Additional Camera Data not found.");
            return;
        }

        // Base / Overlay 타입 강제
        baseData.renderType = CameraRenderType.Base;
        overlayData.renderType = CameraRenderType.Overlay;

        if (clearBeforeAdd)
            baseData.cameraStack.Clear();

        if (!baseData.cameraStack.Contains(overlayCamera))
            baseData.cameraStack.Add(overlayCamera);

        Debug.Log($"[CameraStackBinder] Bound overlay '{overlayCamera.name}' to base '{baseCamera.name}'.");
    }

    [ContextMenu("Unbind Camera Stack")]
    public void Unbind()
    {
        if (baseCamera == null || overlayCamera == null) return;

        var baseData = baseCamera.GetUniversalAdditionalCameraData();
        if (baseData == null) return;

        if (baseData.cameraStack.Contains(overlayCamera))
            baseData.cameraStack.Remove(overlayCamera);
    }
}