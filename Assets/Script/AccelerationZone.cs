using UnityEngine;

public class AccelerationZone : MonoBehaviour, IResettable
{
    [Range(1.1f, 3f)]
    public float multiply = 1.8f;

    public float minSpeed = 0.5f;

    bool used = false;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (used) return;
        if (!other.CompareTag("Marble")) return;

        Rigidbody2D rb = other.attachedRigidbody;
        if (rb == null) return;

        Vector2 v = rb.velocity;
        float speed = v.magnitude;

        if (speed < minSpeed)
            return;

        Vector2 dir = transform.right.normalized;
        rb.velocity = dir * speed * multiply;

        used = true;
    }

    // 리셋 시 호출됨
    public void ResetState()
    {
        used = false;
    }
}
