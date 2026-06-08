using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class StageUndoHistoryManager : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] StageSaveManager stageSaveManager;

    [Header("Options")]
    [SerializeField] int maxHistoryCount = 30;

    readonly Stack<object> undoStack = new();
    readonly Stack<object> redoStack = new();

    bool _suppressCapture;
    bool _suppressResetFromRestoreEvent;
    bool _initialized;
    string _lastStageId;
    object _pendingBeginSnapshot;
    bool _restoreRunning;
    public bool CanUndo => undoStack.Count > 1;
    public bool CanRedo => redoStack.Count > 0;

    void Reset()
    {
        if (stageSaveManager == null)
            stageSaveManager = FindFirstObjectByType<StageSaveManager>();
    }

    void OnEnable()
    {
        if (stageSaveManager == null)
            stageSaveManager = FindFirstObjectByType<StageSaveManager>();

        if (stageSaveManager != null)
        {
            stageSaveManager.OnStageChangeBeginSnapshotCaptured += HandleStageChangeBeginSnapshotCaptured;
            stageSaveManager.OnStageChangedCommitted += HandleStageChangedCommitted;
            stageSaveManager.OnStageRestored += HandleStageRestored;
            stageSaveManager.OnStageChangeBeginCanceled += HandleStageChangeBeginCanceled;

        }


        SceneManager.activeSceneChanged += HandleActiveSceneChanged;
    }

    void Start()
    {
        //StartCoroutine(CoInitializeAtStart());
    }

    void OnDisable()
    {
        if (stageSaveManager != null)
        {
            stageSaveManager.OnStageChangeBeginSnapshotCaptured -= HandleStageChangeBeginSnapshotCaptured;
            stageSaveManager.OnStageChangedCommitted -= HandleStageChangedCommitted;
            stageSaveManager.OnStageRestored -= HandleStageRestored;
            stageSaveManager.OnStageChangeBeginCanceled -= HandleStageChangeBeginCanceled;
        }

        SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
    }

    void HandleStageChangeBeginCanceled()
    {
        _pendingBeginSnapshot = null;
        Debug.Log("[UndoHistory] Begin snapshot canceled.");
    }

    IEnumerator CoInitializeAtStart()
    {
        yield return null;
        yield return new WaitForEndOfFrame();

        ForceResetForCurrentStage("Start");
    }

    void HandleStageRestored()
    {
        if (_suppressResetFromRestoreEvent)
            return;

        StartCoroutine(CoResetAfterRestoreStable());
    }

    IEnumerator CoResetAfterRestoreStable()
    {
        yield return null;
        yield return new WaitForEndOfFrame();

        ForceResetForCurrentStage("OnStageRestored");
    }


    void HandleActiveSceneChanged(Scene oldScene, Scene newScene)
    {
        StartCoroutine(CoResetAfterSceneChanged());
    }

    IEnumerator CoResetAfterSceneChanged()
    {
        yield return null;
        yield return new WaitForEndOfFrame();

        ForceResetForCurrentStage("SceneChanged");
    }

    void HandleStageChangeBeginSnapshotCaptured(object snapshot)
    {
        if (_suppressCapture) return;
        if (!_initialized) return;

        if (PuzzleSnapshotManager.IsBulkClearingPlaced)
            return;

        string currentStageId = GetCurrentStageId();
        if (string.IsNullOrEmpty(currentStageId)) return;

        if (_lastStageId != currentStageId)
        {
            ForceResetForCurrentStage("StageIdChangedDuringBeginCapture");
            return;
        }

        if (snapshot == null) return;

        _pendingBeginSnapshot = snapshot;

        Debug.Log("[UndoHistory] Begin snapshot pending.");
    }

    void HandleStageChangedCommitted()
    {
        if (_suppressCapture) return;
        if (!_initialized) return;

        if (PuzzleSnapshotManager.IsBulkClearingPlaced)
            return;

        string currentStageId = GetCurrentStageId();
        if (string.IsNullOrEmpty(currentStageId)) return;

        if (_lastStageId != currentStageId)
        {
            ForceResetForCurrentStage("StageIdChangedDuringCommit");
            return;
        }

        // 1) begin 시점 snapshot 먼저 반영
        if (_pendingBeginSnapshot != null)
        {
            bool shouldPushBegin = true;

            if (undoStack.Count > 0)
            {
                var top = undoStack.Peek();
                if (AreSnapshotsEquivalent(top, _pendingBeginSnapshot))
                    shouldPushBegin = false;
            }


            if (shouldPushBegin)
            {
                undoStack.Push(_pendingBeginSnapshot);
                TrimUndoStack();
            }

            _pendingBeginSnapshot = null;
        }

        // 2) commit 후 현재 상태 반영
        CaptureCurrentState(clearRedo: true);

        Debug.Log($"[UndoHistory] Commit captured. undo={undoStack.Count}, redo={redoStack.Count}");
    }

    string GetCurrentStageId()
    {
        if (stageSaveManager == null)
            return null;

        return stageSaveManager.GetCurrentStageIdForUndo();
    }

    void ForceResetForCurrentStage(string reason)
    {
        if (stageSaveManager == null)
            return;

        string currentStageId = GetCurrentStageId();
        if (string.IsNullOrEmpty(currentStageId))
            return;

        undoStack.Clear();
        redoStack.Clear();
        _pendingBeginSnapshot = null;

        _lastStageId = currentStageId;
        _initialized = false;

        CaptureCurrentState(clearRedo: false);

        _initialized = true;

        _pendingBeginSnapshot = null;
        Debug.Log($"[UndoHistory] Reset history for stage={currentStageId}, reason={reason}, undoCount={undoStack.Count}");
    }

    void CaptureCurrentState(bool clearRedo)
    {
        if (stageSaveManager == null) return;

        var snapshot = stageSaveManager.CaptureRuntimeSnapshot();
        if (snapshot == null) return;

        if (undoStack.Count > 0)
        {
            var top = undoStack.Peek();
            if (AreSnapshotsEquivalent(top, snapshot))
                return;
        }

        undoStack.Push(snapshot);
        TrimUndoStack();

        if (clearRedo)
            redoStack.Clear();

        Debug.Log($"[UndoHistory] Captured. undo={undoStack.Count}, redo={redoStack.Count}");
    }

    void TrimUndoStack()
    {
        if (undoStack.Count <= maxHistoryCount) return;

        var temp = new List<object>(undoStack);
        temp.Reverse();

        while (temp.Count > maxHistoryCount)
            temp.RemoveAt(0);

        undoStack.Clear();

        for (int i = 0; i < temp.Count; i++)
            undoStack.Push(temp[i]);
    }

    public void Undo()
    {
        if (_restoreRunning || StageSaveManager.IsRestoringNow)
        {
            Debug.Log("[UndoHistory] Undo blocked: restore running.");
            return;
        }

        if (!CanUndo || stageSaveManager == null)
        {
            Debug.Log("[UndoHistory] Undo blocked.");
            return;
        }

        var current = undoStack.Pop();
        redoStack.Push(current);

        var target = undoStack.Peek();
        StartCoroutine(CoRestoreWithoutRecapture(target));
    }

    public void Redo()
    {
        if (_restoreRunning || StageSaveManager.IsRestoringNow)
        {
            Debug.Log("[UndoHistory] Redo blocked: restore running.");
            return;
        }

        if (!CanRedo || stageSaveManager == null)
        {
            Debug.Log("[UndoHistory] Redo blocked.");
            return;
        }

        var target = redoStack.Pop();
        undoStack.Push(target);

        StartCoroutine(CoRestoreWithoutRecapture(target));
    }

    IEnumerator CoRestoreWithoutRecapture(object snapshot)
    {
        if (_restoreRunning)
            yield break;

        _restoreRunning = true;
        _suppressCapture = true;
        _suppressResetFromRestoreEvent = true;
        _pendingBeginSnapshot = null;
        StageSaveManager.PushSuppressStageChangedNotify();

        Debug.Log("[UndoHistory] Restore begin.");

        yield return stageSaveManager.RestoreRuntimeSnapshotCo(snapshot);

        yield return null;
        yield return new WaitForEndOfFrame();
        yield return null;
        yield return new WaitForEndOfFrame();

        stageSaveManager.ForceSaveCurrentStage();

        StageSaveManager.PopSuppressStageChangedNotify();
        _suppressResetFromRestoreEvent = false;
        _suppressCapture = false;
        _restoreRunning = false;

        Debug.Log($"[UndoHistory] Restore end. undo={undoStack.Count}, redo={redoStack.Count}");

        _pendingBeginSnapshot = null;
    }

    bool AreSnapshotsEquivalent(object a, object b)
    {
        if (a == null || b == null) return false;
        return JsonUtility.ToJson(a) == JsonUtility.ToJson(b);
    }
}