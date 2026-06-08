using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PlacementObject))]
public class PlacementObjectEditor : Editor
{
    SerializedProperty useManualOccupancyProp;
    SerializedProperty manualCellOffsetsProp;

    static int gridMinX = -5;
    static int gridMaxX = 5;
    static int gridMinY = -5;
    static int gridMaxY = 5;

    const float CellButtonSize = 26f;

    void OnEnable()
    {
        useManualOccupancyProp = serializedObject.FindProperty("useManualOccupancy");
        manualCellOffsetsProp = serializedObject.FindProperty("manualCellOffsets");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawDefaultInspector();

        EditorGUILayout.Space(10f);
        EditorGUILayout.LabelField("Manual Occupancy Painter", EditorStyles.boldLabel);

        EditorGUILayout.PropertyField(useManualOccupancyProp);

        if (useManualOccupancyProp.boolValue)
        {
            DrawBoundsUI();
            EditorGUILayout.Space(6f);
            DrawPaintGrid();
            EditorGUILayout.Space(6f);
            DrawUtilityButtons();
        }

        serializedObject.ApplyModifiedProperties();
    }

    void DrawBoundsUI()
    {
        EditorGUILayout.LabelField("Painter Bounds");

        EditorGUILayout.BeginHorizontal();
        gridMinX = EditorGUILayout.IntField("Min X", gridMinX);
        gridMaxX = EditorGUILayout.IntField("Max X", gridMaxX);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        gridMinY = EditorGUILayout.IntField("Min Y", gridMinY);
        gridMaxY = EditorGUILayout.IntField("Max Y", gridMaxY);
        EditorGUILayout.EndHorizontal();

        if (gridMinX > gridMaxX) (gridMinX, gridMaxX) = (gridMaxX, gridMinX);
        if (gridMinY > gridMaxY) (gridMinY, gridMaxY) = (gridMaxY, gridMinY);
    }

    void DrawPaintGrid()
    {
        HashSet<Vector2Int> occupied = GetCurrentCells();

        EditorGUILayout.LabelField("Click cells to toggle");

        for (int y = gridMaxY; y >= gridMinY; y--)
        {
            EditorGUILayout.BeginHorizontal();

            GUILayout.Label(y.ToString(), GUILayout.Width(28f));

            for (int x = gridMinX; x <= gridMaxX; x++)
            {
                Vector2Int cell = new Vector2Int(x, y);
                bool isOn = occupied.Contains(cell);

                Color prev = GUI.backgroundColor;
                GUI.backgroundColor = isOn ? new Color(0.3f, 0.9f, 1f) : Color.white;

                if (GUILayout.Button("", GUILayout.Width(CellButtonSize), GUILayout.Height(CellButtonSize)))
                {
                    Undo.RecordObject(target, "Toggle Manual Occupancy Cell");
                    ToggleCell(cell);
                    serializedObject.ApplyModifiedProperties();
                    EditorUtility.SetDirty(target);
                    GUI.changed = true;
                    GUIUtility.ExitGUI();
                }

                GUI.backgroundColor = prev;
            }

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(28f);
        for (int x = gridMinX; x <= gridMaxX; x++)
        {
            GUILayout.Label(x.ToString(), GUILayout.Width(CellButtonSize));
        }
        EditorGUILayout.EndHorizontal();
    }

    void DrawUtilityButtons()
    {
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Clear All"))
        {
            manualCellOffsetsProp.ClearArray();
        }

        if (GUILayout.Button("Fill Bounds"))
        {
            FillAllCellsInBounds();
        }

        if (GUILayout.Button("Rect"))
        {
            MakeSolidRectFromBounds();
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField($"Cell Count: {manualCellOffsetsProp.arraySize}");
    }

    HashSet<Vector2Int> GetCurrentCells()
    {
        HashSet<Vector2Int> set = new HashSet<Vector2Int>();

        for (int i = 0; i < manualCellOffsetsProp.arraySize; i++)
        {
            SerializedProperty element = manualCellOffsetsProp.GetArrayElementAtIndex(i);
            Vector2Int value = element.vector2IntValue;
            set.Add(value);
        }

        return set;
    }

    void ToggleCell(Vector2Int cell)
    {
        for (int i = 0; i < manualCellOffsetsProp.arraySize; i++)
        {
            SerializedProperty element = manualCellOffsetsProp.GetArrayElementAtIndex(i);
            if (element.vector2IntValue == cell)
            {
                manualCellOffsetsProp.DeleteArrayElementAtIndex(i);
                return;
            }
        }

        int newIndex = manualCellOffsetsProp.arraySize;
        manualCellOffsetsProp.InsertArrayElementAtIndex(newIndex);
        manualCellOffsetsProp.GetArrayElementAtIndex(newIndex).vector2IntValue = cell;
    }

    void FillAllCellsInBounds()
    {
        List<Vector2Int> cells = new List<Vector2Int>();

        for (int y = gridMinY; y <= gridMaxY; y++)
        {
            for (int x = gridMinX; x <= gridMaxX; x++)
            {
                cells.Add(new Vector2Int(x, y));
            }
        }

        SetCells(cells);
    }

    void MakeSolidRectFromBounds()
    {
        List<Vector2Int> cells = new List<Vector2Int>();

        for (int y = gridMinY; y <= gridMaxY; y++)
        {
            for (int x = gridMinX; x <= gridMaxX; x++)
            {
                cells.Add(new Vector2Int(x, y));
            }
        }

        SetCells(cells);
    }

    void SetCells(List<Vector2Int> cells)
    {
        manualCellOffsetsProp.ClearArray();

        for (int i = 0; i < cells.Count; i++)
        {
            manualCellOffsetsProp.InsertArrayElementAtIndex(i);
            manualCellOffsetsProp.GetArrayElementAtIndex(i).vector2IntValue = cells[i];
        }
    }
}