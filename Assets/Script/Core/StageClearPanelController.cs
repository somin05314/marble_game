using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class StageClearPanelController : MonoBehaviour
{
    [Header("Panel Root")]
    [SerializeField] GameObject panelRoot;
    [SerializeField] CanvasGroup panelGroup;

    [Header("Animated Panels")]
    [SerializeField] RectTransform topPanel;
    [SerializeField] RectTransform rightPanel;   // bottomPanel -> rightPanel

    [Header("Shown Position (Final)")]
    [SerializeField] Vector2 topShownPos;
    [SerializeField] Vector2 rightShownPos;      // bottomShownPos -> rightShownPos

    [Header("Slide Animation")]
    [SerializeField] float topSlideDistance = 140f;
    [SerializeField] float rightSlideDistance = 300f; // 오른쪽에서 들어올 거리
    [SerializeField] float showDuration = 0.3f;
    [SerializeField] bool useUnscaledTime = true;

    [Header("Side Panel Exit")]
    [SerializeField] float sidePanelExitDuration = 0.28f;
    [SerializeField] float sidePanelLeftExitX = 220f;
    [SerializeField] float sidePanelRightExitX = 220f;
    [SerializeField] float sidePanelExitY = 24f;
    [SerializeField, Range(0f, 1f)] float sidePanelExitAlpha = 0.15f;

    [Header("Auto Find Stage UI (CanvasGroup)")]
    [SerializeField] string stageUiTag = "StageUI";

    CanvasGroup _stageUiGroupCached;

    [Header("Buttons")]
    [SerializeField] Button buildModeButton;
    [SerializeField] Button nextStageButton;
    [SerializeField] Button stageSelectButton;

    [Header("Next Stage Rule")]
    [SerializeField] string stagePrefix = "Stage";
    [SerializeField] int stageNumberDigits = 2;
    [SerializeField] bool disableNextIfSceneMissing = true;

    [Header("Stage Order")]
    [SerializeField] StageOrderAsset stageOrderAsset;

    Coroutine _showCo;
    Coroutine _sideExitCo;
    Coroutine _sideRestoreCo;
    Coroutine _resizeCo;

    readonly List<SidePanelRuntime> _sidePanels = new List<SidePanelRuntime>(4);
    bool _sidePanelsResolved;

    class SidePanelRuntime
    {
        public StageClearSidePanelTarget marker;
        public RectTransform rect;
        public CanvasGroup group;
        public Vector2 shownPos;
        public float shownAlpha = 1f;
    }

    void Reset()
    {
        panelGroup = GetComponent<CanvasGroup>();
    }

    void Awake()
    {
        if (panelGroup == null && panelRoot != null)
            panelGroup = panelRoot.GetComponent<CanvasGroup>();

        if (buildModeButton != null) buildModeButton.onClick.AddListener(OnClickBuildMode);
        if (nextStageButton != null) nextStageButton.onClick.AddListener(OnClickNextStage);
        if (stageSelectButton != null) stageSelectButton.onClick.AddListener(OnClickStageSelect);

        HideImmediate();
    }

    void OnEnable()
    {
        GameModeManager.OnStageCleared += HandleStageCleared;
        SceneFlow.OnBeforeContentUnload += HandleBeforeContentUnload;

        HideImmediate();
        UnlockStageUI();
    }

    void OnDisable()
    {
        GameModeManager.OnStageCleared -= HandleStageCleared;
        SceneFlow.OnBeforeContentUnload -= HandleBeforeContentUnload;
    }

    void HandleBeforeContentUnload()
    {
        RestoreSidePanelsImmediate();
        HideImmediate();
        UnlockStageUI();
        _stageUiGroupCached = null;
        ClearSidePanelCache();
    }

    void HandleStageCleared()
    {
        Show();
    }

    public void Show()
    {
        _isClearPanelShown = true;
        _lastScreenWidth = Screen.width;
        _lastScreenHeight = Screen.height;

        CloseBlockingPanels();
        LockStageUI();

        if (_showCo != null)
        {
            StopCoroutine(_showCo);
            _showCo = null;
        }

        if (_sideExitCo != null)
        {
            StopCoroutine(_sideExitCo);
            _sideExitCo = null;
        }

        ResolveSidePanels();
        CacheCurrentSidePanelState();

        if (panelRoot != null)
            panelRoot.SetActive(true);

        if (panelGroup != null)
        {
            panelGroup.alpha = 1f;
            panelGroup.interactable = true;
            panelGroup.blocksRaycasts = true;
        }

        ApplyHiddenStateImmediate();
        RefreshNextStageButtonInteractable();

        _sideExitCo = StartCoroutine(CoExitSidePanels());
        _showCo = StartCoroutine(CoShow());
    }

    public void HideImmediate()
    {
        _isClearPanelShown = false;

        if (_resizeCo != null)
        {
            StopCoroutine(_resizeCo);
            _resizeCo = null;
        }

        if (_showCo != null)
        {
            StopCoroutine(_showCo);
            _showCo = null;
        }

        if (_sideExitCo != null)
        {
            StopCoroutine(_sideExitCo);
            _sideExitCo = null;
        }

        if (_sideRestoreCo != null)
        {
            StopCoroutine(_sideRestoreCo);
            _sideRestoreCo = null;
        }

        if (panelGroup != null)
        {
            panelGroup.alpha = 0f;
            panelGroup.interactable = false;
            panelGroup.blocksRaycasts = false;
        }

        ApplyHiddenStateImmediate();

        if (panelRoot != null)
            panelRoot.SetActive(false);
    }

    Vector2 GetTopHiddenPos()
    {
        return topShownPos + Vector2.up * topSlideDistance;
    }

    Vector2 GetRightHiddenPos()
    {
        return rightShownPos + Vector2.right * rightSlideDistance;
    }

    void ApplyHiddenStateImmediate()
    {
        if (topPanel != null)
            topPanel.anchoredPosition = GetTopHiddenPos();

        if (rightPanel != null)
            rightPanel.anchoredPosition = GetRightHiddenPos();
    }

    void ApplyShownStateImmediate()
    {
        if (topPanel != null)
            topPanel.anchoredPosition = topShownPos;

        if (rightPanel != null)
            rightPanel.anchoredPosition = rightShownPos;
    }

    IEnumerator CoShow()
    {
        float t = 0f;
        float dur = Mathf.Max(0.0001f, showDuration);

        Vector2 topFrom = GetTopHiddenPos();
        Vector2 topTo = topShownPos;

        Vector2 rightFrom = GetRightHiddenPos();
        Vector2 rightTo = rightShownPos;

        while (t < dur)
        {
            t += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            float a = Mathf.Clamp01(t / dur);
            a = a * a * (3f - 2f * a);

            if (topPanel != null)
                topPanel.anchoredPosition = Vector2.Lerp(topFrom, topTo, a);

            if (rightPanel != null)
                rightPanel.anchoredPosition = Vector2.Lerp(rightFrom, rightTo, a);

            yield return null;
        }

        ApplyShownStateImmediate();
        _showCo = null;
    }

    IEnumerator CoExitSidePanels()
    {
        float t = 0f;
        float dur = Mathf.Max(0.0001f, sidePanelExitDuration);

        while (t < dur)
        {
            t += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            float a = Mathf.Clamp01(t / dur);
            a = a * a * (3f - 2f * a);

            for (int i = 0; i < _sidePanels.Count; i++)
            {
                var p = _sidePanels[i];
                if (p == null || p.rect == null || p.marker == null) continue;

                Vector2 to = GetSidePanelExitPos(p);
                p.rect.anchoredPosition = Vector2.Lerp(p.shownPos, to, a);

                if (p.group != null)
                    p.group.alpha = Mathf.Lerp(p.shownAlpha, sidePanelExitAlpha, a);
            }

            yield return null;
        }

        for (int i = 0; i < _sidePanels.Count; i++)
        {
            var p = _sidePanels[i];
            if (p == null || p.rect == null || p.marker == null) continue;

            p.rect.anchoredPosition = GetSidePanelExitPos(p);

            if (p.group != null)
            {
                p.group.alpha = sidePanelExitAlpha;
                p.group.interactable = false;
                p.group.blocksRaycasts = false;
            }
        }

        _sideExitCo = null;
    }

    Vector2 GetSidePanelExitPos(SidePanelRuntime p)
    {
        Vector2 offset;

        if (p.marker.PanelSide == StageClearSidePanelTarget.Side.Left)
            offset = new Vector2(-Mathf.Abs(sidePanelLeftExitX), sidePanelExitY);
        else
            offset = new Vector2(Mathf.Abs(sidePanelRightExitX), sidePanelExitY);

        return p.shownPos + offset;
    }

    void ResolveSidePanels()
    {
        if (_sidePanelsResolved) return;

        _sidePanels.Clear();

        var markers = FindObjectsByType<StageClearSidePanelTarget>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        if (markers == null || markers.Length == 0)
        {
            _sidePanelsResolved = true;
            return;
        }

        for (int i = 0; i < markers.Length; i++)
        {
            var m = markers[i];
            if (m == null) continue;

            var rect = m.TargetRect;
            if (rect == null) continue;

            var runtime = new SidePanelRuntime
            {
                marker = m,
                rect = rect,
                group = m.TargetCanvasGroup,
                shownPos = rect.anchoredPosition,
                shownAlpha = m.TargetCanvasGroup != null ? m.TargetCanvasGroup.alpha : 1f
            };

            _sidePanels.Add(runtime);
        }

        _sidePanelsResolved = true;
    }

    void CacheCurrentSidePanelState()
    {
        for (int i = 0; i < _sidePanels.Count; i++)
        {
            var p = _sidePanels[i];
            if (p == null || p.rect == null) continue;

            p.shownPos = p.rect.anchoredPosition;

            if (p.group != null)
            {
                p.shownAlpha = p.group.alpha;
                p.group.interactable = false;
                p.group.blocksRaycasts = false;
            }
        }
    }

    void ClearSidePanelCache()
    {
        _sidePanels.Clear();
        _sidePanelsResolved = false;
    }

    void LockStageUI()
    {
        var cg = GetStageUiGroup();
        if (cg == null) return;
        cg.interactable = false;
        cg.blocksRaycasts = false;
    }

    void UnlockStageUI()
    {
        var cg = GetStageUiGroup();
        if (cg == null) return;
        cg.interactable = true;
        cg.blocksRaycasts = true;
    }

    CanvasGroup GetStageUiGroup()
    {
        if (_stageUiGroupCached != null)
            return _stageUiGroupCached;

        if (!string.IsNullOrEmpty(stageUiTag))
        {
            var go = GameObject.FindGameObjectWithTag(stageUiTag);
            if (go != null)
            {
                _stageUiGroupCached = go.GetComponent<CanvasGroup>();
                if (_stageUiGroupCached != null)
                    return _stageUiGroupCached;
            }
        }

        return null;
    }

    void RefreshNextStageButtonInteractable()
    {
        if (nextStageButton == null) return;

        string nextId = BuildNextStageId();
        if (string.IsNullOrEmpty(nextId))
        {
            nextStageButton.interactable = false;
            return;
        }

        bool sceneExists = !disableNextIfSceneMissing || Application.CanStreamedLevelBeLoaded(nextId);

        bool blockedByDemo = false;
        if (StageProgressManager.I != null)
            blockedByDemo = StageProgressManager.I.IsBlockedByDemo(nextId);

        nextStageButton.interactable = sceneExists && !blockedByDemo;
    }

    void OnClickBuildMode()
    {
        HideImmediate();
        RestoreSidePanels();
        UnlockStageUI();

        if (GameModeManager.Instance != null)
            GameModeManager.Instance.ReturnToBuildModeFromClearUI();
    }

    void OnClickNextStage()
    {
        HideImmediate();
        UnlockStageUI();

        if (GameModeManager.Instance != null)
            GameModeManager.Instance.GoToNextStage();
        else if (SceneFlow.I != null)
            SceneFlow.I.GoStageSelect();
    }

    void OnClickStageSelect()
    {
        HideImmediate();
        UnlockStageUI();

        if (GameModeManager.Instance != null)
            GameModeManager.Instance.GoToStageSelect();
        else if (SceneFlow.I != null)
            SceneFlow.I.GoStageSelect();
    }

    string BuildNextStageId()
    {
        string cur = StageContext.CurrentStageId;
        if (string.IsNullOrEmpty(cur)) return null;
        if (stageOrderAsset == null) return null;

        if (stageOrderAsset.mainStageSequences != null)
        {
            for (int i = 0; i < stageOrderAsset.mainStageSequences.Length; i++)
            {
                var seq = stageOrderAsset.mainStageSequences[i].stageIds;
                if (seq == null) continue;

                for (int j = 0; j < seq.Length; j++)
                {
                    if (!string.Equals(seq[j], cur, StringComparison.OrdinalIgnoreCase))
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

    void CloseBlockingPanels()
    {
        OptionsPanelController.I?.Close();

        var tutorial = FindFirstObjectByType<TutorialPanelController>();
        if (tutorial != null)
            tutorial.CloseTutorial();

        TooltipManager.I?.Cancel();
    }

    void RestoreSidePanelsImmediate()
    {
        ResolveSidePanels();

        for (int i = 0; i < _sidePanels.Count; i++)
        {
            var p = _sidePanels[i];
            if (p == null || p.rect == null) continue;

            p.rect.anchoredPosition = p.shownPos;

            if (p.group != null)
            {
                p.group.alpha = p.shownAlpha;
                p.group.interactable = true;
                p.group.blocksRaycasts = true;
            }
        }
    }

    public void RestoreSidePanels()
    {
        ResolveSidePanels();

        if (_sideRestoreCo != null)
        {
            StopCoroutine(_sideRestoreCo);
            _sideRestoreCo = null;
        }

        if (_sideExitCo != null)
        {
            StopCoroutine(_sideExitCo);
            _sideExitCo = null;
        }

        _sideRestoreCo = StartCoroutine(CoRestoreSidePanels());
    }

    IEnumerator CoRestoreSidePanels()
    {
        float t = 0f;
        float dur = Mathf.Max(0.0001f, sidePanelExitDuration);

        var fromPositions = new Vector2[_sidePanels.Count];
        var fromAlphas = new float[_sidePanels.Count];

        for (int i = 0; i < _sidePanels.Count; i++)
        {
            var p = _sidePanels[i];
            if (p == null || p.rect == null) continue;

            fromPositions[i] = p.rect.anchoredPosition;
            fromAlphas[i] = p.group != null ? p.group.alpha : 1f;
        }

        while (t < dur)
        {
            t += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            float a = Mathf.Clamp01(t / dur);
            a = a * a * (3f - 2f * a);

            for (int i = 0; i < _sidePanels.Count; i++)
            {
                var p = _sidePanels[i];
                if (p == null || p.rect == null) continue;

                p.rect.anchoredPosition = Vector2.Lerp(fromPositions[i], p.shownPos, a);

                if (p.group != null)
                    p.group.alpha = Mathf.Lerp(fromAlphas[i], p.shownAlpha, a);
            }

            yield return null;
        }

        for (int i = 0; i < _sidePanels.Count; i++)
        {
            var p = _sidePanels[i];
            if (p == null || p.rect == null) continue;

            p.rect.anchoredPosition = p.shownPos;

            if (p.group != null)
            {
                p.group.alpha = p.shownAlpha;
                p.group.interactable = true;
                p.group.blocksRaycasts = true;
            }
        }

        _sideRestoreCo = null;
    }

    int _lastScreenWidth;
    int _lastScreenHeight;
    bool _isClearPanelShown;

    void LateUpdate()
    {
        if (!_isClearPanelShown) return;

        if (_lastScreenWidth != Screen.width || _lastScreenHeight != Screen.height)
        {
            _lastScreenWidth = Screen.width;
            _lastScreenHeight = Screen.height;

            if (_resizeCo != null)
                StopCoroutine(_resizeCo);

            _resizeCo = StartCoroutine(CoHandleScreenSizeChangedDelayed());
        }
    }

    IEnumerator CoHandleScreenSizeChangedDelayed()
    {
        yield return null;
        yield return new WaitForEndOfFrame();
        yield return null;

        Canvas.ForceUpdateCanvases();

        HandleScreenSizeChanged();

        _resizeCo = null;
    }

    void HandleScreenSizeChanged()
    {
        Canvas.ForceUpdateCanvases();

        if (_showCo != null)
        {
            StopCoroutine(_showCo);
            _showCo = null;
        }

        ApplyShownStateImmediate();

        // 사이드 패널은 현재 클리어 UI가 떠 있는 상태이므로 다시 퇴장 위치로 보정
        for (int i = 0; i < _sidePanels.Count; i++)
        {
            var p = _sidePanels[i];
            if (p == null || p.rect == null || p.marker == null) continue;

            p.rect.anchoredPosition = GetSidePanelExitPos(p);

            if (p.group != null)
            {
                p.group.alpha = sidePanelExitAlpha;
                p.group.interactable = false;
                p.group.blocksRaycasts = false;
            }
        }
    }
}