using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

/// <summary>
/// ✅ Stage File Save/Load (V2 ONLY)
/// - PO(PlacementObject): placementDataId + position/rotation/localScale(=flip) + physicsType + railBindings + embeddedAnchorNodes
/// - Rails(RailSpanSnapshot): start/end + nodeId
/// - Legacy 포맷/별도 rails 파일 ❌
/// </summary>
public class StageSaveManager : MonoBehaviour
{
    [Header("Development")]
    [SerializeField] bool disableAutoSaveLoadInEditor = true;

    #region Inspector

    [Header("Refs")]
    [SerializeField] PlacementDataCatalog catalog;

    [Header("Rails")]
    [Tooltip("레일 복원을 위해 기본 레일 프리팹을 지정하세요. (레일 타입이 1종이면 1개면 충분)")]
    [SerializeField] GameObject railPrefab;

    [Header("Options")]
    [SerializeField] bool autoLoadOnStart = true;

    [Header("Save Guard")]
    [SerializeField] float minSaveInterval = 0.1f; // 연속 저장 방지(초)

    [Header("Restore (Snapshot-style)")]
    [SerializeField] bool restoreLikePuzzleSnapshot = true;
    [SerializeField] bool waitOneFrameAfterDestroy = true;
    [SerializeField] bool clearMarblesOnLoad = true;

    [Header("FixedRoot (Never Delete / Never Save)")]
    [Tooltip("FixedRoot 아래에 있는 PlacementObject는 저장/복원 과정에서 삭제/재생성하지 않습니다. 비워두면 fixedRootName으로 자동 탐색합니다.")]
    [SerializeField] Transform fixedRoot;
    [SerializeField] string fixedRootName = "FixedRoot";

    [Header("Restore Hooks")]
    [Tooltip("복원 시작/종료 동안 입력을 막기 위해, 아래 컴포넌트들을 임시로 disable 합니다. 비워두면 자동 탐색합니다.")]
    [SerializeField] MonoBehaviour[] inputBlockTargets;

    [Tooltip("복원이 끝났을 때(PO/레일/바인딩/점유 재빌드까지 완료) 호출됩니다.")]
    public UnityEvent OnStageRestoredUnityEvent = new UnityEvent();

    [Header("Stage Save Revision")]
    [SerializeField] StageSaveRevisionDatabase stageSaveRevisionDatabase;

    [Header("Demo / Full Save Separation")]
    [SerializeField] string fullSaveFolderName = "Stages";
    [SerializeField] string demoSaveFolderName = "DemoStages";


    // ✅ Undo 복원일 때만 바인딩 Sync를 복원 후로 미루기
    bool _deferBindingSyncUntilPostRestore = false;
    readonly List<PlacementObject> _postRestoreBindingSyncTargets = new List<PlacementObject>(64);

#if UNITY_EDITOR
    [Header("DEV (Editor Only)")]
    [SerializeField] bool enableDevHotkeys = true;
    [SerializeField] KeyCode saveKey = KeyCode.F5;
    [SerializeField] KeyCode loadKey = KeyCode.F9;
    [SerializeField] KeyCode deleteKey = KeyCode.F8;
#endif

    bool AutoSaveLoadBlocked
    {
        get
        {
#if UNITY_EDITOR
            return disableAutoSaveLoadInEditor;
#else
        return false;
#endif
        }
    }

    [Header("Exit/Scene Save Hooks")]
    [SerializeField] bool saveOnDisable = true;
    [SerializeField] bool saveOnAppQuit = true;
    [SerializeField] bool saveOnAppPauseOrFocusLost = true;
    [SerializeField] bool saveOnSceneChange = true;

    [Header("StageId Filter")]
    [SerializeField] string stageScenePrefix = "Stage";

    [Header("Editor Focus Save (Recommended OFF)")]
    [SerializeField] bool enableFocusSaveInEditor = false;

    #endregion

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

    #region Events / State

    /// <summary>코드에서 구독할 수 있는 복원 완료 이벤트</summary>
    public event Action OnStageRestored;

    bool _isRestoring;
    bool _inputBlocked;
    readonly List<(MonoBehaviour comp, bool wasEnabled)> _blockedStates = new();

    float lastSaveTime = -999f;
    bool _hasSavedOnQuit;

    // ✅ Exit-save gate: 스테이지가 안정화된 뒤에만 종료/씬전환 저장 허용
    bool _stageReadyForExitSaves;

    // queued save
    bool saveQueued;

    // autosave debounce
    int dirtyVersion;
    Coroutine autosaveCo;
    string autosaveStageId;

    bool _commitEventQueued = false;
    int _queuedCommitVersion = -1;

    public static bool IsRestoringNow { get; private set; }
    public static bool IsRestoreStabilizingNow { get; private set; }
    string StageId => ResolveStageId();

    string ResolveStageId()
    {
        // 1) StageContext 우선
        string id = StageContext.CurrentStageId;
        if (IsPlayableStageId(id))
            return id;

        // 2) 이 StageSaveManager가 들어있는 자기 씬 이름
        var myScene = gameObject.scene;
        if (myScene.IsValid() && myScene.isLoaded && IsPlayableStageId(myScene.name))
            return myScene.name;

        // 3) 마지막 폴백: ActiveScene
        var active = SceneManager.GetActiveScene();
        if (active.IsValid() && active.isLoaded && IsPlayableStageId(active.name))
            return active.name;

        return null;
    }
    #endregion

    #region File Format (V2)

