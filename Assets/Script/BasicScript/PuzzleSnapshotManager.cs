using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
/// <summary>
/// 간단한 "스냅샷 리셋" 매니저.
/// - PO(PlacementObject): placementData + pose + (선택) Rail 바인딩
/// - Rail(RailSpan2D): 양 끝 노드/좌표 기반으로 재생성
///
/// 참고:
/// RailSnapNode2D의 PersistentId는 런타임 재생성 시 달라질 수 있어,
/// 복원 중 (oldNodeId -> newNode) 매핑을 만들어 바인딩 복원에 사용한다.
/// </summary>
public class PuzzleSnapshotManager : MonoBehaviour
{
    public static bool SuppressDeleteSfx { get; private set; }
    public static bool IsBulkClearingPlaced { get; private set; }

    public event System.Action OnRestoreCompleted;
    public bool IsRestoring => _isRestoring;

    [Header("PO Snapshot")]
    [SerializeField] List<PlacementSnapshot> snapshot = new();

    [Header("Rail Snapshot")]
    [Tooltip("레일 복원을 위해 기본 레일 프리팹을 지정하세요. (레일 타입이 1종이면 1개면 충분)")]
    [SerializeField] GameObject railPrefab;

    [SerializeField] List<RailSpanSnapshot> railSnapshot = new();

    [Header("Reset Options")]
    [SerializeField] bool waitOneFrameAfterDestroy = true;
    [SerializeField] bool blockReentry = true;


    [Header("FixedRoot (Never Delete / Never Save)")]
    [Tooltip("FixedRoot 아래에 있는 PlacementObject는 스냅샷 저장/복원 과정에서 삭제/재생성하지 않습니다. 비워두면 fixedRootName으로 자동 탐색합니다.")]
    [SerializeField] Transform fixedRoot;
    [SerializeField] string fixedRootName = "FixedRoot";

    [SerializeField] bool clearSnapshotOnSceneChanged = true;
    [SerializeField] bool autoSaveBaselineOnSceneChanged = true; // 새 스테이지 진입 시 baseline 자동 저장(추천)

    [Header("Clear Placed Button")]
    [Tooltip("true면 '전부 삭제' 버튼에서 구슬도 함께 제거합니다(선택).")]
    [SerializeField] bool clearMarblesOnClearPlaced = false;



