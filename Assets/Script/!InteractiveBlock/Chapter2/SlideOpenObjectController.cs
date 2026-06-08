using System.Collections;
using UnityEngine;

public class SlideOpenObjectController : MonoBehaviour, IPoResettable, IDragStateHandler
{
    [Header("Trigger")]
    [SerializeField] TriggerZone triggerZone;

    [Header("Strength Source")]
    [SerializeField] StrengthBasedOccupancyCells strengthSource;

    [Header("Move Motors")]
    [SerializeField] PoMove moveA;
    [SerializeField] PoMove moveB;
    [SerializeField] PoMove moveC;

    [Header("Move Delta")]
    [Tooltip("각 물체가 한 번 열릴 때 이동할 값")]
    [SerializeField] Vector3 openDelta = new Vector3(5f, 0f, 0f);

    [Header("Sequence")]
    [Tooltip("물체 하나 이동이 끝난 뒤 다음 물체로 넘어가기 전 대기 시간")]
    [SerializeField, Min(0f)] float betweenObjectDelay = 0.1f;

    [Header("State")]
    [SerializeField] bool startOpened = false;

    [Header("Build Visual")]
    [Tooltip("배치 모드에서만 보일 시작점/구슬 스프라이트 루트")]
    [SerializeField] GameObject buildVisualRoot;

    [Header("Audio")]
    [SerializeField] PoMachineAudioPlayer audioPlayer;

    bool _isSequencing;

    bool _opened;
    bool _dragLocked;

    Vector3 _initialPositionA;
    Vector3 _initialPositionB;
    Vector3 _initialPositionC;
    bool _captured;

    Coroutine _pendingRoutine;
    Coroutine _sequenceRoutine;

    public bool IsOpened => _opened;

    void Reset()
    {
        if (triggerZone == null)
            triggerZone = GetComponentInChildren<TriggerZone>();

        if (strengthSource == null)
            strengthSource = GetComponent<StrengthBasedOccupancyCells>();

        if (moveA == null || moveB == null || moveC == null)
        {
            var moves = GetComponentsInChildren<PoMove>(true);

            if (moves.Length > 0 && moveA == null) moveA = moves[0];
            if (moves.Length > 1 && moveB == null) moveB = moves[1];
            if (moves.Length > 2 && moveC == null) moveC = moves[2];
        }

        if (audioPlayer == null)
            audioPlayer = GetComponentInChildren<PoMachineAudioPlayer>(true);
    }

    void OnEnable()
    {
        GameModeManager.OnModeChanged += HandleModeChanged;

        if (triggerZone != null)
            triggerZone.PressedChanged += HandleTriggerPressedChanged;
    }

    void OnDisable()
    {
        GameModeManager.OnModeChanged -= HandleModeChanged;

        if (triggerZone != null)
            triggerZone.PressedChanged -= HandleTriggerPressedChanged;
    }

    void Awake()
    {
        CaptureInitialState();
        ResetState();
    }

    void Start()
    {
        if (GameModeManager.Instance != null)
            SetBuildVisualVisible(GameModeManager.Instance.currentMode == GameMode.Build);
    }

    void HandleModeChanged(GameMode mode)
    {
        SetBuildVisualVisible(mode == GameMode.Build);
    }

    void HandleTriggerPressedChanged(bool pressed)
    {
        if (!pressed) return;
        if (triggerZone == null) return;

        if (triggerZone.UseToggleMode)
            Toggle();
        else
            Open();
    }

    public void Open()
    {
        if (_dragLocked) return;
        if (_opened) return;
        if (_isSequencing) return;

        StopPendingRoutine();
        StopSequenceRoutine();

        if (IsAnyMoving())
            return;

        _sequenceRoutine = StartCoroutine(CoApplyOpenSequential());
    }

    public void Close()
    {
        if (_dragLocked) return;
        if (!_opened) return;
        if (_isSequencing) return;

        StopPendingRoutine();
        StopSequenceRoutine();

        if (IsAnyMoving())
            return;

        _sequenceRoutine = StartCoroutine(CoApplyCloseSequential());
    }

    public void Toggle()
    {
        if (_isSequencing) return;

        if (_opened) Close();
        else Open();
    }

    public void BeginDragState()
    {
        _dragLocked = true;
        ResetStateImmediate(reopenIfStartOpened: false);
    }

    public void EndDragState(bool committed)
    {
        _dragLocked = false;
        StopPendingRoutine();
        StopSequenceRoutine();
        CancelAllMoves();

        if (committed)
        {
            CaptureInitialState();
            _opened = false;

            if (startOpened)
                Open();
        }
        else
        {
            ResetStateImmediate(reopenIfStartOpened: false);
        }
    }

