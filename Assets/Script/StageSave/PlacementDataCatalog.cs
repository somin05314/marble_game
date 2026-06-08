using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Placement/PlacementDataCatalog")]
public class PlacementDataCatalog : ScriptableObject
{
    public List<PlacementData> all;

    Dictionary<string, PlacementData> map;

    public void Build()
    {
        map = new Dictionary<string, PlacementData>();
        foreach (var pd in all)
        {
            if (pd == null || string.IsNullOrEmpty(pd.id)) continue;
            map[pd.id] = pd;
        }
    }

    public bool TryGet(string id, out PlacementData data)
    {
        if (map == null) Build();
        return map.TryGetValue(id, out data);
    }
}
