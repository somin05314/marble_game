using UnityEngine;

public static class SnapManager
{
    static int GhostLayer => LayerMask.NameToLayer("Ghost");

    /// <summary>
    /// Ghost / Preview 전용 스냅 계산 (가장 가까운 1쌍만 반환)
    /// </summary>
    public static bool TryGetSnapPreview(
        PlacementObject previewObj,
        out SnapPreviewPair best
    )
    {
        best = default;

        if (previewObj == null)
            return false;

        // ✅ 내 스냅 포인트들
        var myPoints = previewObj.GetComponentsInChildren<SnapPoint>();

        // ✅ 씬 전체 스냅 포인트 (LINQ 제거: GC 감소)
        var allPoints = Object.FindObjectsOfType<SnapPoint>();

        float bestDistSq = float.PositiveInfinity;
        bool found = false;

        for (int i = 0; i < myPoints.Length; i++)
        {
            var my = myPoints[i];
            if (my == null || my.root == null || my.root.owner == null)
                continue;

            Vector3 myPos = my.transform.position;

            for (int j = 0; j < allPoints.Length; j++)
            {
                var other = allPoints[j];
                if (other == null || other.root == null || other.root.owner == null)
                    continue;

                var otherOwner = other.root.owner;

                // ✅ 자기 자신(프리뷰 오브젝트) 제외
                if (otherOwner == previewObj)
                    continue;

                // ✅ 고스트 레이어 제외 (상대 오브젝트 기준)
                if (otherOwner.gameObject.layer == GhostLayer)
                    continue;

                // ✅ (옵션) 비활성 오브젝트 제외
                if (!otherOwner.gameObject.activeInHierarchy)
                    continue;

                Vector3 otherPos = other.transform.position;

                // ✅ 거리 비교: sqrt 없는 sqrMagnitude
                float radius = Mathf.Min(my.snapRadius, other.snapRadius);
                float radiusSq = radius * radius;

                Vector3 delta = otherPos - myPos;
                float distSq = delta.sqrMagnitude;

                if (distSq > radiusSq)
                    continue;

                if (distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    found = true;

                    best = new SnapPreviewPair
                    {
                        myPoint = my,
                        otherPoint = other,
                        previewObjectPos = previewObj.transform.position + delta
                    };
                }
            }
        }

        return found;
    }

    public static void CommitSnapDirect(
        PlacementObject myObj,
        SnapRoot myRoot,
        SnapPoint myPoint,
        PlacementObject otherObj,
        SnapRoot otherRoot,
        SnapPoint otherPoint
    )
    {
        if (myObj == null || otherObj == null) return;
        if (myObj == otherObj) return;

        if (myRoot == null || otherRoot == null) return;
        if (myPoint == null || otherPoint == null) return;

        // ✅ 중복 커밋 방지 (같은 점 조합이 이미 있으면 추가하지 않음)
        bool already =
            myObj.connections.Exists(c =>
                c.otherRoot == otherRoot &&
                c.otherPoint == otherPoint &&
                c.myRoot == myRoot &&
                c.myPoint == myPoint
            );

        if (already)
            return;

        var c1 = new SnapConnection
        {
            myRoot = myRoot,
            myPoint = myPoint,
            otherRoot = otherRoot,
            otherPoint = otherPoint
        };

        var c2 = new SnapConnection
        {
            myRoot = otherRoot,
            myPoint = otherPoint,
            otherRoot = myRoot,
            otherPoint = myPoint
        };

        myObj.connections.Add(c1);
        otherObj.connections.Add(c2);
    }

}