    [Serializable]
    class StageSaveFileV2
    {
        public int version = 2;
        public string stageId;
        public int stageRevision = 0;
        public List<PlacementSnapshotV2> objects = new();
        public List<RailSpanSnapshot> rails = new();
    }

    [Serializable]
    class PlacementSnapshotV2
    {
        public string persistentId;
        public string placementDataId;
        public Vector3 position;
        public float rotationZ;
        public Vector3 localScale;
        public PhysicsType physicsType;

        public int strengthLevel = -1;

        public List<RailBindingSnapshot> railBindings = new();

        public List<EmbeddedSnapNodeSnapshot> embeddedAnchorNodes = new();
    }

    [Serializable]
    class EmbeddedSnapNodeSnapshot
    {
        public string path;   // po.transform 기준 relative path
        public string nodeId; // RailSnapNode2D.PersistentId
    }

    // restore 과정에서 oldNodeId -> newNode 매핑 (레일/바인딩 복원용)
    readonly Dictionary<string, RailSnapNode2D> _restoredNodeMap = new Dictionary<string, RailSnapNode2D>(256);

    #endregion

    #region Unity Lifecycle

    void OnEnable()
    {
        if (saveOnSceneChange)
            SceneManager.activeSceneChanged += HandleActiveSceneChanged;
    }

    void Start()
    {
        if (AutoSaveLoadBlocked || !autoLoadOnStart)
        {
            _stageReadyForExitSaves = true;
            return;
        }

        StartCoroutine(CoAutoLoadOnStart());
    }

    void Update()
    {
#if UNITY_EDITOR
        if (!enableDevHotkeys) return;

        if (Input.GetKeyDown(saveKey))
            TrySaveNow(StageId);

        if (Input.GetKeyDown(loadKey))
            LoadCurrentStage();

        if (Input.GetKeyDown(deleteKey))
            DeleteCurrentSave();
#endif
    }

    void OnDisable()
    {
        if (!AutoSaveLoadBlocked && saveOnDisable)
            SafeForceSave("OnDisable");

        if (saveOnSceneChange)
            SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
    }

    void OnApplicationQuit()
    {
        if (AutoSaveLoadBlocked) return;
        if (!saveOnAppQuit) return;
        if (_hasSavedOnQuit) return;

        SafeForceSave("OnApplicationQuit");
        _hasSavedOnQuit = true;
    }

    void OnApplicationPause(bool pause)
    {
        if (AutoSaveLoadBlocked) return;
        if (!saveOnAppPauseOrFocusLost) return;
        if (pause) SafeForceSave("OnApplicationPause(true)");
    }

    void OnApplicationFocus(bool hasFocus)
    {
        if (AutoSaveLoadBlocked) return;
        if (!saveOnAppPauseOrFocusLost) return;

#if UNITY_EDITOR
        if (!enableFocusSaveInEditor) return;
#endif

        if (!hasFocus) SafeForceSave("OnApplicationFocus(false)");
    }

    #endregion

    #region Scene Transition Hooks

    void HandleActiveSceneChanged(Scene oldScene, Scene newScene)
    {
        if (AutoSaveLoadBlocked)
            return;

        var oldId = oldScene.IsValid() ? oldScene.name : null;
        if (_stageReadyForExitSaves && IsPlayableStageId(oldId))
            SafeForceSaveWithStageId("activeSceneChanged(old->save)", oldId);

        var newId = newScene.IsValid() ? newScene.name : null;
        if (autoLoadOnStart && IsPlayableStageId(newId))
            StartCoroutine(CoAutoLoadForStageOnSceneEnter(newId));
    }

    IEnumerator CoAutoLoadForStageOnSceneEnter(string stageId)
    {

        _stageReadyForExitSaves = false;

        yield return null;
        yield return new WaitForEndOfFrame();

        if (!IsPlayableStageId(stageId)) yield break;

        LoadOrEmpty(stageId);
        _stageReadyForExitSaves = true;

#if UNITY_EDITOR
        Debug.Log($"[StageSave] AutoLoaded on scene enter: {stageId}");
#endif
    }

    IEnumerator CoAutoLoadOnStart()
    {

        // StageContext/SceneFlow가 stageId 세팅할 시간을 준다
        yield return null;
        yield return new WaitForEndOfFrame();

        var stageId = StageId;
        if (string.IsNullOrEmpty(stageId))
        {
#if UNITY_EDITOR
            Debug.LogWarning("[StageSave] AutoLoad skipped: StageId is empty on Start. (StageContext not ready yet?)");
#endif
            yield break;
        }

        LoadOrEmpty(stageId);
        _stageReadyForExitSaves = true;
    }

    #endregion

    #region Public API

    public void SaveCurrentStage() => TrySaveNow(StageId);

    /// <summary>
    /// ✅ 강제 저장: minSaveInterval 가드를 무시하고 즉시 저장합니다.
    /// (씬 전환 직전/강제 저장 버튼 등)
    /// </summary>
    public void ForceSaveCurrentStage() => ForceSaveNow(StageId);

    public bool ForceSaveNow(string stageId)
    {
        if (string.IsNullOrEmpty(stageId)) return false;
        if (_isRestoring) return false;

        lastSaveTime = Time.unscaledTime;
        Save(stageId);
        return true;
    }

    public void LoadCurrentStage()
    {
        var stageId = StageId;

        if (string.IsNullOrEmpty(stageId))
        {
            Debug.LogWarning("[StageSave] LoadCurrentStage failed: stageId is empty.");
            return;
        }

        LoadOrEmpty(stageId);
    }

