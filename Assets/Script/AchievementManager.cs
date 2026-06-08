using UnityEngine;
using Steamworks;


public class AchievementManager : MonoBehaviour
{
    public static AchievementManager I;

    [SerializeField] StageOrderAsset stageOrderAsset;

    [SerializeField] int chapter2UnlockRequiredClearCount = 10;
    [SerializeField] int chapter3UnlockRequiredClearCount = 28;

    const string LocalAchievementPrefix = "LOCAL_ACHIEVEMENT_";

    void Awake()
    {
        if (I != null) { Destroy(gameObject); return; }
        I = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        CheckAchievements();
        SyncLocalAchievementsToSteam();
    }

    void SyncLocalAchievementsToSteam()
    {
        if (stageOrderAsset != null && stageOrderAsset.IsDemoBuild())
            return;

        string[] achievementIds =
        {
        "ACH_TUTORIAL_CLEAR",
        "ACH_CHAPTER_1_CLEAR",
        "ACH_CHAPTER_2_CLEAR",
        "ACH_CHAPTER_3_CLEAR",
        "ACH_CHAPTER_2_UNLOCK",
        "ACH_CHAPTER_3_UNLOCK",
        "ACH_FIRST_EXTRA_CLEAR",
        "ACH_BUTTERFLY_CLEAR",
        "ACH_SNAIL_CLEAR",
        "ACH_ENDING",
        "ACH_ALL_CLEAR"
    };

        foreach (var id in achievementIds)
        {
            if (IsLocalAchievementUnlocked(id))
                TrySyncAchievementToSteam(id);
        }
    }

    public void CheckAchievements()
    {
        if (StageProgressManager.I == null) return;
        if (stageOrderAsset == null) return;

        CheckTutorialClear();
        CheckChapterClear(1, "ACH_CHAPTER_1_CLEAR");
        CheckChapterClear(2, "ACH_CHAPTER_2_CLEAR");
        CheckChapterClear(3, "ACH_CHAPTER_3_CLEAR");

        CheckChapterUnlocked(2, "ACH_CHAPTER_2_UNLOCK");
        CheckChapterUnlocked(3, "ACH_CHAPTER_3_UNLOCK");

        CheckFirstExtraClear();
        CheckHiddenClears();
        CheckAllClear();
        UpdateSteamProgressStats();
    }

    void Unlock(string achievementId)
    {
        if (stageOrderAsset != null && stageOrderAsset.IsDemoBuild())
        {
            Debug.Log($"[Achievement] Demo build - skip unlock: {achievementId}");
            return;
        }

        if (IsLocalAchievementUnlocked(achievementId))
        {
            TrySyncAchievementToSteam(achievementId);
            return;
        }

        SetLocalAchievementUnlocked(achievementId);

        Debug.Log($"[Achievement] Unlock Local: {achievementId}");

        TrySyncAchievementToSteam(achievementId);
    }

    bool IsLocalAchievementUnlocked(string achievementId)
    {
        return PlayerPrefs.GetInt(LocalAchievementPrefix + achievementId, 0) == 1;
    }

    void SetLocalAchievementUnlocked(string achievementId)
    {
        PlayerPrefs.SetInt(LocalAchievementPrefix + achievementId, 1);
        PlayerPrefs.Save();
    }

    void TrySyncAchievementToSteam(string achievementId)
    {
        if (!SteamManager.Initialized) return;

        SteamUserStats.GetAchievement(achievementId, out bool achieved);
        if (achieved) return;

        SteamUserStats.SetAchievement(achievementId);
        SteamUserStats.StoreStats();

        Debug.Log($"[Achievement] Sync Steam: {achievementId}");
    }

    void UpdateSteamProgressStats()
    {
        if (stageOrderAsset != null && stageOrderAsset.IsDemoBuild())
            return;

        if (!SteamManager.Initialized) return;

        SetProgress(
            "STAT_TUTORIAL_CLEAR_PROGRESS",
            "ACH_TUTORIAL_CLEAR",
            GetTutorialProgress(),
            GetTutorialProgressMax()
        );

        SetProgress(
            "STAT_CHAPTER_1_CLEAR_PROGRESS",
            "ACH_CHAPTER_1_CLEAR",
            GetChapterClearProgress(1),
            GetChapterClearProgressMax(1)
        );

        SetProgress(
            "STAT_CHAPTER_2_CLEAR_PROGRESS",
            "ACH_CHAPTER_2_CLEAR",
            GetChapterClearProgress(2),
            GetChapterClearProgressMax(2)
        );

        SetProgress(
            "STAT_CHAPTER_3_CLEAR_PROGRESS",
            "ACH_CHAPTER_3_CLEAR",
            GetChapterClearProgress(3),
            GetChapterClearProgressMax(3)
        );

        SetProgress(
            "STAT_CHAPTER_2_UNLOCK_PROGRESS",
            "ACH_CHAPTER_2_UNLOCK",
            GetChapterUnlockProgress(2),
            GetChapterUnlockProgressMax(2)
        );

        SetProgress(
            "STAT_CHAPTER_3_UNLOCK_PROGRESS",
            "ACH_CHAPTER_3_UNLOCK",
            GetChapterUnlockProgress(3),
            GetChapterUnlockProgressMax(3)
        );

        SteamUserStats.StoreStats();
    }

    void SetProgress(string statId, string achievementId, int current, int max)
    {
        SteamUserStats.SetStat(statId, current);

        Debug.Log($"[SteamStat] {statId}: {current} / {max}");
    }

    void CheckTutorialClear()
    {
        var stages = stageOrderAsset.GetMainStages(0); // 챕터 1

        if (stages.Length < 3) return;

        if (StageProgressManager.I.IsCleared(stages[0]) &&
            StageProgressManager.I.IsCleared(stages[1]) &&
            StageProgressManager.I.IsCleared(stages[2]))
        {
            Unlock("ACH_TUTORIAL_CLEAR");
        }
    }

