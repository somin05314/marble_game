using UnityEngine;

public class StageIdGuard : MonoBehaviour
{
    string lockedId;

    void Awake()
    {
        lockedId = StageContext.CurrentStageId;
    }

    void LateUpdate()
    {
        if (StageContext.CurrentStageId != lockedId)
        {
            Debug.LogWarning($"[StageIdGuard] StageId changed! {StageContext.CurrentStageId} -> {lockedId} (reverted)");
            StageContext.SetStageId(lockedId);
        }
    }
}
