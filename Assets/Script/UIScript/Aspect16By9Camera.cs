using UnityEngine;

[RequireComponent(typeof(Camera))]
public class Aspect16By9Camera : MonoBehaviour
{
    [SerializeField] float targetAspect = 16f / 9f;
    [SerializeField] Color barColor = Color.black;

    Camera _cam;
    int _lastWidth = -1;
    int _lastHeight = -1;

    void Awake()
    {
        _cam = GetComponent<Camera>();
        Apply();
    }

    void OnEnable()
    {
        Apply();
    }

    void Update()
    {
        if (_lastWidth != Screen.width || _lastHeight != Screen.height)
            Apply();
    }

    void Apply()
    {
        if (_cam == null) return;

        _lastWidth = Screen.width;
        _lastHeight = Screen.height;

        float windowAspect = (float)Screen.width / Screen.height;
        float x = 0f;
        float y = 0f;
        float w = 1f;
        float h = 1f;

        if (windowAspect > targetAspect)
        {
            // ´õ ³ÐÀ½ ¡æ ÁÂ¿ì ¿©¹é
            w = targetAspect / windowAspect;
            x = (1f - w) * 0.5f;
        }
        else if (windowAspect < targetAspect)
        {
            // ´õ Á¼À½ ¡æ À§¾Æ·¡ ¿©¹é
            h = windowAspect / targetAspect;
            y = (1f - h) * 0.5f;
        }

        _cam.clearFlags = CameraClearFlags.SolidColor;
        _cam.backgroundColor = barColor;
        _cam.rect = new Rect(x, y, w, h);
    }
}