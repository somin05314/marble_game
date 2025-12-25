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
        // 카메라는 항상 허용
        HandlePan();
        HandleZoom();

        // 리셋만 Build 모드에서
        if (GameModeManager.Instance.currentMode == GameMode.Build)
            HandleReset();
    }

    bool IsMousePositionValid()
    {
        Vector3 mouse = Input.mousePosition;

        return mouse.x >= 0 && mouse.y >= 0 &&
               mouse.x <= Screen.width &&
               mouse.y <= Screen.height;
    }


    Vector3 GetMouseWorld()
    {
        if (!IsMousePositionValid())
            return lastMouseWorld; // 이전 값 유지

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

