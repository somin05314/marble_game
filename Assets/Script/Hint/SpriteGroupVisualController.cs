using UnityEngine;

[ExecuteAlways]
public class SpriteGroupVisualController : MonoBehaviour
{
    [Header("Targets")]
    [SerializeField] bool includeInactive = true;

    [Header("Alpha")]
    [Range(0f, 1f)]
    [SerializeField] float alpha = 0.35f;

    [Header("Brightness")]
    [Range(0f, 2f)]
    [SerializeField] float brightness = 1f;

    [Header("Apply")]
    [SerializeField] bool applyOnEnable = true;
    [SerializeField] bool applyOnValidate = true;

    SpriteRenderer[] cachedRenderers;

    void OnEnable()
    {
        if (applyOnEnable)
            ApplyVisual();
    }

    void OnValidate()
    {
        if (!applyOnValidate)
            return;

        ApplyVisual();
    }

    [ContextMenu("Apply Visual")]
    public void ApplyVisual()
    {
        cachedRenderers = GetComponentsInChildren<SpriteRenderer>(includeInactive);

        if (cachedRenderers == null)
            return;

        foreach (var sr in cachedRenderers)
        {
            if (sr == null) continue;

            Color c = sr.color;

            // RGB 밝기 조절
            c.r = Mathf.Clamp01(c.r * brightness);
            c.g = Mathf.Clamp01(c.g * brightness);
            c.b = Mathf.Clamp01(c.b * brightness);

            // 알파 조절
            c.a = alpha;

            sr.color = c;
        }
    }

    [ContextMenu("Refresh Renderer Cache")]
    public void RefreshRendererCache()
    {
        cachedRenderers = GetComponentsInChildren<SpriteRenderer>(includeInactive);
    }
}