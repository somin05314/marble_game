using UnityEngine;

public enum PlacementType
{
    Block,
    Curve,
    Nail
}

[CreateAssetMenu(menuName = "Placement/Object")]
public class PlacementData : ScriptableObject
{
    public string id;
    public GameObject prefab;
    public PlacementType placementType;
    public Vector2Int size;   // Grid Á¡À¯
}
