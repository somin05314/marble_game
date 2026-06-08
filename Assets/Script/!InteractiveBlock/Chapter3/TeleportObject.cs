using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class TeleportObject : MonoBehaviour, IPoResettable
{
    static readonly List<TeleportObject> allTeleporters = new List<TeleportObject>();

    [Header("Pair")]
    [SerializeField] int pairId = 0;

    [Header("Sensor")]
    [SerializeField] LaserSensor2D sensor;

    [Header("Teleport Target")]
    [SerializeField] TeleportObject target;

    [Header("Exit")]
    [SerializeField] Transform exitPoint;

    [Header("Settings")]
    [SerializeField] bool startPowered = false;

    [Header("Events")]
    public UnityEvent onTeleport;

    [Header("Visual")]
    [SerializeField] GameObject activeVisual;
    [SerializeField] GameObject inactiveVisual;

    [Header("Powered Rotation")]
    [SerializeField] Transform rotatingObject;
    [SerializeField] float rotationSpeed = 180f;
    [SerializeField] bool rotateWhenPowered = true;

    [Header("Audio")]
    [SerializeField] SciFiAudioPlayer audioPlayer;

    bool _isPowered;
    HashSet<Collider2D> ignoredColliders = new HashSet<Collider2D>();

    public bool IsPowered => _isPowered;
    public Transform ExitPoint => exitPoint;

    void Awake()
    {
        _isPowered = startPowered;
        ApplyVisual();
    }

    void OnEnable()
    {
        if (!allTeleporters.Contains(this))
            allTeleporters.Add(this);

        RebuildPairs();
    }

    void OnDisable()
    {
        allTeleporters.Remove(this);

        if (target != null && target.target == this)
            target.target = null;

        target = null;

        RebuildPairs();
    }

    void Update()
    {
        if (sensor != null)
            SetPowered(sensor.IsReceivingLaser);

        UpdatePoweredRotation();
    }

    void UpdatePoweredRotation()
    {
        if (!rotateWhenPowered) return;
        if (!_isPowered) return;
        if (rotatingObject == null) return;

        rotatingObject.Rotate(0f, 0f, rotationSpeed * Time.deltaTime);
    }

    public void SetPowered(bool powered)
    {
        if (_isPowered == powered) return;

        _isPowered = powered;
        ApplyVisual();
    }

    void ApplyVisual()
    {
        if (activeVisual != null)
            activeVisual.SetActive(_isPowered);

        if (inactiveVisual != null)
            inactiveVisual.SetActive(!_isPowered);
    }

    static void RebuildPairs()
    {
        for (int i = 0; i < allTeleporters.Count; i++)
        {
            var a = allTeleporters[i];
            if (a == null) continue;

            a.target = null;

            for (int j = 0; j < allTeleporters.Count; j++)
            {
                if (i == j) continue;

                var b = allTeleporters[j];
                if (b == null) continue;

                if (a.pairId == b.pairId)
                {
                    a.target = b;
                    break;
                }
            }
        }
    }

    public void TryTeleport(Collider2D col)
    {
        if (!_isPowered) return;
        if (target == null) return;
        if (!col.CompareTag("Marble")) return;

        if (ignoredColliders.Contains(col))
            return;

        Teleport(col);
    }

    void Teleport(Collider2D col)
    {
        Transform exit = target.ExitPoint != null
            ? target.ExitPoint
            : target.transform;

        col.transform.position = exit.position;

        audioPlayer?.PlayTeleport();

        target.IgnoreUntilExit(col);

        onTeleport?.Invoke();
    }

    public void IgnoreUntilExit(Collider2D col)
    {
        if (col == null) return;
        ignoredColliders.Add(col);
    }

    public void NotifyExit(Collider2D col)
    {
        if (col == null) return;
        ignoredColliders.Remove(col);
    }

    public void ResetState()
    {
        if (sensor != null)
            sensor.ResetState();

        _isPowered = startPowered;
        ignoredColliders.Clear();

        ApplyVisual();
        RebuildPairs();
    }
}