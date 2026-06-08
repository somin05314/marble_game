using System.Collections;
using UnityEngine;

public class BothMarbleObjectController : MonoBehaviour, IPoResettable, IDragStateHandler
{
    [Header("Trigger By Level")]
    [SerializeField] TriggerZone level1TriggerZone;
    [SerializeField] TriggerZone level2TriggerZone;
    [SerializeField] TriggerZone level3TriggerZone;

    [Header("Strength")]
    [SerializeField] StrengthBasedOccupancyCells strengthComp;

    [Header("Rotate Motors")]
    [SerializeField] PoRotate level1BottomCoverRotate;
    [SerializeField] PoRotate level2BottomCoverRotate;
    [SerializeField] PoRotate level3BottomCoverRotate;
    [SerializeField] PoRotate gearRotateA;
    [SerializeField] PoRotate gearRotateB;

    [Header("Move Motors By Level (Down Object)")]
    [SerializeField] PoMove level1DownMove;
    [SerializeField] PoMove level2DownMove;
    [SerializeField] PoMove level3DownMove;

    [Header("Move Motors (Always Used)")]
    [SerializeField] PoMove moveUpObjectA;
    [SerializeField] PoMove moveUpObjectB;

    [Header("Action Amounts")]
    [SerializeField] float bottomCoverOpenDelta = 90f;
    [SerializeField] float gearRotateDeltaA = 180f;
    [SerializeField] float gearRotateDeltaB = 180f;
    [SerializeField] float moveDownY = -1f;
    [SerializeField] float moveUpAY = 1f;
    [SerializeField] float moveUpBY = 1f;

    [Header("Flip Handling")]
    [Tooltip("좌우 반전되면 아래쪽 덮개 회전 방향도 반전")]
    [SerializeField] Transform flipProbe;
    [SerializeField] bool invertBottomCoverWhenFlipped = true;

    [Header("State")]
    [SerializeField] bool startOpened = false;

    [Header("Audio")]
[SerializeField] PoMachineAudioPlayer audioPlayer;

    bool _opened;
    bool _dragLocked;
    bool _captured;

    Quaternion _initialBottomCoverLocalRotL1;
    Quaternion _initialBottomCoverLocalRotL2;
    Quaternion _initialBottomCoverLocalRotL3;
    Quaternion _initialGearALocalRot;
    Quaternion _initialGearBLocalRot;

    Vector3 _initialDownPosL1;
    Vector3 _initialDownPosL2;
    Vector3 _initialDownPosL3;
    Vector3 _initialUpAPos;
    Vector3 _initialUpBPos;

    Coroutine _pendingRoutine;
    TriggerZone _subscribedTrigger;

    public bool IsOpened => _opened;

    void Reset()
    {
        if (strengthComp == null)
            strengthComp = GetComponent<StrengthBasedOccupancyCells>();

        if (flipProbe == null)
            flipProbe = transform;

        if (audioPlayer == null)
            audioPlayer = GetComponentInChildren<PoMachineAudioPlayer>(true);
    }

    void Awake()
    {
        if (flipProbe == null)
            flipProbe = transform;

        CaptureInitialState();
        ResetState();
    }

    void OnEnable()
    {
        SubscribeCurrentTrigger();

        if (strengthComp != null)
            strengthComp.OnLevelChanged += HandleStrengthChanged;
    }

    void OnDisable()
    {
        UnsubscribeCurrentTrigger();

        if (strengthComp != null)
            strengthComp.OnLevelChanged -= HandleStrengthChanged;

        StopPendingRoutine();
    }

    void HandleStrengthChanged(int level)
    {
        UnsubscribeCurrentTrigger();
        SubscribeCurrentTrigger();
    }

    void SubscribeCurrentTrigger()
    {
        _subscribedTrigger = GetCurrentTriggerZone();
        if (_subscribedTrigger != null)
            _subscribedTrigger.PressedChanged += HandleTriggerPressedChanged;
    }

    void UnsubscribeCurrentTrigger()
    {
        if (_subscribedTrigger != null)
        {
            _subscribedTrigger.PressedChanged -= HandleTriggerPressedChanged;
            _subscribedTrigger = null;
        }
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

        if (IsAnyActing())
        {
            _pendingRoutine = StartCoroutine(CoOpenWhenReady());
            return;
        }

        ApplyOpen();
    }

