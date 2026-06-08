using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class StageSelectUI : MonoBehaviour
{
    [Header("Main Grid (manual ordered)")]
    [SerializeField] Transform buttonGridParent;

    [Header("Lock Grid (1:1 with Button Grid)")]
    [Tooltip("버튼 그리드와 동일한 순서/개수로 잠금 오버레이를 배치한 부모")]
    [SerializeField] Transform lockGridParent;

    [Header("Extra (free placement)")]
    [Tooltip("엑스트라 스테이지 버튼들을 자유 배치할 부모. GridLayoutGroup 붙이지 말 것!")]
    [SerializeField] Transform extraRoot;

    [Header("Shared Stage Order")]
    [SerializeField] StageOrderAsset stageOrderAsset;

    [Header("Main Sequence Index")]
    [Tooltip("StageOrderAsset 안에서 어떤 메인 시퀀스를 쓸지")]
    [SerializeField] int mainSequenceIndex = 0;

    [Header("Main Stage Labels (Optional)")]
    [Tooltip("비워두면 1,2,3...으로 표시. 값을 넣으면 해당 라벨 사용")]
    [SerializeField] string[] mainStageLabels;

    [Header("Extra Stage Labels (Optional)")]
    [Tooltip("비워두면 EX 1, EX 2, EX 3...으로 표시. 값을 넣으면 해당 라벨 사용")]
    [SerializeField] string[] extraStageLabels;

    [Header("Toast")]
    [SerializeField] SimpleToastUI toastUI;
    [SerializeField] string demoBlockedMessageKey = "toast.demo_end";
    [SerializeField] string lockedMessageKey = "toast.stage_locked";

    void Start()
    {
        RefreshAll();
    }

    void OnEnable()
    {
        RefreshAll();
    }

    public void RefreshAll()
    {
        BindMainGridButtons();
        BindExtraButtons();
    }

    void BindMainGridButtons()
    {
        if (buttonGridParent == null)
        {
            Debug.LogError("[StageSelectUI] buttonGridParent is null.");
            return;
        }

        if (stageOrderAsset == null)
        {
            Debug.LogError("[StageSelectUI] stageOrderAsset is null.");
            return;
        }

        string[] mainStageIds = stageOrderAsset.GetMainStages(mainSequenceIndex);
        if (mainStageIds == null)
        {
            Debug.LogWarning($"[StageSelectUI] mainStageIds is null. sequenceIndex={mainSequenceIndex}");
            return;
        }

        int buttonCount = buttonGridParent.childCount;
        int stageCount = mainStageIds.Length;
        int count = Mathf.Min(stageCount, buttonCount);

        if (lockGridParent != null)
        {
            int lockCount = lockGridParent.childCount;
            if (lockCount < count)
            {
                Debug.LogWarning(
                    $"[StageSelectUI] lockGridParent 자식 수({lockCount})가 버튼 수({count})보다 적음. " +
                    $"일부 버튼은 잠금 오버레이가 표시되지 않을 수 있음.");
            }
        }

        for (int i = 0; i < buttonCount; i++)
        {
            var buttonObj = buttonGridParent.GetChild(i).gameObject;
            var lockObj = GetLockObjectByIndex(i);

            bool active = i < count;
            buttonObj.SetActive(active);

            if (lockObj != null)
            {
                var img = lockObj.GetComponent<Image>();
                if (img != null)
                    img.enabled = active;
            }

            var stateIcon = buttonObj.GetComponent<StageButtonStateIcon>();
            if (stateIcon != null && !active)
                stateIcon.HideAll();

            if (!active)
                continue;

            var btn = buttonObj.GetComponent<Button>();
            if (btn == null)
            {
                Debug.LogWarning($"[StageSelectUI] ButtonGrid child '{buttonObj.name}' 에 Button 컴포넌트가 없음.");
                continue;
            }

            string stageId = mainStageIds[i];
            if (string.IsNullOrWhiteSpace(stageId))
            {
                Debug.LogWarning($"[StageSelectUI] mainStageIds[{i}] 가 비어있음.");
                buttonObj.SetActive(false);

                if (lockObj != null)
                {
                    var img = lockObj.GetComponent<Image>();
                    if (img != null)
                        img.enabled = false;
                }

                if (stateIcon != null)
                    stateIcon.HideAll();

                continue;
            }

            var label = btn.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null)
                label.text = GetMainStageLabel(i);

            ApplyLockAndBind(btn, lockObj, stageId);
            ApplyStateIcon(buttonObj, stageId);
        }

        if (stageCount > buttonCount)
        {
            Debug.LogWarning(
                $"[StageSelectUI] mainStageIds 개수({stageCount}) > 버튼 수({buttonCount}). " +
                $"뒤에 있는 stageId는 표시되지 않음.");
        }
    }

    void BindExtraButtons()
    {
        if (extraRoot == null) return;


        if (stageOrderAsset == null)
        {
            Debug.LogError("[StageSelectUI] stageOrderAsset is null.");
            return;
        }

        string[] extraStageIds = stageOrderAsset.GetExtraStages(mainSequenceIndex);
        if (extraStageIds == null)
        {
            Debug.LogWarning("[StageSelectUI] extraStageIds is null.");
            return;
        }

        var entries = extraRoot.GetComponentsInChildren<StageSelectEntry>(true);

        int entryCount = entries.Length;
        int stageCount = extraStageIds.Length;
        int count = Mathf.Min(entryCount, stageCount);

        for (int i = 0; i < entryCount; i++)
        {
            var e = entries[i];
            if (e == null) continue;

            var btn = e.GetComponent<Button>();
            if (btn == null)
            {
                Debug.LogWarning("[StageSelectUI] Extra entry에 Button이 없음.");
                continue;
            }

            bool active = i < count;
            e.gameObject.SetActive(active);

            if (!active)
                continue;

            string stageId = extraStageIds[i];
            if (string.IsNullOrWhiteSpace(stageId))
            {
                Debug.LogWarning($"[StageSelectUI] extraStageIds[{i}] 가 비어있음.");
                e.gameObject.SetActive(false);
                continue;
            }

            var label = btn.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null)
                label.text = GetExtraStageLabel(i);

            ApplyExtraVisualAndBind(e, btn, stageId);
            ApplyStateIcon(btn.gameObject, stageId);
        }

        if (stageCount > entryCount)
        {
            Debug.LogWarning(
                $"[StageSelectUI] extraStageIds 개수({stageCount}) > extra 버튼 수({entryCount}). " +
                $"뒤에 있는 extra stageId는 표시되지 않음.");
        }
    }

    bool IsDemoBuild()
    {
        return stageOrderAsset != null && stageOrderAsset.IsDemoBuild();
    }

    string GetExtraStageLabel(int index)
    {
        if (extraStageLabels != null &&
            index >= 0 &&
            index < extraStageLabels.Length &&
            !string.IsNullOrWhiteSpace(extraStageLabels[index]))
        {
            return extraStageLabels[index];
        }

        return $"EX {index + 1}";
    }

    void ApplyExtraVisualAndBind(StageSelectEntry entry, Button btn, string stageId)
    {
        if (entry == null || btn == null) return;

        bool unlocked = true;
        bool blockedByDemo = false;
        bool normallyUnlocked = true;

        if (StageProgressManager.I != null)
        {
            blockedByDemo = StageProgressManager.I.IsBlockedByDemo(stageId);
            unlocked = StageProgressManager.I.IsUnlocked(stageId);
            normallyUnlocked = StageProgressManager.I.IsUnlockedIgnoringDemoCut(stageId);
        }
        else
        {
            Debug.LogWarning("[StageSelectUI] StageProgressManager.I missing. Default unlocked=true");
        }

        bool visuallyUnlocked = unlocked || (blockedByDemo && normallyUnlocked);
        entry.ApplyVisual(visuallyUnlocked);

        btn.onClick.RemoveAllListeners();
        btn.interactable = true;

        btn.onClick.AddListener(() =>
        {
            if (blockedByDemo && normallyUnlocked)
            {
                UISoundManager.I?.PlayRelease();
                ShowToastByKey(demoBlockedMessageKey);
                return;
            }

            if (!unlocked)
            {
                UISoundManager.I?.PlayRelease();
                return;
            }

            UISoundManager.I?.PlayEnter();

            if (SceneFlow.I != null)
                SceneFlow.I.GoStage(stageId);
            else
                Debug.LogError("[StageSelectUI] SceneFlow.I missing.");
        });
    }

    void ApplyLockAndBind(Button btn, GameObject lockOverlayObj, string stageId)
    {
        bool unlocked = true;
        bool blockedByDemo = false;
        bool normallyUnlocked = true;

        if (StageProgressManager.I != null)
        {
            blockedByDemo = StageProgressManager.I.IsBlockedByDemo(stageId);
            unlocked = StageProgressManager.I.IsUnlocked(stageId);
            normallyUnlocked = StageProgressManager.I.IsUnlockedIgnoringDemoCut(stageId);
        }
        else
        {
            Debug.LogWarning("[StageSelectUI] StageProgressManager.I missing. Default unlocked=true");
        }


        bool showLockOverlay = !unlocked && !(blockedByDemo && normallyUnlocked);

        if (lockOverlayObj != null)
        {
            var img = lockOverlayObj.GetComponent<Image>();
            if (img != null)
                img.enabled = showLockOverlay;
        }

        btn.interactable = true;
        btn.onClick.RemoveAllListeners();

        btn.onClick.AddListener(() =>
        {
            if (blockedByDemo && normallyUnlocked)
            {
                if (UISoundManager.I != null)
                    UISoundManager.I.PlayRelease();

                ShowToastByKey(demoBlockedMessageKey);
                return;
            }

            if (!unlocked)
            {
                if (UISoundManager.I != null)
                    UISoundManager.I.PlayRelease();

                return;
            }

            if (UISoundManager.I != null)
                UISoundManager.I.PlayEnter();

            if (SceneFlow.I != null)
                SceneFlow.I.GoStage(stageId);
            else
                Debug.LogError("[StageSelectUI] SceneFlow.I missing.");
        });
    }

    void ShowToastByKey(string key)
    {
        string message = GetLocalizedText(key);

        if (toastUI != null)
            toastUI.Show(message);
        else
            Debug.Log(message);
    }

    string GetLocalizedText(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return string.Empty;

        if (LocalizationManager.I != null)
            return LocalizationManager.I.GetText(key);

        return key;
    }

    void ApplyStateIcon(GameObject buttonObj, string stageId)
    {
        if (buttonObj == null) return;

        var stateIcon = buttonObj.GetComponent<StageButtonStateIcon>();
        if (stateIcon == null) return;

        bool isCleared = false;
        bool isSkipped = false;

        if (StageProgressManager.I != null)
        {
            isCleared = StageProgressManager.I.IsCleared(stageId);
            isSkipped = StageProgressManager.I.IsSkipped(stageId);
        }

        if (isCleared)
            isSkipped = false;

        stateIcon.SetState(isCleared, isSkipped);
    }

    GameObject GetLockObjectByIndex(int index)
    {
        if (lockGridParent == null) return null;
        if (index < 0 || index >= lockGridParent.childCount) return null;
        return lockGridParent.GetChild(index).gameObject;
    }

    string GetMainStageLabel(int index)
    {
        if (mainStageLabels != null &&
            index >= 0 &&
            index < mainStageLabels.Length &&
            !string.IsNullOrWhiteSpace(mainStageLabels[index]))
        {
            return mainStageLabels[index];
        }

        return (index + 1).ToString();
    }
}