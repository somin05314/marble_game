using UnityEngine;

public class PressurePlateTrigger2D : MonoBehaviour
{
    [SerializeField] LayerMask activatorMask;
    [SerializeField] TrapDoorKinematic2D trapDoor;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (((1 << other.gameObject.layer) & activatorMask) == 0) return;
        trapDoor?.TriggerOpen();
    }
}
