using UnityEngine;

public class RailAnchorBake2D : MonoBehaviour
{
    [SerializeField] bool bakeOnStart = true;

    void Start()
    {
        if (!bakeOnStart) return;

        var mgr = RailSnapNodeManager.Instance;
        if (mgr == null) return;

        var points = Object.FindObjectsByType<SnapPoint>(FindObjectsSortMode.None);
        for (int i = 0; i < points.Length; i++)
        {
            var sp = points[i];
            if (sp == null) continue;

            if (sp.IsAnchorRoot)
            {
                // ✅ AnchorRoot는 노드로 만들고 앵커 플래그 세팅
                mgr.GetOrCreate((Vector2)sp.transform.position, asAnchorRoot: true);
            }
        }
    }
}

