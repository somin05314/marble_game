using UnityEngine;

public class SnapRoot : MonoBehaviour
{
    [HideInInspector] public int index;
    [HideInInspector] public PlacementObject owner;

    void Awake()
    {

        var points = GetComponentsInChildren<SnapPoint>();
        foreach (var p in points)
        {
            p.root = this;
        }
    }

}
