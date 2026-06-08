using UnityEngine;

public class CameraSnapshotManager : MonoBehaviour
{
    Vector3 pos;
    float size;
    bool hasSnapshot;

    public void Save()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        pos = cam.transform.position;
        size = cam.orthographicSize;
        hasSnapshot = true;
    }

    public void Restore()
    {
        if (!hasSnapshot) return;

        Camera cam = Camera.main;
        if (cam == null) return;

        cam.transform.position = pos;
        cam.orthographicSize = size;
    }
}
