using UnityEngine;

[DisallowMultipleComponent]
public class RailRegistryItem : MonoBehaviour
{
    RailSpan2D _rail;

    void Awake()
    {
        _rail = GetComponent<RailSpan2D>();
    }

    void OnEnable()
    {
        if (_rail == null) _rail = GetComponent<RailSpan2D>();
        if (_rail != null)
            StageObjectRegistry.Register(_rail);
    }

    void OnDisable()
    {
        if (_rail != null)
            StageObjectRegistry.Unregister(_rail);
    }

    void OnDestroy()
    {
        if (_rail != null)
            StageObjectRegistry.Unregister(_rail);
    }
}