    string _lastSceneName;

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void Start()
    {
        TryInitializeBaselineForCurrentStage();
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    List<PlacementObject> GetRegisteredPlacementObjects(bool includeFallback = true)
    {
        var result = new List<PlacementObject>(128);

        var registry = StageObjectRegistry.Instance;
        if (registry != null)
        {
            registry.CleanupNulls();

            var list = registry.PlacementObjects;
            for (int i = 0; i < list.Count; i++)
            {
                var po = list[i];
                if (po == null) continue;
                result.Add(po);
            }

            return result;
        }

        if (includeFallback)
        {
            var found = FindObjectsOfType<PlacementObject>();
            for (int i = 0; i < found.Length; i++)
            {
                var po = found[i];
                if (po == null) continue;
                result.Add(po);
            }
        }

        return result;
    }

    List<RailSpan2D> GetRegisteredRails(bool includeFallback = true)
    {
        var result = new List<RailSpan2D>(256);

        var registry = StageObjectRegistry.Instance;
        if (registry != null)
        {
            registry.CleanupNulls();

            var list = registry.Rails;
            for (int i = 0; i < list.Count; i++)
            {
                var rail = list[i];
                if (rail == null) continue;
                result.Add(rail);
            }

            return result;
        }

        if (includeFallback)
        {
            var found = FindObjectsOfType<RailSpan2D>();
            for (int i = 0; i < found.Length; i++)
            {
                var rail = found[i];
                if (rail == null) continue;
                result.Add(rail);
            }
        }

        return result;
    }

    void TryInitializeBaselineForCurrentStage()
    {
        string mySceneName = gameObject.scene.name;
        string currentStageId = StageContext.CurrentStageId;

        bool isCurrentStage =
            !string.IsNullOrEmpty(currentStageId)
            ? string.Equals(mySceneName, currentStageId, System.StringComparison.Ordinal)
            : string.Equals(mySceneName, SceneManager.GetActiveScene().name, System.StringComparison.Ordinal);

        if (!isCurrentStage)
            return;

        bool hasAnySnapshot =
            (snapshot != null && snapshot.Count > 0) ||
            (railSnapshot != null && railSnapshot.Count > 0);

        if (!hasAnySnapshot && autoSaveBaselineOnSceneChanged)
        {
            Save();
#if UNITY_EDITOR
            Debug.Log($"[PuzzleSnapshotManager] Baseline auto-saved at Start. scene={mySceneName}");
#endif
        }

        _lastSceneName = mySceneName;
    }

    void OnSceneLoaded(Scene s, LoadSceneMode m)
    {
        if (!clearSnapshotOnSceneChanged) return;

        string mySceneName = gameObject.scene.name;

        // 내 씬과 무관한 로드는 무시
        if (!string.Equals(s.name, mySceneName, System.StringComparison.Ordinal))
            return;

        if (string.Equals(_lastSceneName, s.name, System.StringComparison.Ordinal))
            return;

        _lastSceneName = s.name;

        ClearSnapshots();

        if (autoSaveBaselineOnSceneChanged)
        {
            Save();
#if UNITY_EDITOR
            Debug.Log($"[PuzzleSnapshotManager] Baseline auto-saved on sceneLoaded. scene={s.name}");
#endif
        }
    }

    public void ClearSnapshots()
    {
        snapshot?.Clear();
        railSnapshot?.Clear();
        _restoredNodeMap?.Clear();
        _isRestoring = false;

#if UNITY_EDITOR
        Debug.Log("[PuzzleSnapshotManager] Cleared snapshots (scene changed).");
#endif
    }


    bool _isRestoring;

    // 복원 중: 저장된 nodeId -> 새로 생성/확보된 node
    readonly Dictionary<string, RailSnapNode2D> _restoredNodeMap = new Dictionary<string, RailSnapNode2D>(256);


    Transform GetFixedRoot()
    {
        if (fixedRoot != null) return fixedRoot;
        if (!string.IsNullOrEmpty(fixedRootName))
        {
            var go = GameObject.Find(fixedRootName);
            if (go != null) return go.transform;
        }
        return null;
    }

    bool IsUnderFixedRoot(Transform t)
    {
        var fr = GetFixedRoot();
        if (fr == null || t == null) return false;
        return t == fr || t.IsChildOf(fr);
    }

    // =============================
    // ResetNow: Save -> Restore (Safe)
    // =============================
    public void ResetNow()
    {
        if (blockReentry && _isRestoring) return;

        Save();

        StartCoroutine(CoRestore_Safe());
    }

    // =============================
    // Legacy / Wrapper API (호환용)
    // =============================
    public void Restore() => RestoreNow();

    public void RestoreNow()
    {
        if (blockReentry && _isRestoring) return;

        StartCoroutine(CoRestore_Safe());
    }

    public void SaveAndRestore() => ResetNow();

    // =============================
    // CLEAR PLACED (Rails + PO)
    // - Rails: 전부 삭제
    // - PO: FixedRoot 아래는 유지, 그 외 전부 삭제
    // =============================
    public void ClearPlacedNow()
    {
        if (blockReentry && _isRestoring) return;
        StartCoroutine(CoClearPlaced());
    }

    IEnumerator CoClearPlaced()
    {
        _isRestoring = true;
        SuppressDeleteSfx = true;
        IsBulkClearingPlaced = true;

        try
        {
            var gridPlacer = FindFirstObjectByType<GridPlacer>();
            var railTool = FindFirstObjectByType<RailToolPlacer2D>();

            var currentPO = GetRegisteredPlacementObjects();
            for (int i = 0; i < currentPO.Count; i++)
            {
                var po = currentPO[i];
                if (po == null) continue;
                if (po.gameObject.layer == LayerMask.NameToLayer("Ghost")) continue;

                if (IsUnderFixedRoot(po.transform))
                    continue;

                if (gridPlacer != null) gridPlacer.DeletePlacementObjectForReset(po);
                else Destroy(po.gameObject);
            }

            var currentRails = GetRegisteredRails();
            for (int i = 0; i < currentRails.Count; i++)
            {
                var r = currentRails[i];
                if (r == null) continue;
                if (r.gameObject.layer == LayerMask.NameToLayer("Ghost")) continue;

                int railId = r.gameObject.GetInstanceID();

                bool containsNow = RailEdgeRegistry2D.Contains(r.StartWorld, r.EndWorld);
                bool hasOwnerNow = RailEdgeRegistry2D.DebugHasOwner(railId);
                Debug.Log($"[ClearPlaced] BeforeDelete railId={railId} contains={containsNow} hasOwner={hasOwnerNow} keys={RailEdgeRegistry2D.DebugKeyCount}");

                RailEdgeRegistry2D.Unregister(railId);

                if (railTool != null) railTool.DeleteRailForReset(r);
                else Destroy(r.gameObject);
            }

            if (clearMarblesOnClearPlaced)
            {
                var marbles = GameObject.FindGameObjectsWithTag("Marble");
                for (int i = 0; i < marbles.Length; i++)
                    if (marbles[i] != null) Destroy(marbles[i]);
            }

            RailSnapNodeManager.Instance?.DestroyRuntimeNodesUnderManager();
            RailCellMap2D.Instance?.ResetAndRescanRails();

            if (waitOneFrameAfterDestroy)
                yield return null;

            RailSnapNodeManager.Instance?.RebuildCachesFromScene();
            GridOccupancy2D.Instance?.ForceRebuildNow();

            if (railTool != null) railTool.SyncRailBudgetFromScene();
            else RailBudget2D.Instance?.SyncUsedWithScene();

            RailSnapNodeManager.Instance?.RebuildCachesFromScene();
            GridOccupancy2D.Instance?.ForceRebuildNow();

            SyncAllRailSnapNodes();

            if (railTool != null) railTool.SyncRailBudgetFromScene();
            else RailBudget2D.Instance?.SyncUsedWithScene();


        }
        finally
        {
            IsBulkClearingPlaced = false;
            SuppressDeleteSfx = false;
            _isRestoring = false;
        }

        // ✅ 전체 삭제 완료 후 현재 상태를 "한 번만" undo 히스토리에 남기기
        var ssm = FindFirstObjectByType<StageSaveManager>();
        if (ssm != null)
        {
            ssm.NotifyStageChanged();
        }
    }

    // =============================
    // SAVE
    // =============================
    public void Save()
    {
        snapshot.Clear();
        railSnapshot.Clear();

        int ghostLayer = LayerMask.NameToLayer("Ghost");

        // ---- PO ----
        var placedObjects = GetRegisteredPlacementObjects();
        for (int i = 0; i < placedObjects.Count; i++)
        {
            var po = placedObjects[i];
            if (po == null) continue;
            if (po.gameObject.layer == ghostLayer) continue;

            if (po.placementData == null || po.placementData.prefab == null) continue;

            var ps = new PlacementSnapshot
            {
                placementData = po.placementData,
                position = po.transform.position,
                rotation = po.transform.rotation,
                localScale = po.transform.localScale,
                railBindings = null,
                underFixedRoot = IsUnderFixedRoot(po.transform),
                strengthLevel = -1
            };

            var strength = po.GetComponent<StrengthBasedOccupancyCells>();
            if (strength != null && po.placementData != null && po.placementData.allowStrengthControl)
                ps.strengthLevel = strength.CurrentLevel;

            var bind = po.GetComponent<RailNodeFollowBinding2D>();
            if (bind != null && bind.Entries != null && bind.Entries.Count > 0)
            {
                bind.BakeLocalOffsetsFromCurrent();
                var list = new List<RailBindingSnapshot>(bind.Entries.Count);

                for (int e = 0; e < bind.Entries.Count; e++)
                {
                    var ent = bind.Entries[e];

                    if (ent.node != null && string.IsNullOrEmpty(ent.nodeId))
                    {
                        ent.node.EnsurePersistentId();
                        ent.nodeId = ent.node.PersistentId;
                    }

                    if (string.IsNullOrEmpty(ent.nodeId))
                        continue;

                    var path = SnapshotPathUtil.GetPath(po.transform, ent.anchorPoint);
                    if (path == null) continue;

                    var nodeWorld = (ent.node != null) ? ent.node.WorldPos : (Vector2)ent.anchorPoint.position;

                    list.Add(new RailBindingSnapshot
                    {
                        nodeId = ent.nodeId,
                        anchorPath = path,
                        localOffset = ent.localOffset,
                        nodeWorldPos = nodeWorld
                    });
                }

                if (list.Count > 0)
                    ps.railBindings = list;
            }

            snapshot.Add(ps);
        }

        // ---- Rails ----
        var rails = GetRegisteredRails();
        for (int i = 0; i < rails.Count; i++)
        {
            var r = rails[i];
            if (r == null) continue;
            if (r.gameObject.layer == ghostLayer) continue;

            var a = r.startNode;
            var b = r.endNode;

            string aId = null;
            string bId = null;
            Vector2 aPos = r.StartWorld;
            Vector2 bPos = r.EndWorld;

            if (a != null)
            {
                a.EnsurePersistentId();
                aId = a.PersistentId;
                aPos = a.WorldPos;
            }
            if (b != null)
            {
                b.EnsurePersistentId();
                bId = b.PersistentId;
                bPos = b.WorldPos;
            }

            railSnapshot.Add(new RailSpanSnapshot
            {
                railTypeId = null,
                startWorld = aPos,
                endWorld = bPos,
                startNodeId = aId,
                endNodeId = bId
            });
        }

#if UNITY_EDITOR
        int fixedCount = 0;
        for (int i = 0; i < snapshot.Count; i++)
            if (snapshot[i] != null && snapshot[i].underFixedRoot) fixedCount++;

        Debug.Log($"[PuzzleSnapshotManager] Saved PO={snapshot.Count} (fixedRoot={fixedCount}), Rails={railSnapshot.Count}");
#endif
    }

    // =============================
    // RESTORE (Coroutine Safe)
    // =============================
    IEnumerator CoRestore_Safe()
    {
        _isRestoring = true;
        SuppressDeleteSfx = true;
        StageSaveManager.PushSuppressStageChangedNotify();
        _restoredNodeMap.Clear();

        try
        {
            var gridPlacer = FindFirstObjectByType<GridPlacer>();
            var railTool = FindFirstObjectByType<RailToolPlacer2D>();

            var current = GetRegisteredPlacementObjects();
            for (int i = 0; i < current.Count; i++)
            {
                var po = current[i];
                if (po == null) continue;

                if (gridPlacer != null) gridPlacer.DeletePlacementObjectForReset(po);
                else Destroy(po.gameObject);
            }

            var currentRails = GetRegisteredRails();
            for (int i = 0; i < currentRails.Count; i++)
            {
                var r = currentRails[i];
                if (r == null) continue;
                if (r.gameObject.layer == LayerMask.NameToLayer("Ghost")) continue;

                if (railTool != null) railTool.DeleteRailForReset(r);
                else Destroy(r.gameObject);
            }

            // 2) 구슬 제거
            var marbles = GameObject.FindGameObjectsWithTag("Marble");
            for (int i = 0; i < marbles.Length; i++)
            {
                if (marbles[i] != null)
                    Destroy(marbles[i]);
            }

            RailSnapNodeManager.Instance?.DestroyRuntimeNodesUnderManager();
            RailCellMap2D.Instance?.ResetAndRescanRails();

            if (waitOneFrameAfterDestroy)
                yield return null;

            RailSnapNodeManager.Instance?.RebuildCachesFromScene();

            // 3) PO 복원
            var spawnedPO = new List<(PlacementObject po, List<RailBindingSnapshot> binds)>(snapshot.Count);

            for (int i = 0; i < snapshot.Count; i++)
            {
                var snap = snapshot[i];
                if (snap == null || snap.placementData == null || snap.placementData.prefab == null)
                    continue;

                GameObject obj = Instantiate(snap.placementData.prefab, snap.position, snap.rotation);

                if (snap.underFixedRoot)
                {
                    var fr = GetFixedRoot();
                    if (fr != null) obj.transform.SetParent(fr, worldPositionStays: true);
                }

                obj.transform.localScale = (snap.localScale == default) ? obj.transform.localScale : snap.localScale;

                var po = obj.GetComponent<PlacementObject>();
                if (po != null)
                    po.placementData = snap.placementData;

                var strength = obj.GetComponent<StrengthBasedOccupancyCells>();
                if (strength != null && snap.placementData != null && snap.placementData.allowStrengthControl)
                {
                    int targetLevel = snap.strengthLevel > 0
                        ? snap.strengthLevel
                        : snap.placementData.defaultStrengthLevel;

                    strength.SetLevel(targetLevel);
                }

                var rb = obj.GetComponent<Rigidbody2D>();
                if (rb != null)
                {
                    rb.bodyType = RigidbodyType2D.Kinematic;
                    rb.gravityScale = 0;
                    rb.simulated = true;
                }

                var cols = obj.GetComponentsInChildren<Collider2D>(true);
                for (int c = 0; c < cols.Length; c++)
                {
                    if (cols[c] != null) cols[c].enabled = true;
                }

                if (po != null)
                    spawnedPO.Add((po, snap.railBindings));
            }

            RestoreRailsAndBuildNodeMap();

            RailCellMap2D.Instance?.ResetAndRescanRails();
            SyncAllRailSnapNodes();

            if (railTool != null) railTool.SyncRailBudgetFromScene();
            else RailBudget2D.Instance?.SyncUsedWithScene();

            for (int i = 0; i < spawnedPO.Count; i++)
            {
                var (po, binds) = spawnedPO[i];
                if (po == null || binds == null || binds.Count == 0) continue;
                SnapshotRailBindingUtil.ApplyRailBindings(po, binds, _restoredNodeMap);
            }

            var goals = FindObjectsOfType<GoalZoneRotate>(true);
            for (int i = 0; i < goals.Length; i++)
            {
                if (goals[i] != null)
                    goals[i].ResetToInitial();
            }

            GridOccupancy2D.Instance?.ForceRebuildNow();
        }
        finally
        {
            StageSaveManager.PopSuppressStageChangedNotify();
            SuppressDeleteSfx = false;
            _isRestoring = false;
        }

        OnRestoreCompleted?.Invoke();
    }
    void RestoreRailsAndBuildNodeMap()
    {
        if (railSnapshot == null || railSnapshot.Count == 0) return;

        if (railPrefab == null)
        {
            Debug.LogWarning("[PuzzleSnapshotManager] railPrefab is null. Rails won't be restored.");
            return;
        }

        var railPrefabSpan = railPrefab.GetComponent<RailSpan2D>();
        if (railPrefabSpan == null)
        {
            Debug.LogError("[PuzzleSnapshotManager] railPrefab has no RailSpan2D.");
            return;
        }

        var grid = FindFirstObjectByType<GridManager>();
        var mgr = RailSnapNodeManager.Instance;

        for (int i = 0; i < railSnapshot.Count; i++)
        {
            var rs = railSnapshot[i];
            if (rs == null) continue;

            // 노드 확보 (가능하면 nodeId 기준 매핑을 위해 좌표 기반으로 생성)
            RailSnapNode2D a = null;
            RailSnapNode2D b = null;

            if (mgr != null)
            {
                // 우선: 기존에 같은 id를 가진 노드가 있다면 사용
                if (!string.IsNullOrEmpty(rs.startNodeId)) a = mgr.FindById(rs.startNodeId);
                if (!string.IsNullOrEmpty(rs.endNodeId)) b = mgr.FindById(rs.endNodeId);

                // 폴백: 좌표로 노드 생성/확보
                if (a == null) a = mgr.GetOrCreate(rs.startWorld, asAnchorRoot: false);
                if (b == null) b = mgr.GetOrCreate(rs.endWorld, asAnchorRoot: false);
            }

            // 매핑 기록 (id가 있으면 old->new)
            if (a != null && !string.IsNullOrEmpty(rs.startNodeId) && !_restoredNodeMap.ContainsKey(rs.startNodeId))
                _restoredNodeMap.Add(rs.startNodeId, a);
            if (b != null && !string.IsNullOrEmpty(rs.endNodeId) && !_restoredNodeMap.ContainsKey(rs.endNodeId))
                _restoredNodeMap.Add(rs.endNodeId, b);

            // 레일 생성
            var go = Instantiate(railPrefab);
            var span = go.GetComponent<RailSpan2D>();
            if (span == null)
            {
                Destroy(go);
                continue;
            }

            if (a != null && b != null)
                span.InitializeNodes(grid, a, b);
            else
                span.Initialize(grid, rs.startWorld, rs.endWorld);
        }
    }

    void SyncAllRailSnapNodes()
    {
        var nodes = FindObjectsByType<RailSnapNode2D>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < nodes.Length; i++)
        {
            var node = nodes[i];
            if (node == null) continue;

            node.RebuildConnectedRailsFromScene();
            node.RefreshVisualState();
        }
    }
}

