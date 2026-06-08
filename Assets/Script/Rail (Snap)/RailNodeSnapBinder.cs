using System.Collections.Generic;
using UnityEngine;

public static class RailNodeSnapBinder
{
    const int MAX_RAIL_DEGREE_FOR_OBJECT_ATTACH = 99;

    static readonly HashSet<int> _detaching = new HashSet<int>(128);

    static readonly Collider2D[] _nodeHitsAny = new Collider2D[32];

    static readonly Collider2D[] _pickHits = new Collider2D[64];

    static bool IsTargetBelongsToPO(Transform t, PlacementObject po)
    {
        if (t == null || po == null) return false;
        var root = po.transform;
        return t == root || t.IsChildOf(root);
    }

    // ✅ 레일의 startNode/endNode가 비어있으면 보정하되,
    //    "Follow 포함해서" 이미 있는 노드를 먼저 잡고,
    //    정말 없을 때만 GetOrCreate()로 생성한다.
    //    (중복 노드 생성 → PO/레일 분리 버그 방지)
    // ✅ 레일의 startNode/endNode가 비어있으면 보정하되,
    //    "Follow 포함해서" 이미 있는 노드를 먼저 잡고,
    //    정말 없을 때만 GetOrCreate()로 생성한다.
    // ✅ 레일의 startNode/endNode가 비어있거나 위치가 어긋나면 보정하되,
    //    "Follow 포함해서" 이미 있는 노드를 먼저 잡고,
    //    정말 없을 때만 GetOrCreate()로 생성한다.
    //    (중복 노드 생성 → PO/레일 분리 버그 방지)
    static void EnsureRailNodesForCounting(RailSpan2D r)
    {
        if (r == null) return;

        var mgr = RailSnapNodeManager.Instance;
        if (mgr == null) return;

        // 노드가 같은 위치라고 볼 허용 오차(월드 단위)
        // 너무 작으면 부동소수 오차로 계속 “need”가 켜질 수 있음
        const float EPS = 0.001f; // 1mm
        float epsSq = EPS * EPS;

        void Fix(ref RailSnapNode2D node, Vector2 pos, string tag)
        {
            bool need =
                (node == null) ||
                (((Vector2)node.transform.position - pos).sqrMagnitude > epsSq);

            if (!need) return;

            // ✅ "Follow 포함" 근처 노드를 먼저 잡는다
            var any = FindNearestAnyNodeIncludingFollow(pos);

            // ✅ 정말 없을 때만 새로 만든다(중복 생성 방지)
            node = (any != null) ? any : mgr.GetOrCreate(pos);

            // (선택) 디버그 필요하면 여기에 로그 추가 가능
            // Debug.Log($"[EnsureRailNodesForCounting] {tag} fixed -> {(node ? node.name : "null")}");
        }

        // ✅ RailSpan2D 필드명 그대로 사용
        Fix(ref r.startNode, r.start, "start");
        Fix(ref r.endNode, r.end, "end");
    }





    static int CountRailsUsingNodeFindObjects(RailSnapNode2D node)
    {
        if (node == null) return 0;

#if UNITY_2022_2_OR_NEWER
        var rails = Object.FindObjectsByType<RailSpan2D>(FindObjectsSortMode.None);
#else
        var rails = Object.FindObjectsOfType<RailSpan2D>();
#endif
        int count = 0;
        for (int i = 0; i < rails.Length; i++)
        {
            var r = rails[i];
            if (r == null) continue;

            EnsureRailNodesForCounting(r);
            if (r.startNode == node || r.endNode == node) count++;
        }
        return count;
    }

    static int CountRailsUsingNode(RailSnapNode2D node, IReadOnlyList<RailSpan2D> rails)
    {
        if (node == null) return 0;

        // 1) 주입된 rails로 먼저 센다
        if (rails != null)
        {
            int count = 0;
            for (int i = 0; i < rails.Count; i++)
            {
                var r = rails[i];
                if (r == null) continue;

                EnsureRailNodesForCounting(r);
                if (r.startNode == node || r.endNode == node) count++;
            }

            // ✅ 주입 리스트가 stale/비어있을 수 있으니 fallback 허용
            if (count > 0) return count;
            return CountRailsUsingNodeFindObjects(node);
        }

        // 2) rails가 null이면 원래 방식
        return CountRailsUsingNodeFindObjects(node);
    }

