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
    public bool allowRotate = true;
    public bool allowFlipX = true;
    public bool allowFlipY = false; // 필요하면

    [Header("Strength / State")]
    public bool allowStrengthControl = false;

    [Min(1)] public int minStrengthLevel = 1;
    [Min(1)] public int maxStrengthLevel = 3;
    [Min(1)] public int defaultStrengthLevel = 1;

    [Header("Palette Icon UI Prefabs")]
    public GameObject iconNormalUIPrefab;
    public GameObject iconExhaustedUIPrefab; // 선택(없으면 normal로 처리)

    [Header("Palette UI")]
    public bool useCustomPaletteIconSize = false;
    public Vector2 paletteIconSize = new Vector2(80f, 80f);

    [Tooltip("아이콘 크기 대신 배율로 맞추고 싶을 때 사용")]
    public bool useCustomPaletteIconScale = false;
    public Vector3 paletteIconScale = Vector3.one;
}
