using UnityEngine;

public class PuzzleCamera : MonoBehaviour
{
    public Camera cam;

    [Header("Pan")]
    public float panSpeed = 1f;

    [Header("Zoom")]
    public float zoomSpeed = 5f;
    public float minZoom = 4f;
    public float maxZoom = 14f;

    Vector3 lastMouseWorld;

    void Update()
    {
        HandlePan();
        HandleZoom();
        HandleReset();
    }

    Vector3 GetMouseWorld()
    {
        Vector3 mouse = Input.mousePosition;
        mouse.z = -cam.transform.position.z; 
        return cam.ScreenToWorldPoint(mouse);
    }


    void HandlePan()
    {
        if (Input.GetMouseButtonDown(1))
        {
            lastMouseWorld = GetMouseWorld();
        }

        if (Input.GetMouseButton(1))
        {
            Vector3 current = GetMouseWorld();
            Vector3 delta = lastMouseWorld - current;
            transform.position += delta;
        }
    }

    void HandleZoom()
    {
        float scroll = Input.mouseScrollDelta.y;
        if (scroll == 0) return;

        cam.orthographicSize -= scroll * zoomSpeed;
        cam.orthographicSize = Mathf.Clamp(
            cam.orthographicSize,
            minZoom,
            maxZoom
        );
    }

    void HandleReset()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            transform.position = new Vector3(0, 0, transform.position.z);
            cam.orthographicSize = 8f;
        }
    }
}

