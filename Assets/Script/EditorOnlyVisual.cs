using UnityEngine;

public class EditorOnlyVisual : MonoBehaviour
{
    Renderer rend;

    void Awake()
    {
        rend = GetComponent<Renderer>();

        if (Application.isPlaying && rend != null)
        {
            rend.enabled = false; // 게임 중엔 안 보임
        }
    }
}
