using UnityEngine;

public class RailAnchorBinding2D : MonoBehaviour
{
    [Header("Follow Target")]
    [Tooltip("이 앵커 노드에 연결된 레일 endpoint가 따라갈 SnapPoint")]
    [SerializeField] SnapPoint followSnapPoint;

    public SnapPoint FollowSnapPoint => followSnapPoint;

    public bool HasValidFollowSnapPoint => followSnapPoint != null;

    public void SetFollowSnapPoint(SnapPoint sp)
    {
        followSnapPoint = sp;
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        // 가능하면 Connector를 쓰는 걸 권장
        // AnchorRoot를 넣어도 막지는 않지만, 의도 확인용
        if (followSnapPoint != null && followSnapPoint.role == SnapPointRole.AnchorRoot)
        {
            // 필요하면 여기서 경고만 띄우고 그대로 둬도 됨
            // Debug.LogWarning($"{name}: followSnapPoint는 보통 Connector를 권장합니다.", this);
        }
    }
#endif
}