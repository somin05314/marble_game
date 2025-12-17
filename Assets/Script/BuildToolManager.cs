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
}
