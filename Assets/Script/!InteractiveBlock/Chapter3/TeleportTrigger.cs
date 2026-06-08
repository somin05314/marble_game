using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class TeleportTrigger : MonoBehaviour
{
    [SerializeField] TeleportObject owner;

    void Awake()
    {
        if (owner == null)
            owner = GetComponentInParent<TeleportObject>();

        var col = GetComponent<Collider2D>();
        col.isTrigger = true;
    }

    void OnTriggerEnter2D(Collider2D col)
    {
        if (owner == null) return;

        owner.TryTeleport(col);
    }

    void OnTriggerExit2D(Collider2D col)
    {
        if (owner == null) return;

        owner.NotifyExit(col);
    }
}