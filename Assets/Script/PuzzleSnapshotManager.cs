using System.Collections.Generic;
using UnityEngine;

public class PuzzleSnapshotManager : MonoBehaviour
{
    List<PlacementSnapshot> snapshot = new();

    

    // =============================
    // SAVE
    // =============================
    public void Save()
    {
        snapshot.Clear();

        var placedObjects = FindObjectsOfType<PlacementObject>();
        Dictionary<PlacementObject, int> indexMap = new();

        int idx = 0;
        foreach (var po in placedObjects)
        {
            if (po.gameObject.layer == LayerMask.NameToLayer("Ghost"))
                continue;

            indexMap[po] = idx++;
        }

        foreach (var po in placedObjects)
        {
            if (!indexMap.ContainsKey(po))
                continue;

            PlacementSnapshot ps = new PlacementSnapshot
            {
                placementData = po.placementData,   // ⭐ ScriptableObject
                position = po.transform.position,
                rotation = po.transform.rotation
            };

            foreach (var c in po.connections)
            {
                if (!indexMap.ContainsKey(c.otherRoot.owner))
                    continue;

                ps.snapLinks.Add(new SnapLinkSnapshot
                {
                    myRootIndex = c.myRoot.index,
                    otherObjectIndex = indexMap[c.otherRoot.owner],
                    otherRootIndex = c.otherRoot.index,

                    // ✅ 추가
                    mySnapId = c.myPoint != null ? c.myPoint.snapId : -1,
                    otherSnapId = c.otherPoint != null ? c.otherPoint.snapId : -1
                });

            }

            snapshot.Add(ps);
        }
    }

    // =============================
    // RESTORE
    // =============================
    public void Restore()
    {
        // 기존 PlacementObject 제거
        var current = FindObjectsOfType<PlacementObject>();
        foreach (var po in current)
        {
            Destroy(po.gameObject);
        }

        // 구슬 제거
        var marbles = GameObject.FindGameObjectsWithTag("Marble");
        foreach (var m in marbles)
        {
            Destroy(m);
        }

        List<PlacementObject> restored = new();

        // 오브젝트 복원
        foreach (var snap in snapshot)
        {
            if (snap.placementData == null)
                continue;

            GameObject obj = Instantiate(
                snap.placementData.prefab,   // ⭐ 항상 안전
                snap.position,
                snap.rotation
            );

            var po = obj.GetComponent<PlacementObject>();
            po.placementData = snap.placementData;
            restored.Add(po);

            var rb = obj.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.bodyType = RigidbodyType2D.Kinematic;
                rb.gravityScale = 0;
                rb.simulated = true;
            }

            foreach (var col in obj.GetComponentsInChildren<Collider2D>())
            {
                col.enabled = true;
                col.isTrigger = false;
            }
        }

        // =============================
        // SnapLink 복원
        // =============================
        for (int i = 0; i < snapshot.Count; i++)
        {
            var snapA = snapshot[i];
            var objA = restored[i];

            foreach (var link in snapA.snapLinks)
            {
                // 양쪽에서 중복 저장된 연결을 1번만 복원
                if (link.otherObjectIndex < i)
                    continue;

                if (link.otherObjectIndex < 0 || link.otherObjectIndex >= restored.Count)
                    continue;

                var objB = restored[link.otherObjectIndex];

                var rootA = FindRootByIndex(objA, link.myRootIndex);
                var rootB = FindRootByIndex(objB, link.otherRootIndex);
                if (rootA == null || rootB == null)
                    continue;

                var pointA = FindPointBySnapId(rootA, link.mySnapId);
                var pointB = FindPointBySnapId(rootB, link.otherSnapId);
                if (pointA == null || pointB == null)
                    continue;

                SnapManager.CommitSnapDirect(
                    objA, rootA, pointA,
                    objB, rootB, pointB
                );
            }
        }

        // --- helper ---
        static SnapRoot FindRootByIndex(PlacementObject po, int rootIndex)
        {
            var roots = po.GetComponentsInChildren<SnapRoot>(true);
            foreach (var r in roots)
            {
                if (r != null && r.index == rootIndex)
                    return r;
            }
            return null;
        }

        static SnapPoint FindPointBySnapId(SnapRoot root, int snapId)
        {
            var points = root.GetComponentsInChildren<SnapPoint>(true);
            if (points == null || points.Length == 0)
                return null;

            // snapId 정보가 있으면 그걸로 찾고
            if (snapId >= 0)
            {
                foreach (var p in points)
                {
                    if (p != null && p.snapId == snapId)
                        return p;
                }
            }

            // 못 찾으면 첫 번째로 fallback (root에 점이 1개인 구조면 이걸로도 OK)
            return points[0];
        }

    }
}