    public void Close()
    {
        if (_dragLocked) return;
        if (!_opened) return;

        StopPendingRoutine();

        if (IsAnyActing())
        {
            _pendingRoutine = StartCoroutine(CoCloseWhenReady());
            return;
        }

        ApplyClose();
    }

    public void Toggle()
    {
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
        CancelAllActions();
        StopPendingRoutine();

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
        if (level1BottomCoverRotate != null) _initialBottomCoverLocalRotL1 = level1BottomCoverRotate.transform.localRotation;
        if (level2BottomCoverRotate != null) _initialBottomCoverLocalRotL2 = level2BottomCoverRotate.transform.localRotation;
        if (level3BottomCoverRotate != null) _initialBottomCoverLocalRotL3 = level3BottomCoverRotate.transform.localRotation;

        if (gearRotateA != null) _initialGearALocalRot = gearRotateA.transform.localRotation;
        if (gearRotateB != null) _initialGearBLocalRot = gearRotateB.transform.localRotation;

        if (level1DownMove != null) _initialDownPosL1 = level1DownMove.GetStoredPosition();
        if (level2DownMove != null) _initialDownPosL2 = level2DownMove.GetStoredPosition();
        if (level3DownMove != null) _initialDownPosL3 = level3DownMove.GetStoredPosition();

        if (moveUpObjectA != null) _initialUpAPos = moveUpObjectA.GetStoredPosition();
        if (moveUpObjectB != null) _initialUpBPos = moveUpObjectB.GetStoredPosition();

        _captured = true;
    }

    public void ResetState()
    {
        ResetStateImmediate(reopenIfStartOpened: true);
    }

    public void ResetStateImmediate(bool reopenIfStartOpened)
    {
        StopPendingRoutine();
        CancelAllActions();

        if (!_captured)
            CaptureInitialState();

        if (level1BottomCoverRotate != null)
            level1BottomCoverRotate.SetLocalRotationImmediate(_initialBottomCoverLocalRotL1);

        if (level2BottomCoverRotate != null)
            level2BottomCoverRotate.SetLocalRotationImmediate(_initialBottomCoverLocalRotL2);

        if (level3BottomCoverRotate != null)
            level3BottomCoverRotate.SetLocalRotationImmediate(_initialBottomCoverLocalRotL3);

        if (gearRotateA != null)
            gearRotateA.SetLocalRotationImmediate(_initialGearALocalRot);

        if (gearRotateB != null)
            gearRotateB.SetLocalRotationImmediate(_initialGearBLocalRot);

        if (level1DownMove != null)
            level1DownMove.SetPositionImmediate(_initialDownPosL1);

        if (level2DownMove != null)
            level2DownMove.SetPositionImmediate(_initialDownPosL2);

        if (level3DownMove != null)
            level3DownMove.SetPositionImmediate(_initialDownPosL3);

        if (moveUpObjectA != null)
            moveUpObjectA.SetPositionImmediate(_initialUpAPos);

        if (moveUpObjectB != null)
            moveUpObjectB.SetPositionImmediate(_initialUpBPos);

        _opened = false;

        if (reopenIfStartOpened && !_dragLocked && startOpened)
            Open();
    }

    IEnumerator CoOpenWhenReady()
    {
        yield return new WaitUntil(() => !IsAnyActing());

        if (!_opened && !_dragLocked)
            ApplyOpen();

        _pendingRoutine = null;
    }

    IEnumerator CoCloseWhenReady()
    {
        yield return new WaitUntil(() => !IsAnyActing());

        if (_opened && !_dragLocked)
            ApplyClose();

        _pendingRoutine = null;
    }

    void ApplyOpen()
    {
        audioPlayer?.PlayStart();

        PoRotate currentBottomCoverRotate = GetCurrentBottomCoverRotate();
        PoMove currentDownMove = GetCurrentDownMove();

        float coverDelta = GetBottomCoverOpenDelta();

        if (currentBottomCoverRotate != null)
            currentBottomCoverRotate.RotateBy(coverDelta);

        if (gearRotateA != null)
            gearRotateA.RotateBy(gearRotateDeltaA);

        if (gearRotateB != null)
            gearRotateB.RotateBy(gearRotateDeltaB);

        if (currentDownMove != null)
            currentDownMove.MoveByY(moveDownY);

        if (moveUpObjectA != null)
            moveUpObjectA.MoveByY(moveUpAY);

        if (moveUpObjectB != null)
            moveUpObjectB.MoveByY(moveUpBY);

        _opened = true;
    }

