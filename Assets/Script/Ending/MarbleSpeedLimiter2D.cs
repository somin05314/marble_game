using UnityEngine;

public class MarbleSpeedLimiter2D : MonoBehaviour
{
    [SerializeField] float maxSpeed = 6f;
    [SerializeField] float maxAngularVelocity = 360f;

    Rigidbody2D rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void FixedUpdate()
    {
        if (rb == null) return;

        if (rb.velocity.magnitude > maxSpeed)
            rb.velocity = rb.velocity.normalized * maxSpeed;

        rb.angularVelocity = Mathf.Clamp(
            rb.angularVelocity,
            -maxAngularVelocity,
            maxAngularVelocity
        );
    }
}