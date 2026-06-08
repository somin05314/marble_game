using UnityEngine;


public static class MouseUtil
{
    public static bool TryGetMouseWorld(Camera cam, out Vector3 world)
    {
        world = Vector3.zero;

        if (!Application.isFocused)
            return false;

        if (cam == null)
            return false;

        Vector3 mouse = Input.mousePosition;

        if (mouse.x < 0 || mouse.y < 0 ||
            mouse.x > Screen.width || mouse.y > Screen.height)
            return false;

        // ★ 핵심: Z는 항상 0 평면
        mouse.z = -cam.transform.position.z;

        world = cam.ScreenToWorldPoint(mouse);
        world.z = 0f;

        return true;
    }
}


