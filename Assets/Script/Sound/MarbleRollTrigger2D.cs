using UnityEngine;

public class MarbleRollTrigger2D : MonoBehaviour
{
    MarbleRollingAudio _owner;

    public void Init(MarbleRollingAudio owner)
    {
        _owner = owner;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (_owner != null)
            _owner.NotifyRollTriggerEnter(other);
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (_owner != null)
            _owner.NotifyRollTriggerExit(other);
    }
}