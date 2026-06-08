using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// PlacementObject의 점유 셀을 상태 기반으로 공급하는 인터페이스.
/// 반환하는 셀은 "피벗 셀 기준 로컬 오프셋"이다.
/// 예: (0,0), (1,0), (2,0)
/// 회전/플립/월드 변환은 PlacementObject가 공통 처리한다.
/// </summary>
public interface IOccupancyCellProvider
{
    bool TryGetOccupancyCellOffsets(List<Vector2Int> outOffsets);
}