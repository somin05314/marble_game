using UnityEngine;

public class ToggleLaserSwitchObject : MonoBehaviour, IPoResettable
{
    [Header("Trigger")]
    [SerializeField] TriggerZone triggerZone;

    [Header("Laser")]
    [SerializeField] LaserEmitter2D laserEmitter;

    [Header("Visual")]
    [SerializeField] GameObject[] onVisuals;
    [SerializeField] GameObject[] offVisuals;

    [Header("State")]
    [SerializeField] bool startOn = false;

    [Header("Audio")]
    [SerializeField] SciFiAudioPlayer audioPlayer;

    bool _isOn;

    void Reset()
    {
        if (triggerZone == null)
            triggerZone = GetComponentInChildren<TriggerZone>();

        if (laserEmitter == null)
            laserEmitter = GetComponentInChildren<LaserEmitter2D>(true);
    }

    void Awake()
    {
        _isOn = startOn;
        Apply();
    }

    void OnEnable()
    {
        if (triggerZone != null)
            triggerZone.PressedChanged += HandlePressedChanged;
    }

    void OnDisable()
    {
        if (triggerZone != null)
            triggerZone.PressedChanged -= HandlePressedChanged;
    }

    void HandlePressedChanged(bool pressed)
    {
        if (!pressed) return;

        bool wasOn = _isOn;

        if (triggerZone != null && triggerZone.UseToggleMode)
            _isOn = triggerZone.ToggleState;
        else
            _isOn = !_isOn;

        Apply();

        if (!wasOn && _isOn)
            audioPlayer?.PlayLaserFire();
    }

    void Apply()
    {
        if (laserEmitter != null)
            laserEmitter.SetPowered(_isOn);

        RefreshVisual();
    }

    void RefreshVisual()
    {
        SetVisualsActive(onVisuals, _isOn);
        SetVisualsActive(offVisuals, !_isOn);
    }

    void SetVisualsActive(GameObject[] visuals, bool active)
    {
        if (visuals == null) return;

        for (int i = 0; i < visuals.Length; i++)
        {
            if (visuals[i] != null)
                visuals[i].SetActive(active);
        }
    }

    public void ResetState()
    {
        _isOn = startOn;
        Apply();
    }
}