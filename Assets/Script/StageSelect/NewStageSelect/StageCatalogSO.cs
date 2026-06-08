using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Stage Catalog")]
public class StageCatalogSO : ScriptableObject
{
    public List<ChapterEntry> chapters = new List<ChapterEntry>();

    public bool TryGetStage(string stageId, out StageEntry result)
    {
        result = null;
        if (string.IsNullOrEmpty(stageId)) return false;

        for (int i = 0; i < chapters.Count; i++)
        {
            var chapter = chapters[i];
            if (chapter == null || chapter.stages == null) continue;

            for (int j = 0; j < chapter.stages.Count; j++)
            {
                var stage = chapter.stages[j];
                if (stage == null) continue;

                if (string.Equals(stage.stageId, stageId, StringComparison.OrdinalIgnoreCase))
                {
                    result = stage;
                    return true;
                }
            }
        }

        return false;
    }

    public bool TryGetNextMainStage(string clearedStageId, out StageEntry result)
    {
        result = null;

        for (int i = 0; i < chapters.Count; i++)
        {
            var chapter = chapters[i];
            if (chapter == null || chapter.stages == null) continue;

            for (int j = 0; j < chapter.stages.Count; j++)
            {
                var stage = chapter.stages[j];
                if (stage == null) continue;

                if (!string.Equals(stage.stageId, clearedStageId, StringComparison.OrdinalIgnoreCase))
                    continue;

                for (int k = j + 1; k < chapter.stages.Count; k++)
                {
                    var next = chapter.stages[k];
                    if (next == null) continue;
                    if (next.isExtra) continue;

                    result = next;
                    return true;
                }

                return false;
            }
        }

        return false;
    }

    public List<StageEntry> GetStagesInChapter(int chapterIndex)
    {
        if (chapterIndex < 0 || chapterIndex >= chapters.Count)
            return null;

        return chapters[chapterIndex].stages;
    }
}

[Serializable]
public class ChapterEntry
{
    public string chapterId;
    public string displayName;
    public List<StageEntry> stages = new List<StageEntry>();
}

[Serializable]
public class StageEntry
{
    public string stageId;      // 저장/진행도 기준
    public string sceneName;    // 실제 씬 로드 이름
    public string displayName;  // UI 표시용
    public bool isExtra;        // 추가 스테이지 여부
}