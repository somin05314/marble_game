using System.Collections;
using UnityEngine;

public class ElevatorObjectController : MonoBehaviour, IPoResettable, IDragStateHandler
{
    [Header("Trigger")]
    [SerializeField] TriggerZone triggerZone;

    [Header("Strength")]
    [SerializeField] StrengthBasedOccupancyCells strengthComp;

    [Header("Move Targets")]
    [SerializeField] PoMove elevatorMove;
    [SerializeField] PoMove doorMove;

    [Header("Start Delay")]
    [SerializeField, Min(0f)] float startDelay = 0.5f;

    [Header("Elevator Move Amount By Level")]
    [SerializeField] float level1MoveY = 10f;
    [SerializeField] float level2MoveY = 20f;
    [SerializeField] float level3MoveY = 30f;

    [Header("Elevator Duration By Level")]
    [SerializeField] float level1Duration = 4f;
    [SerializeField] float level2Duration = 8f;
    [SerializeField] float level3Duration = 12f;

    [Header("Door Move")]
    [SerializeField] float doorMoveY = -5f;
    [SerializeField, Min(0f)] float doorDuration = 1f;

    [Header("State")]
    [SerializeField] bool startOpened = false;

    [Header("Audio")]
    [SerializeField] PoMachineAudioPlayer elevatorAudio;
    [SerializeField] PoMachineAudioPlayer doorAudio;

    bool _opened;
    bool _dragLocked;

    Vector3 _initialElevatorPos;
    Vector3 _initialDoorPos;
    bool _captured;

    Coroutine _pendingRoutine;
    Coroutine _sequenceRoutine;

    public bool IsOpened => _opened;

    void Reset()
    {
        if (triggerZone == null)
            triggerZone = GetComponentInChildren<TriggerZone>();

        if (strengthComp == null)
            strengthComp = GetComponent<StrengthBasedOccupancyCells>();

        if (elevatorMove == null || doorMove == null)
        {
            var moves = GetComponentsInChildren<PoMove>(true);

            if (moves.Length > 0 && elevatorMove == null) elevatorMove = moves[0];
            if (moves.Length > 1 && doorMove == null) doorMove = moves[1];
        }


        var audioPlayers = GetComponentsInChildren<PoMachineAudioPlayer>(true);

        if (audioPlayers.Length > 0 && elevatorAudio == null)
            elevatorAudio = audioPlayers[0];

        if (audioPlayers.Length > 1 && doorAudio == null)
            doorAudio = audioPlayers[1];
    }

    void Awake()
    {
        CaptureInitialState();
        ResetState();
    }

    void OnEnable()
    {
        if (triggerZone != null)
            triggerZone.PressedChanged += HandleTriggerPressedChanged;
    }

    void OnDisable()
    {
        if (triggerZone != null)
            triggerZone.PressedChanged -= HandleTriggerPressedChanged;
    }

    void HandleTriggerPressedChanged(bool pressed)
    {
        if (!pressed) return;
        Open();
    }

    public void Open()
    {
        if (_dragLocked) return;
        if (_opened) return;

        StopPendingRoutine();
        StopSequenceRoutine();
        CancelAllMoves();

        if (IsAnyMoving())
        {
            _pendingRoutine = StartCoroutine(CoOpenWhenReady());
            return;
        }

        _sequenceRoutine = StartCoroutine(CoOpenSequence());
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
        if (elevatorMove != null)
            _initialElevatorPos = elevatorMove.GetStoredPosition();

        if (doorMove != null)
            _initialDoorPos = doorMove.GetStoredPosition();

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

        if (!_captured)
            CaptureInitialState();

        if (elevatorMove != null)
            elevatorMove.SetPositionImmediate(_initialElevatorPos);

        if (doorMove != null)
            doorMove.SetPositionImmediate(_initialDoorPos);

        _opened = false;

        if (reopenIfStartOpened && !_dragLocked && startOpened)
            Open();
    }

    IEnumerator CoOpenWhenReady()
    {
        yield return new WaitUntil(() => !IsAnyMoving());

        if (!_opened && !_dragLocked)
            _sequenceRoutine = StartCoroutine(CoOpenSequence());

        _pendingRoutine = null;
    }

    IEnumerator CoOpenSequence()
    {
        if (_dragLocked)
        {
            _sequenceRoutine = null;
            yield break;
        }

        if (startDelay > 0f)
            yield return new WaitForSeconds(startDelay);

        if (_dragLocked)
        {
            _sequenceRoutine = null;
            yield break;
        }

        int level = GetCurrentStrengthLevel();
        float elevatorDeltaY = GetElevatorMoveY(level);
        float elevatorDuration = GetElevatorDuration(level);

        if (elevatorMove != null)
        {
            elevatorMove.SetMoveDuration(elevatorDuration);

            elevatorAudio?.PlayElevatorLoop();

            elevatorMove.MoveByY(elevatorDeltaY);

            yield return new WaitUntil(() => elevatorMove == null || !elevatorMove.IsMoving);

            elevatorAudio?.StopLoop();
        }

        if (_dragLocked)
        {
            _sequenceRoutine = null;
            yield break;
        }

        if (doorMove != null)
        {
            doorAudio?.PlayStart();

            doorMove.SetMoveDuration(doorDuration);
            doorMove.MoveByY(doorMoveY);

            yield return new WaitUntil(() => doorMove == null || !doorMove.IsMoving);
        }

        _opened = true;
        _sequenceRoutine = null;
    }

    int GetCurrentStrengthLevel()
    {
        if (strengthComp != null)
            return strengthComp.CurrentLevel;

        return 1;
    }

    float GetElevatorMoveY(int level)
    {
        switch (level)
        {
            case 1: return level1MoveY;
            case 2: return level2MoveY;
            case 3: return level3MoveY;
            default: return level1MoveY;
        }
    }

    float GetElevatorDuration(int level)
    {
        switch (level)
        {
            case 1: return level1Duration;
            case 2: return level2Duration;
            case 3: return level3Duration;
            default: return level1Duration;
        }
    }

    bool IsAnyMoving()
    {
        return IsMoving(elevatorMove) || IsMoving(doorMove);
    }

    bool IsMoving(PoMove move)
    {
        return move != null && move.IsMoving;
    }

    void CancelAllMoves()
    {
        if (elevatorMove != null) elevatorMove.CancelMove();
        if (doorMove != null) doorMove.CancelMove();

        elevatorAudio?.StopAll();
        doorAudio?.StopAll();
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

    public void CancelSequence()
    {
        StopPendingRoutine();
        StopSequenceRoutine();
        CancelAllMoves();
        _opened = false;
    }
}