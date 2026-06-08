using UnityEngine;

public static class SnapManager
{
    static int GhostLayer => LayerMask.NameToLayer("Ghost");

    static bool IsOccupied(SnapPoint p)
    {
        if (p == null || p.root == null || p.root.owner == null) return true;
        var owner = p.root.owner;

        // owner.connections에서 "내 점"으로 잡힌 게 있으면 점유
        return owner.connections.Exists(c => c.myPoint == p);
    }

    /// <summary>
    /// Ghost/Preview 스냅 계산: 가장 가까운 1쌍 + 스냅 대상 + 허용 관통
    /// (점유된 포인트는 후보 제외)
    /// </summary>
    public static bool TryGetBestSnapPreview(
    PlacementObject previewObj,
    out SnapPreviewPair best,
    out PlacementObject snapTarget,
    out float allowedSnapPenetration
)
    {
        best = default;
        snapTarget = null;
        allowedSnapPenetration = 0f;

        if (previewObj == null) return false;

        var myPoints = previewObj.GetComponentsInChildren<SnapPoint>(true);
        var allPoints = Object.FindObjectsOfType<SnapPoint>(true);

        // ✅ 아직 어디에도 연결 안 된 “떠있는 블록”이면 AnchorRoot에만 스냅 허용
        bool requireAnchorTarget = (previewObj.connections == null || previewObj.connections.Count == 0);

        float bestDistSq = float.PositiveInfinity;
        bool found = false;

        for (int i = 0; i < myPoints.Length; i++)
        {
            var my = myPoints[i];
            if (my == null || my.root == null || my.root.owner == null) continue;

            if (IsOccupied(my)) continue;

            Vector3 myPos = my.transform.position;

            for (int j = 0; j < allPoints.Length; j++)
            {
                var other = allPoints[j];
                if (other == null || other.root == null || other.root.owner == null) continue;

                var otherOwner = other.root.owner;

                if (otherOwner == previewObj) continue;
                if (otherOwner.gameObject.layer == GhostLayer) continue;
                if (!otherOwner.gameObject.activeInHierarchy) continue;

                if (IsOccupied(other)) continue;

                // ✅ (A 적용) 떠있는 상태면 AnchorRoot만 허용
                if (requireAnchorTarget && !other.IsAnchorRoot)
                    continue;

                Vector3 otherPos = other.transform.position;

                float radius = Mathf.Min(my.snapRadius, other.snapRadius);
                float radiusSq = radius * radius;

                Vector3 delta = otherPos - myPos;
                float distSq = delta.sqrMagnitude;

                if (distSq > radiusSq) continue;

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

                    snapTarget = otherOwner;

                    allowedSnapPenetration = Mathf.Min(
                        my.allowedPenetration,
                        other.allowedPenetration
                    );
                }
            }
        }

        return found;
    }


    /// <summary>
    /// GridPlacer(드래그/확정)에서 쓰기 편한 래퍼
    /// </summary>
    public static PlacementObject GetSnapTargetIfAny(
        PlacementObject obj,
        out float allowedSnapPenetration,
        out SnapPreviewPair pair
    )
    {
        if (TryGetBestSnapPreview(obj, out pair, out var target, out allowedSnapPenetration))
            return target;

        pair = default;
        allowedSnapPenetration = 0f;
        return null;
    }

    public static PlacementObject GetSnapTargetIfAny(
        PlacementObject obj,
        out float allowedSnapPenetration
    )
    {
        return GetSnapTargetIfAny(obj, out allowedSnapPenetration, out _);
    }

    /// <summary>
    /// 점유 규칙 포함 커밋(안전)
    /// </summary>
    public static bool CommitSnapDirect(
        PlacementObject myObj,
        SnapRoot myRoot,
        SnapPoint myPoint,
        PlacementObject otherObj,
        SnapRoot otherRoot,
        SnapPoint otherPoint
    )
    {
        if (myObj == null || otherObj == null) return false;
        if (myObj == otherObj) return false;
        if (myRoot == null || otherRoot == null) return false;
        if (myPoint == null || otherPoint == null) return false;

        // ✅ 점유면 실패
        if (IsOccupied(myPoint) || IsOccupied(otherPoint))
            return false;

        // ✅ 중복 방지
        bool already =
            myObj.connections.Exists(c =>
                c.otherRoot == otherRoot &&
                c.otherPoint == otherPoint &&
                c.myRoot == myRoot &&
                c.myPoint == myPoint
            );

        if (already) return false;

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

        return true;
    }
}
