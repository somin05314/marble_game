using System.Collections;
using UnityEngine;

public class BridgeBlockController : MonoBehaviour, IPoResettable
{
    [Header("Trigger")]
    [SerializeField] TriggerZone triggerZone;

    [Header("Rotate Motor")]
    [SerializeField] PoRotate rot;

    [Header("Rotate Amount (degrees)")]
    [Tooltip("정방향(Flip 안된 상태)에서 회전해야 하는 각도(상대 회전)")]
    [SerializeField] float openDelta = 90f;

    [Header("Open Delay")]
    [SerializeField] float openDelay = 0.2f;

    [Header("Flip Handling")]
    [Tooltip("Flip 판정 기준 Transform. 비워두면 자기 자신(transform)")]
    [SerializeField] Transform flipProbe;

    [Tooltip("Flip되었을 때 회전 방향(+/-)을 반전합니다.")]
    [SerializeField] bool invertWhenFlipped = true;

    [Header("State")]
    [SerializeField] bool startOpened = false;

    bool _opened;

    Quaternion _initialLocalRot;
    bool _captured;

    Coroutine _pendingRoutine;
    Coroutine _delayedOpenRoutine;

    public bool IsOpened => _opened;

    void Reset()
    {
        if (flipProbe == null)
            flipProbe = transform;

        if (triggerZone == null)
            triggerZone = GetComponentInChildren<TriggerZone>();
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

    void Awake()
    {
        if (flipProbe == null)
            flipProbe = transform;

        CaptureInitialState();
        ResetState();
    }

    void HandleTriggerPressedChanged(bool pressed)
    {
        // 한 번 눌리면 openDelay 뒤에 열리고, 떼져도 닫히지 않음
        if (pressed)
        {
            if (_opened) return;

            StopDelayedOpenRoutine();
            _delayedOpenRoutine = StartCoroutine(CoDelayedOpen());
        }
    }

    IEnumerator CoDelayedOpen()
    {
        if (openDelay > 0f)
            yield return new WaitForSeconds(openDelay);

        _delayedOpenRoutine = null;

        if (!_opened)
            Open();
    }

    public void Open()
    {
        if (_opened) return;

        StopPendingRoutine();

        if (IsRotating())
        {
            _pendingRoutine = StartCoroutine(CoOpenWhenReady());
            return;
        }

        ApplyOpen();
    }

    public void Close()
    {
        StopDelayedOpenRoutine();
        StopPendingRoutine();

        if (IsRotating())
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
        if (rot != null)
            _initialLocalRot = rot.transform.localRotation;

        _captured = true;
    }

    public void ResetState()
    {
        StopDelayedOpenRoutine();
        StopPendingRoutine();

        if (!_captured)
            CaptureInitialState();

        if (rot != null)
            rot.SetLocalRotationImmediate(_initialLocalRot);

        _opened = false;

        if (startOpened)
            Open();
    }

    IEnumerator CoOpenWhenReady()
    {
        yield return new WaitUntil(() => !IsRotating());

        if (!_opened)
            ApplyOpen();

        _pendingRoutine = null;
    }

    IEnumerator CoCloseWhenReady()
    {
        yield return new WaitUntil(() => !IsRotating());

        if (_opened)
            ApplyClose();

        _pendingRoutine = null;
    }

    void ApplyOpen()
    {
        ApplyDelta(openDelta);
        _opened = true;
    }

    void ApplyClose()
    {
        ApplyDelta(-openDelta);
        _opened = false;
    }

    bool IsRotating()
    {
        return rot != null && rot.IsRotating;
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

    void ApplyDelta(float delta)
    {
        bool flipped = IsFlipped();
        if (invertWhenFlipped && flipped)
            delta = -delta;

        if (rot != null)
            rot.RotateBy(delta);
    }

    bool IsFlipped()
    {
        return flipProbe != null && flipProbe.lossyScale.x < 0f;
    }
}