using UnityEngine;

[System.Serializable]
public class SnapConnection
{
    public SnapRoot myRoot;
    public SnapRoot otherRoot;

    // 회전/유지용 핵심 정보
    public SnapPoint myPoint;
    public SnapPoint otherPoint;
}

