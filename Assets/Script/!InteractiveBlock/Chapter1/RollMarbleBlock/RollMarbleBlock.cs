using System.Collections;
using UnityEngine;

public class RollMarbleBlock : MonoBehaviour, IPoResettable, IDragStateHandler
{
    [Header("Trigger")]
    [SerializeField] TriggerZone triggerZone;

    [Header("Move Motor")]
    [SerializeField] PoMove moveA;

    [Header("Open Move Delta")]
    [Tooltip("열릴 때 이동할 상대 이동값")]
    [SerializeField] Vector3 openDelta = new Vector3(0f, 3f, 0f);

    [Header("State")]
    [SerializeField] bool startOpened = false;

    [Header("Build Visual")]
    [Tooltip("배치 모드에서만 보일 시작점/구슬 비주얼 루트")]
    [SerializeField] GameObject buildVisualRoot;

    bool _opened;
    bool _dragLocked;

    Vector3 _initialPositionA;
    bool _captured;

    Coroutine _pendingRoutine;

    public bool IsOpened => _opened;

    void Reset()
    {
        if (triggerZone == null)
            triggerZone = GetComponentInChildren<TriggerZone>();

        if (moveA == null)
            moveA = GetComponentInChildren<PoMove>(true);
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
        if (pressed)
            Open();
    }

    public void Open()
    {
        if (_dragLocked) return;
        if (_opened) return;

        StopPendingRoutine();

        if (IsMoving())
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

        if (IsMoving())
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
        ResetStateImmediate(reopenIfStartOpened: false);
    }

    public void EndDragState(bool committed)
    {
        _dragLocked = false;
        StopPendingRoutine();

        if (moveA != null)
            moveA.CancelMove();

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
        if (moveA != null)
            _initialPositionA = moveA.GetStoredPosition();

        _captured = true;
    }

    public void ResetState()
    {
        ResetStateImmediate(reopenIfStartOpened: true);
    }

    public void ResetStateImmediate(bool reopenIfStartOpened)
    {
        StopPendingRoutine();

        if (moveA != null)
            moveA.CancelMove();

        if (!_captured)
            CaptureInitialState();

        if (moveA != null)
            moveA.SetPositionImmediate(_initialPositionA);

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
        yield return new WaitUntil(() => !IsMoving());

        if (!_opened && !_dragLocked)
            ApplyOpen();

        _pendingRoutine = null;
    }

    IEnumerator CoCloseWhenReady()
    {
        yield return new WaitUntil(() => !IsMoving());

        if (_opened && !_dragLocked)
            ApplyClose();

        _pendingRoutine = null;
    }

    void ApplyOpen()
    {
        if (moveA != null)
            moveA.MoveBy(openDelta);

        _opened = true;
    }

    void ApplyClose()
    {
        if (moveA != null)
            moveA.MoveBy(-openDelta);

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

    bool IsMoving()
    {
        return moveA != null && moveA.IsMoving;
    }
}