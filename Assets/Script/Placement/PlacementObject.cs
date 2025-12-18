using UnityEngine;

public class PlacementObject : MonoBehaviour
{
    public GameObject prefab;

    [Header("Placement")]
    public Collider2D placementCollider;   // Visual에 있는 Collider

    public PhysicsType physicsType = PhysicsType.Static;

    [Header("Snap")]
    public Transform[] snapPoints;
    public bool isSnapped;

    [HideInInspector]
    public PlacementObject snappedTo; // 스냅된 상대

    // Awake에서 Collider 찾지 않는다
    // void Awake() { }

    public bool CanPlace(LayerMask blockLayer, PlacementObject self = null)
    {
        if (placementCollider == null)
            return false;

        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(blockLayer);
        filter.useTriggers = true;

        Collider2D[] results = new Collider2D[10];
        int count = placementCollider.OverlapCollider(filter, results);

        for (int i = 0; i < count; i++)
        {
            var other = results[i].GetComponentInParent<PlacementObject>();
            if (other == null || other == self)
                continue;

            // 스냅된 대상만 예외 가능
            if (self != null && self.snappedTo == other)
            {
                // 스냅 포인트 근처 겹침만 허용
                if (IsOverlapNearSnap(self, other))
                    continue;
            }

            // 그 외 모든 겹침은 불허
            return false;
        }

        return true;
    }

    bool IsOverlapNearSnap(
    PlacementObject self,
    PlacementObject other,
    float allowRadius = 0.25f // 튜닝 포인트
)
    {
        if (self.snapPoints == null || other.snapPoints == null)
            return false;

        foreach (var mySnap in self.snapPoints)
        {
            foreach (var otherSnap in other.snapPoints)
            {
                float dist = Vector2.Distance(mySnap.position, otherSnap.position);
                if (dist <= allowRadius)
                    return true;
            }
        }

        return false;
    }


}