    /// <summary>
    /// 즉시 저장(씬 전환 직전/ESC 나가기 등). minSaveInterval 가드 적용.
    /// stageId를 파라미터로 받아 "캡처된 stageId"로 저장한다.
    /// </summary>
    public bool TrySaveNow(string stageId)
    {

        if (string.IsNullOrEmpty(stageId)) return false;
        if (_isRestoring) return false;

        var now = Time.unscaledTime;
        if (now - lastSaveTime < minSaveInterval)
            return false;

        lastSaveTime = now;
        Save(stageId);
        return true;
    }

    /// <summary>
    /// 여러 군데에서 막 호출해도 프레임당 1번만 저장되도록 큐잉.
    /// (변경 이벤트에서 호출용)
    /// </summary>
    public void RequestSave()
    {
        if (_isRestoring) return;
        if (saveQueued) return;

        var stageId = StageId;
        if (string.IsNullOrEmpty(stageId)) return;

        saveQueued = true;
        StartCoroutine(CoSaveEndOfFrame(stageId));
    }

    public void NotifyStageChanged()
    {
        if (_isRestoring) return;
        if (SuppressStageChangedNotify) return;

        if (_deferStageChangedCount > 0)
        {
            _pendingStageChanged = true;
            return;
        }

        NotifyStageChangedImmediate();
    }

    void NotifyStageChangedImmediate()
    {
        var stageId = StageId;
        if (string.IsNullOrEmpty(stageId)) return;

        dirtyVersion++;

        if (autosaveCo != null && autosaveStageId != stageId)
        {
            StopCoroutine(autosaveCo);
            autosaveCo = null;
        }

        autosaveStageId = stageId;

        if (autosaveCo == null)
            autosaveCo = StartCoroutine(CoAutoSaveDebounce(stageId));

        QueueCommittedEvent();
    }

    int _deferStageChangedCount = 0;
    bool _pendingStageChanged = false;

    #endregion

    #region Save / Load (V2 only)

    public void Save(string stageId)
    {
        if (string.IsNullOrEmpty(stageId)) return;

        var dataV2 = BuildSaveFileV2(stageId);
        var json = JsonUtility.ToJson(dataV2, false);

        var path = GetPath(stageId);
        var tmpPath = path + ".tmp";
        var bakPath = path + ".bak";

        try
        {
            // 1) tmp에 먼저 쓰기
            File.WriteAllText(tmpPath, json);

            // 2) 기존 파일이 있으면 bak 생성 + 원자적 교체(File.Replace)
            if (File.Exists(path))
                File.Replace(tmpPath, path, bakPath, ignoreMetadataErrors: true);
            else
                File.Move(tmpPath, path);

#if UNITY_EDITOR
            Debug.Log($"[StageSave] Saved(V2): {path} (objects={dataV2.objects?.Count ?? 0}, rails={dataV2.rails?.Count ?? 0})");
#endif
        }
        catch (Exception e)
        {
            Debug.LogError($"[StageSave] Save failed: {e}\npath={path}");
            try { if (File.Exists(tmpPath)) File.Delete(tmpPath); } catch { }
        }
    }

    public void LoadOrEmpty(string stageId)
    {
        if (string.IsNullOrEmpty(stageId)) return;

        if (catalog == null)
        {
            Debug.LogError("[StageSave] catalog is null. Cannot restore.");
            return;
        }

        var path = GetPath(stageId);
        var tmpPath = path + ".tmp";
        var bakPath = path + ".bak";

        if (File.Exists(tmpPath))
        {
            try { File.Delete(tmpPath); } catch { }
        }

        if (!File.Exists(path))
            return;

        StageSaveFileV2 dataV2 = null;

        try
        {
            var json = File.ReadAllText(path);
            dataV2 = JsonUtility.FromJson<StageSaveFileV2>(json);

            if (dataV2 == null || dataV2.version != 2 || dataV2.objects == null || dataV2.rails == null)
                throw new Exception("Invalid V2 save data.");
        }
        catch (Exception e)
        {
            Debug.LogError($"[StageSave] Load/Parse failed: {e}\npath={path}");

            if (File.Exists(bakPath))
            {
                try
                {
                    var bakJson = File.ReadAllText(bakPath);
                    var bakV2 = JsonUtility.FromJson<StageSaveFileV2>(bakJson);

                    if (bakV2 != null && bakV2.version == 2 && bakV2.objects != null && bakV2.rails != null)
                    {
                        dataV2 = bakV2;

                        try { File.Copy(bakPath, path, overwrite: true); } catch { }

                        Debug.LogWarning($"[StageSave] Recovered from .bak: {bakPath}");
                    }
                }
                catch (Exception be)
                {
                    Debug.LogError($"[StageSave] .bak recovery failed: {be}\npath={bakPath}");
                }
            }

            if (dataV2 == null)
                return;
        }

        int currentRevision = GetCurrentStageRevision(stageId);

        if (dataV2.stageRevision != currentRevision)
        {
            Debug.Log($"[StageSave] Revision mismatch. stageId={stageId}, saveRevision={dataV2.stageRevision}, currentRevision={currentRevision}. Save will be deleted.");
            DeleteSaveByStageId(stageId);
            return;
        }

        StartCoroutine(CoLoadRestoreFromFile(dataV2));
    }

    IEnumerator CoLoadRestoreFromFile(StageSaveFileV2 dataV2)
    {
        _isRestoring = true;
        IsRestoringNow = true;
        PushSuppressStageChangedNotify();

        PreCleanupForRestore();

        if (waitOneFrameAfterDestroy)
        {
            yield return null;
            yield return new WaitForEndOfFrame();
        }

        _deferBindingSyncUntilPostRestore = false;
        _postRestoreBindingSyncTargets.Clear();

        TryRebuildFromSaveFileV2(dataV2, "Restore failed");

        PopSuppressStageChangedNotify();
        _isRestoring = false;
        IsRestoringNow = false;
    }

