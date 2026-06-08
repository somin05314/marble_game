using UnityEngine;

public class RailNodeOwnerTag2D : MonoBehaviour
{
    [SerializeField] PlacementObject owner;
    public PlacementObject Owner => owner;

    public void SetOwner(PlacementObject o)
    {
        if (o != null) owner = o;
    }
}
