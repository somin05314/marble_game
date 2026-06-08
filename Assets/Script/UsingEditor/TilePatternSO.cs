using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(menuName = "Tiles/Tile Pattern")]
public class TilePatternSO : ScriptableObject
{
    public Vector2Int size; // ¿¹: (3,5)

    [Tooltip("Row-major: (x=0..w-1, y=0..h-1) ¼ø¼­. ÃÑ w*h°³")]
    public TileBase[] tiles;

    public TileBase Get(int x, int y)
    {
        if (x < 0 || x >= size.x || y < 0 || y >= size.y)
            return null;

        int w = size.x;
        int index = y * w + x;

        if (tiles == null || index < 0 || index >= tiles.Length)
            return null;

        return tiles[index];
    }

    private void OnValidate()
    {
        int need = Mathf.Max(0, size.x * size.y);

        if (tiles == null)
        {
            tiles = new TileBase[need];
            return;
        }

        if (tiles.Length != need)
        {
            System.Array.Resize(ref tiles, need);
        }
    }
}