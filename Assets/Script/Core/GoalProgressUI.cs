using System.Collections.Generic;
using UnityEngine;

public class GoalProgressUI : MonoBehaviour
{
    [Header("UI Root")]
    [SerializeField] GameObject visualRoot;
    [SerializeField] RectTransform container;

    [Header("Slot Prefab")]
    [Tooltip("GoalProgressSlotUI°¡ ºÙ¾îÀÖ´Â ½½·Ô ÇÁ¸®ÆÕ")]
    [SerializeField] GoalProgressSlotUI slotPrefab;

    [Header("Layout")]
    [SerializeField] bool clearChildrenOnRebuild = true;

    readonly List<GoalProgressSlotUI> _slots = new List<GoalProgressSlotUI>(8);

    void OnEnable()
    {
        GameModeManager.OnGoalProgressChanged += HandleGoalProgressChanged;
        GameModeManager.OnGameReset += HandleGameReset;
        GameModeManager.OnModeChanged += HandleModeChanged;

        ApplyCurrentModeVisual();
    }

    void OnDisable()
    {
        GameModeManager.OnGoalProgressChanged -= HandleGoalProgressChanged;
        GameModeManager.OnGameReset -= HandleGameReset;
        GameModeManager.OnModeChanged -= HandleModeChanged;
    }

    void Start()
    {
        ApplyCurrentModeVisual();
    }

    void HandleGoalProgressChanged(int reached, int required)
    {
        RebuildIfNeeded(required);
        RefreshFill(reached);
    }

    void HandleGameReset()
    {
        ClearAllSlots();
    }

    void HandleModeChanged(GameMode mode)
    {
        SetVisualVisible(mode == GameMode.Play);

        if (mode == GameMode.Build)
        {
            ClearAllSlots();
        }
    }

    void ApplyCurrentModeVisual()
    {
        var gm = GameModeManager.Instance;
        bool isPlay = gm != null && gm.currentMode == GameMode.Play;

        SetVisualVisible(isPlay);

        if (!isPlay)
            ClearAllSlots();
    }

    void SetVisualVisible(bool visible)
    {
        if (visualRoot != null)
            visualRoot.SetActive(visible);
    }

    void RebuildIfNeeded(int required)
    {
        if (required < 0) required = 0;

        if (_slots.Count == required)
            return;

        Rebuild(required);
    }

    void Rebuild(int count)
    {
        ClearSlotObjects();

        if (container == null || slotPrefab == null)
        {
            Debug.LogWarning("[GoalProgressUI] container or slotPrefab is missing.");
            return;
        }

        for (int i = 0; i < count; i++)
        {
            GoalProgressSlotUI slot = Instantiate(slotPrefab, container);
            slot.SetImmediateEmpty();
            _slots.Add(slot);
        }
    }

    void RefreshFill(int reached)
    {
        if (reached < 0) reached = 0;

        for (int i = 0; i < _slots.Count; i++)
        {
            if (_slots[i] == null) continue;
            _slots[i].SetFilled(i < reached);
        }
    }

    void ClearAllSlots()
    {
        for (int i = 0; i < _slots.Count; i++)
        {
            if (_slots[i] == null) continue;
            _slots[i].SetImmediateEmpty();
        }
    }

    void ClearSlotObjects()
    {
        _slots.Clear();

        if (container == null) return;
        if (!clearChildrenOnRebuild) return;

        for (int i = container.childCount - 1; i >= 0; i--)
        {
            Destroy(container.GetChild(i).gameObject);
        }
    }
}