using System.Collections.Generic;
using UnityEngine;


[System.Serializable]
public class PlacementSnapshot
{
    public PlacementData placementData;   // ⭐ 프리팹 정의
    public Vector3 position;
    public Quaternion rotation;

    public List<SnapLinkSnapshot> snapLinks = new();
}


[System.Serializable]
public class SnapLinkSnapshot
{
    public int myRootIndex;
    public int otherObjectIndex;
    public int otherRootIndex;

    public int mySnapId;
    public int otherSnapId;
}