    public void CaptureInitialState()
    {
        if (moveA != null) _initialPositionA = moveA.GetStoredPosition();
        if (moveB != null) _initialPositionB = moveB.GetStoredPosition();
        if (moveC != null) _initialPositionC = moveC.GetStoredPosition();

        _captured = true;
    }

    public void ResetState()
    {
        ResetStateImmediate(reopenIfStartOpened: true);
    }

    public void ResetStateImmediate(bool reopenIfStartOpened)
    {
        StopPendingRoutine();
        StopSequenceRoutine();
        CancelAllMoves();

        // ? 토글 트리거 상태도 같이 OFF로 정리
        ResetTriggerState();

        if (!_captured)
            CaptureInitialState();

        if (moveA != null) moveA.SetPositionImmediate(_initialPositionA);
        if (moveB != null) moveB.SetPositionImmediate(_initialPositionB);
        if (moveC != null) moveC.SetPositionImmediate(_initialPositionC);

        _opened = false;

        if (reopenIfStartOpened && !_dragLocked && startOpened)
            Open();
    }

    public void SetBuildVisualVisible(bool visible)
    {
        if (buildVisualRoot != null)
            buildVisualRoot.SetActive(visible);
    }

    IEnumerator CoOpenWhenReady()
    {
        yield return new WaitUntil(() => !IsAnyMoving());

        if (!_opened && !_dragLocked)
            _sequenceRoutine = StartCoroutine(CoApplyOpenSequential());

        _pendingRoutine = null;
    }

    IEnumerator CoCloseWhenReady()
    {
        yield return new WaitUntil(() => !IsAnyMoving());

        if (_opened && !_dragLocked)
            _sequenceRoutine = StartCoroutine(CoApplyCloseSequential());

        _pendingRoutine = null;
    }

    IEnumerator CoApplyOpenSequential()
    {
        _isSequencing = true;
        _opened = true;

        int level = GetCurrentStrengthLevel();

        yield return MoveSingleSequential(moveA, level >= 1, openDelta);
        yield return MoveSingleSequential(moveB, level >= 2, openDelta);
        yield return MoveSingleSequential(moveC, level >= 3, openDelta);

        _isSequencing = false;
        _sequenceRoutine = null;
    }

    IEnumerator CoApplyCloseSequential()
    {
        _isSequencing = true;
        _opened = false;

        int level = GetCurrentStrengthLevel();

        yield return MoveSingleSequential(moveC, level >= 3, -openDelta);
        yield return MoveSingleSequential(moveB, level >= 2, -openDelta);
        yield return MoveSingleSequential(moveA, level >= 1, -openDelta);

        _isSequencing = false;
        _sequenceRoutine = null;
    }

    IEnumerator MoveSingleSequential(PoMove move, bool shouldMove, Vector3 delta)
    {
        if (_dragLocked)
            yield break;

        if (!shouldMove || move == null)
            yield break;

        yield return new WaitUntil(() => !move.IsMoving);

        audioPlayer?.PlayStart();
        move.MoveBy(delta);

        yield return new WaitUntil(() => !move.IsMoving);

        if (betweenObjectDelay > 0f)
            yield return new WaitForSeconds(betweenObjectDelay);
    }

    int GetCurrentStrengthLevel()
    {
        if (strengthSource != null)
            return strengthSource.CurrentLevel;

        return 1;
    }

    void CancelAllMoves()
    {
        if (moveA != null) moveA.CancelMove();
        if (moveB != null) moveB.CancelMove();
        if (moveC != null) moveC.CancelMove();
    }

    void StopPendingRoutine()
    {
        if (_pendingRoutine != null)
        {
            StopCoroutine(_pendingRoutine);
            _pendingRoutine = null;
        }
    }

    void StopSequenceRoutine()
    {
        if (_sequenceRoutine != null)
        {
            StopCoroutine(_sequenceRoutine);
            _sequenceRoutine = null;
        }
    }

    bool IsAnyMoving()
    {
        return IsMoving(moveA) || IsMoving(moveB) || IsMoving(moveC);
    }

    bool IsMoving(PoMove move)
    {
        return move != null && move.IsMoving;
    }

    void ResetTriggerState()
    {
        if (triggerZone == null)
            return;

        // 토글/버튼 상태를 강제로 OFF 쪽으로 정리
        triggerZone.StopDemo(false);
        triggerZone.ForceExit();
    }
}