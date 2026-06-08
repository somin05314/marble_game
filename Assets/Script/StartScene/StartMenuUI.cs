using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class StartMenuUI : MonoBehaviour
{
    [Header("Buttons")]
    [SerializeField] Button startOrContinueButton;
    [SerializeField] Button stageSelectButton;
    [SerializeField] Button optionsButton;
    [SerializeField] Button exitButton;

    [Header("Stage Order")]
    [SerializeField] StageOrderAsset stageOrderAsset;

    [Header("UI")]
    [SerializeField] TMP_Text startOrContinueLabel;

    [Header("Format")]
    [SerializeField] string mainStageFormat = "Stage {0}-{1}";
    [SerializeField] string extraStageFormat = "Extra {0}";
    [SerializeField] string unknownText = "Continue";

    const string PREF_LAST_STAGE = "LastStageScene";
    const string PREF_HAS_PLAYED = "HasPlayed";

    void Start()
    {
        if (startOrContinueButton != null) startOrContinueButton.onClick.AddListener(OnClickStartOrContinue);
        if (stageSelectButton != null) stageSelectButton.onClick.AddListener(OnClickStageSelect);
        if (optionsButton != null) optionsButton.onClick.AddListener(OnClickOptions);
        if (exitButton != null) exitButton.onClick.AddListener(OnClickExit);

        RefreshStartLabel();
    }

    void OnEnable()
    {
        RefreshStartLabel();
    }

    void RefreshStartLabel()
    {
        bool hasPlayed = PlayerPrefs.GetInt(PREF_HAS_PLAYED, 0) == 1;
        string last = PlayerPrefs.GetString(PREF_LAST_STAGE, "");

        bool hasPreviousStage = hasPlayed && !string.IsNullOrWhiteSpace(last);

        bool isValidContinue =
            hasPreviousStage &&
            StageProgressManager.I != null &&
            StageProgressManager.I.IsUnlocked(last) &&
            !StageProgressManager.I.IsBlockedByDemo(last);

        if (!isValidContinue)
        {
            if (startOrContinueButton != null)
                startOrContinueButton.gameObject.SetActive(false);

            return;
        }

        if (startOrContinueButton != null)
            startOrContinueButton.gameObject.SetActive(true);

        if (startOrContinueLabel != null)
        {
            if (stageOrderAsset != null)
            {
                startOrContinueLabel.text =
                    stageOrderAsset.BuildStageLabel(last, mainStageFormat, extraStageFormat, unknownText);
            }
            else
            {
                startOrContinueLabel.text = unknownText;
            }
        }
    }

    void OnClickStartOrContinue()
    {
        string last = PlayerPrefs.GetString(PREF_LAST_STAGE, "");

        if (string.IsNullOrWhiteSpace(last))
            return;

        if (StageProgressManager.I == null ||
            !StageProgressManager.I.IsUnlocked(last) ||
            StageProgressManager.I.IsBlockedByDemo(last))
        {
            SceneFlow.I.GoChapterSelect();
            return;
        }

        SceneFlow.I.GoStage(last);
    }

    void OnClickStageSelect()
    {
        SceneFlow.I.GoChapterSelect();
    }

    void OnClickOptions()
    {
        if (OptionsPanelController.I != null)
            OptionsPanelController.I.Toggle();
        else
            Debug.LogWarning("[StartMenuUI] OptionsPanelController.I is null");
    }

    void OnClickExit()
    {
        Application.Quit();
    }
}