    static IReadOnlyList<RailSpan2D> EnsureRails(IReadOnlyList<RailSpan2D> rails)
    {
        if (rails != null) return rails;

#if UNITY_2022_2_OR_NEWER
        return UnityEngine.Object.FindObjectsByType<RailSpan2D>(FindObjectsSortMode.None);
#else
    return UnityEngine.Object.FindObjectsOfType<RailSpan2D>();
#endif
    }

    // =========================================================
    // ✅ EnsureAttachedOrKeepExisting
    // - Revision/조건 달라지면 RESCAN
    // - 아니면 KEEP
    //   * KEEP에서 node==null 감지되면 nodeId로 복구 시도
    //   * 복구 실패(또는 null 존재)면 RESCAN 강제
    // =========================================================
    public static bool EnsureAttachedOrKeepExisting(
        PlacementObject po,
        float radius,
        LayerMask mask,
        IReadOnlyList<RailSpan2D> rails,
        bool debug = false
    )
    {
        rails = EnsureRails(rails);
        if (po == null) return false;

        var bind = po.GetComponent<RailNodeFollowBinding2D>();
        if (bind == null) return false;

        Physics2D.SyncTransforms();

        int curRev = RailGraphRevision.Value;
        bool needRescan =
            bind.builtRevision != curRev ||
            !Mathf.Approximately(bind.builtRadius, radius) ||
            bind.builtMaskValue != mask.value;

        // ✅ 추가: 바인딩에 null node가 하나라도 있으면 “복구/재스캔”이 필요함
        var entries0 = bind.Entries;
        if (!needRescan && entries0 != null && entries0.Count > 0)
        {
            for (int i = 0; i < entries0.Count; i++)
            {
                if (entries0[i].node == null)
                {
                    needRescan = true;
                    if (debug) Debug.Log($"[Binder][KEEP->RESCAN] po={po.name} reason=NULL_NODE entryIndex={i}", po);
                    break;
                }
            }
        }

        if (needRescan)
        {
            if (debug)
            {
                Debug.Log(
                    $"[Binder][KEEP->RESCAN] po={po.name} " +
                    $"builtRev={bind.builtRevision} curRev={curRev} " +
                    $"builtRadius={bind.builtRadius:0.###} radius={radius:0.###} " +
                    $"builtMask={bind.builtMaskValue} mask={mask.value} " +
                    $"rails={(rails == null ? -1 : rails.Count)}",
                    po
                );
            }

            return TryAttachAllNearestNodesBySnapPoints(po, radius, mask, rails: rails, debug: debug);
        }

        // ===== KEEP 시도 =====
        int ensured = 0;
        int conflict = 0;
        int nullNode = 0;

        int myId = po.GetInstanceID();

        var entries = bind.Entries;
        if (entries != null && entries.Count > 0)
        {
            bool changed = false;
            bool hasNull = false;

            var mgr = RailSnapNodeManager.Instance;

            // IReadOnlyList 이라 수정하려면 복제 후 SetEntries
            var newEntries = new List<RailNodeFollowBinding2D.Entry>(entries.Count);

            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];

                // ✅ node가 null이면 nodeId로 복구 시도
                if (e.node == null)
                {
                    hasNull = true;

                    if (mgr != null && !string.IsNullOrEmpty(e.nodeId))
                    {
                        var byId = mgr.FindById(e.nodeId);
                        if (byId != null)
                        {
                            e.node = byId;
                            changed = true;

                            if (debug) Debug.Log($"[Binder][KEEP][REPAIR] entry#{i} nodeId={e.nodeId} -> {byId.name}", po);
                        }
                    }
                }

                if (e.node == null)
                {
                    nullNode++;
                    newEntries.Add(e); // null 상태 유지(기대 연결 개수 유지)
                    continue;
                }

                if (e.anchorPoint != null && !e.anchorPoint.gameObject.activeInHierarchy)
                {
                    hasNull = true;
                    continue;
                }


                // ✅ 레일에 실제로 붙어있는 노드인지 재검증 (stale/복구노드 방지)
                int railsCountNow = CountRailsUsingNode(e.node, rails);
                if (railsCountNow <= 0 || railsCountNow > MAX_RAIL_DEGREE_FOR_OBJECT_ATTACH)
                {
                    // 이 노드는 "Follow 유지" 대상이 아님 → RESCAN 유도
                    hasNull = true; // (의미: KEEP만으로는 안정적으로 유지 못함)
                    if (debug)
                        Debug.Log($"[Binder][KEEP->RESCAN] po={po.name} reason=INVALID_RAILS_COUNT node={e.node.name} rails={railsCountNow}", po);

                    newEntries.Add(e); // 엔트리는 유지(개수 축소 방지)
                    continue;
                }


                // ✅ nodeId 비어있으면 채움(보험)
                if (string.IsNullOrEmpty(e.nodeId))
                {
                    e.node.EnsurePersistentId();
                    e.nodeId = e.node.PersistentId;
                    changed = true;
                }

                // ✅ ownerId 기록(보험)
                if (e.ownerId != myId)
                {
                    e.ownerId = myId;
                    changed = true;
                }

                var follow = e.node.GetComponent<RailNodeFollow2D>();
                if (follow == null) follow = e.node.gameObject.AddComponent<RailNodeFollow2D>();

                Transform expected = (e.anchorPoint != null) ? e.anchorPoint : po.transform;

                // ✅ 소유권/타겟 충돌 방지
                if (follow.ownerId != 0 && follow.ownerId != myId)
                {
                    conflict++;
                    if (debug) Debug.LogWarning($"[Binder][KEEP][CONFLICT][OWNER] node={e.node.name} ownerId={follow.ownerId} != myId={myId}", po);
                    newEntries.Add(e);
                    continue;
                }

                if (follow.target != null && !IsTargetBelongsToPO(follow.target, po))
                {
                    conflict++;
                    if (debug) Debug.LogWarning($"[Binder][KEEP][CONFLICT][TARGET] node={e.node.name} target={follow.target.name} != thisPO={po.name}", po);
                    newEntries.Add(e);
                    continue;
                }

                if (follow.target != expected || follow.ownerId != myId)
                    follow.Attach(expected, myId);

                ensured++;
                newEntries.Add(e);
            }

