using UnityEngine;

public class DropCenteringZone2D : MonoBehaviour
{
    [Header("Filter")]
    [SerializeField] LayerMask ballMask;

    [Header("Angular Lock")]
    [SerializeField] bool zeroAngularOnEnter = true;
    [SerializeField] bool zeroAngularOnExit = true;

    [Header("X Velocity Lock")]
    [SerializeField] bool zeroXVelocityOnEnter = true;
    [SerializeField] bool zeroXVelocityOnExit = true;

    bool IsInMask(int layer) => (ballMask.value & (1 << layer)) != 0;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsInMask(other.gameObject.layer)) return;

        var rb = other.attachedRigidbody;
        if (rb == null) return;

        if (zeroAngularOnEnter)
            rb.angularVelocity = 0f;

        if (zeroXVelocityOnEnter)
            ClearXVelocity(rb);
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (!IsInMask(other.gameObject.layer)) return;

        var rb = other.attachedRigidbody;
        if (rb == null) return;

        if (zeroAngularOnExit)
            rb.angularVelocity = 0f;

        if (zeroXVelocityOnExit)
            ClearXVelocity(rb);
    }

    void ClearXVelocity(Rigidbody2D rb)
    {
        rb.velocity = new Vector2(0f, rb.velocity.y);
    }
}