using System.Collections.Generic;
using UnityEngine;


[System.Serializable]
public class PlacementSnapshot
{
    public bool underFixedRoot;
    public PlacementData placementData;   // ⭐ 프리팹 정의
    public Vector3 position;
    public Quaternion rotation;

    public Vector3 localScale;            // ✅ 추가 (Flip/Scale 저장용)

    public List<RailBindingSnapshot> railBindings; // ✅ 추가
    public int strengthLevel = -1;
}

[System.Serializable]
public class RailBindingSnapshot
{
    public string nodeId;        // RailSnapNode2D의 고유 ID
    public string anchorPath;    // PO 루트 기준 Transform 경로 (예: "SnapPoints/A")
    public Vector2 localOffset;  // 보험

    // ✅ nodeId가 복원 시 바뀌거나 못 찾는 경우를 대비한 좌표 폴백
    public Vector2 nodeWorldPos;
}

[System.Serializable]
public class RailSpanSnapshot
{
    // 레일 종류가 1개라면 비워도 됨(필요 시 확장)
    public string railTypeId;

    public Vector2 startWorld;
    public Vector2 endWorld;

    public string startNodeId;
    public string endNodeId;
}

