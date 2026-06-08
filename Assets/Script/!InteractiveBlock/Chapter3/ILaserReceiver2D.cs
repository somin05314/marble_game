using UnityEngine;

public interface ILaserReceiver2D
{
    void ReceiveLaser(Vector2 incomingDir, Vector2 hitPoint);
}