using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class LaserSensor2D : MonoBehaviour, IPoResettable, ILaserReceiver2D
{
    [Header("Visual")]
    [SerializeField] GameObject[] receivingVisuals;
    [SerializeField] GameObject[] idleVisuals;

    bool _isReceivingLaser;
    int _lastLaserFrame = -1;

    public bool IsReceivingLaser => _isReceivingLaser;

    void Awake()
    {
        RefreshVisual();
    }

    void Reset()
    {
        var col = GetComponent<Collider2D>();
        if (col != null)
            col.isTrigger = true;
    }

    void LateUpdate()
    {
        if (_isReceivingLaser && _lastLaserFrame != Time.frameCount)
        {
            _isReceivingLaser = false;
            RefreshVisual();
        }
    }

    public void ReceiveLaser()
    {
        _lastLaserFrame = Time.frameCount;

        if (_isReceivingLaser) return;

        _isReceivingLaser = true;
        RefreshVisual();
    }

    public void ReceiveLaser(Vector2 incomingDir, Vector2 hitPoint)
    {
        ReceiveLaser();
    }

    public void ResetState()
    {
        _isReceivingLaser = false;
        _lastLaserFrame = -1;
        RefreshVisual();
    }

    void RefreshVisual()
    {
        SetVisualsActive(receivingVisuals, _isReceivingLaser);
        SetVisualsActive(idleVisuals, !_isReceivingLaser);
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
}