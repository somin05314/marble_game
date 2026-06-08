using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class LaserMirror2D : MonoBehaviour, ILaserReceiver2D
{
    [SerializeField] float maxDistance = 20f;
    [SerializeField] float laserWidth = 0.4f;
    [SerializeField] LayerMask blockingMask;

    [Header("Hit Effect")]
    [SerializeField] GameObject hitEffect;

    LineRenderer _line;
    int _lastLaserFrame = -1;

    void Awake()
    {
        _line = GetComponent<LineRenderer>();
        _line.positionCount = 2;
        _line.useWorldSpace = true;
        _line.startWidth = laserWidth;
        _line.endWidth = laserWidth;
        _line.enabled = false;

        if (hitEffect != null)
            hitEffect.SetActive(false);
    }

    void LateUpdate()
    {
        if (_lastLaserFrame != Time.frameCount)
        {
            _line.enabled = false;

            if (hitEffect != null)
                hitEffect.SetActive(false);
        }
    }

    public void ReceiveLaser(Vector2 incomingDir, Vector2 hitPoint)
    {
        _lastLaserFrame = Time.frameCount;

        Vector2 outDir = GetReflectedDir(incomingDir.normalized);

        Vector3 start = hitPoint + outDir * 0.05f;
        Vector3 end = CastLaser(start, outDir);

        _line.enabled = true;
        _line.SetPosition(0, start);
        _line.SetPosition(1, end);
    }

    Vector2 GetReflectedDir(Vector2 incomingDir)
    {
        Vector2 localIn = transform.InverseTransformDirection(-incomingDir);
        localIn = SnapDir(localIn);

        Vector2 localOut;

        if (localIn == Vector2.right) localOut = Vector2.up;
        else if (localIn == Vector2.up) localOut = Vector2.right;
        else if (localIn == Vector2.left) localOut = Vector2.down;
        else if (localIn == Vector2.down) localOut = Vector2.left;
        else localOut = Vector2.zero;

        return transform.TransformDirection(localOut).normalized;
    }

    Vector2 SnapDir(Vector2 dir)
    {
        if (Mathf.Abs(dir.x) > Mathf.Abs(dir.y))
            return dir.x > 0 ? Vector2.right : Vector2.left;

        return dir.y > 0 ? Vector2.up : Vector2.down;
    }

    Vector3 CastLaser(Vector3 start, Vector2 dir)
    {
        RaycastHit2D[] hits = Physics2D.RaycastAll(start, dir, maxDistance, blockingMask);

        float blockDistance = maxDistance;
        RaycastHit2D blockHit = default;
        bool hasBlockHit = false;

        foreach (var hit in hits)
        {
            if (hit.collider == null) continue;
            if (hit.collider.transform == transform) continue;

            bool isLaserReceiver =
                hit.collider.GetComponentInParent<ILaserReceiver2D>() != null;

            // 일반 Trigger는 통과
            // LaserSensor, LaserMirror 같은 수신기는 레이저를 막음
            if (hit.collider.isTrigger && !isLaserReceiver)
                continue;

            if (hit.distance < blockDistance)
            {
                blockDistance = hit.distance;
                blockHit = hit;
                hasBlockHit = true;
            }
        }

        foreach (var hit in hits)
        {
            if (hit.collider == null) continue;
            if (hit.collider.transform == transform) continue;
            if (!hit.collider.isTrigger) continue;
            if (hit.distance > blockDistance) continue;

            var receiver = hit.collider.GetComponentInParent<ILaserReceiver2D>();

            if (receiver != null)
                receiver.ReceiveLaser(dir, hit.point);
        }

        if (hasBlockHit)
        {
            if (hitEffect != null)
            {
                hitEffect.SetActive(true);
                hitEffect.transform.position = blockHit.point;
                hitEffect.transform.right = blockHit.normal;
            }

            return blockHit.point;
        }

        if (hitEffect != null)
            hitEffect.SetActive(false);

        return start + (Vector3)(dir * maxDistance);
    }
}