    void ApplyClose()
    {
        PoRotate currentBottomCoverRotate = GetCurrentBottomCoverRotate();
        PoMove currentDownMove = GetCurrentDownMove();

        float coverDelta = GetBottomCoverOpenDelta();

        if (currentBottomCoverRotate != null)
            currentBottomCoverRotate.RotateBy(-coverDelta);

        if (gearRotateA != null)
            gearRotateA.RotateBy(-gearRotateDeltaA);

        if (gearRotateB != null)
            gearRotateB.RotateBy(-gearRotateDeltaB);

        if (currentDownMove != null)
            currentDownMove.MoveByY(-moveDownY);

        if (moveUpObjectA != null)
            moveUpObjectA.MoveByY(-moveUpAY);

        if (moveUpObjectB != null)
            moveUpObjectB.MoveByY(-moveUpBY);

        _opened = false;
    }

    float GetBottomCoverOpenDelta()
    {
        float delta = bottomCoverOpenDelta;

        if (!invertBottomCoverWhenFlipped)
            return delta;

        Transform probe = flipProbe != null ? flipProbe : transform;

        if (probe.lossyScale.x < 0f)
            delta = -delta;

        return delta;
    }

    TriggerZone GetCurrentTriggerZone()
    {
        int level = GetCurrentStrengthLevel();

        switch (level)
        {
            case 1: return level1TriggerZone;
            case 2: return level2TriggerZone;
            case 3: return level3TriggerZone;
            default: return level1TriggerZone;
        }
    }

    PoRotate GetCurrentBottomCoverRotate()
    {
        int level = GetCurrentStrengthLevel();

        switch (level)
        {
            case 1: return level1BottomCoverRotate;
            case 2: return level2BottomCoverRotate;
            case 3: return level3BottomCoverRotate;
            default: return level1BottomCoverRotate;
        }
    }

    PoMove GetCurrentDownMove()
    {
        int level = GetCurrentStrengthLevel();

        switch (level)
        {
            case 1: return level1DownMove;
            case 2: return level2DownMove;
            case 3: return level3DownMove;
            default: return level1DownMove;
        }
    }

    int GetCurrentStrengthLevel()
    {
        if (strengthComp != null)
            return strengthComp.CurrentLevel;

        return 1;
    }

    bool IsAnyActing()
    {
        return IsRotating(level1BottomCoverRotate)
            || IsRotating(level2BottomCoverRotate)
            || IsRotating(level3BottomCoverRotate)
            || IsRotating(gearRotateA)
            || IsRotating(gearRotateB)
            || IsMoving(level1DownMove)
            || IsMoving(level2DownMove)
            || IsMoving(level3DownMove)
            || IsMoving(moveUpObjectA)
            || IsMoving(moveUpObjectB);
    }

    bool IsRotating(PoRotate rot)
    {
        return rot != null && rot.IsRotating;
    }

    bool IsMoving(PoMove move)
    {
        return move != null && move.IsMoving;
    }

    void CancelAllActions()
    {
        if (level1BottomCoverRotate != null) level1BottomCoverRotate.CancelRotate();
        if (level2BottomCoverRotate != null) level2BottomCoverRotate.CancelRotate();
        if (level3BottomCoverRotate != null) level3BottomCoverRotate.CancelRotate();

        if (gearRotateA != null) gearRotateA.CancelRotate();
        if (gearRotateB != null) gearRotateB.CancelRotate();

        if (level1DownMove != null) level1DownMove.CancelMove();
        if (level2DownMove != null) level2DownMove.CancelMove();
        if (level3DownMove != null) level3DownMove.CancelMove();

        if (moveUpObjectA != null) moveUpObjectA.CancelMove();
        if (moveUpObjectB != null) moveUpObjectB.CancelMove();

        audioPlayer?.StopAll();
    }

    void StopPendingRoutine()
    {
        if (_pendingRoutine != null)
        {
            StopCoroutine(_pendingRoutine);
            _pendingRoutine = null;
        }
    }
}