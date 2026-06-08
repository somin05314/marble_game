using TMPro;
using UnityEngine;

public class StageDisplayUI : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] StageOrderAsset stageOrderAsset;
    [SerializeField] TMP_Text stageText;

    [Header("Format")]
    [SerializeField] string mainStageFormat = "Stage {0}-{1}";
    [SerializeField] string extraStageFormat = "Extra {0}";
    [SerializeField] string unknownText = "";

    string _lastStageId = null;

    void Start()
    {
        RefreshUI();
    }

    void Update()
    {
        string currentStageId = GetCurrentStageId();
        if (_lastStageId != currentStageId)
            RefreshUI();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (!Application.isPlaying)
            RefreshUI();
    }
#endif

    string GetCurrentStageId()
    {
        if (SceneFlow.I == null)
            return "";

        return SceneFlow.I.CurrentStageId;
    }

    public void RefreshUI()
    {
        if (stageText == null)
            return;

        string currentStageId = GetCurrentStageId();
        _lastStageId = currentStageId;

        Debug.Log($"[StageDisplayUI] CurrentStageId = '{currentStageId}'", this);

        stageText.text = BuildStageLabel(currentStageId);
    }

    string BuildStageLabel(string stageId)
    {
        if (stageOrderAsset == null || string.IsNullOrWhiteSpace(stageId))
            return unknownText;

        int chapterIndex = stageOrderAsset.FindSequenceIndexByMainStage(stageId);
        if (chapterIndex >= 0)
        {
            var ids = stageOrderAsset.GetMainStages(chapterIndex);
            for (int i = 0; i < ids.Length; i++)
            {
                if (ids[i] == stageId)
                {
                    int chapterNumber = chapterIndex + 1;
                    int stageNumber = i + 1;
                    return string.Format(mainStageFormat, chapterNumber, stageNumber);
                }
            }
        }

        chapterIndex = stageOrderAsset.FindSequenceIndexByExtraStage(stageId);
        if (chapterIndex >= 0)
        {
            int extraNumber = 0;

            for (int i = 0; i < chapterIndex; i++)
                extraNumber += stageOrderAsset.GetExtraStages(i).Length;

            var currentExtras = stageOrderAsset.GetExtraStages(chapterIndex);
            for (int i = 0; i < currentExtras.Length; i++)
            {
                if (currentExtras[i] == stageId)
                {
                    extraNumber += (i + 1);
                    return string.Format(extraStageFormat, extraNumber);
                }
            }
        }

        return unknownText;
    }
}