using UnityEngine;

[ExecuteAlways]
public class FrameInnerSizeBackgroundBinder : MonoBehaviour
{
    enum IntConvertMode
    {
        Floor,
        Ceil,
        Round,
        Truncate
    }

    [Header("Source")]
    [SerializeField] HollowRectSpriteFrame frame;

    [Header("Background Target")]
    [SerializeField] Transform backgroundTarget;

    [Header("Background Scale")]
    [SerializeField] bool syncScale = true;
    [SerializeField] bool matchXYOnly = true;
    [SerializeField] float zScale = 1f;

    [Header("Background Position")]
    [SerializeField] bool syncLocalPositionToCenter = false;

    [Header("Strip Pattern Fillers")]
    [SerializeField] StripPatternFiller[] stripPatternFillers;
    [SerializeField] bool syncStripPattern = true;

    [Tooltip("outerSize = innerSize + this value")]
    [SerializeField] Vector2Int outerSizePadding = new Vector2Int(10, 10);

    [Tooltip("origin을 frame.Center로 할지, backgroundTarget.localPosition으로 할지 선택")]
    [SerializeField] bool useBackgroundPositionForOrigin = true;

    [Tooltip("소수점 좌표를 Vector2Int origin으로 바꿀 때 어떤 방식으로 정수화할지")]
    [SerializeField] IntConvertMode originConvertMode = IntConvertMode.Round;

    [Tooltip("Apply Now 할 때 FillStrip까지 자동 실행")]
    [SerializeField] bool refillStripOnApply = true;

    [ContextMenu("Apply Now")]
    public void ApplyNow()
    {
        if (frame == null)
            return;

        SyncBackground();
        SyncStripPatterns();
    }

    void SyncBackground()
    {
        if (backgroundTarget == null)
            return;

        if (syncScale)
        {
            Vector2 size = frame.InnerSize;

            Vector3 scale = backgroundTarget.localScale;
            scale.x = size.x;
            scale.y = size.y;

            if (!matchXYOnly)
                scale.z = zScale;

            backgroundTarget.localScale = scale;
        }

        if (syncLocalPositionToCenter)
        {
            Vector2 center = frame.Center;
            Vector3 pos = backgroundTarget.localPosition;
            pos.x = center.x;
            pos.y = center.y;
            backgroundTarget.localPosition = pos;
        }
    }

    void SyncStripPatterns()
    {
        if (!syncStripPattern) return;
        if (stripPatternFillers == null || stripPatternFillers.Length == 0) return;

        Vector2 sourcePos;

        if (useBackgroundPositionForOrigin && backgroundTarget != null)
            sourcePos = backgroundTarget.localPosition;
        else
            sourcePos = frame.Center;

        Vector2Int newOrigin = ToVector2Int(sourcePos, originConvertMode);

        Vector2 inner = frame.InnerSize;
        Vector2Int innerAsInt = ToVector2Int(inner, IntConvertMode.Round);
        Vector2Int newOuterSize = innerAsInt + outerSizePadding;

        for (int i = 0; i < stripPatternFillers.Length; i++)
        {
            var filler = stripPatternFillers[i];
            if (filler == null) continue;

            filler.SetLayout(newOrigin, newOuterSize, refillStripOnApply);
        }
    }

    Vector2Int ToVector2Int(Vector2 v, IntConvertMode mode)
    {
        return new Vector2Int(
            ConvertFloat(v.x, mode),
            ConvertFloat(v.y, mode)
        );
    }

    int ConvertFloat(float value, IntConvertMode mode)
    {
        switch (mode)
        {
            case IntConvertMode.Floor:
                return Mathf.FloorToInt(value);
            case IntConvertMode.Ceil:
                return Mathf.CeilToInt(value);
            case IntConvertMode.Round:
                return Mathf.RoundToInt(value);
            case IntConvertMode.Truncate:
                return (int)value;
        }

        return Mathf.RoundToInt(value);
    }
}