using UnityEngine;

public class PlacementObject : MonoBehaviour
{
    public GameObject prefab;

    Collider2D col;

    public PhysicsType physicsType = PhysicsType.Static;

    void Awake()
    {
        col = GetComponent<Collider2D>();
    }

    public bool CanPlace(LayerMask blockLayer, PlacementObject self = null)
    {
        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(blockLayer);
        filter.useTriggers = true;

        Collider2D[] results = new Collider2D[10];
        int count = col.OverlapCollider(filter, results);

        for (int i = 0; i < count; i++)
        {
            var other = results[i].GetComponentInParent<PlacementObject>();
            if (other != null && other != self)
                return false;
        }

        return true;
    }

}