            if (changed)
                bind.SetEntries(newEntries);

            if (debug)
                Debug.Log($"[Binder][KEEP] po={po.name} ensured={ensured} conflict={conflict} nullNode={nullNode} entries={entries.Count}", po);

            // ✅ KEEP 결과가 0이거나, null 엔트리가 있었다면 RESCAN(복구 시도)
            if (ensured == 0 || hasNull || conflict > 0)
            {
                if (debug)
                    Debug.Log($"[Binder][KEEP->RESCAN] po={po.name} ensured={ensured} hasNull={hasNull} conflict={conflict} -> RESCAN", po);

                return TryAttachAllNearestNodesBySnapPoints(po, radius, mask, rails: rails, debug: debug);
            }

            return true;
        }

        // legacy 단일
        if (bind.node != null )
        {
            var follow = bind.node.GetComponent<RailNodeFollow2D>();
            if (follow == null) follow = bind.node.gameObject.AddComponent<RailNodeFollow2D>();

            Transform expected = (bind.anchorPoint != null) ? bind.anchorPoint : po.transform;

            if (follow.ownerId != 0 && follow.ownerId != myId)
            {
                if (debug) Debug.LogWarning($"[Binder][KEEP][CONFLICT][LEGACY][OWNER] node={bind.node.name} ownerId={follow.ownerId} != myId={myId}", po);
                return false;
            }

            if (follow.target != null && !IsTargetBelongsToPO(follow.target, po))
            {
                if (debug) Debug.LogWarning($"[Binder][KEEP][CONFLICT][LEGACY][TARGET] node={bind.node.name} target={follow.target.name} != thisPO={po.name}", po);
                return false;
            }

            if (follow.target != expected || follow.ownerId != myId)
                follow.Attach(expected, myId);

            if (debug) Debug.Log($"[Binder][KEEP][LEGACY] po={po.name} node={bind.node.name} ok=true", po);
            return true;
        }

        if (debug) Debug.Log($"[Binder][KEEP] po={po.name} ok=false (no entries)", po);
        return false;
    }

    // ✅ (호환 유지)
    public static bool EnsureAttachedOrKeepExisting(
        PlacementObject po,
        float radius,
        LayerMask mask,
        bool debug = false
    )
    {
        return EnsureAttachedOrKeepExisting(po, radius, mask, rails: null, debug: debug);
    }

    // =========================================================
    // ✅ Restore: nodeId 기반 복구 + 근접 Pick 복구
    // - restoredCount 반환
    // - restored==0 이면 false로 이어지게(상위에서 재시도/로그/리커버)
    // =========================================================
    static int RestoreFollowFromBinding(
        PlacementObject po,
        RailNodeFollowBinding2D bind,
        float radius,
        LayerMask mask,
        IReadOnlyList<RailSpan2D> rails,
        bool debug
    )
    {
        rails = EnsureRails(rails);

        if (po == null || bind == null) return 0;

        int myId = po.GetInstanceID();
        int restored = 0;

        var mgr = RailSnapNodeManager.Instance;
        var used = new HashSet<RailSnapNode2D>();

        var es = bind.Entries;
        if (es == null || es.Count == 0)
        {
            // 레거시 단일
            if (bind.node != null)
            {
                var follow = bind.node.GetComponent<RailNodeFollow2D>();
                if (follow != null && follow.ownerId != 0 && follow.ownerId != myId)
                    return 0;

                Transform t = (bind.anchorPoint != null) ? bind.anchorPoint : po.transform;

                if (follow != null && follow.target != null && !IsTargetBelongsToPO(follow.target, po))
                    return 0;

                if (follow == null) follow = bind.node.gameObject.AddComponent<RailNodeFollow2D>();
                follow.Attach(t, myId);
                return 1;
            }
            return 0;
        }

        // ✅ 엔트리 개수 유지한 채로 node 복구만 업데이트(축소 방지)
        var newEntries = new List<RailNodeFollowBinding2D.Entry>(es.Count);

        for (int i = 0; i < es.Count; i++)
        {
            var e = es[i];

            RailSnapNode2D node = e.node;

            // 1) nodeId로 복구
            if (node == null && mgr != null && !string.IsNullOrEmpty(e.nodeId))
                node = mgr.FindById(e.nodeId);

            // 2) 그래도 null이면 근접 Pick으로 복구(보험)
            if (node == null)
            {
                Vector2 pivot =
                    (e.anchorPoint != null) ? (Vector2)e.anchorPoint.position : (Vector2)po.transform.position;

                if (RailSnapNodeUtil.TryPickNode(pivot, radius, mask, out var picked, excludeNodes: used) && picked != null)
                    node = picked;
            }

            // node를 못 구하면 엔트리 유지(노드 null 유지)
            if (node == null)
            {
                newEntries.Add(e);
                continue;
            }

            used.Add(node);

            int railsCount = CountRailsUsingNode(node, rails);
            if (railsCount <= 0 || railsCount > MAX_RAIL_DEGREE_FOR_OBJECT_ATTACH)
            {
                // 레일에 안 붙은 노드면 복구 의미가 없으니 엔트리 유지
                e.node = node; // 그래도 참조는 넣어둠
                e.ownerId = myId;
                if (string.IsNullOrEmpty(e.nodeId))
                {
                    node.EnsurePersistentId();
                    e.nodeId = node.PersistentId;
                }
                newEntries.Add(e);
                continue;
            }

            Transform t = (e.anchorPoint != null) ? e.anchorPoint : po.transform;

            var follow = node.GetComponent<RailNodeFollow2D>();

            // ✅ 소유권 체크
            if (follow != null && follow.ownerId != 0 && follow.ownerId != myId)
            {
                if (debug) Debug.Log($"[Binder][Restore SKIP][OWNER] node={node.name} ownerId={follow.ownerId} != myId={myId}", po);
                e.node = node; // 참조만 갱신
                newEntries.Add(e);
                continue;
            }

            // ✅ 타겟 체크(레거시 보호)
            if (follow != null && follow.target != null && !IsTargetBelongsToPO(follow.target, po))
            {
                if (debug) Debug.Log($"[Binder][Restore SKIP][TARGET] node={node.name} target={follow.target.name} != thisPO={po.name}", po);
                e.node = node;
                newEntries.Add(e);
                continue;
            }

            if (follow == null) follow = node.gameObject.AddComponent<RailNodeFollow2D>();
            follow.Attach(t, myId);

            node.EnsurePersistentId();

            e.node = node;
            e.ownerId = myId;
            e.nodeId = node.PersistentId;

            restored++;

            if (debug)
                Debug.Log($"[Binder][Restore] po={po.name} entry#{i} node={node.name} nodeId={e.nodeId} restored={restored}", po);

            newEntries.Add(e);
        }

        // ✅ 복구 과정에서 node 참조/ID 갱신 반영
        bind.SetEntries(newEntries);

        return restored;
    }

    // =========================================================
    // ✅ Public wrappers
    // =========================================================
    public static bool TryAttachAllNearestNodesBySnapPoints(PlacementObject po, float radius, LayerMask mask)
    {
        return TryAttachAllNearestNodesBySnapPoints(po, radius, mask, rails: null, debug: false);
    }

    public static bool TryAttachAllNearestNodesBySnapPoints(PlacementObject po, float radius, LayerMask mask, bool debug)
    {
        return TryAttachAllNearestNodesBySnapPoints(po, radius, mask, rails: null, debug: debug);
    }

    public static bool TryAttachAllNearestNodesBySnapPoints(
        PlacementObject po,
        float radius,
        LayerMask mask,
        IReadOnlyList<RailSpan2D> rails,
        bool debug
    )
    {
        if (po == null) return false;
        if (radius <= 0f) return false;
        if (mask.value == 0) return false;

        rails = EnsureRails(rails);

        int myId = po.GetInstanceID();


        Physics2D.SyncTransforms();

        // ✅ 기존 바인딩 스냅샷(개수)
        var existing = po.GetComponent<RailNodeFollowBinding2D>();
        int existingCount = 0;
        if (existing != null)
        {
            if (existing.Entries != null && existing.Entries.Count > 0) existingCount = existing.Entries.Count;
            else if (existing.node != null) existingCount = 1;
        }

        var points = po.GetComponentsInChildren<SnapPoint>(true);

        var plannedEntries = new List<RailNodeFollowBinding2D.Entry>(4);
        var plannedPairs = new List<(RailSnapNode2D node, Transform anchor)>(4);
        var usedNodes = new HashSet<RailSnapNode2D>();

        bool hadAnyConnector = false;

        // =========================
        // 1) PLAN 단계
        // =========================
        for (int i = 0; i < points.Length; i++)
        {
            var sp = points[i];
            if (sp == null) continue;

            if (!sp.gameObject.activeInHierarchy) continue;
            if (!sp.isActiveAndEnabled) continue;

            if (sp.role != SnapPointRole.Connector) continue;

            hadAnyConnector = true;

            Vector2 pivot = sp.transform.position;

            // ✅ 기존 바인딩에서 "이 SnapPoint(anchor)"에 붙던 노드를 우선 후보로
            RailSnapNode2D preferredNode = null;
            string preferredNodeId = null;

            if (existing != null && existing.Entries != null && existing.Entries.Count > 0)
            {
                for (int ei = 0; ei < existing.Entries.Count; ei++)
                {
                    var e0 = existing.Entries[ei];

                    if (e0.anchorPoint == sp.transform)
                    {
                        preferredNode = e0.node;
                        preferredNodeId = e0.nodeId;

                        // node가 null이면 nodeId로 복구 시도
                        if (preferredNode == null && !string.IsNullOrEmpty(preferredNodeId))
                        {
                            var mgr0 = RailSnapNodeManager.Instance;
                            if (mgr0 != null)
                                preferredNode = mgr0.FindById(preferredNodeId);
                        }
                        break;
                    }
                }
            }

            if (!TryPickBestBindableNode(
                    pivot, radius, mask,
                    po, myId,
                    usedNodes, rails,
                    out var node,
                    preferredNode,
                    preferredNodeId,
                    debug: debug
                ) || node == null)
            {
                continue;
            }

            // ✅ 이미 다른 PO가 점유중이면 스킵 (절대 뺏지 않기)
            var followExisting = node.GetComponent<RailNodeFollow2D>();
            if (followExisting != null)
            {
                if (followExisting.ownerId != 0 && followExisting.ownerId != myId)
                {
                    continue;
                }

                if (followExisting.target != null && !IsTargetBelongsToPO(followExisting.target, po))
                {
                    continue;
                }
            }

            int railsCount = CountRailsUsingNode(node, rails);
            if (railsCount <= 0)
            {
                continue;
            }

            if (railsCount > MAX_RAIL_DEGREE_FOR_OBJECT_ATTACH)
            {
                continue;
            }

            // ✅ 이제 "진짜로" plan에 포함될 때만 used 등록
            usedNodes.Add(node);

            node.EnsurePersistentId();

            plannedPairs.Add((node, sp.transform));
            plannedEntries.Add(new RailNodeFollowBinding2D.Entry
            {
                node = node,
                anchorPoint = sp.transform,
                localOffset = (Vector2)po.transform.InverseTransformPoint(sp.transform.position),
                ownerId = myId,
                nodeId = node.PersistentId
            });
        }

        // fallback: 커넥터가 없으면 PO 중심
        if (!hadAnyConnector)
        {
            Vector2 pivot = po.transform.position;

            if (RailSnapNodeUtil.TryPickNode(pivot, radius, mask, out var node) && node != null)
            {
                
                int railsCount = CountRailsUsingNode(node, rails);
                if (railsCount > 0 && railsCount <= MAX_RAIL_DEGREE_FOR_OBJECT_ATTACH)
                {
                    var followExisting = node.GetComponent<RailNodeFollow2D>();
                    bool ok = true;

                    if (followExisting != null)
                    {
                        if (followExisting.ownerId != 0 && followExisting.ownerId != myId) ok = false;
                        else if (followExisting.target != null && !IsTargetBelongsToPO(followExisting.target, po)) ok = false;
                    }

                    if (ok)
                    {
                        node.EnsurePersistentId();

                        plannedPairs.Add((node, po.transform));
                        plannedEntries.Add(new RailNodeFollowBinding2D.Entry
                        {
                            node = node,
                            anchorPoint = po.transform,
                            localOffset = Vector2.zero,
                            ownerId = myId,
                            nodeId = node.PersistentId
                        });

                    }
                }
                
            }
        }

        // ✅ 계획이 0이면: 기존 바인딩 있으면 복구 시도
        if (plannedEntries.Count == 0)
        {
            if (existingCount > 0)
            {
                int restored = RestoreFollowFromBinding(po, existing, radius, mask, rails, debug);
                return restored > 0;
            }

            return false;
        }

        // ✅ 기존보다 적게 잡히면 축소 커밋 금지
        if (existingCount > 0 && plannedEntries.Count < existingCount)
        {
            int restored = RestoreFollowFromBinding(po, existing, radius, mask, rails, debug);
            return restored > 0;
        }

        // =========================
        // 2) COMMIT 단계 (SWAP COMMIT)
        // =========================

        // 0) 이전 바인딩 스냅샷 (obsolete follow 제거용)
        var oldBind = po.GetComponent<RailNodeFollowBinding2D>();
        List<RailNodeFollowBinding2D.Entry> oldEntriesSnapshot = null;

        if (oldBind != null && oldBind.Entries != null && oldBind.Entries.Count > 0)
        {
            oldEntriesSnapshot = new List<RailNodeFollowBinding2D.Entry>(oldBind.Entries.Count);
            for (int i = 0; i < oldBind.Entries.Count; i++)
                oldEntriesSnapshot.Add(oldBind.Entries[i]);
        }
        else if (oldBind != null && oldBind.node != null)
        {
            oldEntriesSnapshot = new List<RailNodeFollowBinding2D.Entry>(1);
            var n = oldBind.node;
            if (n != null)
            {
                n.EnsurePersistentId();
                oldEntriesSnapshot.Add(new RailNodeFollowBinding2D.Entry
                {
                    node = n,
                    anchorPoint = oldBind.anchorPoint,
                    localOffset = Vector2.zero,
                    ownerId = myId,
                    nodeId = n.PersistentId
                });
            }
        }

        // 1) planned 노드 집합
        var plannedNodeSet = new HashSet<RailSnapNode2D>();
        for (int i = 0; i < plannedPairs.Count; i++)
        {
            if (plannedPairs[i].node != null)
                plannedNodeSet.Add(plannedPairs[i].node);
        }

        // 2) 먼저 Attach를 끊지 않고 시도
        int attached = 0;

        for (int i = 0; i < plannedPairs.Count; i++)
        {
            var pair = plannedPairs[i];
            var node = pair.node;
            var anchor = pair.anchor;

            if (node == null || anchor == null) continue;

            var follow = node.GetComponent<RailNodeFollow2D>();
            if (follow == null) follow = node.gameObject.AddComponent<RailNodeFollow2D>();

            // ✅ 경쟁/충돌 최종 체크
            if (follow.ownerId != 0 && follow.ownerId != myId)
            {
                continue;
            }
            if (follow.target != null && !IsTargetBelongsToPO(follow.target, po))
            {
                continue;
            }

            follow.Attach(anchor, myId);
            attached++;
        }

        Physics2D.SyncTransforms();

        // ✅ “부분 Attach 성공”은 entries 세팅하면 위험 → 전부 붙었을 때만 커밋
        if (attached < plannedPairs.Count)
        {
            if (debug)

            return false;
        }

        // ✅ 기존이 있었다면 축소 방지 (보험)
        if (existingCount > 0 && attached < existingCount)
        {
            return false;
        }

        // 3) entries 교체(확정)
        var bind = po.GetComponent<RailNodeFollowBinding2D>();
        if (bind == null) bind = po.gameObject.AddComponent<RailNodeFollowBinding2D>();

        bind.SetEntries(plannedEntries);
        bind.builtRevision = RailGraphRevision.Value;
        bind.builtRadius = radius;
        bind.builtMaskValue = mask.value;

        // ✅ 추가: 커밋 즉시 노드 위치/그래프 갱신을 한 번에 끝냄(드래그 없이도 즉시 정상)
        bind.SyncNow(syncPhysics: true);

        // 4) 이제 “안 쓰게 된 노드”만 follow 제거 (내 ownerId인 것만)
        if (oldEntriesSnapshot != null)
        {
            for (int i = 0; i < oldEntriesSnapshot.Count; i++)
            {
                var e = oldEntriesSnapshot[i];
                var n = e.node;
                if (n == null) continue;

                if (plannedNodeSet.Contains(n))
                    continue;

                var f = n.GetComponent<RailNodeFollow2D>();
                if (f != null && f.ownerId == myId)
                {
                    // ✅ 즉시 점유 해제(이 프레임에서 다른 PO가 바로 잡을 수 있게)
                    f.Detach();
                    Object.Destroy(f);
                }
            }
        }


        return true;
    }
    // =========================================================
    // ✅ Detach: 내가 붙였던 Follow만 제거 (ownerId 기반)
    // =========================================================
    public static void Detach(PlacementObject po)
    {
        if (po == null) return;
        Debug.LogWarning($"[Binder][DETACH CALLED] po={po.name} myId={po.GetInstanceID()} frame={Time.frameCount}", po);
        int myId = po.GetInstanceID();
        if (!_detaching.Add(myId)) return;

        try
        {
            var bind = po.GetComponent<RailNodeFollowBinding2D>();
            if (bind == null) return;

            var entries = bind.Entries;

            if (entries != null && entries.Count > 0)
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    var e = entries[i];
                    if (e.node == null) continue;

                    var follow = e.node.GetComponent<RailNodeFollow2D>();
                    if (follow == null) continue;

                    if (follow.ownerId == myId)
                    {
                        follow.Detach();          // ✅ 즉시 점유 해제
                        Object.Destroy(follow);
                    }

                }
            }
            else
            {
                if (bind.node != null)
                {
                    var follow = bind.node.GetComponent<RailNodeFollow2D>();
                    if (follow != null && follow.ownerId == myId)
                        Object.Destroy(follow);
                }
            }

            bind.Clear();
            Object.Destroy(bind);
        }
        finally
        {
            _detaching.Remove(myId);
        }
    }

    static RailSnapNode2D FindNearestAnyNodeIncludingFollow(Vector2 worldPos)
    {
        var mgr = RailSnapNodeManager.Instance;
        if (mgr == null) return null;

        Physics2D.SyncTransforms();

        // mergeRadius/railNodeMask는 매니저 설정 그대로 사용
        int n = Physics2D.OverlapCircleNonAlloc(worldPos, mgr.mergeRadius, _nodeHitsAny, mgr.railNodeMask);
        if (n <= 0) return null;

        RailSnapNode2D best = null;
        float bestDist = float.MaxValue;

        for (int i = 0; i < n; i++)
        {
            var col = _nodeHitsAny[i];
            if (col == null) continue;

            var node = col.GetComponentInParent<RailSnapNode2D>();
            if (node == null) continue;

            float d = ((Vector2)node.transform.position - worldPos).sqrMagnitude;
            if (d < bestDist)
            {
                bestDist = d;
                best = node;
            }
        }

        return best;
    }

    static bool TryPickBestBindableNode(
    Vector2 pivot,
    float radius,
    LayerMask mask,
    PlacementObject po,
    int myId,
    HashSet<RailSnapNode2D> excludeNodes,
    IReadOnlyList<RailSpan2D> rails,
    out RailSnapNode2D bestNode,
    RailSnapNode2D preferredNode,
    string preferredNodeId,
    bool debug = false
)
    {
        bestNode = null;
        if (po == null) return false;
        if (radius <= 0f) return false;
        if (mask.value == 0) return false;

        float radiusSq = radius * radius;

        bool IsExcluded(RailSnapNode2D n)
            => (excludeNodes != null && n != null && excludeNodes.Contains(n));

        bool IsBindable(RailSnapNode2D n)
        {
            if (n == null) return false;
            if (IsExcluded(n)) return false;

            // 반경 체크
            if (((Vector2)n.transform.position - pivot).sqrMagnitude > radiusSq)
                return false;

            // 레일 연결 체크(스테일 노드 방지)
            int railsCount = CountRailsUsingNode(n, rails);
            if (railsCount <= 0) return false;
            if (railsCount > MAX_RAIL_DEGREE_FOR_OBJECT_ATTACH) return false;

            // 소유권/타겟 충돌 체크 (절대 뺏지 않기)
            var follow = n.GetComponent<RailNodeFollow2D>();
            if (follow != null)
            {
                if (follow.ownerId != 0 && follow.ownerId != myId)
                    return false;

                if (follow.target != null && !IsTargetBelongsToPO(follow.target, po))
                    return false;
            }

            return true;
        }

        // =========================================================
        // 0) preferredNodeId로 “직접” 복구 시도 (Overlap 의존 X)
        // =========================================================
        if (!string.IsNullOrEmpty(preferredNodeId))
        {
            var mgr = RailSnapNodeManager.Instance;
            if (mgr != null)
            {
                var byId = mgr.FindById(preferredNodeId);
                if (byId != null && IsBindable(byId))
                {
                    bestNode = byId;
                    if (debug) Debug.Log($"[Binder][Pick] preferredNodeId HIT -> {bestNode.name}", po);
                    return true;
                }
            }
        }

        // =========================================================
        // 1) preferredNode 우선 시도
        // =========================================================
        if (preferredNode != null && IsBindable(preferredNode))
        {
            bestNode = preferredNode;
            if (debug) Debug.Log($"[Binder][Pick] preferredNode OK -> {bestNode.name}", po);
            return true;
        }

        // =========================================================
        // 2) 주변에서 최적 후보(가까운 것) 탐색
        //    + Overlap 후보 중 preferredNodeId가 있으면 최우선 채택
        // =========================================================
        Physics2D.SyncTransforms();
        int nHits = Physics2D.OverlapCircleNonAlloc(pivot, radius, _pickHits, mask);
        if (nHits <= 0) return false;

        float bestDist = float.MaxValue;

        for (int i = 0; i < nHits; i++)
        {
            var col = _pickHits[i];
            if (col == null) continue;

            var node = col.GetComponentInParent<RailSnapNode2D>();
            if (node == null) continue;

            if (!IsBindable(node))
                continue;

            // ✅ Overlap 후보 중에서도 preferredNodeId 매칭이면 즉시 선택
            if (!string.IsNullOrEmpty(preferredNodeId))
            {
                node.EnsurePersistentId();
                if (node.PersistentId == preferredNodeId)
                {
                    bestNode = node;
                    if (debug) Debug.Log($"[Binder][Pick] overlap preferredNodeId MATCH -> {bestNode.name}", po);
                    return true;
                }
            }

            float d = ((Vector2)node.transform.position - pivot).sqrMagnitude;
            if (d < bestDist)
            {
                bestDist = d;
                bestNode = node;
            }
        }

        if (debug && bestNode != null)
            Debug.Log($"[Binder][Pick] best={bestNode.name} dist={Mathf.Sqrt(bestDist):0.###}", po);

        return bestNode != null;
    }




}
