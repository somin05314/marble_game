using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(AudioSource))]
public class TriggerZone : MonoBehaviour, IPoResettable
{
    [Header("Filter")]
    public LayerMask ballMask;

    [Header("Action")]
    public UnityEvent onEnter;
    public UnityEvent onExit;

    [Header("Normal Button Visual")]
    [Tooltip("기본(안눌림) 이미지 오브젝트")]
    public GameObject normalVisual;

    [Tooltip("눌림 이미지 오브젝트")]
    public GameObject pressedVisual;

    [Tooltip("공이 나가면 다시 기본 상태로 되돌릴지")]
    public bool revertOnExit = true;

    [Header("Options")]
    public bool oneShot = false;
    public float cooldown = 0f;

    [Header("Use Mode")]
    [Tooltip("켜지면 Enter 시 on/off 토글형으로 동작")]
    [SerializeField] bool useToggleMode = false;
    public bool UseToggleMode => useToggleMode;

    [Header("Toggle Visual (4-State)")]
    [SerializeField] GameObject toggleOffVisual;
    [SerializeField] GameObject toggleOffPressedVisual;
    [SerializeField] GameObject toggleOnVisual;
    [SerializeField] GameObject toggleOnPressedVisual;

    [Header("Demo")]
    [Tooltip("데모에서 눌린 상태를 유지할 시간")]
    [SerializeField] float demoPressDuration = 1.0f;

    [Tooltip("데모 시작 전에 강제로 기본 상태로 초기화할지")]
    [SerializeField] bool resetBeforeDemo = true;

    [Header("Audio")]
    [SerializeField] AudioSource audioSource;
    [SerializeField] AudioClip pressClip;
    [SerializeField] AudioClip releaseClip;
    [SerializeField, Range(0f, 1f)] float pressVolume = 1f;
    [SerializeField, Range(0f, 1f)] float releaseVolume = 0.7f;
    [SerializeField] bool playReleaseSound = false;
    [SerializeField] bool playSoundDuringDemo = false;
    [SerializeField] bool playSoundOnReset = false;

    public event Action<bool> PressedChanged;

    float _nextTime = 0f;
    bool _used = false;

    int _insideCount = 0;
    Coroutine _demoRoutine;
    bool _isForcedPressed = false;
    bool _toggleState = false;

    public bool IsDemoPlaying => _demoRoutine != null;
    public bool IsPressed => _isForcedPressed || _insideCount > 0;
    public bool ToggleState => _toggleState;

    void Reset()
    {
        var col = GetComponent<Collider2D>();
        col.isTrigger = true;

        audioSource = GetComponent<AudioSource>();
        if (audioSource != null)
            audioSource.playOnAwake = false;
    }

    void Awake()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        RefreshVisual();
    }

    bool IsBall(Collider2D other)
    {
        return (((1 << other.gameObject.layer) & ballMask.value) != 0);
    }

    void SetNormalButtonVisual(bool pressed)
    {
        if (normalVisual != null) normalVisual.SetActive(!pressed);
        if (pressedVisual != null) pressedVisual.SetActive(pressed);
    }

    void SetToggleButtonVisual(bool isOn, bool isPressed)
    {
        if (toggleOffVisual != null) toggleOffVisual.SetActive(!isOn && !isPressed);
        if (toggleOffPressedVisual != null) toggleOffPressedVisual.SetActive(!isOn && isPressed);
        if (toggleOnVisual != null) toggleOnVisual.SetActive(isOn && !isPressed);
        if (toggleOnPressedVisual != null) toggleOnPressedVisual.SetActive(isOn && isPressed);
    }

    void RefreshVisual()
    {
        if (useToggleMode)
            SetToggleButtonVisual(_toggleState, IsPressed);
        else
            SetNormalButtonVisual(IsPressed);
    }

    void PlayPressSound()
    {
        if (audioSource == null || pressClip == null) return;
        audioSource.PlayOneShot(pressClip, pressVolume);
    }

    void PlayReleaseSound()
    {
        if (!playReleaseSound) return;
        if (audioSource == null || releaseClip == null) return;
        audioSource.PlayOneShot(releaseClip, releaseVolume);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsBall(other)) return;

        _insideCount++;

        if (_insideCount != 1) return;

        TryEnterInternal(playAudio: true);
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (!IsBall(other)) return;

        _insideCount = Mathf.Max(0, _insideCount - 1);

        if (_insideCount != 0) return;

        ExitInternal(playAudio: true);
    }

    void TryEnterInternal(bool playAudio)
    {
        if (_used && oneShot) return;
        if (cooldown > 0f && Time.time < _nextTime) return;

        if (useToggleMode)
            _toggleState = !_toggleState;

        RefreshVisual();

        PressedChanged?.Invoke(true);
        onEnter?.Invoke();

        if (playAudio)
            PlayPressSound();

        if (cooldown > 0f) _nextTime = Time.time + cooldown;
        if (oneShot) _used = true;
    }

    void ExitInternal(bool playAudio)
    {
        if (!useToggleMode)
        {
            if (revertOnExit)
                SetNormalButtonVisual(false);
        }
        else
        {
            RefreshVisual();
        }

        PressedChanged?.Invoke(false);
        onExit?.Invoke();

        if (playAudio)
            PlayReleaseSound();
    }

    public void ForceEnter()
    {
        _isForcedPressed = true;
        TryEnterInternal(playAudio: playSoundDuringDemo);
    }

    public void ForceExit()
    {
        _isForcedPressed = false;
        ExitInternal(playAudio: playSoundDuringDemo);
    }

    public void PlayDemo()
    {
        PlayDemo(demoPressDuration);
    }

    public void PlayDemo(float pressDuration)
    {
        StopDemo(resetState: false);
        _demoRoutine = StartCoroutine(CoPlayDemo(pressDuration));
    }

    IEnumerator CoPlayDemo(float pressDuration)
    {
        if (resetBeforeDemo)
            ResetState();
        else
            ForceExit();

        yield return null;

        ForceEnter();
        yield return new WaitForSeconds(pressDuration);

        ForceExit();
        _demoRoutine = null;
    }

    public void StopDemo(bool resetState = true)
    {
        if (_demoRoutine != null)
        {
            StopCoroutine(_demoRoutine);
            _demoRoutine = null;
        }

        _isForcedPressed = false;

        if (resetState)
            ResetState();
    }

    public void ResetState()
    {
        if (_demoRoutine != null)
        {
            StopCoroutine(_demoRoutine);
            _demoRoutine = null;
        }

        _insideCount = 0;
        _isForcedPressed = false;
        _nextTime = 0f;
        _used = false;
        _toggleState = false;

        RefreshVisual();
        PressedChanged?.Invoke(false);
        onExit?.Invoke();

        if (playSoundOnReset)
            PlayReleaseSound();
    }
}