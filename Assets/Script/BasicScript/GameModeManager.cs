using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameModeManager : MonoBehaviour
{
    public static GameModeManager Instance;

    public GameMode currentMode = GameMode.Build;

    public bool IsBuildMode => currentMode == GameMode.Build;

    [Header("Play")]
    public GameObject marblePrefab;

    [Header("Snapshot")]
    public PuzzleSnapshotManager snapshotManager;
    public CameraSnapshotManager cameraSnapshot;

    [Header("Physics")]
    public PhysicsModeApplier physicsApplier;

    [Header("Camera FX")]
    [SerializeField] PuzzleCamera puzzleCamera;

    public static event Action OnGameReset;

    bool isRestoring = false;

    // 현재 플레이 중 생성된 마블 추적
    readonly List<GameObject> currentMarbles = new List<GameObject>(8);

    // GoalZone 개별 달성 체크
    readonly HashSet<int> _requiredGoalZoneIds = new HashSet<int>();
    readonly HashSet<int> _reachedGoalZoneIds = new HashSet<int>();

    [Header("Build Tools (enable only in Build mode)")]
    public MonoBehaviour[] buildTools;

    public static event Action OnStageCleared;

    [Header("Stage Clear UI")]
    public float stageClearPanelDelay = 3f;
    public static event Action OnStageClearedDelayed;
    Coroutine _coStageClear;

    [Header("Stage Clear Sound")]
    [SerializeField] AudioSource stageClearAudioSource;
    [SerializeField] AudioClip stageClearClip;
    [SerializeField, Range(0f, 1f)] float stageClearVolume = 1f;

    [Header("Stage Order")]
    [SerializeField] StageOrderAsset stageOrderAsset;

    public bool StageCleared { get; private set; } = false;

    public static event Action<GameMode> OnModeChanged;

    // ✅ Goal 진행 UI용
    public static event Action<int, int> OnGoalProgressChanged;

    public void SetMode(GameMode mode)
    {
        if (currentMode == mode)
        {
            RefreshRailSnapNodeRunMode();
            return;
        }

        currentMode = mode;

        InteractionHintUI.I?.SetPlacementMode(currentMode == GameMode.Build);

        RefreshRailSnapNodeRunMode();
        OnModeChanged?.Invoke(currentMode);
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (puzzleCamera == null)
            puzzleCamera = FindObjectOfType<PuzzleCamera>(true);
    }

    void Start()
    {
        SetBuildToolsEnabled(currentMode == GameMode.Build);

        InteractionHintUI.I?.SetPlacementMode(currentMode == GameMode.Build);

        RefreshRailSnapNodeRunMode();

    }

    void Update()
    {
        if (isRestoring)
            return;
    }

    void LateUpdate()
    {
        if (currentMode == GameMode.Build)
            SaveCameraSnapshot();
    }

    // =========================
    // Build Mode
    // =========================
    public void EnterBuildMode()
    {
        if (isRestoring) return;
        StartCoroutine(RestoreBuildModeRoutine());
    }

    IEnumerator RestoreBuildModeRoutine()
    {
        isRestoring = true;

        SetMode(GameMode.Build);
        SetBuildToolsEnabled(false);

        ClearMarbles();

        _reachedGoalZoneIds.Clear();
        _requiredGoalZoneIds.Clear();
        StageCleared = false;

        // ✅ UI 진행 초기화
        OnGoalProgressChanged?.Invoke(0, 0);

        if (_coStageClear != null)
        {
            StopCoroutine(_coStageClear);
            _coStageClear = null;
        }

        yield return null;

        ResetAllResettables();
        yield return null;
        RestoreSnapshot();

        yield return null;

        RestoreCameraSnapshot();

        BuildToolManager.Instance.SetTool(BuildTool.Select);
        SetBuildToolsEnabled(true);

        OnGameReset?.Invoke();

        isRestoring = false;
    }

    // =========================
    // Play Mode
    // =========================
    public void EnterPlayMode()
    {
        if (currentMode == GameMode.Play) return;

        EnsurePuzzleCamera();

        StageCleared = false;
        BuildToolManager.Instance?.SetTool(BuildTool.Select);

        SetBuildToolsEnabled(false);

        // ✅ 플레이 시작 전에 모든 데모 정지 + 리셋
        StopAllPoDemos();

        SaveSnapshot();

        SetMode(GameMode.Play);
        ApplyPhysics();

        _reachedGoalZoneIds.Clear();
        CollectRequiredGoalZones();

        OnGoalProgressChanged?.Invoke(0, _requiredGoalZoneIds.Count);

        ClearMarbles();
        SpawnMarbles();
    }

    void CollectRequiredGoalZones()
    {
        _requiredGoalZoneIds.Clear();

        var zones = FindObjectsOfType<GoalZone2D>(true);

        for (int i = 0; i < zones.Length; i++)
        {
            if (zones[i] == null) continue;
            _requiredGoalZoneIds.Add(zones[i].GetInstanceID());
        }

        Debug.Log($"[GameModeManager] Required GoalZones = {_requiredGoalZoneIds.Count}");
    }

    public void SpawnMarbles()
    {
        if (marblePrefab == null)
        {
            Debug.LogWarning("[GameModeManager] marblePrefab is missing.");
            return;
        }

        var spawnPoints = FindObjectsOfType<MarbleSpawnPoint>(true);

        Debug.Log($"[GameModeManager] SpawnPoints found = {spawnPoints.Length}");

        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogWarning("[GameModeManager] No MarbleSpawnPoint found.");
            return;
        }

        for (int i = 0; i < spawnPoints.Length; i++)
        {
            var sp = spawnPoints[i];
            if (sp == null) continue;

            Debug.Log($"[GameModeManager] SpawnPoint {i}: {sp.name}, active={sp.gameObject.activeInHierarchy}, scene={sp.gameObject.scene.name}");

            if (!sp.gameObject.activeInHierarchy)
                continue;

            var go = Instantiate(marblePrefab, sp.transform.position, sp.transform.rotation);
            currentMarbles.Add(go);

            Debug.Log($"[GameModeManager] Marble spawned at {sp.transform.position}");
        }
    }

    void ClearMarbles()
    {
        for (int i = 0; i < currentMarbles.Count; i++)
        {
            if (currentMarbles[i] != null)
                Destroy(currentMarbles[i]);
        }
        currentMarbles.Clear();
    }

    // =========================
    // Snapshot
    // =========================
    void SaveSnapshot()
    {
        if (snapshotManager != null)
            snapshotManager.Save();
    }

    void RestoreSnapshot()
    {
        if (snapshotManager != null)
            snapshotManager.Restore();
    }

    // =========================
    // Camera Snapshot
    // =========================
    public void SaveCameraSnapshot()
    {
        if (currentMode != GameMode.Build) return;
        if (isRestoring) return;

        if (cameraSnapshot != null)
            cameraSnapshot.Save();
    }

    void RestoreCameraSnapshot()
    {
        if (cameraSnapshot != null)
            cameraSnapshot.Restore();
    }

    // =========================
    // Physics
    // =========================
    void ApplyPhysics()
    {
        if (physicsApplier != null)
            physicsApplier.Apply(currentMode);
    }

    public void OnGoalReached(GoalZone2D zone)
    {
        if (currentMode != GameMode.Play)
            return;
        if (zone == null)
            return;

        int id = zone.GetInstanceID();

        if (_reachedGoalZoneIds.Contains(id))
            return;

        _reachedGoalZoneIds.Add(id);

        EnsurePuzzleCamera();

        if (puzzleCamera != null)
            puzzleCamera.PlayGoalHitFeedback();

        OnGoalProgressChanged?.Invoke(_reachedGoalZoneIds.Count, _requiredGoalZoneIds.Count);

        if (_requiredGoalZoneIds.Count > 0 &&
            _reachedGoalZoneIds.Count >= _requiredGoalZoneIds.Count)
        {
            ClearStageToBuild();
        }
    }

    void ResetAllResettables()
    {
        var resettables = FindObjectsOfType<MonoBehaviour>();
        foreach (var r in resettables)
        {
            if (r is IPoResettable resettable)
                resettable.ResetState();
        }
    }

    // =========================
    // Build Tools Toggle
    // =========================
    void SetBuildToolsEnabled(bool enable)
    {
        if (buildTools == null) return;

        foreach (var tool in buildTools)
        {
            if (tool == null) continue;

            if (tool is IBuildModeTool bt)
            {
                if (enable) bt.OnEnterBuildMode();
                else bt.OnExitBuildMode();
            }

            tool.enabled = enable;
        }
    }

    public void EnsureBuildModeForStageEntry()
    {
        if (isRestoring) return;

        SetMode(GameMode.Build);

        _reachedGoalZoneIds.Clear();
        _requiredGoalZoneIds.Clear();
        StageCleared = false;

        OnGoalProgressChanged?.Invoke(0, 0);

        if (_coStageClear != null)
        {
            StopCoroutine(_coStageClear);
            _coStageClear = null;
        }

        ClearMarbles();

        SetBuildToolsEnabled(true);
        BuildToolManager.Instance?.SetTool(BuildTool.Select);

        ApplyPhysics();
        RefreshRailSnapNodeRunMode();
    }

    void PlayStageClearSound()
    {
        if (stageClearAudioSource == null || stageClearClip == null)
            return;

        stageClearAudioSource.PlayOneShot(stageClearClip, stageClearVolume);
    }

    void ClearStageToBuild()
    {
        if (StageCleared) return;
        StageCleared = true;



        string stageId = StageContext.CurrentStageId;
        if (!string.IsNullOrEmpty(stageId))
        {
            if (StageProgressManager.I != null)
                StageProgressManager.I.MarkCleared(stageId);
            else
                Debug.LogWarning("[GameModeManager] StageProgressManager missing.");
        }
        else
        {
            Debug.LogWarning("[GameModeManager] StageContext.CurrentStageId is empty. Skip clear save.");
        }

        if (_coStageClear != null) StopCoroutine(_coStageClear);
        _coStageClear = StartCoroutine(CoStageClearDelayed());
    }

    IEnumerator CoStageClearDelayed()
    {
        yield return new WaitForSeconds(stageClearPanelDelay);
        OnStageCleared?.Invoke();
        PlayStageClearSound();
        _coStageClear = null;
    }

    public void CancelStageClearPending()
    {
        if (_coStageClear != null)
        {
            StopCoroutine(_coStageClear);
            _coStageClear = null;
        }
    }

    // ✅ 클리어 패널의 "Build" 버튼에 연결
    public void ReturnToBuildModeFromClearUI()
    {
        EnterBuildMode();
    }

    public void GoToStageSelect()
    {
        if (!StageCleared) return;

        CancelStageClearPending();
        TrySaveBeforeLeave();

        string curStageId = StageContext.CurrentStageId;

        if (SceneFlow.I == null) return;

        int chapterIndex = -1;

        if (stageOrderAsset != null)
        {
            chapterIndex = stageOrderAsset.FindSequenceIndexByMainStage(curStageId);

            if (chapterIndex < 0)
                chapterIndex = stageOrderAsset.FindSequenceIndexByExtraStage(curStageId);
        }

        if (chapterIndex >= 0)
            SceneFlow.I.GoStageSelectForChapter(chapterIndex + 1);
        else
            SceneFlow.I.GoStageSelect();
    }

    public void GoToNextStage()
    {
        if (!StageCleared)
        {
            Debug.LogWarning("[GameModeManager] Not cleared yet.");
            return;
        }

        CancelStageClearPending();
        TrySaveBeforeLeave();

        string cur = StageContext.CurrentStageId;
        string next = BuildNextStageId(cur);

        if (string.IsNullOrEmpty(next))
        {
            Debug.LogWarning("[GameModeManager] Next stage id empty. Go StageSelect.");

            if (SceneFlow.I != null)
            {
                int chapterIndex = -1;

                if (stageOrderAsset != null)
                {
                    chapterIndex = stageOrderAsset.FindSequenceIndexByMainStage(cur);

                    if (chapterIndex < 0)
                        chapterIndex = stageOrderAsset.FindSequenceIndexByExtraStage(cur);
                }

                if (chapterIndex >= 0)
                    SceneFlow.I.GoStageSelectForChapter(chapterIndex + 1);
                else
                    SceneFlow.I.GoStageSelect();
            }

            return;
        }

        if (SceneFlow.I != null)
            SceneFlow.I.GoStage(next);
        else
            Debug.LogError("[GameModeManager] SceneFlow missing.");
    }

    void TrySaveBeforeLeave()
    {
        string curStageId = StageContext.CurrentStageId;
        if (string.IsNullOrEmpty(curStageId))
        {
            Debug.LogWarning("[GameModeManager] CurrentStageId empty. Skip save.");
            return;
        }

        var stageScene = UnityEngine.SceneManagement.SceneManager.GetSceneByName(curStageId);
        if (!stageScene.IsValid() || !stageScene.isLoaded)
        {
            Debug.LogWarning($"[GameModeManager] Stage scene not loaded: {curStageId}");
            return;
        }

        var roots = stageScene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            var save = roots[i].GetComponentInChildren<StageSaveManager>(true);
            if (save != null)
            {
                save.SaveCurrentStage();
                Debug.Log($"[GameModeManager] Saved stage before leave: {curStageId}");
                return;
            }
        }

        Debug.LogWarning("[GameModeManager] StageSaveManager not found in stage scene. Skip save.");
    }

    string BuildNextStageId(string curStageId)
    {
        if (string.IsNullOrEmpty(curStageId)) return null;
        if (stageOrderAsset == null) return null;

        if (stageOrderAsset.mainStageSequences != null)
        {
            for (int i = 0; i < stageOrderAsset.mainStageSequences.Length; i++)
            {
                var seq = stageOrderAsset.mainStageSequences[i].stageIds;
                if (seq == null) continue;

                for (int j = 0; j < seq.Length; j++)
                {
                    if (!string.Equals(seq[j], curStageId, StringComparison.OrdinalIgnoreCase))
                        continue;

                    int nextIndex = j + 1;
                    if (nextIndex < seq.Length)
                        return seq[nextIndex];

                    return null;
                }
            }
        }

        return null;
    }

    void EnsurePuzzleCamera()
    {
        if (puzzleCamera != null)
            return;

        puzzleCamera = FindObjectOfType<PuzzleCamera>(true);
    }

    void RefreshRailSnapNodeRunMode()
    {
        bool isRunMode = currentMode == GameMode.Play;

        var nodes = FindObjectsOfType<RailSnapNode2D>(true);
        for (int i = 0; i < nodes.Length; i++)
        {
            if (nodes[i] == null) continue;
            nodes[i].SetRunMode(isRunMode);
        }
    }

    void StopAllPoDemos()
    {
        var demoLinks = FindObjectsOfType<PoDemoLink>(true);
        for (int i = 0; i < demoLinks.Length; i++)
        {
            if (demoLinks[i] != null)
                demoLinks[i].StopDemoIfPlaying();
        }
    }

    public void EnterEndingPlayMode()
    {
        EnsurePuzzleCamera();

        StageCleared = false;
        BuildToolManager.Instance?.SetTool(BuildTool.Select);

        SetBuildToolsEnabled(false);
        StopAllPoDemos();

        SetMode(GameMode.Play);
        ApplyPhysics();

        _reachedGoalZoneIds.Clear();
        _requiredGoalZoneIds.Clear();

        OnGoalProgressChanged?.Invoke(0, 0);

        // ✅ 엔딩 씬에 새로 로드된 RailSnapNode2D에도 PlayMode 상태 강제 적용
        RefreshRailSnapNodeRunMode();

        // 엔딩에서는 공 생성하지 않음.
        // EndingMarbleSpawner가 담당.
    }

    public void EnterMenuMode()
    {
        CancelStageClearPending();

        StageCleared = false;

        ClearMarbles();

        _reachedGoalZoneIds.Clear();
        _requiredGoalZoneIds.Clear();

        SetMode(GameMode.Build);
        SetBuildToolsEnabled(false);

        OnGoalProgressChanged?.Invoke(0, 0);
    }
}