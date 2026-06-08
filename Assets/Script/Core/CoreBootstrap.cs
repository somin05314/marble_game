using UnityEngine;

public class CoreBootstrap : MonoBehaviour
{
    private void Awake()
    {
        // 씬 전환해도 CoreRoot 유지
        DontDestroyOnLoad(gameObject);
    }
}
