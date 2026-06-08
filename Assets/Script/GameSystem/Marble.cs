using UnityEngine;

public class Marble : MonoBehaviour
{
    [Header("Destroy On Wall")]
    public LayerMask wallMask;
    public float destroyDelay = 0f;

    bool isDestroying = false;
    MarbleRollingAudio rollingAudio;

    void Awake()
    {
        rollingAudio = GetComponent<MarbleRollingAudio>();
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (isDestroying) return;
        if (collision.collider == null) return;
        if (collision.collider.isTrigger) return;

        int layerBit = 1 << collision.gameObject.layer;
        if ((wallMask.value & layerBit) == 0) return;

        isDestroying = true;

        if (rollingAudio != null)
            rollingAudio.PlayDestroySoundDetached(transform.position);

        Destroy(gameObject, destroyDelay);
    }
}