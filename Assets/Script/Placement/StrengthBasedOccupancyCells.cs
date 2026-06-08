using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class StrengthBasedOccupancyCells : MonoBehaviour, IOccupancyCellProvider
{
    [Serializable]
    public struct LevelCells
    {
        public int level;
        public Vector2Int[] cellOffsets;
    }

    [Header("Strength")]
    [SerializeField, Min(1)] int currentLevel = 1;
    [SerializeField, Min(1)] int minLevel = 1;
    [SerializeField, Min(1)] int maxLevel = 3;

#if UNITY_EDITOR
    [Header("Editor Bridge")]
    [Tooltip("기존 PlacementObject 점유영역 설정 툴로 현재 레벨을 편집할 때 켜둠")]
    [SerializeField] bool editCurrentLevelThroughPlacementTool = false;

    [Tooltip("기존 툴로 편집할 레벨")]
    [SerializeField, Min(1)] int editLevel = 1;
#endif

    [Header("Cells By Level")]
    [SerializeField] LevelCells[] levelCells = Array.Empty<LevelCells>();

    [Serializable]
    public struct LevelActiveObjects
    {
        public int level;
        public GameObject[] targets;
    }

    [Header("Active Objects By Level")]
    [SerializeField] LevelActiveObjects[] levelActiveObjects = Array.Empty<LevelActiveObjects>();

    bool initializedFromPlacementData = false;
    bool externalInitialLevelApplied = false;

    public int CurrentLevel => currentLevel;
    public int MinLevel => minLevel;
    public int MaxLevel => maxLevel;

    public event System.Action<int> OnLevelChanged;

#if UNITY_EDITOR
    public int EditLevel => editLevel;
    public bool EditCurrentLevelThroughPlacementTool
    {
        get => editCurrentLevelThroughPlacementTool;
        set => editCurrentLevelThroughPlacementTool = value;
    }
#endif

    void Awake()
    {
        ApplyLevelActiveObjects(currentLevel);
    }

    public bool SetLevel(int level)
    {
        int clamped = Mathf.Clamp(level, minLevel, maxLevel);
        if (currentLevel == clamped)
            return false;

        currentLevel = clamped;
        ApplyLevelActiveObjects(currentLevel);
        OnLevelChanged?.Invoke(currentLevel);
        return true;
    }

    // 외부에서 시작 강도를 처음 한 번만 적용
    public void ApplyExternalInitialLevelOnce(int level, bool invokeEvent = false)
    {
        if (externalInitialLevelApplied) return;

        currentLevel = Mathf.Clamp(level, minLevel, maxLevel);
        ApplyLevelActiveObjects(currentLevel);

        externalInitialLevelApplied = true;

        if (invokeEvent)
            OnLevelChanged?.Invoke(currentLevel);
    }

    public bool TryGetOccupancyCellOffsets(List<Vector2Int> outOffsets)
    {
        outOffsets.Clear();

#if UNITY_EDITOR
        if (!Application.isPlaying && editCurrentLevelThroughPlacementTool)
        {
            var po = GetComponent<PlacementObject>();
            if (po != null)
            {
                var manual = po.EditorGetManualCellOffsetsCopy();
                if (manual != null && manual.Length > 0)
                {
                    for (int i = 0; i < manual.Length; i++)
                        outOffsets.Add(manual[i]);

                    return outOffsets.Count > 0;
                }
            }
        }
#endif

        var src = GetOffsetsForLevel(currentLevel);
        if (src == null || src.Length == 0)
            return false;

        for (int i = 0; i < src.Length; i++)
            outOffsets.Add(src[i]);

        return outOffsets.Count > 0;
    }

    public Vector2Int[] GetOffsetsForLevel(int level)
    {
        for (int i = 0; i < levelCells.Length; i++)
        {
            if (levelCells[i].level == level)
                return levelCells[i].cellOffsets;
        }

        return null;
    }

    public void PreviewLevelActiveObjects(int previewLevel)
    {
        int clamped = Mathf.Clamp(previewLevel, minLevel, maxLevel);
        ApplyLevelActiveObjects(clamped);
    }

    public void ApplyDefaultFromPlacementData(PlacementData data, bool force = false)
    {
        if (data == null) return;
        if (!data.allowStrengthControl) return;

        // 외부 초기 강도가 이미 적용됐으면 PlacementData로 덮어쓰지 않음
        if (externalInitialLevelApplied && !force)
            return;

        if (initializedFromPlacementData && !force)
            return;

        currentLevel = Mathf.Clamp(data.defaultStrengthLevel, minLevel, maxLevel);
        initializedFromPlacementData = true;
        ApplyLevelActiveObjects(currentLevel);
    }

    public void RestoreCurrentLevelActiveObjects()
    {
        ApplyLevelActiveObjects(currentLevel);
    }

    void ApplyLevelActiveObjects(int level)
    {
        if (levelActiveObjects == null || levelActiveObjects.Length == 0)
            return;

        Dictionary<GameObject, bool> finalStates = new Dictionary<GameObject, bool>();

        for (int i = 0; i < levelActiveObjects.Length; i++)
        {
            var entry = levelActiveObjects[i];
            bool shouldBeOnForThisEntry = (entry.level == level);

            if (entry.targets == null) continue;

            for (int j = 0; j < entry.targets.Length; j++)
            {
                var go = entry.targets[j];
                if (go == null) continue;

                if (!finalStates.ContainsKey(go))
                    finalStates.Add(go, false);

                if (shouldBeOnForThisEntry)
                    finalStates[go] = true;
            }
        }

        foreach (var kv in finalStates)
        {
            if (kv.Key != null)
                kv.Key.SetActive(kv.Value);
        }
    }

    public bool IsTargetOrParentActiveAtLevel(Transform target, int level)
    {
        if (target == null) return false;

        Transform t = target;
        while (t != null)
        {
            if (IsTargetActiveAtLevel(t.gameObject, level))
                return true;

            t = t.parent;
        }

        return false;
    }

    public bool IsTargetActiveAtLevel(GameObject target, int level)
    {
        if (target == null) return false;
        if (levelActiveObjects == null || levelActiveObjects.Length == 0)
            return false;

        for (int i = 0; i < levelActiveObjects.Length; i++)
        {
            var entry = levelActiveObjects[i];
            if (entry.level != level) continue;
            if (entry.targets == null) continue;

            for (int j = 0; j < entry.targets.Length; j++)
            {
                if (entry.targets[j] == target)
                    return true;
            }
        }

        return false;
    }
#if UNITY_EDITOR
    public void EditorLoadEditLevelToPlacementTool()
    {
        var po = GetComponent<PlacementObject>();
        if (po == null)
        {
            Debug.LogWarning("[StrengthBasedOccupancyCells] PlacementObject가 필요합니다.", this);
            return;
        }

        var src = GetOffsetsForLevel(editLevel);
        if (src == null)
            src = Array.Empty<Vector2Int>();

        po.EditorSetManualCellOffsets(src);

        editCurrentLevelThroughPlacementTool = true;

        UnityEditor.EditorUtility.SetDirty(po);
        UnityEditor.EditorUtility.SetDirty(this);
    }

    public void EditorSavePlacementToolToEditLevel()
    {
        var po = GetComponent<PlacementObject>();
        if (po == null)
        {
            Debug.LogWarning("[StrengthBasedOccupancyCells] PlacementObject가 필요합니다.", this);
            return;
        }

        var manual = po.EditorGetManualCellOffsetsCopy();
        SetOffsetsForLevel(editLevel, manual);

        UnityEditor.EditorUtility.SetDirty(this);
    }

    public void EditorStopEditingThroughPlacementTool()
    {
        editCurrentLevelThroughPlacementTool = false;
        UnityEditor.EditorUtility.SetDirty(this);
    }

    void SetOffsetsForLevel(int level, Vector2Int[] offsets)
    {
        for (int i = 0; i < levelCells.Length; i++)
        {
            if (levelCells[i].level == level)
            {
                levelCells[i].cellOffsets = (offsets != null)
                    ? (Vector2Int[])offsets.Clone()
                    : Array.Empty<Vector2Int>();
                return;
            }
        }

        Array.Resize(ref levelCells, levelCells.Length + 1);
        levelCells[levelCells.Length - 1] = new LevelCells
        {
            level = level,
            cellOffsets = (offsets != null)
                ? (Vector2Int[])offsets.Clone()
                : Array.Empty<Vector2Int>()
        };
    }

    

    


    [ContextMenu("Editor/Load Edit Level To Placement Tool")]
    void ContextLoadEditLevelToPlacementTool() => EditorLoadEditLevelToPlacementTool();

    [ContextMenu("Editor/Save Placement Tool To Edit Level")]
    void ContextSavePlacementToolToEditLevel() => EditorSavePlacementToolToEditLevel();

    [ContextMenu("Editor/Stop Editing Through Placement Tool")]
    void ContextStopEditingThroughPlacementTool() => EditorStopEditingThroughPlacementTool();

    void OnValidate()
    {
        if (minLevel < 1) minLevel = 1;
        if (maxLevel < minLevel) maxLevel = minLevel;
        currentLevel = Mathf.Clamp(currentLevel, minLevel, maxLevel);
        editLevel = Mathf.Clamp(editLevel, minLevel, maxLevel);
    }
#else
    void OnValidate()
    {
        if (minLevel < 1) minLevel = 1;
        if (maxLevel < minLevel) maxLevel = minLevel;
        currentLevel = Mathf.Clamp(currentLevel, minLevel, maxLevel);
    }
#endif
}