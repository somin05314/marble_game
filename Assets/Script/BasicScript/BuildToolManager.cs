using System;
using UnityEngine;

public class BuildToolManager : MonoBehaviour
{
    public static BuildToolManager Instance;

    [SerializeField] private GridPlacer gridPlacer;   // ✅ B 방식: 인스펙터 참조
    [SerializeField] private RailToolPlacer2D railPlacer;


    public BuildTool currentTool = BuildTool.Select; // 기본은 Select

    public event Action<BuildTool> OnToolChanged;

    void Awake()
    {
        // (선택) 중복 방지
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // (선택) 인스펙터 연결 깜빡했을 때 자동 탐색
        if (gridPlacer == null)
            gridPlacer = FindFirstObjectByType<GridPlacer>();
    }

    public void SetTool(BuildTool tool)
    {
        if (currentTool == tool) return;

        currentTool = tool;

        // ✅ Place가 아니면 프리뷰 끄기
        if (tool != BuildTool.Place)
            gridPlacer?.ClearPlacePreviewObjects();

        if (tool == BuildTool.None)
        {
            SelectionManager.Instance?.Deselect();
            gridPlacer?.ClearPlacePreviewObjects();
        }

        OnToolChanged?.Invoke(tool);
    }

}
