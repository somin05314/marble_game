using UnityEditor;
using UnityEngine;

/// <summary>
/// 선택 오브젝트를 지정 스텝(기본 0.5)으로 강제 스냅.
/// - Position(월드) 스냅
/// - LocalScale 스냅
/// - (옵션) BoxCollider2D/PolygonCollider2D offset 스냅
/// - (옵션) SpriteRenderer (Tiled/Sliced) size 스냅
/// </summary>
public static class SnapToGridTools
{
    const string STEP_PREF_KEY = "SnapToGridTools.Step";
    const float DEFAULT_STEP = 1f;

    static float Step
    {
        get => Mathf.Max(0.0001f, EditorPrefs.GetFloat(STEP_PREF_KEY, DEFAULT_STEP));
        set => EditorPrefs.SetFloat(STEP_PREF_KEY, Mathf.Max(0.0001f, value));
    }

    // =========================
    // Menu
    // =========================

    [MenuItem("Tools/Snap/Grid Step/Set Step = 0.5")]
    static void SetStep05() => Step = 1.0f;

    [MenuItem("Tools/Snap/Grid Step/Set Step = 1.0")]
    static void SetStep10() => Step = 1.0f;

    [MenuItem("Tools/Snap/Grid Step/Set Step = 0.25")]
    static void SetStep025() => Step = 0.25f;

    // Ctrl + Shift + G : Position 스냅
    [MenuItem("Tools/Snap/Snap Position (World) %&g")] // Ctrl + Alt + G
    static void SnapPositionWorld()
    {
        float step = Step;

        foreach (var t in Selection.transforms)
        {
            Undo.RecordObject(t, "Snap Position (World)");

            var p = t.position;
            p.x = RoundTo(p.x, step);
            p.y = RoundTo(p.y, step);
            // z는 그대로
            t.position = p;
        }
    }

    [MenuItem("Tools/Snap/Snap Local Scale %&h")]     // Ctrl + Alt + H
    static void SnapLocalScale()
    {
        float step = Step;

        foreach (var t in Selection.transforms)
        {
            Undo.RecordObject(t, "Snap Local Scale");

            var s = t.localScale;
            s.x = RoundTo(s.x, step);
            s.y = RoundTo(s.y, step);
            s.z = RoundTo(s.z, step);
            t.localScale = s;
        }
    }

    [MenuItem("Tools/Snap/Snap All (Pos+Scale+2D) %&j")] // Ctrl + Alt + J
    static void SnapAll()
    {
        float step = Step;

        foreach (var t in Selection.transforms)
        {
            Undo.RecordObject(t, "Snap All (Pos+Scale+2D)");

            // 1) Position (World)
            var p = t.position;
            p.x = RoundTo(p.x, step);
            p.y = RoundTo(p.y, step);
            t.position = p;

            // 2) LocalScale
            var s = t.localScale;
            s.x = RoundTo(s.x, step);
            s.y = RoundTo(s.y, step);
            s.z = RoundTo(s.z, step);
            t.localScale = s;

            // 3) BoxCollider2D offset/size (옵션)
            var box = t.GetComponent<BoxCollider2D>();
            if (box != null)
            {
                Undo.RecordObject(box, "Snap BoxCollider2D");
                var off = box.offset;
                off.x = RoundTo(off.x, step);
                off.y = RoundTo(off.y, step);
                box.offset = off;

                var size = box.size;
                size.x = RoundTo(size.x, step);
                size.y = RoundTo(size.y, step);
                box.size = size;
            }

            // 4) PolygonCollider2D offset (옵션)
            var poly = t.GetComponent<PolygonCollider2D>();
            if (poly != null)
            {
                Undo.RecordObject(poly, "Snap PolygonCollider2D");
                var off = poly.offset;
                off.x = RoundTo(off.x, step);
                off.y = RoundTo(off.y, step);
                poly.offset = off;
            }

            // 5) SpriteRenderer size (Tiled/Sliced) (옵션)
            var sr = t.GetComponent<SpriteRenderer>();
            if (sr != null && (sr.drawMode == SpriteDrawMode.Sliced || sr.drawMode == SpriteDrawMode.Tiled))
            {
                Undo.RecordObject(sr, "Snap SpriteRenderer Size");
                var size = sr.size;
                size.x = RoundTo(size.x, step);
                size.y = RoundTo(size.y, step);
                sr.size = size;
            }
        }
    }

    static float RoundTo(float v, float step)
    {
        // -0.0000 같은 표시 방지
        float r = Mathf.Round(v / step) * step;
        if (Mathf.Abs(r) < 1e-6f) r = 0f;
        return r;
    }
}
