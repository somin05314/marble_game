using UnityEngine;

public enum LaserDirection
{
    Right,
    Left,
    Up,
    Down,
    FirePointRight,
    FirePointUp
}

[RequireComponent(typeof(LineRenderer))]

public class LaserEmitter2D : MonoBehaviour
{
    [Header("Laser")]
    [SerializeField] Transform firePoint;
    [SerializeField] float maxDistance = 20f;
    [SerializeField] float laserWidth = 0.4f;
    [SerializeField] LayerMask blockingMask;

    [Header("Hit Effect")]
    [SerializeField] GameObject hitEffect;

    LineRenderer _line;

    [Header("Power")]
    [SerializeField] bool startPowered = true;

    [Header("Direction")]
    [SerializeField] LaserDirection laserDirection = LaserDirection.FirePointRight;

    [Header("Audio")]
    [SerializeField] SciFiAudioPlayer audioPlayer;

    bool _isPowered;

    public bool IsPowered => _isPowered;

    void Awake()
    {
        _line = GetComponent<LineRenderer>();

        _line.positionCount = 2;
        _line.useWorldSpace = true;
        _line.startWidth = laserWidth;
        _line.endWidth = laserWidth;

        if (firePoint == null)
            firePoint = transform;

        _isPowered = startPowered;

        if (hitEffect != null)
            hitEffect.SetActive(false);

        SetVisualActive(_isPowered);
    }

    public void SetPowered(bool powered)
    {
        if (_isPowered == powered)
            return;

        _isPowered = powered;
        SetVisualActive(_isPowered);

        if (_isPowered)
            audioPlayer?.PlayLaserFire();
    }

    void SetVisualActive(bool active)
    {
        if (_line != null)
            _line.enabled = active;
    }

    void Update()
    {
        if (!_isPowered)
        {
            SetVisualActive(false);

            if (hitEffect != null)
                hitEffect.SetActive(false);

            return;
        }

        SetVisualActive(true);
        UpdateLaser();
    }

    void UpdateLaser()
    {
        Vector3 start = firePoint.position;
        Vector2 dir = GetLaserDirection();

        RaycastHit2D[] hits = Physics2D.RaycastAll(
            start,
            dir,
            maxDistance,
            blockingMask
        );

        float blockDistance = maxDistance;
        RaycastHit2D blockHit = default;
        bool hasBlockHit = false;

        // 1. 첫 번째 막힘 지점 찾기
        foreach (var hit in hits)
        {
            if (hit.collider == null)
                continue;

            bool isLaserReceiver =
                hit.collider.GetComponentInParent<ILaserReceiver2D>() != null;

            // Trigger라도 레이저 센서면 막힘 처리
            if (hit.collider.isTrigger && !isLaserReceiver)
                continue;

            if (hit.distance < blockDistance)
            {
                blockDistance = hit.distance;
                blockHit = hit;
                hasBlockHit = true;
            }
        }

        // 2. 막힘 지점 이전에 있는 센서만 작동
        foreach (var hit in hits)
        {
            if (hit.collider == null) continue;
            if (!hit.collider.isTrigger) continue;

            if (hit.distance > blockDistance) continue;

            var receiver = hit.collider.GetComponentInParent<ILaserReceiver2D>();

            if (receiver != null)
                receiver.ReceiveLaser(dir, hit.point);
        }

        Vector3 end;

        if (hasBlockHit)
        {
            end = blockHit.point;

            if (hitEffect != null)
            {
                hitEffect.SetActive(true);

                // 충돌 지점에서 표면 바깥쪽으로 살짝 빼기
                hitEffect.transform.position = blockHit.point + blockHit.normal * 0.03f;

                // 표면 방향에 맞춰 회전
                hitEffect.transform.right = blockHit.normal;
            }
        }
        else
        {
            end = start + (Vector3)(dir * maxDistance);

            if (hitEffect != null)
                hitEffect.SetActive(false);
        }

        _line.SetPosition(0, start);
        _line.SetPosition(1, end);
    }

    Vector2 GetLaserDirection()
    {
        switch (laserDirection)
        {
            case LaserDirection.Right:
                return Vector2.right;

            case LaserDirection.Left:
                return Vector2.left;

            case LaserDirection.Up:
                return Vector2.up;

            case LaserDirection.Down:
                return Vector2.down;

            case LaserDirection.FirePointUp:
                return firePoint.up;

            case LaserDirection.FirePointRight:
            default:
                return firePoint.right;
        }
    }
}