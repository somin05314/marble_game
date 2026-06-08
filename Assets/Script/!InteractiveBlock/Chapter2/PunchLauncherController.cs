using System.Collections;
using UnityEngine;

public class PunchLauncherController : MonoBehaviour, IPoResettable, IDragStateHandler
{
    [Header("Trigger")]
    [SerializeField] TriggerZone triggerZone;

    [Header("Move Motor")]
    [SerializeField] PoMove moveA;

    [Header("Sequence")]
    [Tooltip("발사 전 왼쪽으로 당기는 거리")]
    [SerializeField] Vector3 pullBackDelta = new Vector3(-0.5f, 0f, 0f);

    [Tooltip("오른쪽으로 강하게 펀치하는 거리")]
    [SerializeField] Vector3 punchDelta = new Vector3(2f, 0f, 0f);

    [Header("Durations")]
    [SerializeField] float pullBackDuration = 0.2f;
    [SerializeField] float punchDuration = 0.08f;
    [SerializeField] float returnDuration = 0.3f;
    [SerializeField] float holdAfterPunch = 0.03f;

    [Header("Build Visual")]
    [SerializeField] GameObject buildVisualRoot;

    [Header("Audio")]
    [SerializeField] PoMachineAudioPlayer audioPlayer;

    bool _dragLocked;
    bool _captured;
    bool _isBusy;

    float _defaultMoveDuration;
    Vector3 _initialPositionA;

    Coroutine _sequenceRoutine;

    public bool IsBusy => _isBusy;

    void Reset()
    {
        if (triggerZone == null)
            triggerZone = GetComponentInChildren<TriggerZone>();

        if (moveA == null)
            moveA = GetComponentInChildren<PoMove>(true);

        if (audioPlayer == null)
            audioPlayer = GetComponentInChildren<PoMachineAudioPlayer>(true);
    }

    void Awake()
    {
        CaptureInitialState();

        if (moveA != null)
            _defaultMoveDuration = moveA.MoveDuration;
    }

    void Start()
    {
        ResetState();

        if (GameModeManager.Instance != null)
            SetBuildVisualVisible(GameModeManager.Instance.currentMode == GameMode.Build);
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

    void HandleModeChanged(GameMode mode)
    {
        SetBuildVisualVisible(mode == GameMode.Build);
    }

    void HandleTriggerPressedChanged(bool pressed)
    {
        if (pressed)
            Fire();
    }

    public void Fire()
    {
        if (_dragLocked) return;
        if (_isBusy) return;
        if (moveA == null) return;

        if (_sequenceRoutine != null)
            StopCoroutine(_sequenceRoutine);

        _sequenceRoutine = StartCoroutine(CoFireSequence());
    }

    IEnumerator CoFireSequence()
    {
        _isBusy = true;

        moveA.CancelMove();
        moveA.SetPositionImmediate(_initialPositionA);

        moveA.SetMoveDuration(pullBackDuration);
        moveA.MoveBy(pullBackDelta);
        yield return new WaitUntil(() => moveA == null || !moveA.IsMoving);

        audioPlayer?.PlayPunchTing();

        moveA.SetMoveDuration(punchDuration);
        moveA.MoveBy(punchDelta);
        yield return new WaitUntil(() => moveA == null || !moveA.IsMoving);

        if (holdAfterPunch > 0f)
            yield return new WaitForSeconds(holdAfterPunch);

        moveA.SetMoveDuration(returnDuration);
        moveA.MoveTo(_initialPositionA);
        yield return new WaitUntil(() => moveA == null || !moveA.IsMoving);

        moveA.SetMoveDuration(_defaultMoveDuration);

        _isBusy = false;
        _sequenceRoutine = null;
    }

    public void BeginDragState()
    {
        _dragLocked = true;
        ResetStateImmediate();
    }

    public void EndDragState(bool committed)
    {
        _dragLocked = false;

        if (committed)
        {
            CaptureInitialState();
        }

        ResetStateImmediate();
    }

    public void CaptureInitialState()
    {
        if (moveA != null)
            _initialPositionA = moveA.GetStoredPosition();

        _captured = true;
    }

    public void ResetState()
    {
        ResetStateImmediate();
    }

    public void ResetStateImmediate()
    {
        if (!_captured)
            CaptureInitialState();

        if (_sequenceRoutine != null)
        {
            StopCoroutine(_sequenceRoutine);
            _sequenceRoutine = null;
        }

        audioPlayer?.StopAll();

        if (moveA != null)
        {
            moveA.CancelMove();
            moveA.SetPositionImmediate(_initialPositionA);
            moveA.SetMoveDuration(_defaultMoveDuration);
        }

        _isBusy = false;
    }

    public void SetBuildVisualVisible(bool visible)
    {
        if (buildVisualRoot != null)
            buildVisualRoot.SetActive(visible);
    }
}