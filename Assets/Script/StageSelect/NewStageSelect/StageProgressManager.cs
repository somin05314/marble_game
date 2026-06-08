using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using System.Text;
public class StageProgressManager : MonoBehaviour
{
    public static StageProgressManager I;

    [Header("Save")]
    [SerializeField] bool autoSaveOnPauseQuit = true;

    [SerializeField] string demoFileName = "stage_progress_demo.json";
    [SerializeField] string fullFileName = "stage_progress.json";

    [Header("Shared Stage Order")]
    [SerializeField] StageOrderAsset stageOrderAsset;

    ProgressData _data;
    HashSet<string> _clearedSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    HashSet<string> _skippedSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    bool _loaded;
    bool _dirty;

    public StageOrderAsset StageOrderAsset => stageOrderAsset;

    void Awake()
    {
        if (I != null) { Destroy(gameObject); return; }
        I = this;
        DontDestroyOnLoad(gameObject);

        Load();
        Debug.Log($"[StageProgress] path = {Application.persistentDataPath}");
    }

    void OnApplicationPause(bool pause)
    {
        if (!autoSaveOnPauseQuit) return;
        if (pause) SaveIfDirty();
    }

    void OnApplicationQuit()
    {
        if (!autoSaveOnPauseQuit) return;
        SaveIfDirty();
    }

    public void MarkCleared(string stageId)
    {
        EnsureLoaded();

        Debug.Log($"[StageProgress] MarkCleared called: {stageId}, achievementManager={AchievementManager.I != null}");

        if (string.IsNullOrEmpty(stageId))
        {
            Debug.LogWarning("[StageProgress] MarkCleared called with empty stageId.");
            return;
        }

        bool changed = false;

        // 스킵했던 스테이지를 나중에 직접 클리어하면 스킵 해제
        if (_skippedSet.Remove(stageId))
        {
            _data.skippedStages = new List<string>(_skippedSet);
            changed = true;
        }

        if (_clearedSet.Add(stageId))
        {
            _data.clearedStages = new List<string>(_clearedSet);
            changed = true;
        }

        if (changed)
        {
            _dirty = true;
            SaveIfDirty();

            AchievementManager.I?.CheckAchievements();
        }
    }

    public void MarkSkipped(string stageId)
    {
        EnsureLoaded();

        if (string.IsNullOrEmpty(stageId))
        {
            Debug.LogWarning("[StageProgress] MarkSkipped called with empty stageId.");
            return;
        }

        // 이미 클리어한 스테이지는 굳이 스킵으로 바꾸지 않음
        if (_clearedSet.Contains(stageId))
            return;

        if (_skippedSet.Add(stageId))
        {
            _data.skippedStages = new List<string>(_skippedSet);
            _dirty = true;
            SaveIfDirty();
        }
    }

    public bool IsCleared(string stageId)
    {
        EnsureLoaded();
        if (string.IsNullOrEmpty(stageId)) return false;
        return _clearedSet.Contains(stageId);
    }

    public bool IsSkipped(string stageId)
    {
        EnsureLoaded();
        if (string.IsNullOrEmpty(stageId)) return false;
        return _skippedSet.Contains(stageId);
    }

    public bool IsCompleted(string stageId)
    {
        EnsureLoaded();
        if (string.IsNullOrEmpty(stageId)) return false;
        return _clearedSet.Contains(stageId) || _skippedSet.Contains(stageId);
    }

    public bool IsUnlocked(string stageId)
    {
        EnsureLoaded();
        if (string.IsNullOrEmpty(stageId)) return false;

        // 0) 데모 버전 컷
        if (IsBlockedByDemoCut(stageId))
            return false;

        // 1) 메인 스테이지 순서 기반 해금
        if (TryIsUnlockedByMainSequence(stageId, out bool unlockedBySequence))
            return unlockedBySequence;

        // 2) extra / 분기 해금 룰
        var rules = stageOrderAsset != null ? stageOrderAsset.unlockRules : null;
        if (rules != null)
        {
            for (int i = 0; i < rules.Length; i++)
            {
                var r = rules[i];
                if (string.IsNullOrEmpty(r.unlockStageId) || string.IsNullOrEmpty(r.clearStageId))
                    continue;

                // 특수 해금은 진짜 클리어 기준 유지
                if (StringEqualsIgnoreCase(r.unlockStageId, stageId) && IsCleared(r.clearStageId))
                    return true;
            }
        }

        return false;
    }

