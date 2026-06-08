using UnityEngine;

[RequireComponent(typeof(Camera))]
public class AspectRatioLetterbox : MonoBehaviour
{
    [SerializeField] float targetAspect = 16f / 9f;
    [SerializeField] Color barColor = Color.black;

    Camera _cam;
    int _lastScreenWidth = -1;
    int _lastScreenHeight = -1;

    void Awake()
    {
        _cam = GetComponent<Camera>();
        Apply();
    }

    void OnEnable()
    {
        Apply();
    }

    void LateUpdate()
    {
        if (_lastScreenWidth != Screen.width || _lastScreenHeight != Screen.height)
            Apply();
    }

    void Apply()
    {
        if (_cam == null) return;

        _lastScreenWidth = Screen.width;
        _lastScreenHeight = Screen.height;

        float windowAspect = (float)Screen.width / Screen.height;
        float scaleHeight = windowAspect / targetAspect;

        _cam.clearFlags = CameraClearFlags.SolidColor;
        _cam.backgroundColor = barColor;

        if (scaleHeight < 1f)
        {
            // 위아래 검은 여백
            _cam.rect = new Rect(0f, (1f - scaleHeight) * 0.5f, 1f, scaleHeight);
        }
        else
        {
            // 좌우 검은 여백
            float scaleWidth = 1f / scaleHeight;
            _cam.rect = new Rect((1f - scaleWidth) * 0.5f, 0f, scaleWidth, 1f);
        }
    }
}