    void CheckChapterClear(int chapterIndex, string achievementId)
    {
        int cleared = StageProgressManager.I.GetMainClearedCountInChapter(chapterIndex);
        int total = StageProgressManager.I.GetMainStageCountInChapter(chapterIndex);

        if (total > 0 && cleared >= total)
            Unlock(achievementId);
    }

    void CheckChapterUnlocked(int chapterIndex, string achievementId)
    {
        int required = 0;

        if (chapterIndex == 2)
            required = chapter2UnlockRequiredClearCount;
        else if (chapterIndex == 3)
            required = chapter3UnlockRequiredClearCount;
        else
            return;

        int clearedCount = StageProgressManager.I.GetTotalClearedCount();

        if (clearedCount >= required)
            Unlock(achievementId);
    }

    void CheckFirstExtraClear()
    {
        for (int chapter = 1; chapter <= 3; chapter++)
        {
            if (StageProgressManager.I.GetExtraClearedCountInChapter(chapter) > 0)
            {
                Unlock("ACH_FIRST_EXTRA_CLEAR");
                return;
            }
        }
    }

    void CheckHiddenClears()
    {
        string butterflyStageId = GetExtraStageIdByGlobalIndex(8);
        string snailStageId = GetExtraStageIdByGlobalIndex(12);

        if (!string.IsNullOrEmpty(butterflyStageId) &&
            StageProgressManager.I.IsCleared(butterflyStageId))
        {
            Unlock("ACH_BUTTERFLY_CLEAR");
        }

        if (!string.IsNullOrEmpty(snailStageId) &&
            StageProgressManager.I.IsCleared(snailStageId))
        {
            Unlock("ACH_SNAIL_CLEAR");
        }
    }

    string GetExtraStageIdByGlobalIndex(int extraNumber)
    {
        int current = 0;

        for (int chapter = 0; chapter < stageOrderAsset.mainStageSequences.Length; chapter++)
        {
            var extras = stageOrderAsset.GetExtraStages(chapter);

            for (int i = 0; i < extras.Length; i++)
            {
                current++;

                if (current == extraNumber)
                    return extras[i];
            }
        }

        return null;
    }

    void CheckAllClear()
    {
        // 일단 나중에 정확한 전체 스테이지 목록 기준으로 구현 추천
    }

    public void UnlockEndingAchievement()
    {
        Unlock("ACH_ENDING");
    }

    public int GetTutorialProgress()
    {
        if (stageOrderAsset == null || StageProgressManager.I == null)
            return 0;

        var stages = stageOrderAsset.GetMainStages(0);

        int count = 0;
        int max = Mathf.Min(3, stages.Length);

        for (int i = 0; i < max; i++)
        {
            if (StageProgressManager.I.IsCleared(stages[i]))
                count++;
        }

        return count;
    }

    public int GetTutorialProgressMax()
    {
        return 3;
    }

    public int GetChapterClearProgress(int chapterIndex)
    {
        if (StageProgressManager.I == null)
            return 0;

        return StageProgressManager.I.GetMainClearedCountInChapter(chapterIndex);
    }

    public int GetChapterClearProgressMax(int chapterIndex)
    {
        if (StageProgressManager.I == null)
            return 0;

        return StageProgressManager.I.GetMainStageCountInChapter(chapterIndex);
    }

    public int GetChapterUnlockProgress(int chapterIndex)
    {
        if (StageProgressManager.I == null)
            return 0;

        int required = GetChapterUnlockRequiredCount(chapterIndex);
        int cleared = StageProgressManager.I.GetTotalClearedCount();

        return Mathf.Clamp(cleared, 0, required);
    }

    public int GetChapterUnlockProgressMax(int chapterIndex)
    {
        return GetChapterUnlockRequiredCount(chapterIndex);
    }

    int GetChapterUnlockRequiredCount(int chapterIndex)
    {
        if (chapterIndex == 2)
            return chapter2UnlockRequiredClearCount;

        if (chapterIndex == 3)
            return chapter3UnlockRequiredClearCount;

        return 0;
    }

    [ContextMenu("Reset Steam Achievements Stats And Local Progress")]
    public void ResetSteamAchievementsStatsAndLocalProgress()
    {
        StageProgressManager.I?.ClearAllProgress();

        ClearLocalAchievements();

        if (!SteamManager.Initialized)
        {
            Debug.LogWarning("[Achievement] SteamManager not initialized. Local progress cleared only.");
            return;
        }

        SteamUserStats.ResetAllStats(true);
        SteamUserStats.StoreStats();

        Debug.Log("[Achievement] Reset Steam achievements, stats, and local progress.");
    }

    void ClearLocalAchievements()
    {
        string[] achievementIds =
        {
        "ACH_TUTORIAL_CLEAR",
        "ACH_CHAPTER_1_CLEAR",
        "ACH_CHAPTER_2_CLEAR",
        "ACH_CHAPTER_3_CLEAR",
        "ACH_CHAPTER_2_UNLOCK",
        "ACH_CHAPTER_3_UNLOCK",
        "ACH_FIRST_EXTRA_CLEAR",
        "ACH_BUTTERFLY_CLEAR",
        "ACH_SNAIL_CLEAR",
        "ACH_ENDING",
        "ACH_ALL_CLEAR"
    };

        foreach (var id in achievementIds)
        {
            PlayerPrefs.DeleteKey(LocalAchievementPrefix + id);
        }

        PlayerPrefs.Save();
    }

    public void UnlockTrueEndingAchievement()
    {
        Unlock("ACH_ALL_CLEAR");
    }
}