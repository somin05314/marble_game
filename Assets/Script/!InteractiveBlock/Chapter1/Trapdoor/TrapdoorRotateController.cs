using System.Collections;
using UnityEngine;

public class TrapdoorRotateController : MonoBehaviour, IPoResettable
{
    [Header("Trigger")]
    [SerializeField] TriggerZone triggerZone;

    [Header("Rotate Motors")]
    [SerializeField] PoRotate rotA;
    [SerializeField] PoRotate rotB;

    [Header("Rotate Amount (degrees)")]
    [SerializeField] float openDeltaA = 90f;
    [SerializeField] float openDeltaB = -90f;

    [Header("Flip Handling")]
    [SerializeField] Transform flipProbe;
    [SerializeField] bool invertWhenFlipped = true;

    [Header("Delay")]
    [SerializeField] float openDelay = 0.1f;

    [Header("State")]
    [SerializeField] bool startOpened = false;

    bool _opened;
    Quaternion _initialLocalRotA;
    Quaternion _initialLocalRotB;
    bool _captured;

    Coroutine _pendingRoutine;
    Coroutine _delayedOpenRoutine;

    public bool IsOpened => _opened;

    void Reset()
    {
        if (flipProbe == null) flipProbe = transform;
        if (triggerZone == null) triggerZone = GetComponentInChildren<TriggerZone>();
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

        StopPendingRoutine();
        StopDelayedOpenRoutine();
    }

    void HandleTriggerPressedChanged(bool pressed)
    {
        if (!pressed) return;

        StopDelayedOpenRoutine();
        _delayedOpenRoutine = StartCoroutine(CoDelayedOpen());
    }

    IEnumerator CoDelayedOpen()
    {
        yield return new WaitForSeconds(openDelay);

        Open();
        _delayedOpenRoutine = null;
    }

    public void Open()
    {
        if (_opened) return;

        StopPendingRoutine();

        if (IsAnyRotating())
        {
            _pendingRoutine = StartCoroutine(CoOpenWhenReady());
            return;
        }

        ApplyOpen();
    }

    public void Close()
    {
        StopPendingRoutine();
        StopDelayedOpenRoutine();

        if (IsAnyRotating())
        {
            _pendingRoutine = StartCoroutine(CoCloseWhenReady());
            return;
        }

        if (!_opened) return;

        ApplyClose();
    }

    public void Toggle()
    {
        if (_opened) Close();
        else Open();
    }

    public void CaptureInitialState()
    {
        if (rotA != null)
            _initialLocalRotA = rotA.transform.localRotation;

        if (rotB != null)
            _initialLocalRotB = rotB.transform.localRotation;

        _captured = true;
    }

    public void ResetState()
    {
        StopPendingRoutine();
        StopDelayedOpenRoutine();

        if (!_captured)
            CaptureInitialState();

        if (rotA != null)
            rotA.SetLocalRotationImmediate(_initialLocalRotA);

        if (rotB != null)
            rotB.SetLocalRotationImmediate(_initialLocalRotB);

        _opened = false;

        if (startOpened)
            Open();
    }

    IEnumerator CoOpenWhenReady()
    {
        yield return new WaitUntil(() => !IsAnyRotating());

        if (!_opened)
            ApplyOpen();

        _pendingRoutine = null;
    }

    IEnumerator CoCloseWhenReady()
    {
        yield return new WaitUntil(() => !IsAnyRotating());

        if (_opened)
            ApplyClose();

        _pendingRoutine = null;
    }

    void ApplyOpen()
    {
        ApplyDelta(openDeltaA, openDeltaB);
        _opened = true;
    }

    void ApplyClose()
    {
        ApplyDelta(-openDeltaA, -openDeltaB);
        _opened = false;
    }

    bool IsAnyRotating()
    {
        return (rotA != null && rotA.IsRotating)
            || (rotB != null && rotB.IsRotating);
    }

    void StopPendingRoutine()
    {
        if (_pendingRoutine != null)
        {
            StopCoroutine(_pendingRoutine);
            _pendingRoutine = null;
        }
    }

    void StopDelayedOpenRoutine()
    {
        if (_delayedOpenRoutine != null)
        {
            StopCoroutine(_delayedOpenRoutine);
            _delayedOpenRoutine = null;
        }
    }

    void ApplyDelta(float deltaA, float deltaB)
    {
        bool flipped = IsFlipped();

        if (invertWhenFlipped && flipped)
        {
            deltaA = -deltaA;
            deltaB = -deltaB;
        }

        if (rotA != null)
            rotA.RotateBy(deltaA);

        if (rotB != null)
            rotB.RotateBy(deltaB);
    }

    bool IsFlipped()
    {
        return flipProbe != null && flipProbe.lossyScale.x < 0f;
    }
}