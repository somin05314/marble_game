using UnityEngine;

public class SnapPoint : MonoBehaviour
{
    [Header("Snap ID")]
    public int snapId;          //

    public float snapRadius = 1f;

    public float allowedPenetration = 1f;

    [HideInInspector] public SnapRoot root;
}
