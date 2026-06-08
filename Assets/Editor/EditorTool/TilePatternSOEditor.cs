using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

[CustomEditor(typeof(TilePatternSO))]
public class TilePatternSOEditor : Editor
{
    private Tilemap sourceTilemap;
    private Vector3Int startCell;
    private Vector2Int captureSize = new Vector2Int(3, 3);

    public override void OnInspectorGUI()
    {
        TilePatternSO pattern = (TilePatternSO)target;

        serializedObject.Update();

        DrawDefaultInspector();

        EditorGUILayout.Space(12);
        EditorGUILayout.LabelField("Tilemap에서 패턴 가져오기", EditorStyles.boldLabel);

        sourceTilemap = (Tilemap)EditorGUILayout.ObjectField(
            "Source Tilemap",
            sourceTilemap,
            typeof(Tilemap),
            true
        );

        startCell = EditorGUILayout.Vector3IntField("Start Cell", startCell);
        captureSize = EditorGUILayout.Vector2IntField("Capture Size", captureSize);

        EditorGUILayout.HelpBox(
            "Start Cell을 좌상단 시작점으로 사용해서, 왼쪽 위 → 오른쪽 아래 순서로 타일을 읽어옵니다.",
            MessageType.Info
        );

        GUI.enabled = sourceTilemap != null && captureSize.x > 0 && captureSize.y > 0;

        if (GUILayout.Button("타일맵에서 가져오기"))
        {
            ImportFromTilemap(pattern);
        }

        GUI.enabled = true;

        serializedObject.ApplyModifiedProperties();
    }

    private void ImportFromTilemap(TilePatternSO pattern)
    {
        if (sourceTilemap == null)
        {
            Debug.LogWarning("Source Tilemap이 없습니다.");
            return;
        }

        int w = Mathf.Max(1, captureSize.x);
        int h = Mathf.Max(1, captureSize.y);

        Undo.RecordObject(pattern, "Import Tile Pattern");

        pattern.size = new Vector2Int(w, h);
        pattern.tiles = new TileBase[w * h];

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                // startCell을 "좌상단"으로 해석
                Vector3Int cell = new Vector3Int(
                    startCell.x + x,
                    startCell.y - y,
                    startCell.z
                );

                TileBase tile = sourceTilemap.GetTile(cell);

                int index = y * w + x;
                pattern.tiles[index] = tile;
            }
        }

        EditorUtility.SetDirty(pattern);
        AssetDatabase.SaveAssets();

        Debug.Log($"{pattern.name}: Tilemap에서 {w}x{h} 패턴을 가져왔습니다. (좌상단 기준)");
    }
}