using UnityEngine;

public class TrueEndingCameraFollow2D : MonoBehaviour
{
    [SerializeField] Camera targetCamera;

    [Header("Follow")]
    [SerializeField] Transform target;
    [SerializeField] Vector2 offset = Vector2.zero;

    [Tooltip("낮을수록 빠르게 따라감. 0.12~0.25 추천")]
    [SerializeField] float smoothTime = 0.18f;

    [SerializeField] float maxSpeed = 100f;

    [Header("Look Ahead")]
    [SerializeField] bool useLookAhead = true;
    [SerializeField] Rigidbody2D targetRb;
    [SerializeField] float lookAheadTime = 0.25f;
    [SerializeField] Vector2 lookAheadMultiplier = new Vector2(0.35f, 0.15f);

    [Header("Camera")]
    [SerializeField] float orthoSize = 8f;
    [SerializeField] bool applyOrthoSizeOnStart = true;

    [SerializeField] bool snapToTargetOnSet = true;

    Vector3 velocity;

    void Awake()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;
    }

    void Start()
    {
        if (targetCamera != null && applyOrthoSizeOnStart)
            targetCamera.orthographicSize = orthoSize;
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        targetRb = target != null ? target.GetComponent<Rigidbody2D>() : null;

        if (snapToTargetOnSet && targetCamera != null && target != null)
        {
            Vector3 pos = new Vector3(
                target.position.x + offset.x,
                target.position.y + offset.y,
                targetCamera.transform.position.z
            );

            targetCamera.transform.position = pos;
            velocity = Vector3.zero;
        }
    }

    void LateUpdate()
    {
        if (targetCamera == null || target == null)
            return;

        Vector2 lookAhead = Vector2.zero;

        if (useLookAhead && targetRb != null)
        {
            Vector2 predicted = targetRb.velocity * lookAheadTime;
            lookAhead = new Vector2(
                predicted.x * lookAheadMultiplier.x,
                predicted.y * lookAheadMultiplier.y
            );
        }

        Vector3 desiredPos = new Vector3(
            target.position.x + offset.x + lookAhead.x,
            target.position.y + offset.y + lookAhead.y,
            targetCamera.transform.position.z
        );

        targetCamera.transform.position = Vector3.SmoothDamp(
            targetCamera.transform.position,
            desiredPos,
            ref velocity,
            smoothTime,
            maxSpeed,
            Time.deltaTime
        );
    }
}