using UnityEngine;

public class AnchorManager : MonoBehaviour
{
    public static AnchorManager Instance { get; private set; }

    [Header("Prefab")]
    public AnchorPoint2D anchorPrefab;

    [Header("Masks")]
    public LayerMask anchorMask;

    [Header("Pick/Merge")]
    public float mergeRadius = 0.15f; // 너무 가까우면 같은 앵커로 취급

    static readonly Collider2D[] hits = new Collider2D[32];

    void Awake()
    {
        Instance = this;
    }

    public AnchorPoint2D GetOrCreate(Vector2 worldPos)
    {
        if (anchorPrefab == null)
        {
            Debug.LogError("[Anchor] anchorPrefab is NULL! AnchorManager 인스펙터에 프리팹을 지정하세요.");
            return null;
        }

        Physics2D.SyncTransforms();

        // 주변에 이미 앵커 있으면 재사용
        int count = Physics2D.OverlapCircleNonAlloc(worldPos, mergeRadius, hits, anchorMask);

        AnchorPoint2D bestAnchor = null;
        float best = float.MaxValue;

        for (int i = 0; i < count; i++)
        {
            var col = hits[i];
            if (col == null) continue;

            var a = col.GetComponentInParent<AnchorPoint2D>();
            if (a == null) continue;

            float d = ((Vector2)a.transform.position - worldPos).sqrMagnitude;
            if (d < best)
            {
                best = d;
                bestAnchor = a;
            }
        }

        if (bestAnchor != null)
            return bestAnchor;

        // 없으면 새로 생성
        var created = Instantiate(anchorPrefab, worldPos, Quaternion.identity);
        created.name = $"Anchor_{worldPos.x:F2}_{worldPos.y:F2}";
        return created;
    }
}
