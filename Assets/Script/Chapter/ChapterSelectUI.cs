using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public class ChapterSelectUI : MonoBehaviour, IBeginDragHandler, IEndDragHandler, IDragHandler
{
    [Header("Click vs Drag")]
    [Tooltip("이 픽셀 이상 움직이면 '드래그'로 판단하고 클릭을 무시")]
    [SerializeField] float dragThresholdPx = 10f;

    [Header("Text Format")]
    [SerializeField] string progressFormat = "{0} / {1}";
    [SerializeField] string unlockRequirementProgressFormat = "{0} / {1}";

    bool _dragging;
    Vector2 _dragStartPos;

    [System.Serializable]
    public class ChapterEntry
    {
        public int chapterIndex = 1;

        [Tooltip("이 챕터 버튼")]
        public Button button;

        [Tooltip("잠금 오버레이(잠금일 때 켜짐)")]
        public GameObject lockOverlay;

        [Tooltip("전체 클리어 수가 이 개수 이상이면 해금됨. 0이면 항상 해금")]
        public int requiredTotalClearCount = 0;

        [Header("Progress UI")]
        [Tooltip("메인 진행도 텍스트 (예: 3 / 16)")]
        public TMP_Text mainProgressText;

        [Tooltip("엑스트라 진행도 텍스트 (예: 1 / 4)")]
        public TMP_Text extraProgressText;

        [Header("Unlock Requirement UI")]
        [Tooltip("잠겨 있을 때만 보이고, 챕터 해금 시 숨길 텍스트")]
        public TMP_Text unlockRequirementText;
    }

    [Header("Chapters")]
    [SerializeField] ChapterEntry[] chapters;

    void Start() => RefreshLocks();
    void OnEnable() => RefreshLocks();

    public void RefreshLocks()
    {
        for (int i = 0; i < (chapters?.Length ?? 0); i++)
        {
            var e = chapters[i];
            if (e == null) continue;

            bool unlocked = IsChapterUnlocked(e);

            if (e.button != null)
                e.button.interactable = unlocked;

            if (e.lockOverlay != null)
                e.lockOverlay.SetActive(!unlocked);

            RefreshProgressText(e);
            RefreshUnlockRequirementText(e, unlocked);
        }
    }

    void RefreshProgressText(ChapterEntry e)
    {
        if (e == null)
            return;

        // 엔딩 / 진엔딩일 경우
        if (e.chapterIndex == 999 || e.chapterIndex == 1000)
        {
            int cleared = 0;

            if (StageProgressManager.I != null)
                cleared = StageProgressManager.I.GetTotalClearedCount();

            if (e.mainProgressText != null)
            {
                if (e.chapterIndex == 999)
                    e.mainProgressText.text = $"{Mathf.Clamp(cleared, 0, 48)} / 48";
                else
                    e.mainProgressText.text = $"{cleared} / 60";
            }

            if (e.extraProgressText != null)
                e.extraProgressText.text = "";

            return;
        }

        int mainCleared = 0;
        int extraCleared = 0;
        int mainTotal = 0;
        int extraTotal = 0;

        if (StageProgressManager.I != null)
        {
            mainCleared = StageProgressManager.I.GetMainClearedCountInChapter(e.chapterIndex);
            extraCleared = StageProgressManager.I.GetExtraClearedCountInChapter(e.chapterIndex);

            mainTotal = StageProgressManager.I.GetMainStageCountInChapter(e.chapterIndex);
            extraTotal = StageProgressManager.I.GetExtraStageCountInChapter(e.chapterIndex);
        }

        if (e.mainProgressText != null)
            e.mainProgressText.text = $"{mainCleared} / {mainTotal}";

        if (e.extraProgressText != null)
            e.extraProgressText.text = $"Ex {extraCleared} / {extraTotal}";
    }

    void RefreshUnlockRequirementText(ChapterEntry e, bool unlocked)
    {
        if (e == null || e.unlockRequirementText == null)
            return;

        bool hasUnlockRequirement = e.chapterIndex > 1 && e.requiredTotalClearCount > 0;
        bool show = hasUnlockRequirement && !unlocked;

        e.unlockRequirementText.gameObject.SetActive(show);

        if (!show)
            return;

        int clearedCount = 0;

        if (StageProgressManager.I != null)
            clearedCount = StageProgressManager.I.GetTotalClearedCount();

        clearedCount = Mathf.Clamp(clearedCount, 0, e.requiredTotalClearCount);

        e.unlockRequirementText.text = string.Format(
            unlockRequirementProgressFormat,
            clearedCount,
            e.requiredTotalClearCount
        );
    }
    bool IsChapterUnlocked(ChapterEntry e)
    {
        if (e == null)
            return false;

        // 챕터 1은 항상 해금
        if (e.chapterIndex <= 1)
            return true;

        // 조건이 0 이하면 항상 해금
        if (e.requiredTotalClearCount <= 0)
            return true;

        if (StageProgressManager.I == null)
            return false;

        int clearedCount = StageProgressManager.I.GetTotalClearedCount();

        return clearedCount >= e.requiredTotalClearCount;
    }

    // ---- Drag Handling ----
    public void OnBeginDrag(PointerEventData eventData)
    {
        _dragging = true;
        _dragStartPos = eventData.position;
    }

    public void OnDrag(PointerEventData eventData) { }

    public void OnEndDrag(PointerEventData eventData)
    {
        _dragging = false;
    }

    bool DidDrag(Vector2 releasePos)
        => Vector2.Distance(_dragStartPos, releasePos) >= dragThresholdPx;

    // ---- Buttons ----
    public void OnClickChapter(int chapterIndex)
    {
        if (_dragging) return;

        var entry = FindEntry(chapterIndex);
        if (entry != null && !IsChapterUnlocked(entry))
        {
            Debug.Log($"[ChapterSelectUI] Chapter {chapterIndex} locked.");
            return;
        }

        if (SceneFlow.I == null)
        {
            Debug.LogError("[ChapterSelectUI] SceneFlow.I is null");
            return;
        }

        if (chapterIndex == 999)
        {
            SceneFlow.I.GoEndingScene();
            return;
        }

        if (chapterIndex == 1000)
        {
            SceneFlow.I.GoTrueEndingScene();
            return;
        }

        SceneFlow.I.GoStageSelectForChapter(chapterIndex);
    }

    ChapterEntry FindEntry(int chapterIndex)
    {
        for (int i = 0; i < (chapters?.Length ?? 0); i++)
        {
            var e = chapters[i];
            if (e != null && e.chapterIndex == chapterIndex)
                return e;
        }
        return null;
    }

    public void OnClickBack()
    {
        if (SceneFlow.I != null)
            SceneFlow.I.GoBack();
    }

}