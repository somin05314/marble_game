using UnityEngine;

public class BlackholeObject : MonoBehaviour
{
    [Header("Sensor")]
    [SerializeField] LaserSensor2D sensor;

    [Header("Center")]
    [SerializeField] Transform centerPoint;

    [Header("Range")]
    [SerializeField] float radius = 3f;

    [Header("Force")]
    [SerializeField] float maxForce = 180f;
    [SerializeField] float minForce = 50f;
    [SerializeField] float velocityPullStrength = 3f;
    [SerializeField] float maxPullSpeed = 12f;

    [Header("Snap")]
    [SerializeField] float snapRadius = 0.3f;

    [Header("Settings")]
    [SerializeField] bool startPowered = false;

    [Header("Visual")]
    [SerializeField] GameObject[] connectedVisuals;
    [SerializeField] GameObject[] disconnectedVisuals;

    [Header("Audio")]
    [SerializeField] SciFiAudioPlayer audioPlayer;
    [SerializeField] bool useLoopSound = true;

    bool _isPowered;

    void Awake()
    {
        _isPowered = startPowered;
        RefreshVisual();
    }

    void Update()
    {
        bool nextPowered = sensor != null
            ? sensor.IsReceivingLaser
            : startPowered;

        if (_isPowered == nextPowered)
            return;

        _isPowered = nextPowered;
        RefreshVisual();

        if (_isPowered)
        {
            audioPlayer?.PlayPowerOn();

            if (useLoopSound)
                audioPlayer?.StartBlackholeLoop();
        }
        else
        {
            audioPlayer?.StopLoop();
        }
    }

    void FixedUpdate()
    {
        if (!_isPowered)
            return;

        Vector2 center = GetCenter();

        Collider2D[] hits = Physics2D.OverlapCircleAll(center, radius);

        foreach (var col in hits)
        {
            if (!col.CompareTag("Marble"))
                continue;

            Rigidbody2D rb = col.attachedRigidbody;
            if (rb == null)
                continue;

            Vector2 dir = center - rb.position;
            float dist = dir.magnitude;

            if (dist < 0.001f)
                continue;

            float t = 1f - Mathf.Clamp01(dist / radius);
            float force = Mathf.Lerp(minForce, maxForce, t);

            Vector2 pullDir = dir.normalized;

            rb.AddForce(pullDir * force, ForceMode2D.Force);

            float targetSpeed = Mathf.Lerp(4f, maxPullSpeed, t);
            Vector2 targetVelocity = pullDir * targetSpeed;

            rb.velocity = Vector2.Lerp(
                rb.velocity,
                targetVelocity,
                Time.fixedDeltaTime * velocityPullStrength
            );

            if (dist < snapRadius)
            {
                rb.velocity = Vector2.zero;
                rb.position = center;
            }
        }
    }

    void RefreshVisual()
    {
        SetActiveArray(connectedVisuals, _isPowered);
        SetActiveArray(disconnectedVisuals, !_isPowered);
    }

    void SetActiveArray(GameObject[] targets, bool active)
    {
        if (targets == null) return;

        foreach (var obj in targets)
        {
            if (obj != null)
                obj.SetActive(active);
        }
    }

    Vector2 GetCenter()
    {
        return centerPoint != null
            ? (Vector2)centerPoint.position
            : (Vector2)transform.position;
    }

    void OnDrawGizmosSelected()
    {
        Vector2 center = GetCenter();

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(center, radius);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(center, snapRadius);
    }
}