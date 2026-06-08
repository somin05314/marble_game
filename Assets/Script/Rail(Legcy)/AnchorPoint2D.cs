using UnityEngine;

public class AnchorPoint2D : MonoBehaviour
{
    public Vector2 WorldPos => transform.position;

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        Gizmos.DrawWireSphere(transform.position, 0.15f);
    }
#endif
}
