using UnityEngine;

public class StageBootstrap : MonoBehaviour
{
    [Header("Dev Convenience")]
    [SerializeField] string devDefaultStageId = "Stage_03";

    void Awake()
    {
        // StageSelect를 거치지 않고 StageScene을 바로 실행하는 경우 대비
        if (string.IsNullOrEmpty(StageContext.CurrentStageId))
            StageContext.SetStageId(devDefaultStageId);
    }
}
