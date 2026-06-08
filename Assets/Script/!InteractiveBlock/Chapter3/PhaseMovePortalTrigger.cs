using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class PhaseMovePortalTrigger : MonoBehaviour
{
    [SerializeField] PhaseMovePortalObject portal;

    void Reset()
    {
        Collider2D col = GetComponent<Collider2D>();
        col.isTrigger = true;

        portal = GetComponentInParent<PhaseMovePortalObject>();
    }

    void Awake()
    {
        if (portal == null)
            portal = GetComponentInParent<PhaseMovePortalObject>();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (portal == null) return;

        portal.HandleTriggerEnter(other);
    }
}