    bool TryRebuildFromSaveFileV2(StageSaveFileV2 data, string errorContext)
    {
        try
        {
            RebuildFromSaveFileV2(data);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[StageSave] {errorContext}: {e}");
            return false;
        }
    }

    #endregion

    #region Save Build (V2)

    StageSaveFileV2 BuildSaveFileV2(string stageId)
    {
        var data = new StageSaveFileV2
        {
            stageId = stageId,
            stageRevision = GetCurrentStageRevision(stageId)
        };

        int ghostLayer = LayerMask.NameToLayer("Ghost");

        // 1) Objects + railBindings + embedded anchor nodes
        var objs = GetRegisteredPlacementObjects();

        for (int i = 0; i < objs.Count; i++)
        {
            var po = objs[i];
            if (po == null) continue;
            if (po.gameObject.layer == ghostLayer) continue;
            if (IsUnderFixedRoot(po.transform)) continue;

            po.EnsureId();

            if (po.placementData == null) continue;
            if (string.IsNullOrEmpty(po.placementData.id)) continue;

            var snap = new PlacementSnapshotV2
            {
                persistentId = po.PersistentId,
                placementDataId = po.placementData.id,
                position = po.transform.position,
                rotationZ = po.transform.eulerAngles.z,
                localScale = po.transform.localScale,
                physicsType = po.physicsType,
            };

            var strength = po.GetComponent<StrengthBasedOccupancyCells>();
            if (strength != null && po.placementData != null && po.placementData.allowStrengthControl)
                snap.strengthLevel = strength.CurrentLevel;

            var bind = po.GetComponent<RailNodeFollowBinding2D>();
            if (bind != null && bind.Entries != null && bind.Entries.Count > 0)
            {
                bind.BakeLocalOffsetsFromCurrent();

                for (int e = 0; e < bind.Entries.Count; e++)
                {
                    var ent = bind.Entries[e];

                    if (ent.node != null && string.IsNullOrEmpty(ent.nodeId))
                    {
                        ent.node.EnsurePersistentId();
                        ent.nodeId = ent.node.PersistentId;
                    }

                    if (string.IsNullOrEmpty(ent.nodeId)) continue;

                    var anchorPath = GetPath(po.transform, ent.anchorPoint);
                    if (anchorPath == null) continue;

                    var nodeWorld = (ent.node != null)
                        ? ent.node.WorldPos
                        : (Vector2)(ent.anchorPoint != null ? ent.anchorPoint.position : po.transform.position);

                    snap.railBindings.Add(new RailBindingSnapshot
                    {
                        nodeId = ent.nodeId,
                        anchorPath = anchorPath,
                        localOffset = ent.localOffset,
                        nodeWorldPos = nodeWorld,
                    });
                }
            }

            CaptureEmbeddedAnchorNodes(po, snap);
            data.objects.Add(snap);
        }

        // 2) Rails
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

            data.rails.Add(new RailSpanSnapshot
            {
                railTypeId = null,
                startWorld = aPos,
                endWorld = bPos,
                startNodeId = aId,
                endNodeId = bId
            });
        }

