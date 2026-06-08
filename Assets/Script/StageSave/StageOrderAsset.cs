using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Stage Order Asset", fileName = "StageOrderAsset")]
public class StageOrderAsset : ScriptableObject
{
    [Header("Chapter Sequences")]
    public StageSequence[] mainStageSequences;

    [Header("Extra Unlock Rules")]
    public UnlockRule[] unlockRules;

    [Header("Demo")]
    [Tooltip("체크하면 demoAllowedStageIds에 있는 스테이지만 플레이 가능")]
    public bool isDemoBuild;

    [Tooltip("데모에서 플레이 가능한 스테이지 ID 목록")]
    public string[] demoAllowedStageIds;

    public bool IsDemoBuild()
    {
        return isDemoBuild;
    }

    public bool IsDemoAllowedStage(string stageId)
    {
        if (!isDemoBuild)
            return true;

        if (string.IsNullOrWhiteSpace(stageId))
            return false;

        if (demoAllowedStageIds == null)
            return false;

        for (int i = 0; i < demoAllowedStageIds.Length; i++)
        {
            if (string.Equals(demoAllowedStageIds[i], stageId, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    [Serializable]
    public struct StageSequence
    {
        public string name;

        [Tooltip("이 순서대로 메인 스테이지 해금/표시")]
        public string[] stageIds;

        [Tooltip("이 챕터에 속하는 엑스트라 스테이지들")]
        public string[] extraStageIds;
    }

    [Serializable]
    public struct UnlockRule
    {
        [Tooltip("이 스테이지를 클리어하면")]
        public string clearStageId;

        [Tooltip("이 스테이지가 해금된다")]
        public string unlockStageId;
    }

    public string[] GetMainStages(int sequenceIndex)
    {
        if (mainStageSequences == null) return Array.Empty<string>();
        if (sequenceIndex < 0 || sequenceIndex >= mainStageSequences.Length) return Array.Empty<string>();

        return mainStageSequences[sequenceIndex].stageIds ?? Array.Empty<string>();
    }

    public string[] GetExtraStages(int sequenceIndex)
    {
        if (mainStageSequences == null) return Array.Empty<string>();
        if (sequenceIndex < 0 || sequenceIndex >= mainStageSequences.Length) return Array.Empty<string>();

        return mainStageSequences[sequenceIndex].extraStageIds ?? Array.Empty<string>();
    }

    public int FindSequenceIndexByMainStage(string stageId)
    {
        if (string.IsNullOrWhiteSpace(stageId) || mainStageSequences == null)
            return -1;

        for (int i = 0; i < mainStageSequences.Length; i++)
        {
            var ids = mainStageSequences[i].stageIds;
            if (ids == null) continue;

            for (int j = 0; j < ids.Length; j++)
            {
                if (ids[j] == stageId)
                    return i;
            }
        }

        return -1;
    }

    public int FindSequenceIndexByExtraStage(string stageId)
    {
        if (string.IsNullOrWhiteSpace(stageId) || mainStageSequences == null)
            return -1;

        for (int i = 0; i < mainStageSequences.Length; i++)
        {
            var ids = mainStageSequences[i].extraStageIds;
            if (ids == null) continue;

            for (int j = 0; j < ids.Length; j++)
            {
                if (ids[j] == stageId)
                    return i;
            }
        }

        return -1;
    }

    public bool IsExtraStage(string stageId)
    {
        return FindSequenceIndexByExtraStage(stageId) >= 0;
    }

    public bool IsMainStage(string stageId)
    {
        return FindSequenceIndexByMainStage(stageId) >= 0;
    }

    public string BuildStageLabel(
    string stageId,
    string mainStageFormat = "Stage {0}-{1}",
    string extraStageFormat = "Extra {0}",
    string unknownText = "")
    {
        if (string.IsNullOrWhiteSpace(stageId))
            return unknownText;

        int chapterIndex = FindSequenceIndexByMainStage(stageId);
        if (chapterIndex >= 0)
        {
            var ids = GetMainStages(chapterIndex);
            for (int i = 0; i < ids.Length; i++)
            {
                if (string.Equals(ids[i], stageId, StringComparison.OrdinalIgnoreCase))
                {
                    int chapterNumber = chapterIndex + 1;
                    int stageNumber = i + 1;
                    return string.Format(mainStageFormat, chapterNumber, stageNumber);
                }
            }
        }

        chapterIndex = FindSequenceIndexByExtraStage(stageId);
        if (chapterIndex >= 0)
        {
            int extraNumber = 0;

            for (int i = 0; i < chapterIndex; i++)
                extraNumber += GetExtraStages(i).Length;

            var currentExtras = GetExtraStages(chapterIndex);
            for (int i = 0; i < currentExtras.Length; i++)
            {
                if (string.Equals(currentExtras[i], stageId, StringComparison.OrdinalIgnoreCase))
                {
                    extraNumber += i + 1;
                    return string.Format(extraStageFormat, extraNumber);
                }
            }
        }

        return unknownText;
    }

}