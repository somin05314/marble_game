using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelConfigApplier : MonoBehaviour
{
    [SerializeField] StageConfig fallbackConfig;

    Coroutine _co;

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        SceneManager.activeSceneChanged += OnActiveSceneChanged;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
    }

    void Start() => ScheduleApply();
    void OnSceneLoaded(Scene s, LoadSceneMode m) => ScheduleApply();
    void OnActiveSceneChanged(Scene prev, Scene next) => ScheduleApply();

    void ScheduleApply()
    {
        if (_co != null) StopCoroutine(_co);
        _co = StartCoroutine(CoApplyAfterLoadStable());
    }

    IEnumerator CoApplyAfterLoadStable()
    {
        yield return null;
        yield return new WaitForEndOfFrame(); // additive 안정화
        Apply();
        _co = null;
    }

    void Apply()
    {
        var gm = GameModeManager.Instance;
        if (gm == null) return;

        var active = SceneManager.GetActiveScene();

        // ActiveScene 안에 있는 StageConfigHolder 우선 탐색
        StageConfigHolder holder = null;
        var holders = Object.FindObjectsByType<StageConfigHolder>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < holders.Length; i++)
        {
            var h = holders[i];
            if (h != null && h.gameObject.scene == active)
            {
                holder = h;
                break;
            }
        }

        StageConfig cfg = null;
        if (holder != null) cfg = holder.config;
        if (cfg == null) cfg = fallbackConfig;

        // StageSelect 같은 씬(=holder도 없고 config도 없을 수 있음)에서는 아무것도 하지 않음
        if (cfg == null) return;

        // 1) RailBudget 적용
        var budget = Object.FindFirstObjectByType<RailBudget2D>();
        if (budget != null) budget.SetMaxRails(cfg.maxRails);

        // 2) MarbleSpawnPoint (ActiveScene 기준 + 비활성 포함)
        var allSpawnPoints = Object.FindObjectsByType<MarbleSpawnPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        var spawnPoints = new List<MarbleSpawnPoint>(allSpawnPoints.Length);
        for (int i = 0; i < allSpawnPoints.Length; i++)
        {
            var sp = allSpawnPoints[i];
            if (sp != null && sp.gameObject.scene == active)
                spawnPoints.Add(sp);
        }

        // 스폰포인트 없으면 덮어쓰지 않음
        if (spawnPoints.Count == 0) return;

        // marble prefab 하나만 공통 사용
        var marblePrefab = cfg.marblePrefab;

        // prefab 없으면 marbleSpawns는 덮어쓰지 않음
        if (marblePrefab == null) return;

        gm.marblePrefab = cfg.marblePrefab;

        // 스테이지 진입은 무조건 Build 모드로 강제
        gm.EnsureBuildModeForStageEntry();
    }
}