        return data;
    }

    #endregion

    #region Restore (V2)

    void RestoreFromSaveFileV2(StageSaveFileV2 data)
    {
        PreCleanupForRestore();
        RebuildFromSaveFileV2(data);
    }

    void PreCleanupForRestore()
    {
        _restoredNodeMap.Clear();
        _postRestoreBindingSyncTargets.Clear();

        int ghostLayer = LayerMask.NameToLayer("Ghost");

        // 1) 기존 바인딩 흔적 정리
        var existingPOs = GetRegisteredPlacementObjects();
        for (int i = 0; i < existingPOs.Count; i++)
        {
            var po = existingPOs[i];
            if (po == null) continue;
            if (IsUnderFixedRoot(po.transform)) continue;

            var bind = po.GetComponent<RailNodeFollowBinding2D>();
            if (bind != null)
                RailNodeSnapBinder.Detach(po);
        }

        // 2) 기존 레일 먼저 삭제
        var currentRails = GetRegisteredRails();
        for (int i = 0; i < currentRails.Count; i++)
        {
            var r = currentRails[i];
            if (r == null) continue;
            if (r.gameObject.layer == ghostLayer) continue;

            r.startNode?.UnregisterRail(r);
            r.endNode?.UnregisterRail(r);

            Destroy(r.gameObject);
        }

        // 3) 기존 PO 삭제
        for (int i = 0; i < existingPOs.Count; i++)
        {
            var po = existingPOs[i];
            if (po == null) continue;
            if (IsUnderFixedRoot(po.transform)) continue;

            po.gameObject.SetActive(false);
            Destroy(po.gameObject);
        }
    }

    void RebuildFromSaveFileV2(StageSaveFileV2 data)
    {
        // 1) PO 스폰
        if (data.objects != null)
        {
            for (int i = 0; i < data.objects.Count; i++)
            {
                var obj = data.objects[i];
                if (obj == null) continue;
                if (string.IsNullOrEmpty(obj.placementDataId)) continue;

                if (!string.IsNullOrEmpty(obj.persistentId) && FindExistingFixedRootPO(obj.persistentId) != null)
                    continue;

                if (!catalog.TryGet(obj.placementDataId, out var pd)) continue;
                if (pd == null || pd.prefab == null) continue;

                var go = Instantiate(pd.prefab, obj.position, Quaternion.Euler(0, 0, obj.rotationZ));
                var po = go.GetComponent<PlacementObject>();
                if (po == null)
                {
                    Destroy(go);
                    continue;
                }

                po.placementData = pd;
                po.SetPersistentId(obj.persistentId);
                po.AutoRailAttach = true;

                po.physicsType = obj.physicsType;

                if (obj.localScale.sqrMagnitude > 0.0001f)
                    go.transform.localScale = obj.localScale;

                // ✅ 강도 먼저 복원
                var strength = po.GetComponent<StrengthBasedOccupancyCells>();
                if (strength != null && pd != null && pd.allowStrengthControl)
                {
                    int targetLevel = obj.strengthLevel > 0 ? obj.strengthLevel : pd.defaultStrengthLevel;
                    strength.SetLevel(targetLevel);
                }

                Physics2D.SyncTransforms();

                // ✅ 강도 적용 후, 실제 살아있는 앵커/스냅포인트 기준으로 복원
                RestoreEmbeddedAnchorNodes(po, obj);
            }
        }

        // 2) Rails 복원
        RestoreRailsFromList(data.rails);

        // 3) PO-rail 바인딩 복원
        RestoreRailBindingsFromV2(data.objects);

        // 4) 후처리
        StartCoroutine(CoSyncRailBudgetNextFrame());
        ForceRebuildOccupancy();
    }

    void RestoreRailsFromList(List<RailSpanSnapshot> rails)
    {
        int ghostLayer = LayerMask.NameToLayer("Ghost");

        // 1) 기존 레일은 항상 먼저 정리
        var currentRails = GetRegisteredRails();
        for (int i = 0; i < currentRails.Count; i++)
        {
            var r = currentRails[i];
            if (r == null) continue;
            if (r.gameObject.layer == ghostLayer) continue;

            r.startNode?.UnregisterRail(r);
            r.endNode?.UnregisterRail(r);

            Destroy(r.gameObject);
        }

        if (rails == null || rails.Count == 0)
            return;

        if (railPrefab == null)
        {
            Debug.LogWarning("[StageSave] railPrefab is null. Rails won't be restored.");
            return;
        }

        if (railPrefab.GetComponent<RailSpan2D>() == null)
        {
            Debug.LogError("[StageSave] railPrefab has no RailSpan2D.");
            return;
        }

        var grid = FindFirstObjectByType<GridManager>();
        var mgr = RailSnapNodeManager.Instance;

        for (int i = 0; i < rails.Count; i++)
        {
            var rs = rails[i];
            if (rs == null) continue;

            RailSnapNode2D a = null;
            RailSnapNode2D b = null;

            bool aFromAnchorMap = false;
            bool bFromAnchorMap = false;

            if (!string.IsNullOrEmpty(rs.startNodeId) && _restoredNodeMap.TryGetValue(rs.startNodeId, out a) && a != null)
                aFromAnchorMap = true;

            if (!string.IsNullOrEmpty(rs.endNodeId) && _restoredNodeMap.TryGetValue(rs.endNodeId, out b) && b != null)
                bFromAnchorMap = true;

            if (mgr != null)
            {
                if (!aFromAnchorMap && a == null && !string.IsNullOrEmpty(rs.startNodeId))
                    a = mgr.FindById(rs.startNodeId);

                if (!bFromAnchorMap && b == null && !string.IsNullOrEmpty(rs.endNodeId))
                    b = mgr.FindById(rs.endNodeId);

                if (a == null) a = mgr.GetOrCreate(rs.startWorld, asAnchorRoot: false);
                if (b == null) b = mgr.GetOrCreate(rs.endWorld, asAnchorRoot: false);
            }

            if (a != null && !string.IsNullOrEmpty(rs.startNodeId) && !_restoredNodeMap.ContainsKey(rs.startNodeId))
                _restoredNodeMap.Add(rs.startNodeId, a);

            if (b != null && !string.IsNullOrEmpty(rs.endNodeId) && !_restoredNodeMap.ContainsKey(rs.endNodeId))
                _restoredNodeMap.Add(rs.endNodeId, b);

            if (a != null && !a.IsAnchor)
                a.transform.position = rs.startWorld;

            if (b != null && !b.IsAnchor)
                b.transform.position = rs.endWorld;

            Physics2D.SyncTransforms();

            var go = Instantiate(railPrefab);
            var span = go.GetComponent<RailSpan2D>();
            if (span == null) { Destroy(go); continue; }

            if (a != null && b != null)
                span.InitializeNodes(grid, a, b);
            else
                span.Initialize(grid, rs.startWorld, rs.endWorld);

            if (span.startNode != null) span.startNode.RegisterRail(span);
            if (span.endNode != null) span.endNode.RegisterRail(span);
        }
    }

    void RestoreRailBindingsFromV2(List<PlacementSnapshotV2> objects)
    {
        if (objects == null || objects.Count == 0) return;

        var poMap = new Dictionary<string, PlacementObject>(256);
        var all = GetRegisteredPlacementObjects();

        for (int i = 0; i < all.Count; i++)
        {
            var po = all[i];
            if (po == null) continue;

            po.EnsureId();
            if (string.IsNullOrEmpty(po.PersistentId)) continue;

            poMap[po.PersistentId] = po;
        }

        var mgr = RailSnapNodeManager.Instance;


        for (int i = 0; i < objects.Count; i++)
        {
            var o = objects[i];
            if (o == null || string.IsNullOrEmpty(o.persistentId)) continue;
            if (o.railBindings == null || o.railBindings.Count == 0) continue;

            if (!poMap.TryGetValue(o.persistentId, out var po) || po == null)
                continue;

            var bindComp = po.GetComponent<RailNodeFollowBinding2D>();
            if (bindComp == null) bindComp = po.gameObject.AddComponent<RailNodeFollowBinding2D>();

            int myId = po.GetInstanceID();
            var entries = new List<RailNodeFollowBinding2D.Entry>(o.railBindings.Count);

            for (int b = 0; b < o.railBindings.Count; b++)
            {
                var s = o.railBindings[b];
                if (s == null) continue;

                RailSnapNode2D node = null;

                if (!string.IsNullOrEmpty(s.nodeId))
                    _restoredNodeMap.TryGetValue(s.nodeId, out node);

                if (node == null && mgr != null && !string.IsNullOrEmpty(s.nodeId))
                    node = mgr.FindById(s.nodeId);

                if (node == null && mgr != null)
                    node = mgr.GetOrCreate(s.nodeWorldPos, asAnchorRoot: false);

                // ✅ 중요:
                // 바인딩으로 복원되는 movable node는 저장된 월드 위치로 먼저 되돌린다.
                // Anchor node는 PO 내부 고정점이므로 건드리지 않는다.
                if (node != null && !node.IsAnchor)
                    node.transform.position = s.nodeWorldPos;

                Transform anchor = string.IsNullOrEmpty(s.anchorPath)
                    ? po.transform
                    : po.transform.Find(s.anchorPath);

                // ✅ 원래 앵커가 없으면 그 엔트리는 버린다
                if (anchor == null)
                    continue;

                entries.Add(new RailNodeFollowBinding2D.Entry
                {
                    node = node,
                    anchorPoint = anchor,
                    localOffset = s.localOffset,
                    ownerId = myId,
                    nodeId = s.nodeId
                });
            }

            Physics2D.SyncTransforms();
            bindComp.SetEntries(entries);

            // Follow 재부착
            for (int e = 0; e < entries.Count; e++)
            {
                var ent = entries[e];
                if (ent.node == null) continue;
                if (ent.node.IsAnchor) continue;

                var follow = ent.node.GetComponent<RailNodeFollow2D>();
                if (follow == null)
                    follow = ent.node.gameObject.AddComponent<RailNodeFollow2D>();

                // ✅ Undo/Restore에서는 기존 ownerId를 믿지 말고 현재 PO 기준으로 강제 재부착
                follow.Detach();
                follow.Attach(ent.anchorPoint != null ? ent.anchorPoint : po.transform, myId);
                follow.runtimeFollowEnabled = false;
            }

            // ✅ 일반 Load는 기존처럼 즉시 Sync
            // ✅ Undo 복원만 post-restore 단계로 미룸
            if (_deferBindingSyncUntilPostRestore)
            {
                if (!_postRestoreBindingSyncTargets.Contains(po))
                    _postRestoreBindingSyncTargets.Add(po);
            }
            else
            {
                bindComp.SyncNow(syncPhysics: false, broadcastMoved: false);
            }
        }
    }

    void ForceRebuildOccupancy()
    {
        var occ = FindFirstObjectByType<GridOccupancy2D>();
        if (occ == null) return;

        occ.MarkDirty();
        occ.ForceRebuildNow();
    }

    #endregion

    #region Embedded Anchor Node Save/Restore

    static void TrySetNodePersistentId(RailSnapNode2D node, string id)
    {
        if (node == null || string.IsNullOrEmpty(id)) return;

        try
        {
            // 1) method: SetPersistentId(string)
            var mi = node.GetType().GetMethod(
                "SetPersistentId",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic
            );
            if (mi != null)
            {
                mi.Invoke(node, new object[] { id });
                return;
            }

            // 2) property: PersistentId { set; }
            var pi = node.GetType().GetProperty(
                "PersistentId",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic
            );
            if (pi != null && pi.CanWrite)
            {
                pi.SetValue(node, id);
                return;
            }

            // 3) field fallback
            var fi = node.GetType().GetField("persistentId", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
                  ?? node.GetType().GetField("_persistentId", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
                  ?? node.GetType().GetField("id", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);

            if (fi != null) fi.SetValue(node, id);
        }
        catch { /* ignore */ }
    }

    void CaptureEmbeddedAnchorNodes(PlacementObject po, PlacementSnapshotV2 snap)
    {
        if (po == null || snap == null) return;

        var nodes = po.GetComponentsInChildren<RailSnapNode2D>(true);
        if (nodes == null || nodes.Length == 0) return;

        for (int i = 0; i < nodes.Length; i++)
        {
            var n = nodes[i];
            if (n == null) continue;
            if (!n.IsAnchor) continue; // PO 내부 "앵커" 노드만 저장

            var path = GetPath(po.transform, n.transform);
            if (path == null) continue;

            var id = n.PersistentId;
            if (string.IsNullOrEmpty(id))
            {
                id = Guid.NewGuid().ToString("N");
                TrySetNodePersistentId(n, id);
            }

            snap.embeddedAnchorNodes.Add(new EmbeddedSnapNodeSnapshot
            {
                path = path,
                nodeId = id
            });
        }
    }

    void RestoreEmbeddedAnchorNodes(PlacementObject po, PlacementSnapshotV2 snap)
    {
        if (po == null || snap == null) return;
        if (snap.embeddedAnchorNodes == null || snap.embeddedAnchorNodes.Count == 0) return;

        for (int i = 0; i < snap.embeddedAnchorNodes.Count; i++)
        {
            var s = snap.embeddedAnchorNodes[i];
            if (s == null || string.IsNullOrEmpty(s.nodeId)) continue;

            var t = string.IsNullOrEmpty(s.path) ? po.transform : po.transform.Find(s.path);
            if (t == null) continue;

            var node = t.GetComponent<RailSnapNode2D>();
            if (node == null) continue;

            TrySetNodePersistentId(node, s.nodeId);

            if (!_restoredNodeMap.ContainsKey(s.nodeId))
                _restoredNodeMap.Add(s.nodeId, node);
        }
    }

    #endregion

    #region Save Scheduling

    IEnumerator CoSaveEndOfFrame(string stageId)
    {
        yield return new WaitForEndOfFrame();
        saveQueued = false;

        TrySaveNow(stageId); // 캡처된 stageId로 저장
    }

    IEnumerator CoAutoSaveDebounce(string stageId)
    {
        while (true)
        {
            int myVersion = dirtyVersion;

            // minSaveInterval 충족까지 대기
            float targetTime = lastSaveTime + minSaveInterval;
            float wait = targetTime - Time.unscaledTime;
            if (wait > 0f)
                yield return new WaitForSecondsRealtime(wait);

            // 프레임 끝에서 저장(연속 변경 정리)
            yield return new WaitForEndOfFrame();

            // 그 사이 변경이 또 들어오면 다시 루프
            if (myVersion != dirtyVersion)
                continue;

            lastSaveTime = Time.unscaledTime;
            Save(stageId);

#if UNITY_EDITOR
            Debug.Log($"[StageSave] Autosaved (stageId={stageId}).");
#endif

            if (myVersion == dirtyVersion)
                break;
        }

        autosaveCo = null;
    }

    #endregion

    #region Exit Save Guards

    void SafeForceSaveWithStageId(string reason, string stageId)
    {
        if (_isRestoring) return;
        if (!_stageReadyForExitSaves) return;
        if (!IsPlayableStageId(stageId)) return;

        var now = Time.unscaledTime;
        if (now - lastSaveTime < minSaveInterval) return;

        lastSaveTime = now;
        Save(stageId);

#if UNITY_EDITOR
        Debug.Log($"[StageSave] Forced save ({reason}) stageId={stageId}");
#endif
    }

    void SafeForceSave(string reason)
    {
        if (_isRestoring) return;
        if (!_stageReadyForExitSaves) return;

        var stageId = StageId;
        if (!IsPlayableStageId(stageId)) return;

        var now = Time.unscaledTime;
        if (now - lastSaveTime < minSaveInterval) return;

        lastSaveTime = now;
        Save(stageId);

#if UNITY_EDITOR
        Debug.Log($"[StageSave] Forced save ({reason}) stageId={stageId}");
#endif
    }

    bool IsPlayableStageId(string id)
    {
        if (string.IsNullOrEmpty(id)) return false;
        if (string.IsNullOrEmpty(stageScenePrefix)) return true;

        if (!id.StartsWith(stageScenePrefix)) return false;

        // Stage + 숫자만 허용 (StageSelect 같은 것 제외)
        int idx = stageScenePrefix.Length;
        if (id.Length <= idx) return false;
        return char.IsDigit(id[idx]);
    }

    #endregion

    #region FixedRoot Helpers

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

    PlacementObject FindExistingFixedRootPO(string persistentId)
    {
        if (string.IsNullOrEmpty(persistentId)) return null;

        var fr = GetFixedRoot();
        if (fr == null) return null;

        var list = fr.GetComponentsInChildren<PlacementObject>(true);
        for (int i = 0; i < list.Length; i++)
        {
            var po = list[i];
            if (po == null) continue;

            po.EnsureId();
            if (po.PersistentId == persistentId) return po;
        }

        return null;
    }

    #endregion

    #region File / Path Helpers

    string GetPath(string stageId)
    {
        var dir = Path.Combine(Application.persistentDataPath, CurrentSaveFolderName);
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        return Path.Combine(dir, $"{stageId}.json");
    }

    static string GetPath(Transform root, Transform target)
    {
        if (root == null) return null;
        if (target == null || target == root) return "";

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

    #endregion

    #region Utilities

    public void DeleteCurrentSave()
    {
        var stageId = StageId;
        if (string.IsNullOrEmpty(stageId)) return;

        DeleteSaveByStageId(stageId);
    }
    IEnumerator CoSyncRailBudgetNextFrame()
    {
        yield return null;
        yield return new WaitForEndOfFrame();

        ForceRebuildOccupancy();

        var railTool = FindFirstObjectByType<RailToolPlacer2D>();
        if (railTool != null)
            railTool.SyncRailBudgetFromScene();
        else
            Debug.LogWarning("[StageSave] RailToolPlacer2D not found. Budget sync skipped.");

        // ✅ Undo 복원일 때만, 복원 완료 직후 바인딩 Sync를 1회 실행
        if (_deferBindingSyncUntilPostRestore)
        {
            for (int i = 0; i < _postRestoreBindingSyncTargets.Count; i++)
            {
                var po = _postRestoreBindingSyncTargets[i];
                if (po == null) continue;

                var bind = po.GetComponent<RailNodeFollowBinding2D>();
                if (bind == null) continue;

                bind.SyncNow(syncPhysics: true, broadcastMoved: false);

                // ✅ Undo 복원 직후에는 실시간 follow를 꺼둔다.
                var entries = bind.Entries;
                if (entries == null) continue;

                for (int e = 0; e < entries.Count; e++)
                {
                    var node = entries[e].node;
                    if (node == null) continue;

                    var follow = node.GetComponent<RailNodeFollow2D>();
                    if (follow != null)
                        follow.runtimeFollowEnabled = false;
                }
            }

            _postRestoreBindingSyncTargets.Clear();
            _deferBindingSyncUntilPostRestore = false;
        }
        IsRestoreStabilizingNow = false;

        OnStageRestored?.Invoke();
        OnStageRestoredUnityEvent?.Invoke();
    }

    #endregion

    int GetCurrentStageRevision(string stageId)
    {
        if (stageSaveRevisionDatabase == null)
            return 0;

        return stageSaveRevisionDatabase.GetRevision(stageId);
    }

    public void DeleteSaveByStageId(string stageId)
    {
        if (string.IsNullOrEmpty(stageId)) return;

        var path = GetPath(stageId);
        var tmp = path + ".tmp";
        var bak = path + ".bak";

        try { if (File.Exists(path)) File.Delete(path); } catch { }
        try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
        try { if (File.Exists(bak)) File.Delete(bak); } catch { }

        Debug.Log($"[StageSave] Deleted save for stageId={stageId}");
    }



    public event Action OnStageChangedCommitted;

    public object CaptureRuntimeSnapshot()
    {
        var stageId = StageId;
        if (string.IsNullOrEmpty(stageId)) return null;

        return BuildSaveFileV2(stageId);
    }

    public void RestoreRuntimeSnapshot(object snapshot)
    {
        if (snapshot == null) return;

        var data = snapshot as StageSaveFileV2;
        if (data == null) return;

        StartCoroutine(CoRestoreRuntimeSnapshot(data));
    }

    IEnumerator CoRestoreRuntimeSnapshot(StageSaveFileV2 data)
    {
        _isRestoring = true;
        IsRestoringNow = true;
        IsRestoreStabilizingNow = true;
        _deferBindingSyncUntilPostRestore = true;
        _postRestoreBindingSyncTargets.Clear();

        PreCleanupForRestore();

        if (waitOneFrameAfterDestroy)
        {
            yield return null;
            yield return new WaitForEndOfFrame();
        }

        TryRebuildFromSaveFileV2(data, "RestoreRuntimeSnapshot failed");

        yield return null;
        yield return new WaitForEndOfFrame();

        _isRestoring = false;
        IsRestoringNow = false;
    }

    public string GetCurrentStageIdForUndo()
    {
        return StageId;
    }

    public IEnumerator RestoreRuntimeSnapshotCo(object snapshot)
    {
        if (snapshot == null) yield break;

        var data = snapshot as StageSaveFileV2;
        if (data == null) yield break;

        yield return StartCoroutine(CoRestoreRuntimeSnapshot(data));
    }

    public static bool SuppressStageChangedNotify => _suppressStageChangedNotifyCount > 0;
    static int _suppressStageChangedNotifyCount;

    public static void PushSuppressStageChangedNotify()
    {
        _suppressStageChangedNotifyCount++;
    }

    public static void PopSuppressStageChangedNotify()
    {
        _suppressStageChangedNotifyCount = Mathf.Max(0, _suppressStageChangedNotifyCount - 1);
    }

    public event Action<object> OnStageChangeBeginSnapshotCaptured;

    public void NotifyStageChangeBegin()
    {
        if (_isRestoring) return;
        if (SuppressStageChangedNotify) return;

        var snapshot = CaptureRuntimeSnapshot();
        if (snapshot == null) return;

        OnStageChangeBeginSnapshotCaptured?.Invoke(snapshot);
    }

    public event Action OnStageChangeBeginCanceled;

    public void NotifyStageChangeBeginCanceled()
    {
        OnStageChangeBeginCanceled?.Invoke();
    }

    public void BeginDeferredStageChanged()
    {
        _deferStageChangedCount++;
    }

    public void EndDeferredStageChanged(bool commit)
    {
        _deferStageChangedCount = Mathf.Max(0, _deferStageChangedCount - 1);

        if (!commit)
        {
            if (_deferStageChangedCount == 0)
                _pendingStageChanged = false;
            return;
        }

        if (_deferStageChangedCount == 0 && _pendingStageChanged)
        {
            _pendingStageChanged = false;
            NotifyStageChangedImmediate();
        }
    }

    void QueueCommittedEvent()
    {
        _queuedCommitVersion = dirtyVersion;

        if (_commitEventQueued)
            return;

        _commitEventQueued = true;
        StartCoroutine(CoInvokeCommittedEventEndOfFrame());
    }

    IEnumerator CoInvokeCommittedEventEndOfFrame()
    {
        yield return null;
        yield return new WaitForEndOfFrame();

        _commitEventQueued = false;

        // 복원 중이면 커밋 이벤트 무시
        if (_isRestoring)
            yield break;

        // 같은 프레임/직후에 변경이 더 들어왔어도
        // 가장 최신 dirtyVersion 기준으로 1번만 이벤트 발생
        if (_queuedCommitVersion == dirtyVersion)
            OnStageChangedCommitted?.Invoke();
        else
            OnStageChangedCommitted?.Invoke();
    }

    string CurrentSaveFolderName
    {
        get
        {
            if (StageProgressManager.I != null &&
                StageProgressManager.I.StageOrderAsset != null &&
                StageProgressManager.I.StageOrderAsset.IsDemoBuild())
            {
                return demoSaveFolderName;
            }

            return fullSaveFolderName;
        }
    }
}