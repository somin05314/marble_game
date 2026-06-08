using System.Collections;
using UnityEngine;

public class DropMarbleRotateController : MonoBehaviour, IPoResettable, IDragStateHandler
{
    [Header("Trigger")]
    [SerializeField] TriggerZone triggerZone;

    [Header("Rotate Motor")]
    [SerializeField] PoRotate rotA;

    [Header("Rotate Amount (degrees)")]
    [Tooltip("정방향(Flip 안된 상태)에서 A가 회전해야 하는 각도(상대 회전)")]
    [SerializeField] float openDeltaA = 90f;

    [Header("Flip Handling")]
    [Tooltip("Flip 판정 기준 Transform. 비워두면 자기 자신(transform)")]
    [SerializeField] Transform flipProbe;

    [Tooltip("Flip되었을 때 회전 방향(+/-)을 반전합니다.")]
    [SerializeField] bool invertWhenFlipped = true;

    [Header("State")]
    [SerializeField] bool startOpened = false;

    [Header("Build Visual")]
    [Tooltip("배치 모드에서만 보일 시작점/구슬 스프라이트 루트")]
    [SerializeField] GameObject buildVisualRoot;

    bool _opened;
    bool _dragLocked;

    Quaternion _initialLocalRotA;
    bool _captured;

    Coroutine _pendingRoutine;

    public bool IsOpened => _opened;

    void Reset()
    {
        if (flipProbe == null) flipProbe = transform;
        if (triggerZone == null) triggerZone = GetComponentInChildren<TriggerZone>();
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
        if (flipProbe == null) flipProbe = transform;

        CaptureInitialState();
        ResetState();
    }

    void Start()
    {
        // 시작 시 현재 모드 기준으로 표시 동기화
        if (GameModeManager.Instance != null)
            SetBuildVisualVisible(GameModeManager.Instance.currentMode == GameMode.Build);
    }

    void HandleModeChanged(GameMode mode)
    {
        SetBuildVisualVisible(mode == GameMode.Build);
    }

    void HandleTriggerPressedChanged(bool pressed)
    {
        // 한 번 눌리면 열리고, 떼져도 닫히지 않음
        if (pressed)
            Open();
    }

    public void Open()
    {
        if (_dragLocked) return;
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
        if (_dragLocked) return;

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

    public void BeginDragState()
    {
        _dragLocked = true;
        StopPendingRoutine();

        if (rotA != null)
            rotA.CancelRotate();
    }

    public void EndDragState(bool committed)
    {
        _dragLocked = false;
        ResetState();
    }

    public void CaptureInitialState()
    {
        if (rotA != null)
            _initialLocalRotA = rotA.transform.localRotation;

        _captured = true;
    }

    public void ResetState()
    {
        StopPendingRoutine();

        if (!_captured)
            CaptureInitialState();

        if (rotA != null)
            rotA.SetLocalRotationImmediate(_initialLocalRotA);

        _opened = false;

        if (!_dragLocked && startOpened)
            Open();
    }

    public void SetBuildVisualVisible(bool visible)
    {
        if (buildVisualRoot != null)
            buildVisualRoot.SetActive(visible);
    }

    IEnumerator CoOpenWhenReady()
    {
        yield return new WaitUntil(() => !IsRotating());

        if (!_opened && !_dragLocked)
            ApplyOpen();

        _pendingRoutine = null;
    }

    IEnumerator CoCloseWhenReady()
    {
        yield return new WaitUntil(() => !IsRotating());

        if (_opened && !_dragLocked)
            ApplyClose();

        _pendingRoutine = null;
    }

    void ApplyOpen()
    {
        ApplyDelta(openDeltaA);
        _opened = true;
    }

    void ApplyClose()
    {
        ApplyDelta(-openDeltaA);
        _opened = false;
    }

    void StopPendingRoutine()
    {
        if (_pendingRoutine != null)
        {
            StopCoroutine(_pendingRoutine);
            _pendingRoutine = null;
        }
    }

    bool IsRotating()
    {
        return rotA != null && rotA.IsRotating;
    }

    void ApplyDelta(float deltaA)
    {
        bool flipped = IsFlipped();
        if (invertWhenFlipped && flipped)
            deltaA = -deltaA;

        if (rotA != null)
            rotA.RotateBy(deltaA);
    }

    bool IsFlipped()
    {
        return flipProbe != null && flipProbe.lossyScale.x < 0f;
    }
}