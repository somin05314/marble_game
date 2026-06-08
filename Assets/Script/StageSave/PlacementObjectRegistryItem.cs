using UnityEngine;

[DisallowMultipleComponent]
public class PlacementObjectRegistryItem : MonoBehaviour
{
    PlacementObject _po;

    void Awake()
    {
        _po = GetComponent<PlacementObject>();
    }

    void OnEnable()
    {
        if (_po == null) _po = GetComponent<PlacementObject>();
        if (_po != null)
            StageObjectRegistry.Register(_po);
    }

    void OnDisable()
    {
        if (_po != null)
            StageObjectRegistry.Unregister(_po);
    }

    void OnDestroy()
    {
        if (_po != null)
            StageObjectRegistry.Unregister(_po);
    }
}