// =========================================================
// Helpers (snapshot)
// =========================================================
static class SnapshotPathUtil
{
    /// <summary>
    /// root 기준 target의 Transform 경로("A/B/C").
    /// - target==null => "" (루트)
    /// - target이 root 밖이면 null
    /// </summary>
    public static string GetPath(Transform root, Transform target)
    {
        if (root == null) return null;
        if (target == null) return "";
        if (target == root) return "";

        var stack = new Stack<string>();
        var t = target;
        while (t != null && t != root)
        {
            stack.Push(t.name);
            t = t.parent;
        }

        if (t != root) return null;
        return string.Join("/", stack);
    }
}

static class SnapshotRailBindingUtil
{
    public static void ApplyRailBindings(
        PlacementObject po,
        List<RailBindingSnapshot> binds,
        Dictionary<string, RailSnapNode2D> restoredNodeMap
    )
    {
        if (po == null || binds == null || binds.Count == 0) return;

        var bindComp = po.GetComponent<RailNodeFollowBinding2D>();
        if (bindComp == null) bindComp = po.gameObject.AddComponent<RailNodeFollowBinding2D>();

        int myId = po.GetInstanceID();

        var mgr = RailSnapNodeManager.Instance;
        var entries = new List<RailNodeFollowBinding2D.Entry>(binds.Count);

        for (int i = 0; i < binds.Count; i++)
        {
            var b = binds[i];
            if (b == null) continue;

            RailSnapNode2D node = null;

            // 1) 복원 과정에서 만든 매핑 우선
            if (restoredNodeMap != null && !string.IsNullOrEmpty(b.nodeId))
                restoredNodeMap.TryGetValue(b.nodeId, out node);

            // 2) 매니저에서 id로 찾기
            if (node == null && mgr != null && !string.IsNullOrEmpty(b.nodeId))
                node = mgr.FindById(b.nodeId);

            // 3) 폴백: 좌표로 생성/확보
            if (node == null && mgr != null)
                node = mgr.GetOrCreate(b.nodeWorldPos, asAnchorRoot: false);

            Transform anchor = string.IsNullOrEmpty(b.anchorPath) ? po.transform : po.transform.Find(b.anchorPath);
            if (anchor == null) anchor = po.transform; // 보험

            entries.Add(new RailNodeFollowBinding2D.Entry
            {
                node = node,
                anchorPoint = anchor,
                localOffset = b.localOffset,
                ownerId = myId,
                nodeId = b.nodeId
            });
        }

        bindComp.SetEntries(entries);
        // 노드 위치 즉시 맞추기
        bindComp.SyncNow(syncPhysics: false, broadcastMoved: false);
    }


}

