using UnityEngine;
using UnityEngine.Events;

public class DoorObject : MonoBehaviour, IPoResettable
{
    [Header("Sensor")]
    [SerializeField] LaserSensor2D sensor;

    [Header("Door Mover")]
    [SerializeField] PoMove door;

    [Header("Visual")]
    [SerializeField] GameObject[] connectedVisuals;
    [SerializeField] GameObject[] disconnectedVisuals;

    [Header("Open Settings")]
    [SerializeField] float openDistance = 2f;
    [SerializeField] bool startOpened = false;

    [Header("Events")]
    public UnityEvent onOpen;
    public UnityEvent onClose;

    [Header("Audio")]
    [SerializeField] SciFiAudioPlayer audioPlayer;

    Vector3 _closedPos;
    bool _isOpen;

    bool _initialized;

    public bool IsOpen => _isOpen;

    void Awake()
    {
        CacheClosedPosition();

        if (startOpened)
            SetOpenedImmediate();
        else
            SetClosedImmediate();

        RefreshVisual();

        _initialized = true;
    }

    void Update()
    {
        if (sensor == null) return;

        if (sensor.IsReceivingLaser)
            Open();
        else
            Close();
    }

    void CacheClosedPosition()
    {
        if (door != null)
            _closedPos = door.GetStoredPosition();
    }

    public void Open()
    {
        if (_isOpen) return;

        _isOpen = true;

        if (door != null)
            door.MoveTo(_closedPos + Vector3.down * openDistance, true);

        RefreshVisual();

        if (_initialized)
            audioPlayer?.PlayDoorOpen();

        onOpen?.Invoke();
    }

    public void Close()
    {
        if (!_isOpen) return;

        _isOpen = false;

        if (door != null)
            door.MoveTo(_closedPos, true);

        RefreshVisual();

        if (_initialized)
            audioPlayer?.PlayDoorOpen();

        onClose?.Invoke();
    }

    void RefreshVisual()
    {
        SetVisualsActive(connectedVisuals, _isOpen);
        SetVisualsActive(disconnectedVisuals, !_isOpen);
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
        _initialized = false;

        if (sensor != null)
            sensor.ResetState();

        if (startOpened)
            SetOpenedImmediate();
        else
            SetClosedImmediate();

        RefreshVisual();

        _initialized = true;
    }

    public void SetOpenedImmediate()
    {
        _isOpen = true;

        if (door != null)
            door.SetPositionImmediate(_closedPos + Vector3.down * openDistance);

        RefreshVisual();
    }

    public void SetClosedImmediate()
    {
        _isOpen = false;

        if (door != null)
            door.SetPositionImmediate(_closedPos);

        RefreshVisual();
    }
}