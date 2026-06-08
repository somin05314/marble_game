using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneFlow : MonoBehaviour
{
    public static SceneFlow I { get; private set; }

    [SerializeField] string coreScene = "Core";

    [Header("Boot")]
    [Tooltip("게임 실행 시 처음 보여줄 컨텐츠 씬")]
    [SerializeField] string startSceneName = "StartScene";

    [Tooltip("Core가 켜질 때 자동으로 startSceneName을 로드할지")]
    [SerializeField] bool bootToStartOnLaunch = true;

    [Header("StageId Policy")]
    [SerializeField] string stageScenePrefix = "Stage";

    [Header("Loading Overlay")]
    [SerializeField] LoadingOverlay overlay;
    [SerializeField] bool useActivationGate = true;

    [Header("Overlay Timing")]
    [SerializeField] float holdBlackBeforeSwitch = 0.08f;
    [SerializeField] float holdBlackAfterSwitch = 0.08f;

    [Header("Restore Wait")]
    [SerializeField] float restoreWaitTimeout = 8f;

    [Header("Camera Transition (No-Leak)")]
    [Tooltip("전환 중 이전 컨텐츠 씬의 카메라만 끕니다(전체 카메라 OFF 금지).")]
    [SerializeField] bool disablePrevContentCamerasDuringTransition = true;

    [Tooltip("복원 완료 후 오버레이 내리기 전 추가 안정화 프레임")]
    [SerializeField, Range(0, 5)] int extraFramesBeforeFadeOut = 1;

    [Header("Core UI Camera Policy")]
    [SerializeField] bool disableCoreUICameraOnStage = true;
    [SerializeField] string coreUICameraName = "CoreUICamera";
    public static Action OnBeforeContentUnload;

    [Header("Core Camera Restore On Non-Stage")]
    [SerializeField] bool restoreCoreCameraSizeOnNonStage = true;
    [SerializeField] float coreCameraSizeOnNonStage = 20f;

    [SerializeField] bool restoreCoreUICameraSizeOnNonStage = false;
    [SerializeField] float coreUICameraSizeOnNonStage = 20f;

    [Header("Ending")]
    [SerializeField] string endingSceneName = "EndingScene";
    [SerializeField] string trueEndingSceneName = "TrueEndingScene";

    string _currentContent; // Core 제외 현재 컨텐츠 씬
    string _currentStageId = "";
    bool _switching;
    bool _booted;

    public string CurrentStageId => _currentStageId;

    void Awake()
    {
        if (I != null) { Destroy(gameObject); return; }
        I = this;
        DontDestroyOnLoad(gameObject);

        OnBeforeContentUnload -= ClearStatics;
        OnBeforeContentUnload += ClearStatics;
    }

    static void ClearStatics()
    {
        RailEdgeRegistry2D.ClearAll();
    }

    void Start()
    {
        if (!bootToStartOnLaunch) return;
        if (_booted) return;
        _booted = true;

        // ✅ 이미 로드된 컨텐츠가 있으면 그것을 현재 컨텐츠로 채택
        // (에디터에서 Stage 씬 직접 실행 → Bootstrap → Core + Stage 상태 포함)
        _currentContent = FindLoadedContentSceneName();

        if (string.IsNullOrEmpty(_currentContent))
        {
            GoStart();
            return;
        }

        ApplyContentState(_currentContent, ensureBuildModeIfStage: true);
    }

    // --------------------
    // Public API
    // --------------------

    public void GoStart()
    {
        LoadContent(startSceneName);
    }

    public void GoStageSelect()
    {
        LoadContent("StageSelect");
    }

    public void GoChapterSelect()
    {
        LoadContent("ChapterSelect");
    }

    public void GoEndingScene()
    {
        LoadContent(endingSceneName);
    }

    public void GoStageSelectForChapter(int chapterIndex)
    {
        ChapterContext.Set(chapterIndex);
        LoadContent("StageSelect");
    }

    public void GoStage(string stageSceneName)
    {
        PlayerPrefs.SetInt("HasPlayed", 1);
        PlayerPrefs.SetString("LastStageScene", stageSceneName);
        PlayerPrefs.Save();

        LoadContent(stageSceneName);
    }

    public void LoadContent(string next)
    {
        if (_switching) return;
        if (string.IsNullOrEmpty(next)) return;

        if (string.IsNullOrEmpty(_currentContent))
            _currentContent = FindLoadedContentSceneName();

        // ✅ 이미 현재 컨텐츠와 같으면 상태만 다시 보정
        if (string.Equals(_currentContent, next, StringComparison.Ordinal))
        {
            ApplyContentState(next, ensureBuildModeIfStage: true);
            return;
        }

        StartCoroutine(Co_SwitchContent(next));
    }

    public void GoBack()
    {
        if (string.IsNullOrEmpty(_currentContent))
            _currentContent = FindLoadedContentSceneName();

        if (IsPlayableStageScene(_currentContent))
        {
            GameModeManager.Instance?.CancelStageClearPending();
            GoStageSelect();
            return;
        }

        if (IsEndingScene(_currentContent))
        {
            GameModeManager.Instance?.EnterMenuMode();
            GoChapterSelect();
            return;
        }

        if (string.Equals(_currentContent, "StageSelect", StringComparison.OrdinalIgnoreCase))
        {
            GoChapterSelect();
            return;
        }

        if (string.Equals(_currentContent, "ChapterSelect", StringComparison.OrdinalIgnoreCase))
        {
            GoStart();
            return;
        }

        if (string.Equals(_currentContent, startSceneName, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        GoStart();
    }

    // --------------------
    // Internals
    // --------------------

    void ApplyContentState(string contentSceneName, bool ensureBuildModeIfStage)
    {
        if (string.IsNullOrEmpty(contentSceneName))
        {
            StageContext.SetStageId("");
            return;
        }

        var scene = SceneManager.GetSceneByName(contentSceneName);
        if (scene.IsValid() && scene.isLoaded)
            SceneManager.SetActiveScene(scene);

        _currentContent = contentSceneName;

        ApplyStageIdPolicy(contentSceneName);

        bool isStage = IsPlayableStageScene(contentSceneName);
        bool isEnding = IsEndingScene(contentSceneName);

        if (disableCoreUICameraOnStage)
            SetCoreUICameraEnabled(!isStage);

        if (!isStage)
        {
            if (restoreCoreCameraSizeOnNonStage)
                SetNamedCameraOrthographicSize(coreScene, coreUICameraName, coreCameraSizeOnNonStage);

            if (restoreCoreUICameraSizeOnNonStage)
                SetNamedCameraOrthographicSize(coreScene, coreUICameraName, coreUICameraSizeOnNonStage);
        }

        if (ensureBuildModeIfStage && isStage)
        {
            GameModeManager.Instance?.EnsureBuildModeForStageEntry();
        }
        else if (isEnding)
        {
            GameModeManager.Instance?.EnterEndingPlayMode();
        }
    }

    void SetNamedCameraOrthographicSize(string sceneName, string cameraName, float size)
    {
        if (string.IsNullOrWhiteSpace(sceneName)) return;
        if (string.IsNullOrWhiteSpace(cameraName)) return;

        var scene = SceneManager.GetSceneByName(sceneName);
        if (!scene.IsValid() || !scene.isLoaded) return;

        var roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            var cams = roots[i].GetComponentsInChildren<Camera>(true);
            for (int k = 0; k < cams.Length; k++)
            {
                var c = cams[k];
                if (c == null) continue;

                if (string.Equals(c.gameObject.name, cameraName, StringComparison.OrdinalIgnoreCase))
                {
                    if (c.orthographic)
                        c.orthographicSize = size;
                    return;
                }
            }
        }
    }

    void ApplyStageIdPolicy(string contentSceneName)
    {
        if (string.IsNullOrEmpty(contentSceneName))
        {
            _currentStageId = "";
            StageContext.SetStageId("");
            return;
        }

        if (!string.IsNullOrEmpty(stageScenePrefix) && contentSceneName.StartsWith(stageScenePrefix))
        {
            _currentStageId = contentSceneName;
            StageContext.SetStageId(contentSceneName);
        }
        else
        {
            _currentStageId = "";
            StageContext.SetStageId("");
        }
    }

    // ✅ ActiveScene 우선, 없으면 Core 제외 첫 loaded scene
    string FindLoadedContentSceneName()
    {
        var active = SceneManager.GetActiveScene();
        if (active.IsValid() && active.isLoaded && active.name != coreScene)
            return active.name;

        int count = SceneManager.sceneCount;
        for (int i = 0; i < count; i++)
        {
            var sc = SceneManager.GetSceneAt(i);
            if (!sc.IsValid() || !sc.isLoaded) continue;
            if (sc.name == coreScene) continue;
            return sc.name;
        }

        return null;
    }

    IEnumerator Co_SwitchContent(string next)
    {
        _switching = true;

        string prevContent = _currentContent;

        Scene prevScene = default;
        if (!string.IsNullOrEmpty(prevContent))
            prevScene = SceneManager.GetSceneByName(prevContent);

        try
        {
            if (overlay != null)
            {
                overlay.SetProgress(0f);
                overlay.ShowBlackImmediate();
            }

            yield return null;

            if (disablePrevContentCamerasDuringTransition)
                SetSceneCamerasEnabled(prevScene, false);

            if (holdBlackBeforeSwitch > 0f)
                yield return new WaitForSecondsRealtime(holdBlackBeforeSwitch);

            // ✅ 씬 언로드 직전, 툴의 드래그/BEGIN/프리뷰 상태 먼저 정리
            ResetTransientBuildToolStates();

            OnBeforeContentUnload?.Invoke();


            // 1) 이전 컨텐츠 언로드
            if (!string.IsNullOrEmpty(prevContent) && prevContent != coreScene)
            {
                var prev = SceneManager.GetSceneByName(prevContent);
                if (prev.IsValid() && prev.isLoaded)
                {
                    var unloadOp = SceneManager.UnloadSceneAsync(prev);
                    if (unloadOp != null)
                        yield return unloadOp;
                }
            }

            yield return null;

            // 2) 다음 씬 로드
            var already = SceneManager.GetSceneByName(next);
            AsyncOperation loadOp = null;

            if (!already.IsValid() || !already.isLoaded)
            {
                loadOp = SceneManager.LoadSceneAsync(next, LoadSceneMode.Additive);

                if (useActivationGate && loadOp != null)
                    loadOp.allowSceneActivation = false;

                while (loadOp != null && !loadOp.isDone)
                {
                    float p = Mathf.Clamp01(loadOp.progress / 0.9f);
                    if (overlay != null) overlay.SetProgress(p * 0.85f);

                    if (useActivationGate && loadOp.progress >= 0.9f)
                        break;

                    yield return null;
                }

                if (overlay != null) overlay.SetProgress(0.9f);
                yield return null;

                if (useActivationGate && loadOp != null)
                {
                    loadOp.allowSceneActivation = true;
                    while (!loadOp.isDone) yield return null;
                }

                if (overlay != null) overlay.SetProgress(1f);
            }
            else
            {
                if (overlay != null) overlay.SetProgress(1f);
                yield return null;
            }

            // 3) 다음 씬 확정
            var nextScene = SceneManager.GetSceneByName(next);
            if (nextScene.IsValid() && nextScene.isLoaded)
                SceneManager.SetActiveScene(nextScene);

            SetSceneCamerasEnabled(nextScene, true);

            yield return null;

            // ✅ 여기서 current / StageId / BuildMode를 한 번에 확정
            ApplyContentState(next, ensureBuildModeIfStage: true);

            if (holdBlackAfterSwitch > 0f)
                yield return new WaitForSecondsRealtime(holdBlackAfterSwitch);

            // 4) Stage 복원 대기
            yield return Co_WaitStageRestored(nextScene);

            // 옵션 적용
            OptionsApplier.TryApplyAll();

            // 5) 안정화 프레임
            for (int i = 0; i < extraFramesBeforeFadeOut; i++)
                yield return null;

            // 6) 오버레이 해제
            if (overlay != null)
                yield return overlay.FadeOut();
        }
        finally
        {
            if (disablePrevContentCamerasDuringTransition)
                SetSceneCamerasEnabled(prevScene, true);

            _switching = false;
        }
    }

    IEnumerator Co_WaitStageRestored(Scene nextScene)
    {
        if (!IsPlayableStageScene(nextScene.name))
            yield break;

        yield return null;

        var save = FindStageSaveManagerInScene(nextScene);
        if (save == null)
            yield break;

        bool done = false;
        Action onDone = () => done = true;

        save.OnStageRestored += onDone;

        float start = Time.unscaledTime;
        while (!done && (Time.unscaledTime - start) < restoreWaitTimeout)
            yield return null;

        save.OnStageRestored -= onDone;

#if UNITY_EDITOR
        if (!done)
            Debug.LogWarning($"[SceneFlow] WaitStageRestored timeout ({restoreWaitTimeout}s). scene={nextScene.name}");
#endif
    }

    StageSaveManager FindStageSaveManagerInScene(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded) return null;

        var roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            var root = roots[i];
            if (root == null) continue;

            var save = root.GetComponentInChildren<StageSaveManager>(true);
            if (save != null) return save;
        }

        return null;
    }

    bool IsPlayableStageScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName)) return false;
        if (string.IsNullOrEmpty(stageScenePrefix)) return true;
        if (!sceneName.StartsWith(stageScenePrefix)) return false;

        int idx = stageScenePrefix.Length;
        if (sceneName.Length <= idx) return false;
        return char.IsDigit(sceneName[idx]);
    }

    static void SetSceneCamerasEnabled(Scene scene, bool on)
    {
        if (!scene.IsValid() || !scene.isLoaded) return;

        var roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            var cams = roots[i].GetComponentsInChildren<Camera>(true);
            for (int k = 0; k < cams.Length; k++)
                cams[k].enabled = on;
        }
    }

    void SetCoreUICameraEnabled(bool on)
    {
        var core = SceneManager.GetSceneByName(coreScene);
        if (!core.IsValid() || !core.isLoaded) return;

        var roots = core.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            var cams = roots[i].GetComponentsInChildren<Camera>(true);
            for (int k = 0; k < cams.Length; k++)
            {
                var c = cams[k];
                if (c == null) continue;

                if (string.Equals(c.gameObject.name, coreUICameraName, StringComparison.OrdinalIgnoreCase))
                {
                    c.enabled = on;
                    return;
                }
            }
        }
    }

    public void GoStageSelectByStage(string stageId)
    {
        if (string.IsNullOrWhiteSpace(stageId))
        {
            GoChapterSelect();
            return;
        }

        if (StageProgressManager.I == null)
        {
            GoChapterSelect();
            return;
        }

        if (StageProgressManager.I.TryGetChapterIndexOfStage(stageId, out int chapterIndex))
        {
            GoStageSelectForChapter(chapterIndex);
            return;
        }

        GoChapterSelect();
    }

    void ResetTransientBuildToolStates()
    {
        GridPlacer.Instance?.ResetTransientStateForSceneChange();
        RailToolPlacer2D.Instance?.ResetTransientStateForSceneChange();
    }

    public bool IsCurrentStartScene()
    {
        if (string.IsNullOrEmpty(_currentContent))
            _currentContent = FindLoadedContentSceneName();

        return string.Equals(_currentContent, startSceneName, StringComparison.OrdinalIgnoreCase);
    }

    public void GoTrueEndingScene()
    {
        LoadContent(trueEndingSceneName);
    }

    bool IsEndingScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName)) return false;

        return string.Equals(sceneName, endingSceneName, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(sceneName, trueEndingSceneName, StringComparison.OrdinalIgnoreCase);
    }
}