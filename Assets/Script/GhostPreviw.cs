using UnityEngine;

public class GhostPreview : MonoBehaviour
{
    public void SetPosition(Vector3 pos)
    {
        transform.position = pos;
    }

    public void SetColor(Color c)
    {
        foreach (var sr in GetComponentsInChildren<SpriteRenderer>())
            sr.color = new Color(c.r, c.g, c.b, 0.4f);
    }
}
