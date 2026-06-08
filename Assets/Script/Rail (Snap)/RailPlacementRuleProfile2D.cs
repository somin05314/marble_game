using UnityEngine;

[CreateAssetMenu(menuName = "Puzzle/Rail Placement Rule Profile 2D")]
public class RailPlacementRuleProfile2D : ScriptableObject
{
    [Header("Masks")]
    public LayerMask wallMask;
    public LayerMask placedMask;
    public LayerMask railMask;

    [Header("Endpoint / Hint Filters")]
    public bool excludePlacedObjects = true;

    [Header("Hint Radii Scales (Handle-style)")]
    [Tooltip("벽 판정 반경 배율(= rail.endpointBlockRadius * scale)")]
    [Range(0.1f, 1f)] public float hintWallRadiusScale = 0.35f;

    [Tooltip("PO 근접 배제 반경 배율(= baseR * scale). baseR = max(endpointBlockRadius, thickness*0.5)")]
    [Range(0.1f, 2f)] public float hintPlacedRadiusScale = 1f;

    [Header("Wall Allow Near Endpoints")]
    public bool allowStartInsideWall = false;
    public bool allowEndInsideWall = false;
    public float endpointAllowRadius = 0.12f;

    [Header("Snap/Attach Overlap Exception")]
    [Tooltip("스냅된 PO(ignoreOwner)와 겹침을 허용하는 끝점 주변 반경(그리드 칸 단위)")]
    [Range(0f, 5f)]
    public float ignoreOwnerRelaxCells = 1f; // ✅ 1칸으로 쓰고 싶으면 1.0

    [Header("Snap / Relax")]
    [Tooltip("스냅된 PO owner는 점유 판정에서 완화(무시)해주는 총 반경(셀). " +
         "예: 2면 A/B 끝점 주변 합쳐서 2셀까지 완화.")]
    public int ignoreOwnerRelaxTotalCells = 2;

}
