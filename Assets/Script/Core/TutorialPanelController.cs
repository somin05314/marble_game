using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class TutorialPanelController : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] GameObject tutorialButtonRoot; // 스테이지 씬에서만 보이게
    [SerializeField] GameObject tutorialPanelRoot;  // 패널 전체(배경+컨텐츠+닫기버튼)

    [Header("Pages (SetActive 방식)")]
    [Tooltip("각 페이지 루트 GameObject를 순서대로 넣어주세요. (Page0..PageN)")]
    [SerializeField] GameObject[] pageRoots;

    [Header("Page Nav")]
    [SerializeField] Button prevButton;
    [SerializeField] Button nextButton;
    [SerializeField] TMP_Text pageIndicatorText; // "2 / 6" 표시 (선택)

    [Tooltip("마지막 페이지에서 Next 누르면 첫 페이지로 돌아갈지")]
    [SerializeField] bool wrapAround = false;

    [Header("Nav Button Visual")]
    [Tooltip("Prev 버튼에서 색을 바꿀 대상들(Image, TMP_Text 등)")]
    [SerializeField] Graphic[] prevButtonTintTargets;

    [Tooltip("Next 버튼에서 색을 바꿀 대상들(Image, TMP_Text 등)")]
    [SerializeField] Graphic[] nextButtonTintTargets;

    [SerializeField] Color navEnabledColor = Color.white;
    [SerializeField] Color navDisabledColor = new Color(0.45f, 0.45f, 0.45f, 1f);

    [Header("Scene Policy")]
    [SerializeField] string stagePrefix = "Stage";

    [Header("Auto Open on Stage01")]
    [SerializeField] bool autoOpenOnStage01 = true;
    [SerializeField] string stage01Name = "Stage01";
    [SerializeField] string seenKeyStage01 = "TutorialSeen_Stage01";

    [Header("Auto Open Delay")]
    [Tooltip("Stage01 자동 오픈 시 몇 초 뒤에 띄울지(인트로 연출 끝 타이밍 맞추기용)")]
    [SerializeField, Min(0f)] float autoOpenDelaySec = 1.2f;

    [Header("Test Options")]
    [Tooltip("테스트 중엔 Stage01에 들어갈 때마다 항상 튜토리얼이 뜨게 함(SeenKey 무시)")]
    [SerializeField] bool forceShowEveryTimeInStage01 = true;

    Coroutine _autoOpenCo;
    int _pageIndex = 0;

    public bool IsOpen => tutorialPanelRoot != null && tutorialPanelRoot.activeSelf;
    void Awake()
    {
        SceneManager.activeSceneChanged += OnActiveSceneChanged;

        if (prevButton != null) prevButton.onClick.AddListener(PrevPage);
        if (nextButton != null) nextButton.onClick.AddListener(NextPage);
    }

    void OnDestroy()
    {
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;

        if (prevButton != null) prevButton.onClick.RemoveListener(PrevPage);
        if (nextButton != null) nextButton.onClick.RemoveListener(NextPage);
    }

    void Start()
    {
        Apply(SceneManager.GetActiveScene().name);
    }

    void OnActiveSceneChanged(Scene prev, Scene next)
    {
        Apply(next.name);
    }

    void Apply(string sceneName)
    {
        bool isStage = !string.IsNullOrEmpty(stagePrefix) &&
                       sceneName.StartsWith(stagePrefix, StringComparison.OrdinalIgnoreCase);

        // 튜토리얼 버튼은 스테이지에서만
        if (tutorialButtonRoot != null)
            tutorialButtonRoot.SetActive(isStage);

        // 씬 바뀌면 패널은 기본 닫기 + 페이지도 전부 끔
        if (tutorialPanelRoot != null)
            tutorialPanelRoot.SetActive(false);

        DeactivateAllPages();

        // 진행 중인 자동오픈 코루틴 정리
        if (_autoOpenCo != null)
        {
            StopCoroutine(_autoOpenCo);
            _autoOpenCo = null;
        }

        // 네비 버튼 상태도 기본 갱신
        RefreshNavUI();

        // Stage01 자동 오픈(딜레이 포함)
        if (isStage && autoOpenOnStage01 &&
            string.Equals(sceneName, stage01Name, StringComparison.OrdinalIgnoreCase))
        {
            bool alreadySeen = PlayerPrefs.GetInt(seenKeyStage01, 0) == 1;

            if (forceShowEveryTimeInStage01 || !alreadySeen)
                _autoOpenCo = StartCoroutine(CoAutoOpenAfterDelay(autoOpenDelaySec));
        }
    }

    IEnumerator CoAutoOpenAfterDelay(float delay)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        OpenTutorial();
        _autoOpenCo = null;
    }

    // TutorialButton OnClick
    public void OpenTutorial()
    {
        if (tutorialPanelRoot != null)
            tutorialPanelRoot.SetActive(true);

        // 열릴 때 1페이지부터 보여주고 싶으면 0
        SetPage(0);
    }

    // TutorialPanel CloseButton OnClick
    public void CloseTutorial()
    {
        if (tutorialPanelRoot != null)
            tutorialPanelRoot.SetActive(false);

        DeactivateAllPages();
        RefreshNavUI();

        // Stage01이면 "봤음" 처리
        string sceneName = SceneManager.GetActiveScene().name;
        if (string.Equals(sceneName, stage01Name, StringComparison.OrdinalIgnoreCase))
        {
            PlayerPrefs.SetInt(seenKeyStage01, 1);
            PlayerPrefs.Save();
        }
    }

    public void TriggerStage01AutoTutorialNow()
    {
        if (_autoOpenCo != null)
        {
            StopCoroutine(_autoOpenCo);
            _autoOpenCo = null;
        }
        OpenTutorial();
    }

    // =========================================================
    // Paging (SetActive)
    // =========================================================

    public void NextPage()
    {
        if (!HasPages()) return;

        int next = _pageIndex + 1;
        if (next >= pageRoots.Length)
        {
            if (!wrapAround) next = pageRoots.Length - 1;
            else next = 0;
        }
        SetPage(next);
    }

    public void PrevPage()
    {
        if (!HasPages()) return;

        int prev = _pageIndex - 1;
        if (prev < 0)
        {
            if (!wrapAround) prev = 0;
            else prev = pageRoots.Length - 1;
        }
        SetPage(prev);
    }

    public void SetPage(int index)
    {
        if (!HasPages())
        {
            _pageIndex = 0;
            RefreshNavUI();
            return;
        }

        _pageIndex = Mathf.Clamp(index, 0, pageRoots.Length - 1);

        // 현재 페이지만 켠다
        for (int i = 0; i < pageRoots.Length; i++)
        {
            var go = pageRoots[i];
            if (go == null) continue;
            go.SetActive(i == _pageIndex);
        }

        RefreshNavUI();
    }

    void RefreshNavUI()
    {
        bool has = HasPages();

        if (pageIndicatorText != null)
            pageIndicatorText.text = has ? $"{_pageIndex + 1} / {pageRoots.Length}" : "";

        bool prevInteractable = has && (wrapAround || _pageIndex > 0);
        bool nextInteractable = has && (wrapAround || _pageIndex < pageRoots.Length - 1);

        if (prevButton != null)
            prevButton.interactable = prevInteractable;

        if (nextButton != null)
            nextButton.interactable = nextInteractable;

        ApplyGraphicColors(prevButtonTintTargets, prevInteractable ? navEnabledColor : navDisabledColor);
        ApplyGraphicColors(nextButtonTintTargets, nextInteractable ? navEnabledColor : navDisabledColor);
    }

    void ApplyGraphicColors(Graphic[] targets, Color color)
    {
        if (targets == null) return;

        for (int i = 0; i < targets.Length; i++)
        {
            if (targets[i] == null) continue;
            targets[i].color = color;
        }
    }

    void DeactivateAllPages()
    {
        if (pageRoots == null) return;
        for (int i = 0; i < pageRoots.Length; i++)
        {
            if (pageRoots[i] != null)
                pageRoots[i].SetActive(false);
        }
    }

    bool HasPages()
    {
        return pageRoots != null && pageRoots.Length > 0;
    }
}