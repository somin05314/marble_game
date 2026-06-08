using System.Collections;
using UnityEngine;

public class RestBlockSequence : MonoBehaviour, IPoResettable
{
    [Header("Trigger")]
    [SerializeField] TriggerZone triggerZone;

    [Header("Rotate Motors")]
    [SerializeField] PoRotate mainRotator;
    [SerializeField] PoRotate subRotator;
    [SerializeField] PoRotate extraRotator;

    [Header("Rotate Amount (degrees)")]
    [SerializeField] float mainRotateDegrees = 180f;
    [SerializeField] float subRotateDegrees = 180f;
    [SerializeField] float extraRotateDegrees = 90f;

    [Header("Timing")]
    [Tooltip("메인 회전 종료 후 서브 회전 시작 전 대기 시간")]
    [SerializeField] float subRotateDelay = 0f;

    [Header("State")]
    [SerializeField] bool startOpened = false;

    bool _opened;
    bool _busy;

    Quaternion _initialLocalRotMain;
    Quaternion _initialLocalRotSub;
    Quaternion _initialLocalRotExtra;
    bool _captured;

    Coroutine _pendingRoutine;
    Coroutine _sequenceRoutine;

    public bool IsOpened => _opened;

    void Reset()
    {
        if (triggerZone == null)
            triggerZone = GetComponentInChildren<TriggerZone>();
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
        if (pressed)
            Open();
    }

    public void Open()
    {
        if (_opened) return;

        StopPendingRoutine();

        if (IsAnyRotating() || _busy)
        {
            _pendingRoutine = StartCoroutine(CoOpenWhenReady());
            return;
        }

        _sequenceRoutine = StartCoroutine(CoOpenSequence());
    }

    public void Close()
    {
        StopPendingRoutine();
        StopSequenceRoutine();

        if (IsAnyRotating())
        {
            _pendingRoutine = StartCoroutine(CoCloseWhenReady());
            return;
        }

        if (!_opened) return;

        ApplyCloseImmediate();
    }

    public void Toggle()
    {
        if (_opened) Close();
        else Open();
    }

    public void CaptureInitialState()
    {
        if (mainRotator != null) _initialLocalRotMain = mainRotator.transform.localRotation;
        if (subRotator != null) _initialLocalRotSub = subRotator.transform.localRotation;
        if (extraRotator != null) _initialLocalRotExtra = extraRotator.transform.localRotation;

        _captured = true;
    }

    public void ResetState()
    {
        StopPendingRoutine();
        StopSequenceRoutine();

        if (!_captured)
            CaptureInitialState();

        if (mainRotator != null)
            mainRotator.SetLocalRotationImmediate(_initialLocalRotMain);

        if (subRotator != null)
            subRotator.SetLocalRotationImmediate(_initialLocalRotSub);

        if (extraRotator != null)
            extraRotator.SetLocalRotationImmediate(_initialLocalRotExtra);

        _opened = false;
        _busy = false;

        if (startOpened)
            Open();
    }

    IEnumerator CoOpenWhenReady()
    {
        yield return new WaitUntil(() => !IsAnyRotating() && !_busy);

        if (!_opened)
            _sequenceRoutine = StartCoroutine(CoOpenSequence());

        _pendingRoutine = null;
    }

    IEnumerator CoCloseWhenReady()
    {
        yield return new WaitUntil(() => !IsAnyRotating());

        if (_opened)
            ApplyCloseImmediate();

        _pendingRoutine = null;
    }

    IEnumerator CoOpenSequence()
    {
        _busy = true;

        // 1) 메인 회전 시작
        if (mainRotator != null)
            mainRotator.RotateBy(mainRotateDegrees);

        // 2) 엑스트라 회전은 즉시 시작
        if (extraRotator != null)
            extraRotator.RotateBy(extraRotateDegrees);

        // 3) 메인 끝날 때까지 대기
        yield return new WaitUntil(() => mainRotator == null || !mainRotator.IsRotating);

        // 4) 서브 회전 전 딜레이
        if (subRotateDelay > 0f)
            yield return new WaitForSeconds(subRotateDelay);

        // 5) 서브 회전 시작
        if (subRotator != null)
            subRotator.RotateBy(subRotateDegrees);

        // 6) 남은 회전 종료 대기
        yield return new WaitUntil(() =>
            (subRotator == null || !subRotator.IsRotating) &&
            (extraRotator == null || !extraRotator.IsRotating));

        _opened = true;
        _busy = false;
        _sequenceRoutine = null;
    }

    void ApplyCloseImmediate()
    {
        // ResetState와 비슷하게 "닫힘 상태"로 즉시 복귀
        if (!_captured)
            CaptureInitialState();

        if (mainRotator != null)
            mainRotator.SetLocalRotationImmediate(_initialLocalRotMain);

        if (subRotator != null)
            subRotator.SetLocalRotationImmediate(_initialLocalRotSub);

        if (extraRotator != null)
            extraRotator.SetLocalRotationImmediate(_initialLocalRotExtra);

        _opened = false;
        _busy = false;
    }

    bool IsAnyRotating()
    {
        bool main = mainRotator != null && mainRotator.IsRotating;
        bool sub = subRotator != null && subRotator.IsRotating;
        bool extra = extraRotator != null && extraRotator.IsRotating;

        return main || sub || extra;
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

        _busy = false;
    }
}