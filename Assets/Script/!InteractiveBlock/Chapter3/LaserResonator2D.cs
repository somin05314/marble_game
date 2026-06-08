using UnityEngine;

public class LaserResonator2D : MonoBehaviour, IPoResettable
{
    [Header("Sensor")]
    [SerializeField] LaserSensor2D sensor;

    [Header("Range")]
    [SerializeField] float radius = 5f;
    [SerializeField] LayerMask sensorMask;

    [Header("Visual")]
    [SerializeField] GameObject[] resonatingVisuals;
    [SerializeField] GameObject[] idleVisuals;

    [Header("Audio")]
    [SerializeField] SciFiAudioPlayer audioPlayer;

    bool isResonating;

    void Reset()
    {
        sensor = GetComponentInChildren<LaserSensor2D>();
    }

    void Awake()
    {
        RefreshState();
        RefreshVisual();
    }

    void Update()
    {
        RefreshState();

        if (isResonating)
            ActivateNearbySensors();
    }

    void RefreshState()
    {
        bool next = sensor != null && sensor.IsReceivingLaser;

        if (isResonating == next)
            return;

        bool wasResonating = isResonating;

        isResonating = next;
        RefreshVisual();

        if (!wasResonating && isResonating)
            audioPlayer?.PlayPowerOn();
    }

    void ActivateNearbySensors()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, radius, sensorMask);

        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i] == null) continue;

            LaserSensor2D targetSensor = hits[i].GetComponentInParent<LaserSensor2D>();
            if (targetSensor == null) continue;

            // 자기 센서 제외
            if (targetSensor == sensor)
                continue;

            // 자기 자식 센서도 안전하게 제외
            if (targetSensor.transform.IsChildOf(transform))
                continue;

            targetSensor.ReceiveLaser();
        }
    }

    public void ResetState()
    {
        isResonating = false;

        if (sensor != null)
            sensor.ResetState();

        RefreshVisual();
    }

    void RefreshVisual()
    {
        SetVisualsActive(resonatingVisuals, isResonating);
        SetVisualsActive(idleVisuals, !isResonating);
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

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}