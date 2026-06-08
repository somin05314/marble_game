public interface IBuildModeTool
{
    // Build 모드로 들어올 때(켜질 때) 한 번 호출
    void OnEnterBuildMode();

    // Build 모드에서 나갈 때(꺼질 때) 한 번 호출
    void OnExitBuildMode();
}
