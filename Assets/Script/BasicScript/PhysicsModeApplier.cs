using UnityEngine;

public class PhysicsModeApplier : MonoBehaviour
{
    public void Apply(GameMode mode)
    {
        var objects = FindObjectsOfType<PlacementObject>();

        foreach (var po in objects)
        {
            Rigidbody2D rb = po.GetComponent<Rigidbody2D>();
            if (rb == null) continue;

            if (mode == GameMode.Build)
            {
                rb.bodyType = RigidbodyType2D.Kinematic;
                rb.gravityScale = 0;
                rb.velocity = Vector2.zero;
                rb.angularVelocity = 0;
                continue;
            }

            // GameMode.Play
            switch (po.physicsType)
            {
                case PhysicsType.Static:
                    rb.bodyType = RigidbodyType2D.Static;
                    rb.gravityScale = 0;
                    break;

                case PhysicsType.DynamicNoGravity:
                    rb.bodyType = RigidbodyType2D.Dynamic;
                    rb.gravityScale = 0;
                    break;

                case PhysicsType.DynamicGravity:
                    rb.bodyType = RigidbodyType2D.Dynamic;
                    rb.gravityScale = 1;
                    break;
            }
        }
    }

}