    bool TryIsUnlockedByMainSequence(string stageId, out bool unlocked)
    {
        unlocked = false;

        if (stageOrderAsset == null || stageOrderAsset.mainStageSequences == null)
            return false;

        var sequences = stageOrderAsset.mainStageSequences;

        for (int i = 0; i < sequences.Length; i++)
        {
            var seq = sequences[i];
            if (seq.stageIds == null || seq.stageIds.Length == 0)
                continue;

            for (int j = 0; j < seq.stageIds.Length; j++)
            {
                var id = seq.stageIds[j];
                if (string.IsNullOrWhiteSpace(id))
                    continue;

                if (!StringEqualsIgnoreCase(id, stageId))
                    continue;

                // 각 시퀀스 첫 스테이지는 항상 해금
                if (j == 0)
                {
                    unlocked = true;
                    return true;
                }

                string prevId = FindPreviousValidStageId(seq.stageIds, j - 1);

                // 일반 다음 스테이지 해금은 "클리어 or 스킵"
                unlocked = !string.IsNullOrEmpty(prevId) && IsCompleted(prevId);
                return true;
            }
        }

        return false;
    }

    string FindPreviousValidStageId(string[] stageIds, int startIndex)
    {
        if (stageIds == null) return null;

        for (int i = startIndex; i >= 0; i--)
        {
            if (!string.IsNullOrWhiteSpace(stageIds[i]))
                return stageIds[i];
        }

        return null;
    }

    static bool StringEqualsIgnoreCase(string a, string b)
        => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    public void ClearAllProgress()
    {
        EnsureLoaded();
        _clearedSet.Clear();
        _skippedSet.Clear();
        _data.clearedStages = new List<string>();
        _data.skippedStages = new List<string>();
        _dirty = true;
        SaveIfDirty();
    }

    void EnsureLoaded()
    {
        if (_loaded) return;
        Load();
    }

    string CurrentFileName
    {
        get
        {
            if (stageOrderAsset != null && stageOrderAsset.IsDemoBuild())
                return demoFileName;

            return fullFileName;
        }
    }

    string SavePath => Path.Combine(Application.persistentDataPath, CurrentFileName);
    string BackupPath => SavePath + ".bak";
    string TempPath => SavePath + ".tmp";

    public void Load()
    {
        _loaded = true;

        _data = new ProgressData
        {
            version = 3,
            clearedStages = new List<string>(),
            skippedStages = new List<string>()
        };

        _clearedSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        _skippedSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (TryLoadFromPath(SavePath, out var loaded))
        {
            ApplyLoaded(loaded);
            return;
        }

        if (TryLoadFromPath(BackupPath, out loaded))
        {
            Debug.LogWarning("[StageProgress] Main save corrupted/missing. Restored from .bak");
            ApplyLoaded(loaded);
            _dirty = true;
            SaveIfDirty();
            return;
        }

        _dirty = true;
        SaveIfDirty();
    }

