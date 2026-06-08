using System.Collections;
using UnityEngine;

public class ChangeFlowObjectController : MonoBehaviour, IPoResettable
{
    [Header("Trigger")]
    [SerializeField] TriggerZone triggerZone;

    [Header("Rotate Motors (2 targets)")]
    [SerializeField] PoRotate rotA;
    [SerializeField] PoRotate rotB;

    [Header("Doors")]
    [SerializeField] PoMove leftDoor;
    [SerializeField] PoMove rightDoor;

    [Tooltip("문이 열릴 때 위로 올라가는 거리")]
    [SerializeField] float doorOpenYOffset = 1f;

    [Header("Target Angles (local Z offset from initial)")]
    [SerializeField] float stateAAngleA = 30f;
    [SerializeField] float stateAAngleB = -30f;
    [SerializeField] float stateBAngleA = -30f;
    [SerializeField] float stateBAngleB = 30f;

    [Header("Options")]
    [SerializeField] bool startInStateB = false;
    [SerializeField] float toggleDelay = 0f;

    [Header("Flip Handling")]
    [SerializeField] Transform flipProbe;
    [SerializeField] bool invertWhenFlipped = true;

    [Header("Audio")]
    [SerializeField] PoMachineAudioPlayer audioPlayer;

    bool _isStateB = false;

    float _baseZA;
    float _baseZB;

    Vector3 _leftDoorClosedPos;
    Vector3 _rightDoorClosedPos;

    bool _captured;

    Coroutine _toggleRoutine;
    Coroutine _pendingRoutine;

    void Reset()
    {
        if (flipProbe == null) flipProbe = transform;
        if (triggerZone == null) triggerZone = GetComponentInChildren<TriggerZone>();
        if (audioPlayer == null)
            audioPlayer = GetComponentInChildren<PoMachineAudioPlayer>(true);
    }

    void Awake()
    {
        if (flipProbe == null) flipProbe = transform;

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

        StopAllRoutines();
    }

    void HandleTriggerPressedChanged(bool pressed)
    {
        if (!pressed) return;

        // 이미 토글 예약/대기 중이면 추가 입력 무시
        if (_toggleRoutine != null || _pendingRoutine != null)
            return;

        audioPlayer?.PlayStart();

        if (toggleDelay > 0f)
            _toggleRoutine = StartCoroutine(CoDelayedToggle());
        else
            Toggle();
    }

    IEnumerator CoDelayedToggle()
    {
        yield return new WaitForSeconds(toggleDelay);
        Toggle();
        _toggleRoutine = null;
    }

    public void Toggle()
    {
        if (IsAnyMovingOrRotating())
        {
            if (_pendingRoutine == null)
                _pendingRoutine = StartCoroutine(CoToggleWhenReady());

            return;
        }

        _isStateB = !_isStateB;
        ApplyCurrentState();
    }

    IEnumerator CoToggleWhenReady()
    {
        yield return new WaitUntil(() => !IsAnyMovingOrRotating());

        _isStateB = !_isStateB;
        ApplyCurrentState();

        _pendingRoutine = null;
    }

    public void CaptureInitialState()
    {
        if (rotA != null) _baseZA = NormalizeAngle(rotA.transform.localEulerAngles.z);
        if (rotB != null) _baseZB = NormalizeAngle(rotB.transform.localEulerAngles.z);

        if (leftDoor != null) _leftDoorClosedPos = leftDoor.GetStoredPosition();
        if (rightDoor != null) _rightDoorClosedPos = rightDoor.GetStoredPosition();

        _captured = true;
    }

    public void ResetState()
    {
        StopAllRoutines();

        if (!_captured)
            CaptureInitialState();

        _isStateB = startInStateB;
        ApplyCurrentStateImmediate();
    }

    void ApplyCurrentState()
    {
        ApplyRotators();
        ApplyDoors();
    }

    void ApplyCurrentStateImmediate()
    {
        ApplyRotatorsImmediate();
        ApplyDoorsImmediate();
    }

    void ApplyRotators()
    {
        float targetA = _isStateB ? stateBAngleA : stateAAngleA;
        float targetB = _isStateB ? stateBAngleB : stateAAngleB;

        if (invertWhenFlipped && IsFlipped())
        {
            targetA = -targetA;
            targetB = -targetB;
        }

        if (rotA != null) rotA.RotateToShortest(_baseZA + targetA);
        if (rotB != null) rotB.RotateToShortest(_baseZB + targetB);
    }

    void ApplyRotatorsImmediate()
    {
        float targetA = _isStateB ? stateBAngleA : stateAAngleA;
        float targetB = _isStateB ? stateBAngleB : stateAAngleB;

        if (invertWhenFlipped && IsFlipped())
        {
            targetA = -targetA;
            targetB = -targetB;
        }

        if (rotA != null) rotA.SetZImmediate(_baseZA + targetA);
        if (rotB != null) rotB.SetZImmediate(_baseZB + targetB);
    }

    void ApplyDoors()
    {
        Vector3 leftTarget;
        Vector3 rightTarget;

        GetDoorTargets(out leftTarget, out rightTarget);

        if (leftDoor != null)
            leftDoor.MoveTo(leftTarget, true);

        if (rightDoor != null)
            rightDoor.MoveTo(rightTarget, true);
    }

    void ApplyDoorsImmediate()
    {
        Vector3 leftTarget;
        Vector3 rightTarget;

        GetDoorTargets(out leftTarget, out rightTarget);

        if (leftDoor != null)
            leftDoor.SetPositionImmediate(leftTarget);

        if (rightDoor != null)
            rightDoor.SetPositionImmediate(rightTarget);
    }

    void GetDoorTargets(out Vector3 leftTarget, out Vector3 rightTarget)
    {
        Vector3 leftOpenPos = _leftDoorClosedPos + Vector3.up * doorOpenYOffset;
        Vector3 rightOpenPos = _rightDoorClosedPos + Vector3.up * doorOpenYOffset;

        if (!_isStateB)
        {
            // 상태 A
            // 왼쪽 문 막힘, 오른쪽 문 열림
            leftTarget = _leftDoorClosedPos;
            rightTarget = rightOpenPos;
        }
        else
        {
            // 상태 B
            // 오른쪽 문 막힘, 왼쪽 문 열림
            leftTarget = leftOpenPos;
            rightTarget = _rightDoorClosedPos;
        }
    }

    bool IsAnyMovingOrRotating()
    {
        bool rotatingA = rotA != null && rotA.IsRotating;
        bool rotatingB = rotB != null && rotB.IsRotating;

        bool movingLeft = leftDoor != null && leftDoor.IsMoving;
        bool movingRight = rightDoor != null && rightDoor.IsMoving;

        return rotatingA || rotatingB || movingLeft || movingRight;
    }

    bool IsFlipped()
    {
        return flipProbe != null && flipProbe.lossyScale.x < 0f;
    }

    void StopAllRoutines()
    {
        if (_toggleRoutine != null)
        {
            StopCoroutine(_toggleRoutine);
            _toggleRoutine = null;
        }

        if (_pendingRoutine != null)
        {
            StopCoroutine(_pendingRoutine);
            _pendingRoutine = null;
        }

        audioPlayer?.StopAll();
    }

    float NormalizeAngle(float angle)
    {
        angle %= 360f;
        if (angle > 180f) angle -= 360f;
        return angle;
    }
}