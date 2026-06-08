using UnityEngine;

public static class ChapterContext
{
    public static int ChapterIndex { get; private set; } = 1; // ±âº» 1

    public static void Set(int chapterIndex)
    {
        ChapterIndex = Mathf.Max(1, chapterIndex);
    }
}