    bool TryLoadFromPath(string path, out ProgressData loaded)
    {
        loaded = null;

        try
        {
            if (!File.Exists(path)) return false;

            string text = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(text)) return false;

            string json;

            try
            {
                byte[] bytes = Convert.FromBase64String(text);
                json = Encoding.UTF8.GetString(bytes);
            }
            catch
            {
                // 기존 JSON 저장파일 호환
                json = text;
            }

            loaded = JsonUtility.FromJson<ProgressData>(json);
            return loaded != null;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[StageProgress] Load failed: {path}\n{e}");
            return false;
        }
    }

    void ApplyLoaded(ProgressData loaded)
    {
        if (loaded == null || loaded.version < 3)
        {
            _data = new ProgressData
            {
                version = 3,
                clearedStages = new List<string>(),
                skippedStages = new List<string>()
            };

            _clearedSet.Clear();
            _skippedSet.Clear();

            PlayerPrefs.DeleteKey("LastStageScene");
            PlayerPrefs.DeleteKey("HasPlayed");
            PlayerPrefs.Save();

            _dirty = true;
            SaveIfDirty();
            return;
        }

        _data = loaded;

        _clearedSet.Clear();
        _skippedSet.Clear();

        if (_data.clearedStages != null)
        {
            for (int i = 0; i < _data.clearedStages.Count; i++)
            {
                var id = _data.clearedStages[i];
                if (!string.IsNullOrEmpty(id))
                    _clearedSet.Add(id);
            }
        }

        if (_data.skippedStages != null)
        {
            for (int i = 0; i < _data.skippedStages.Count; i++)
            {
                var id = _data.skippedStages[i];
                if (!string.IsNullOrEmpty(id) && !_clearedSet.Contains(id))
                    _skippedSet.Add(id);
            }
        }

        _data.version = 3;
        _data.clearedStages = new List<string>(_clearedSet);
        _data.skippedStages = new List<string>(_skippedSet);
        _dirty = false;
    }

    public void SaveIfDirty()
    {
        if (!_dirty) return;
        Save();
    }

    public void Save()
    {
        EnsureLoaded();

        try
        {
            Directory.CreateDirectory(Application.persistentDataPath);

            _data.version = 3;
            _data.lastSavedUtc = DateTime.UtcNow.ToString("o");
            _data.clearedStages = new List<string>(_clearedSet);
            _data.skippedStages = new List<string>(_skippedSet);

            string json = JsonUtility.ToJson(_data, prettyPrint: false);
            string encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));

            File.WriteAllText(TempPath, encoded);

            if (File.Exists(SavePath))
            {
                File.Copy(SavePath, BackupPath, overwrite: true);
                File.Delete(SavePath);
            }

            File.Move(TempPath, SavePath);
            _dirty = false;
        }
        catch (Exception e)
        {
            Debug.LogError($"[StageProgress] Save failed: {SavePath}\n{e}");
        }
        finally
        {
            try { if (File.Exists(TempPath)) File.Delete(TempPath); } catch { }
        }
    }

    bool IsBlockedByDemoCut(string stageId)
    {
        if (stageOrderAsset == null)
            return false;

        return !stageOrderAsset.IsDemoAllowedStage(stageId);
    }

    int FindStageIndex(string[] stageIds, string targetStageId)
    {
        if (stageIds == null || string.IsNullOrWhiteSpace(targetStageId))
            return -1;

        for (int i = 0; i < stageIds.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(stageIds[i]))
                continue;

            if (StringEqualsIgnoreCase(stageIds[i], targetStageId))
                return i;
        }

        return -1;
    }

    public bool IsBlockedByDemo(string stageId)
    {
        EnsureLoaded();
        if (string.IsNullOrEmpty(stageId)) return false;
        return IsBlockedByDemoCut(stageId);
    }

    public bool IsUnlockedIgnoringDemoCut(string stageId)
    {
        EnsureLoaded();
        if (string.IsNullOrEmpty(stageId)) return false;

        if (TryIsUnlockedByMainSequence(stageId, out bool unlockedBySequence))
            return unlockedBySequence;

        var rules = stageOrderAsset != null ? stageOrderAsset.unlockRules : null;
        if (rules != null)
        {
            for (int i = 0; i < rules.Length; i++)
            {
                var r = rules[i];
                if (string.IsNullOrEmpty(r.unlockStageId) || string.IsNullOrEmpty(r.clearStageId))
                    continue;

                if (StringEqualsIgnoreCase(r.unlockStageId, stageId) && IsCleared(r.clearStageId))
                    return true;
            }
        }

        return false;
    }

    public bool TryGetChapterIndexOfStage(string stageId, out int chapterIndex)
    {
        EnsureLoaded();

        chapterIndex = -1;

        if (string.IsNullOrWhiteSpace(stageId))
            return false;

        if (stageOrderAsset == null || stageOrderAsset.mainStageSequences == null)
            return false;

        var sequences = stageOrderAsset.mainStageSequences;

        for (int i = 0; i < sequences.Length; i++)
        {
            var seq = sequences[i];
            if (seq.stageIds == null || seq.stageIds.Length == 0)
                continue;

            for (int j = 0; j < seq.stageIds.Length; j++)
            {
                var id = seq.stageIds[j];
                if (string.IsNullOrWhiteSpace(id))
                    continue;

                if (StringEqualsIgnoreCase(id, stageId))
                {
                    chapterIndex = i + 1;
                    return true;
                }
            }
        }

        return false;
    }

    public int GetClearedCountInChapter(int chapterIndex)
    {
        EnsureLoaded();

        return GetMainClearedCountInChapter(chapterIndex)
             + GetExtraClearedCountInChapter(chapterIndex);
    }

    public int GetMainClearedCountInChapter(int chapterIndex)
    {
        EnsureLoaded();

        if (chapterIndex <= 0)
            return 0;

        if (stageOrderAsset == null || stageOrderAsset.mainStageSequences == null)
            return 0;

        int seqIndex = chapterIndex - 1;
        if (seqIndex < 0 || seqIndex >= stageOrderAsset.mainStageSequences.Length)
            return 0;

        var seq = stageOrderAsset.mainStageSequences[seqIndex];
        if (seq.stageIds == null || seq.stageIds.Length == 0)
            return 0;

        int count = 0;

        for (int i = 0; i < seq.stageIds.Length; i++)
        {
            string stageId = seq.stageIds[i];
            if (string.IsNullOrWhiteSpace(stageId))
                continue;

            if (IsCleared(stageId))
                count++;
        }

        return count;
    }

    public int GetExtraClearedCountInChapter(int chapterIndex)
    {
        EnsureLoaded();

        if (chapterIndex <= 0)
            return 0;

        if (stageOrderAsset == null || stageOrderAsset.mainStageSequences == null)
            return 0;

        int seqIndex = chapterIndex - 1;
        if (seqIndex < 0 || seqIndex >= stageOrderAsset.mainStageSequences.Length)
            return 0;

        var seq = stageOrderAsset.mainStageSequences[seqIndex];
        if (seq.extraStageIds == null || seq.extraStageIds.Length == 0)
            return 0;

        int count = 0;

        for (int i = 0; i < seq.extraStageIds.Length; i++)
        {
            string stageId = seq.extraStageIds[i];
            if (string.IsNullOrWhiteSpace(stageId))
                continue;

            if (IsCleared(stageId))
                count++;
        }

        return count;
    }

    public int GetMainStageCountInChapter(int chapterIndex)
    {
        if (chapterIndex <= 0)
            return 0;

        if (stageOrderAsset == null || stageOrderAsset.mainStageSequences == null)
            return 0;

        int seqIndex = chapterIndex - 1;
        if (seqIndex < 0 || seqIndex >= stageOrderAsset.mainStageSequences.Length)
            return 0;

        var seq = stageOrderAsset.mainStageSequences[seqIndex];
        if (seq.stageIds == null)
            return 0;

        int count = 0;

        for (int i = 0; i < seq.stageIds.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(seq.stageIds[i]))
                count++;
        }

        return count;
    }

    public int GetExtraStageCountInChapter(int chapterIndex)
    {
        if (chapterIndex <= 0)
            return 0;

        if (stageOrderAsset == null || stageOrderAsset.mainStageSequences == null)
            return 0;

        int seqIndex = chapterIndex - 1;
        if (seqIndex < 0 || seqIndex >= stageOrderAsset.mainStageSequences.Length)
            return 0;

        var seq = stageOrderAsset.mainStageSequences[seqIndex];
        if (seq.extraStageIds == null)
            return 0;

        int count = 0;

        for (int i = 0; i < seq.extraStageIds.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(seq.extraStageIds[i]))
                count++;
        }

        return count;
    }

    [Serializable]
    class ProgressData
    {
        public int version = 3;
        public string lastSavedUtc;
        public List<string> clearedStages = new List<string>();
        public List<string> skippedStages = new List<string>();
    }

    public int GetTotalClearedCount()
    {
        EnsureLoaded();

        if (stageOrderAsset == null || stageOrderAsset.mainStageSequences == null)
            return 0;

        int total = 0;

        for (int i = 0; i < stageOrderAsset.mainStageSequences.Length; i++)
        {
            int chapterIndex = i + 1;
            total += GetClearedCountInChapter(chapterIndex);
        }

        return total;
    }
}