public static class StageContext
{
    public static string CurrentStageId { get; private set; } = "";

    public static void SetStageId(string stageId)
    {
        CurrentStageId = stageId ?? "";
    }

    public static void Clear()
    {
        CurrentStageId = "";
    }
}
