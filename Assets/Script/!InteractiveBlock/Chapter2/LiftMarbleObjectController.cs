using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LiftMarbleObjectController : MonoBehaviour, IPoResettable, IDragStateHandler, IPoDirectDemoPlayable
{
    [System.Serializable]
    public class LevelLiftSet
    {
        [Min(1)] public int level = 1;
        public List<PoMove> liftPlatforms = new List<PoMove>();
        public Transform startPoint;
        public Transform endPoint;
    }

    [Header("Strength")]
    [SerializeField] StrengthBasedOccupancyCells strengthComp;

    [Header("Level Sets")]
    [Tooltip("강도 1/2/3 별로 사용할 리프트, 시작점, 끝점을 지정")]
    [SerializeField] LevelLiftSet[] levelSets = new LevelLiftSet[3];

    [Header("Move")]
    [SerializeField, Min(0.01f)] float moveSpeed = 2f;

    [Tooltip("끝에 도착 후 시작점으로 돌아가고 한 프레임 쉼")]
    [SerializeField] bool waitOneFrameOnLoop = false;

    [Header("Audio")]
    [SerializeField] PoMachineAudioPlayer liftAudio;

    bool _dragLocked;
    bool _captured;
    bool _isRunning;

    int _activeLevel = 1;
    LevelLiftSet _activeSet;

    readonly Dictionary<PoMove, Vector3> _initialPositions = new Dictionary<PoMove, Vector3>();
    readonly List<Coroutine> _platformRoutines = new List<Coroutine>();

    public bool IsRunning => _isRunning;

    void Reset()
    {
        if (strengthComp == null)
            strengthComp = GetComponent<StrengthBasedOccupancyCells>();

        if (liftAudio == null)
            liftAudio = GetComponentInChildren<PoMachineAudioPlayer>(true);
    }

    void Awake()
    {
        if (strengthComp == null)
            strengthComp = GetComponent<StrengthBasedOccupancyCells>();

        _activeLevel = GetCurrentLevel();
        _activeSet = GetSetForLevel(_activeLevel);

        CaptureInitialState();
    }

    void OnEnable()
    {
        GameModeManager.OnModeChanged += HandleModeChanged;

        if (strengthComp != null)
            strengthComp.OnLevelChanged += HandleLevelChanged;
    }

    void OnDisable()
    {
        GameModeManager.OnModeChanged -= HandleModeChanged;

        if (strengthComp != null)
            strengthComp.OnLevelChanged -= HandleLevelChanged;
    }

    void Start()
    {
        // 시작 시 현재 모드 반영
        var modeManager = GameModeManager.Instance;
        if (modeManager != null)
            HandleModeChanged(modeManager.currentMode);
    }

    void HandleModeChanged(GameMode mode)
    {
        if (_dragLocked)
            return;

        if (mode == GameMode.Play)
        {
            RefreshActiveSetFromCurrentLevel();
            Play();
        }
        else
        {
            Stop();
            ResetState();
        }
    }

    void HandleLevelChanged(int level)
    {
        _activeLevel = level;
        _activeSet = GetSetForLevel(_activeLevel);

        CaptureInitialState();

        if (_dragLocked)
            return;

        bool shouldRun =
            GameModeManager.Instance != null &&
            GameModeManager.Instance.currentMode == GameMode.Play;

        if (shouldRun)
            Play();
        else
            ResetState();
    }

    public void BeginDragState()
    {
        _dragLocked = true;
        Stop();
        ResetState();
    }

    public void EndDragState(bool committed)
    {
        _dragLocked = false;

        RefreshActiveSetFromCurrentLevel();
        CaptureInitialState();

        bool shouldRun =
            GameModeManager.Instance != null &&
            GameModeManager.Instance.currentMode == GameMode.Play;

        if (shouldRun)
            Play();
        else
            ResetState();
    }

    public void CaptureInitialState()
    {
        _initialPositions.Clear();

        if (_activeSet == null || _activeSet.liftPlatforms == null)
        {
            _captured = true;
            return;
        }

        for (int i = 0; i < _activeSet.liftPlatforms.Count; i++)
        {
            var move = _activeSet.liftPlatforms[i];
            if (move == null) continue;

            _initialPositions[move] = move.GetStoredPosition();
        }

        _captured = true;
    }

    public void ResetState()
    {
        StopLoopOnly();

        RefreshActiveSetFromCurrentLevel();

        if (!_captured)
            CaptureInitialState();

        if (_activeSet == null || _activeSet.liftPlatforms == null)
            return;

        for (int i = 0; i < _activeSet.liftPlatforms.Count; i++)
        {
            var move = _activeSet.liftPlatforms[i];
            if (move == null) continue;

            if (_initialPositions.TryGetValue(move, out var pos))
                move.SetPositionImmediate(pos);
        }
    }

    public void Play()
    {
        if (_dragLocked) return;

        RefreshActiveSetFromCurrentLevel();

        if (!HasValidActiveSetup())
            return;

        StopLoopOnly();

        _isRunning = true;
        liftAudio?.PlayLoop();

        _platformRoutines.Clear();

        for (int i = 0; i < _activeSet.liftPlatforms.Count; i++)
        {
            var move = _activeSet.liftPlatforms[i];
            if (move == null) continue;

            _platformRoutines.Add(StartCoroutine(CoRunSinglePlatform(move, _activeSet)));
        }
    }

    public void Stop()
    {
        StopLoopOnly();
    }

    IEnumerator CoRunSinglePlatform(PoMove move, LevelLiftSet set)
    {
        while (_isRunning && !_dragLocked && move != null && set != null)
        {
            if (set.startPoint == null || set.endPoint == null)
                yield break;

            float duration = GetMoveDuration(move, set.endPoint);

            move.CancelMove();
            move.SetMoveDuration(duration);
            move.MoveTo(set.endPoint.localPosition);

            yield return new WaitUntil(() =>
                move == null || !move.IsMoving || !_isRunning || _dragLocked || set != _activeSet);

            if (!_isRunning || _dragLocked || move == null || set != _activeSet)
                yield break;

            move.SetPositionImmediate(set.startPoint.localPosition);

            if (waitOneFrameOnLoop)
                yield return null;
        }
    }

    float GetMoveDuration(PoMove move, Transform end)
    {
        if (move == null || end == null)
            return 0.01f;

        Vector3 current = move.GetStoredPosition();
        Vector3 target = end.localPosition;
        float distance = Vector3.Distance(current, target);

        if (moveSpeed <= 0.0001f)
            return 0.01f;

        return distance / moveSpeed;
    }

    void RefreshActiveSetFromCurrentLevel()
    {
        _activeLevel = GetCurrentLevel();
        _activeSet = GetSetForLevel(_activeLevel);
    }

    int GetCurrentLevel()
    {
        if (strengthComp != null)
            return strengthComp.CurrentLevel;

        return 1;
    }

    LevelLiftSet GetSetForLevel(int level)
    {
        if (levelSets == null) return null;

        for (int i = 0; i < levelSets.Length; i++)
        {
            if (levelSets[i] != null && levelSets[i].level == level)
                return levelSets[i];
        }

        return null;
    }

    bool HasValidActiveSetup()
    {
        return _activeSet != null &&
               _activeSet.startPoint != null &&
               _activeSet.endPoint != null &&
               _activeSet.liftPlatforms != null &&
               _activeSet.liftPlatforms.Count > 0;
    }

    void StopLoopOnly()
    {
        for (int i = 0; i < _platformRoutines.Count; i++)
        {
            if (_platformRoutines[i] != null)
                StopCoroutine(_platformRoutines[i]);
        }
        _platformRoutines.Clear();

        if (_activeSet != null && _activeSet.liftPlatforms != null)
        {
            for (int i = 0; i < _activeSet.liftPlatforms.Count; i++)
            {
                var move = _activeSet.liftPlatforms[i];
                if (move != null)
                    move.CancelMove();
            }
        }

        _isRunning = false;
        liftAudio?.StopLoop();


    }

    public void BeginDirectDemo()
    {
        if (_dragLocked) return;

        ResetState();
        Play();
    }

    public void EndDirectDemo()
    {
        Stop();
        ResetState();
    }
}