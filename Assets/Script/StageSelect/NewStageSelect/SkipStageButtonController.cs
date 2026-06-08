using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SkipStageButtonController : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] GameObject skipButtonRoot;
    [SerializeField] Button skipButton;
    [SerializeField] GameObject confirmPanelRoot;

    [Header("Cooldown Overlay")]
    [SerializeField] Image cooldownOverlay; // Filled / Radial360
    [SerializeField] bool hideOverlayWhenReady = true;

    [Header("Stage Detect")]
    [SerializeField] string stagePrefix = "Stage";

    [Header("Show Condition")]
    [SerializeField, Min(0f)] float showDelaySec = 120f;
    [SerializeField] bool hideIfAlreadyCompleted = true;
    [SerializeField] bool useUnscaledTime = true;

    [Header("After Skip")]
    [SerializeField] bool goBackAfterSkip = true;

    // 게임 실행 중에만 유지되는 스테이지별 누적 플레이 시간
    static readonly Dictionary<string, float> s_PlayedTimeByStage = new Dictionary<string, float>();

    Coroutine _cooldownCo;

    string _currentStageId;
    float _accumulatedPlayedSec;

    void Awake()
    {
        SceneManager.activeSceneChanged += OnActiveSceneChanged;

        if (confirmPanelRoot != null)
            confirmPanelRoot.SetActive(false);
    }

    void Start()
    {
        Apply(SceneManager.GetActiveScene().name);
    }

    void OnDestroy()
    {
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
    }

    void Update()
    {
        if (string.IsNullOrWhiteSpace(_currentStageId))
            return;

        float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        if (dt <= 0f)
            return;

        _accumulatedPlayedSec += dt;
        s_PlayedTimeByStage[_currentStageId] = _accumulatedPlayedSec;
    }

    void OnActiveSceneChanged(Scene prev, Scene next)
    {
        Apply(next.name);
    }

    void Apply(string sceneName)
    {
        if (_cooldownCo != null)
        {
            StopCoroutine(_cooldownCo);
            _cooldownCo = null;
        }

        _currentStageId = null;
        _accumulatedPlayedSec = 0f;

        if (confirmPanelRoot != null)
            confirmPanelRoot.SetActive(false);

        bool isStage = IsPlayableStageScene(sceneName);

        if (!isStage)
        {
            SetSkipUIVisible(false);
            return;
        }

        if (hideIfAlreadyCompleted &&
            StageProgressManager.I != null &&
            StageProgressManager.I.IsCompleted(sceneName))
        {
            SetSkipUIVisible(false);
            return;
        }

        _currentStageId = sceneName;

        if (!s_PlayedTimeByStage.TryGetValue(_currentStageId, out _accumulatedPlayedSec))
            _accumulatedPlayedSec = 0f;

        SetSkipUIVisible(true);

        float remainSec = Mathf.Max(0f, showDelaySec - _accumulatedPlayedSec);

        if (remainSec <= 0f)
        {
            SetButtonReady(true);
            SetCooldownVisual(0f);
            return;
        }

        SetButtonReady(false);
        SetCooldownVisual((showDelaySec <= 0f) ? 0f : Mathf.Clamp01(remainSec / showDelaySec));

        _cooldownCo = StartCoroutine(CoCooldown());
    }

    bool IsPlayableStageScene(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
            return false;

        if (string.IsNullOrWhiteSpace(stagePrefix))
            return false;

        if (!sceneName.StartsWith(stagePrefix, System.StringComparison.OrdinalIgnoreCase))
            return false;

        if (sceneName.Length <= stagePrefix.Length)
            return false;

        char nextChar = sceneName[stagePrefix.Length];
        return char.IsDigit(nextChar);
    }

    IEnumerator CoCooldown()
    {
        while (!string.IsNullOrWhiteSpace(_currentStageId))
        {
            float remainSec = Mathf.Max(0f, showDelaySec - _accumulatedPlayedSec);
            float remain01 = (showDelaySec <= 0f) ? 0f : Mathf.Clamp01(remainSec / showDelaySec);

            SetCooldownVisual(remain01);

            if (remainSec <= 0f)
            {
                SetButtonReady(true);
                SetCooldownVisual(0f);
                _cooldownCo = null;
                yield break;
            }

            SetButtonReady(false);
            yield return null;
        }

        _cooldownCo = null;
    }

    void SetSkipUIVisible(bool visible)
    {
        if (skipButtonRoot != null)
            skipButtonRoot.SetActive(visible);
    }

    void SetButtonReady(bool ready)
    {
        if (skipButton != null)
            skipButton.interactable = ready;

        if (cooldownOverlay != null)
        {
            if (hideOverlayWhenReady)
            {
                cooldownOverlay.gameObject.SetActive(!ready);
            }
            else
            {
                cooldownOverlay.gameObject.SetActive(true);
                cooldownOverlay.raycastTarget = !ready;
            }
        }
    }

    void SetCooldownVisual(float remain01)
    {
        if (cooldownOverlay == null)
            return;

        if (!cooldownOverlay.gameObject.activeSelf)
            cooldownOverlay.gameObject.SetActive(true);

        cooldownOverlay.fillAmount = Mathf.Clamp01(remain01);
    }

    // Skip 버튼 OnClick
    public void OnClickOpenConfirm()
    {
        if (skipButton != null && !skipButton.interactable)
            return;

        if (confirmPanelRoot != null)
            confirmPanelRoot.SetActive(true);
        else
            SkipCurrentStage();
    }

    // 팝업의 취소 버튼 OnClick
    public void OnClickCancelConfirm()
    {
        if (confirmPanelRoot != null)
            confirmPanelRoot.SetActive(false);
    }

    // 팝업의 확인 버튼 OnClick
    public void OnClickConfirmSkip()
    {
        if (confirmPanelRoot != null)
            confirmPanelRoot.SetActive(false);

        SkipCurrentStage();
    }

    void SkipCurrentStage()
    {
        string currentStageId = SceneManager.GetActiveScene().name;

        if (string.IsNullOrWhiteSpace(currentStageId))
        {
            Debug.LogWarning("[SkipStageButton] current stage id is empty.");
            return;
        }

        if (StageProgressManager.I != null)
        {
            StageProgressManager.I.MarkSkipped(currentStageId);
            Debug.Log($"[SkipStageButton] Skipped stage: {currentStageId}");
        }

        if (goBackAfterSkip && SceneFlow.I != null)
            SceneFlow.I.GoBack();
    }
}