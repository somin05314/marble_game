using System.Collections;
using UnityEngine;

public class MarbleStart : MonoBehaviour, IPoResettable
{
    [Header("Rotate Motors (Left / Right)")]
    [SerializeField] PoRotate rotLeft;
    [SerializeField] PoRotate rotRight;

    [Header("Open Delta (degrees)")]
    [Tooltip("정방향(Flip 안된 상태)에서 왼쪽 덮개가 회전할 각도(상대값)")]
    [SerializeField] float openDeltaLeft = 90f;

    [Tooltip("정방향(Flip 안된 상태)에서 오른쪽 덮개가 회전할 각도(상대값)")]
    [SerializeField] float openDeltaRight = -90f;

    [Header("Flip Handling")]
    [Tooltip("Flip 판정 기준 Transform. 비워두면 자기 자신(transform)")]
    [SerializeField] Transform flipProbe;

    [Tooltip("Flip되었을 때 회전 방향(+/-)을 반전합니다.")]
    [SerializeField] bool invertWhenFlipped = true;

    [Header("State")]
    [Tooltip("빌드 상태/리셋 직후 기본 상태")]
    [SerializeField] bool startOpened = false;

    [Header("Auto Release")]
    [Tooltip("Play 모드 진입 시 자동으로 열기")]
    [SerializeField] bool autoOpenOnPlay = true;

    [Tooltip("Play 모드 진입 후 몇 초 뒤에 열지")]
    [SerializeField] float openDelaySeconds = 0.5f;

    [Header("Build Visual")]
    [Tooltip("빌드 모드에서만 보일 공 스프라이트 루트")]
    [SerializeField] GameObject buildVisualRoot;

    bool _opened;

    Quaternion _leftRot0;
    Quaternion _rightRot0;
    bool _cached;

    Coroutine _coAutoOpen;

    void Awake()
    {
        if (flipProbe == null)
            flipProbe = transform;

        CacheInitialIfNeeded();
        ApplyInitialState();
        RefreshBuildVisual();
    }

    void OnEnable()
    {
        GameModeManager.OnModeChanged += HandleModeChanged;
        GameModeManager.OnGameReset += HandleGameReset;

        if (GameModeManager.Instance != null)
            HandleModeChanged(GameModeManager.Instance.currentMode);
    }
    void OnDisable()
    {
        GameModeManager.OnModeChanged -= HandleModeChanged;
        GameModeManager.OnGameReset -= HandleGameReset;

        if (_coAutoOpen != null)
        {
            StopCoroutine(_coAutoOpen);
            _coAutoOpen = null;
        }
    }

    void CacheInitialIfNeeded()
    {
        if (_cached) return;

        if (rotLeft != null) _leftRot0 = rotLeft.transform.localRotation;
        if (rotRight != null) _rightRot0 = rotRight.transform.localRotation;

        _cached = true;
    }

    void ApplyInitialState()
    {
        CacheInitialIfNeeded();

        if (rotLeft != null) rotLeft.SetLocalRotationImmediate(_leftRot0);
        if (rotRight != null) rotRight.SetLocalRotationImmediate(_rightRot0);

        _opened = false;

        if (startOpened)
        {
            ApplyDelta(openDeltaLeft, openDeltaRight);
            _opened = true;
        }
    }

    void HandleModeChanged(GameMode mode)
    {
        RefreshBuildVisual();

        if (!autoOpenOnPlay) return;
        if (mode != GameMode.Play) return;

        if (_coAutoOpen != null)
            StopCoroutine(_coAutoOpen);

        _coAutoOpen = StartCoroutine(CoAutoOpen());
    }

    IEnumerator CoAutoOpen()
    {
        if (openDelaySeconds > 0f)
            yield return new WaitForSeconds(openDelaySeconds);

        if (GameModeManager.Instance == null ||
            GameModeManager.Instance.currentMode != GameMode.Play)
        {
            _coAutoOpen = null;
            yield break;
        }

        Open();
        _coAutoOpen = null;
    }

    void HandleGameReset()
    {
        ResetState();
    }

    public void ResetState()
    {
        if (_coAutoOpen != null)
        {
            StopCoroutine(_coAutoOpen);
            _coAutoOpen = null;
        }

        ApplyInitialState();
        RefreshBuildVisual();
    }

    public void Open()
    {
        if (_opened) return;

        ApplyDelta(openDeltaLeft, openDeltaRight);
        _opened = true;
    }

    public void Close()
    {
        if (!_opened) return;

        ApplyDelta(-openDeltaLeft, -openDeltaRight);
        _opened = false;
    }

    public void Toggle()
    {
        if (_opened) Close();
        else Open();
    }

    public void ReleaseMarble()
    {
        Open();
    }

    void ApplyDelta(float deltaLeft, float deltaRight)
    {
        bool flipped = IsFlipped();
        if (invertWhenFlipped && flipped)
        {
            deltaLeft = -deltaLeft;
            deltaRight = -deltaRight;
        }

        if (rotLeft != null) rotLeft.RotateBy(deltaLeft);
        if (rotRight != null) rotRight.RotateBy(deltaRight);
    }

    bool IsFlipped()
    {
        return flipProbe != null && flipProbe.lossyScale.x < 0f;
    }

    void RefreshBuildVisual()
    {
        if (buildVisualRoot == null) return;
        if (GameModeManager.Instance == null) return;

        bool show = GameModeManager.Instance.currentMode == GameMode.Build;
        if (buildVisualRoot.activeSelf != show)
            buildVisualRoot.SetActive(show);
    }
}