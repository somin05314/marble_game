using System;
using System.Collections;
using UnityEngine;

public class GameModeManager : MonoBehaviour
{
    public static GameModeManager Instance;

    public GameMode currentMode = GameMode.Build;

    [Header("Play")]
    public GameObject marblePrefab;
    public Transform spawnPoint;

    [Header("Snapshot")]
    public PuzzleSnapshotManager snapshotManager;
    public CameraSnapshotManager cameraSnapshot;

    [Header("Physics")]
    public PhysicsModeApplier physicsApplier;

    // ✅ (2) 리셋 완료 이벤트: BuildMode 복구가 "완전히 끝난 뒤" 호출
    public static event Action OnGameReset;

    bool isRestoring = false;

    // ✅ (4) 마블 중복 스폰 방지용 추적
    GameObject currentMarble;

    void Awake()
    {
        // ✅ (1) 싱글톤 가드
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // 필요하면 씬 이동에서도 유지
        // DontDestroyOnLoad(gameObject);
    }

    void Update()
    {
        if (isRestoring)
            return;

        if (Input.GetKeyDown(KeyCode.Space))
            EnterPlayMode();

        if (Input.GetKeyDown(KeyCode.R))
            EnterBuildMode();
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
        currentMode = GameMode.Build;

        // ✅ (4) 빌드모드로 돌아올 땐 마블 제거
        ClearMarble();

        yield return null;

        RestoreSnapshot();
        // 퍼즐 기믹 리셋
        ResetAllResettables();

        yield return null;

        RestoreCameraSnapshot();

        // ✅ (5) 복구 끝난 뒤 툴 세팅 (상태 꼬임 방지)
        BuildToolManager.Instance.SetTool(BuildTool.Place);

        // ✅ (2) "리셋 완료" 신호
        OnGameReset?.Invoke();

        isRestoring = false;
    }

    // =========================
    // Play Mode
    // =========================
    public void EnterPlayMode()
    {
        if (currentMode == GameMode.Play)
            return;

        BuildToolManager.Instance.SetTool(BuildTool.None);

        SaveSnapshot();

        currentMode = GameMode.Play;
        ApplyPhysics();

        // ✅ (4) 혹시 남아있던 마블이 있으면 제거 후 스폰
        ClearMarble();
        SpawnMarble();
    }

    public void SpawnMarble()
    {
        if (marblePrefab == null || spawnPoint == null)
            return;

        currentMarble = Instantiate(marblePrefab, spawnPoint.position, Quaternion.identity);
    }

    void ClearMarble()
    {
        if (currentMarble != null)
        {
            Destroy(currentMarble);
            currentMarble = null;
        }
    }

    // =========================
    // Snapshot
    // =========================
    void SaveSnapshot()
    {
        snapshotManager.Save();
    }

    void RestoreSnapshot()
    {
        snapshotManager.Restore();
    }

    // =========================
    // Camera Snapshot
    // =========================
    public void SaveCameraSnapshot()
    {
        if (currentMode != GameMode.Build) return;
        if (isRestoring) return;

        cameraSnapshot.Save();
    }

    void RestoreCameraSnapshot()
    {
        cameraSnapshot.Restore();
    }

    // =========================
    // Physics
    // =========================
    void ApplyPhysics()
    {
        physicsApplier.Apply(currentMode);
    }

    public void OnGoalReached()
    {
        if (currentMode != GameMode.Play)
            return;

        EnterBuildMode();
    }

    void ResetAllResettables()
    {
        var resettables = FindObjectsOfType<MonoBehaviour>();

        foreach (var r in resettables)
        {
            if (r is IResettable resettable)
                resettable.ResetState();
        }
    }
}
