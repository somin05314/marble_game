using UnityEngine;

public class TriggerOpenTrapdoor2D : MonoBehaviour
{
    public TrapdoorKinematic2D trapdoor;
    public LayerMask ballMask;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (((1 << other.gameObject.layer) & ballMask) == 0) return;
        if (trapdoor == null) return;

        trapdoor.Open();
    }
}
