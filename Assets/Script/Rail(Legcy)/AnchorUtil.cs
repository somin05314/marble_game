using UnityEngine;

public static class AnchorUtil
{
    static readonly Collider2D[] hits = new Collider2D[16];

    public static bool TryPickAnchor(
        Vector2 mouseWorld,
        float radius,
        LayerMask anchorMask,
        out AnchorPoint2D anchor
    )
    {
        anchor = null;

        Physics2D.SyncTransforms();
        int count = Physics2D.OverlapCircleNonAlloc(mouseWorld, radius, hits, anchorMask);
        if (count <= 0) return false;

        float best = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            var col = hits[i];
            if (col == null) continue;

            var a = col.GetComponentInParent<AnchorPoint2D>();
            if (a == null) continue;

            float d = ((Vector2)a.transform.position - mouseWorld).sqrMagnitude;
            if (d < best)
            {
                best = d;
                anchor = a;
            }
        }
        return anchor != null;
    }
}
