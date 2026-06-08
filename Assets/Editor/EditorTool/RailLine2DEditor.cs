using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(RailLine2D))]
public class RailLine2DEditor : Editor
{
    RailLine2D rail;

    void OnEnable()
    {
        rail = (RailLine2D)target;
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        GUILayout.Space(8);

        EditorGUILayout.LabelField("Rail Debug", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Current Length", rail.GetCurrentLength().ToString("F2"));

        GUILayout.Space(4);

        if (GUILayout.Button("Clamp Length (Start Fixed)"))
        {
            Undo.RecordObject(rail, "Clamp Rail Length");
            rail.ClampLengthFromStartFixed();
            rail.RefreshVisual();
            EditorUtility.SetDirty(rail);
        }

        if (GUILayout.Button("Clamp Length (End Fixed)"))
        {
            Undo.RecordObject(rail, "Clamp Rail Length");
            rail.ClampLengthFromEndFixed();
            rail.RefreshVisual();
            EditorUtility.SetDirty(rail);
        }

        if (GUILayout.Button("Refresh Visual"))
        {
            rail.RefreshVisual();
            EditorUtility.SetDirty(rail);
        }
    }

    void OnSceneGUI()
    {
        if (rail == null) return;

        DrawEndpointHandle(
            rail.StartWorld,
            Color.green,
            isStartHandle: true
        );

        DrawEndpointHandle(
            rail.EndWorld,
            new Color(1f, 0.45f, 0.2f, 1f),
            isStartHandle: false
        );

        DrawLengthLabel();
    }

    void DrawEndpointHandle(Vector3 currentWorld, Color color, bool isStartHandle)
    {
        Handles.color = color;

        float handleSize = HandleUtility.GetHandleSize(currentWorld) * 0.12f;

        EditorGUI.BeginChangeCheck();
        Vector3 moved = Handles.FreeMoveHandle(
            currentWorld,
            handleSize,
            Vector3.zero,
            Handles.CircleHandleCap
        );

        if (EditorGUI.EndChangeCheck())
        {
            Vector2Int snappedGrid = rail.WorldToGrid(moved);
            bool shift = Event.current != null && Event.current.shift;

            bool changed = false;

            if (shift)
            {
                Vector2Int originGrid = isStartHandle ? rail.StartGrid : rail.EndGrid;
                Vector2Int deltaGrid = snappedGrid - originGrid;

                if (deltaGrid != Vector2Int.zero)
                {
                    Undo.RecordObject(rail, "Translate Rail");
                    rail.TranslateBy(deltaGrid);
                    changed = true;
                }
            }
            else
            {
                if (isStartHandle)
                {
                    if (rail.StartGrid != snappedGrid && rail.IsValidLengthBetween(snappedGrid, rail.EndGrid))
                    {
                        Undo.RecordObject(rail, "Move Rail Start");
                        changed = rail.TrySetStartKeepEnd(snappedGrid);
                    }
                }
                else
                {
                    if (rail.EndGrid != snappedGrid && rail.IsValidLengthBetween(rail.StartGrid, snappedGrid))
                    {
                        Undo.RecordObject(rail, "Move Rail End");
                        changed = rail.TrySetEndKeepStart(snappedGrid);
                    }
                }
            }

            if (changed)
            {
                rail.RefreshVisual();
                EditorUtility.SetDirty(rail);
            }
        }

        Vector3 snappedWorld = isStartHandle ? rail.StartWorld : rail.EndWorld;

        Handles.DrawSolidDisc(snappedWorld, Vector3.forward, handleSize * 0.85f);

        GUIStyle style = new GUIStyle(EditorStyles.boldLabel);
        style.normal.textColor = color;

        Vector2Int grid = isStartHandle ? rail.StartGrid : rail.EndGrid;

        string label = isStartHandle ? $"Start {grid}" : $"End {grid}";
        if (Event.current != null && Event.current.shift)
            label += "  [Shift: Move Rail]";

        Handles.Label(
            snappedWorld + Vector3.up * (handleSize * 2.2f),
            label,
            style
        );
    }

    void DrawLengthLabel()
    {
        Vector3 mid = (rail.StartWorld + rail.EndWorld) * 0.5f;
        float len = rail.GetCurrentLength();

        GUIStyle style = new GUIStyle(EditorStyles.helpBox);
        style.alignment = TextAnchor.MiddleCenter;
        style.fontStyle = FontStyle.Bold;

        Handles.Label(mid + Vector3.up * 0.25f, $"Length: {len:F2}", style);
    }


}