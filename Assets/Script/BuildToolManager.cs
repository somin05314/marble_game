using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BuildToolManager : MonoBehaviour
{
    public static BuildToolManager Instance;

    public BuildTool currentTool = BuildTool.Place;

    void Awake()
    {
        Instance = this;
    }

    void Update()
    {
        // 임시 전환 (UI는 나중에)
        if (Input.GetKeyDown(KeyCode.Alpha1))
            currentTool = BuildTool.Place;

        if (Input.GetKeyDown(KeyCode.Alpha2))
            currentTool = BuildTool.Select;
    }

    public void SetTool(BuildTool tool)
    {
        if (currentTool == tool)
            return;

        currentTool = tool;

        // Select → 다른 툴로 갈 때 선택 해제
        if (tool != BuildTool.Select)
        {
            SelectionManager.Instance.Deselect();
        }
    }
}
