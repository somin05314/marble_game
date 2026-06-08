using UnityEngine;

[RequireComponent(typeof(RectTransform))]
public class Aspect16By9UIRoot : MonoBehaviour
{
    [SerializeField] float targetAspect = 16f / 9f;

    RectTransform _rt;
    int _lastWidth = -1;
    int _lastHeight = -1;

    void Awake()
    {
        _rt = GetComponent<RectTransform>();
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
        if (_rt == null) return;

        _lastWidth = Screen.width;
        _lastHeight = Screen.height;

        RectTransform parent = _rt.parent as RectTransform;
        if (parent == null) return;

        float parentWidth = parent.rect.width;
        float parentHeight = parent.rect.height;

        if (parentWidth <= 0f || parentHeight <= 0f) return;

        float windowAspect = parentWidth / parentHeight;

        float x = 0f;
        float y = 0f;
        float w = 1f;
        float h = 1f;

        if (windowAspect > targetAspect)
        {
            w = targetAspect / windowAspect;
            x = (1f - w) * 0.5f;
        }
        else if (windowAspect < targetAspect)
        {
            h = windowAspect / targetAspect;
            y = (1f - h) * 0.5f;
        }

        _rt.anchorMin = new Vector2(x, y);
        _rt.anchorMax = new Vector2(x + w, y + h);
        _rt.offsetMin = Vector2.zero;
        _rt.offsetMax = Vector2.zero;
    }
}