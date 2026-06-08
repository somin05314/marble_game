using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class HollowRectSpriteFrame : MonoBehaviour
{
    [Header("Placement Area (Base)")]
    [SerializeField] Vector2 placementCenter = Vector2.zero;
    [SerializeField] Vector2 placementInnerSize = new Vector2(10f, 6f);

    [Header("Sprite Frame Offset From Placement Area")]
    [SerializeField] Vector2 spriteCenterOffset = Vector2.zero;
    [SerializeField] Vector2 spriteInnerSizeOffset = Vector2.zero;
    [SerializeField] float borderWidth = 1f;

    [Header("Sprite")]
    [SerializeField] Sprite frameSprite;
    [SerializeField] Color color = Color.white;
    [SerializeField] Material material;
    [SerializeField] int sortingOrder = 0;
    [SerializeField] string sortingLayerName = "Default";

    [Header("Renderer Option")]
    [Tooltip("Tiled를 쓰면 반복, Sliced를 쓰면 9-slice, 아니면 Stretch처럼 scale만 적용")]
    [SerializeField] SpriteDrawMode drawMode = SpriteDrawMode.Sliced;

    [Header("Build")]
    [SerializeField] bool rebuildOnValidate = true;
    [SerializeField] bool hideChildInHierarchy = false;

    [Header("Placement Area")]
    [SerializeField] bool autoRegisterToGridPlacer = true;

    Transform _top;
    Transform _bottom;
    Transform _left;
    Transform _right;

    const string TOP_NAME = "Frame_Top";
    const string BOTTOM_NAME = "Frame_Bottom";
    const string LEFT_NAME = "Frame_Left";
    const string RIGHT_NAME = "Frame_Right";

    // =========================================================
    // Public Properties
    // =========================================================

    // 기존 외부 코드 호환용: 설치 가능 영역 기준
    public Vector2 InnerSize => placementInnerSize;
    public Vector2 Center => placementCenter;

    public Vector2 PlacementCenter => placementCenter;
    public Vector2 PlacementInnerSize => placementInnerSize;

    public Vector2 FrameCenter => placementCenter + spriteCenterOffset;

    public Vector2 FrameInnerSize
    {
        get
        {
            return new Vector2(
                Mathf.Max(0.01f, placementInnerSize.x + spriteInnerSizeOffset.x),
                Mathf.Max(0.01f, placementInnerSize.y + spriteInnerSizeOffset.y)
            );
        }
    }

    // =========================================================
    // Frame Build
    // =========================================================

    [ContextMenu("Rebuild Frame")]
    public void RebuildFrame()
    {
        if (frameSprite == null)
        {
            Debug.LogWarning("[HollowRectSpriteFrame] frameSprite가 비어있음", this);
            return;
        }

        if (placementInnerSize.x <= 0f || placementInnerSize.y <= 0f)
        {
            Debug.LogWarning("[HollowRectSpriteFrame] placementInnerSize는 0보다 커야 함", this);
            return;
        }

        if (borderWidth <= 0f)
        {
            Debug.LogWarning("[HollowRectSpriteFrame] borderWidth는 0보다 커야 함", this);
            return;
        }

        Vector2 frameCenter = FrameCenter;
        Vector2 frameInnerSize = FrameInnerSize;

        _top = GetOrCreateChild(TOP_NAME);
        _bottom = GetOrCreateChild(BOTTOM_NAME);
        _left = GetOrCreateChild(LEFT_NAME);
        _right = GetOrCreateChild(RIGHT_NAME);

        float innerW = frameInnerSize.x;
        float innerH = frameInnerSize.y;
        float bw = borderWidth;

        float outerW = innerW + bw * 2f;
        float outerH = innerH + bw * 2f;

        Vector2 topPos = frameCenter + new Vector2(0f, innerH * 0.5f + bw * 0.5f);
        Vector2 bottomPos = frameCenter + new Vector2(0f, -innerH * 0.5f - bw * 0.5f);
        Vector2 leftPos = frameCenter + new Vector2(-innerW * 0.5f - bw * 0.5f, 0f);
        Vector2 rightPos = frameCenter + new Vector2(innerW * 0.5f + bw * 0.5f, 0f);

        Vector2 topSize = new Vector2(outerW, bw);
        Vector2 bottomSize = new Vector2(outerW, bw);
        Vector2 leftSize = new Vector2(bw, innerH);
        Vector2 rightSize = new Vector2(bw, innerH);

        SetupPiece(_top, topPos, topSize);
        SetupPiece(_bottom, bottomPos, bottomSize);
        SetupPiece(_left, leftPos, leftSize);
        SetupPiece(_right, rightPos, rightSize);
    }

    [ContextMenu("Clear Frame")]
    public void ClearFrame()
    {
        TryDestroyChild(TOP_NAME);
        TryDestroyChild(BOTTOM_NAME);
        TryDestroyChild(LEFT_NAME);
        TryDestroyChild(RIGHT_NAME);

        _top = null;
        _bottom = null;
        _left = null;
        _right = null;
    }

    void Awake()
    {
        TryRegisterToGridPlacer();
    }

    void OnEnable()
    {
        TryRegisterToGridPlacer();
    }

    void OnValidate()
    {
        placementInnerSize.x = Mathf.Max(0.01f, placementInnerSize.x);
        placementInnerSize.y = Mathf.Max(0.01f, placementInnerSize.y);
        borderWidth = Mathf.Max(0.01f, borderWidth);

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            if (rebuildOnValidate)
                RebuildFrame();

            TryRegisterToGridPlacer();
        }
#endif
    }

    void TryRegisterToGridPlacer()
    {
        if (!autoRegisterToGridPlacer) return;

        var placer = FindFirstObjectByType<GridPlacer>(FindObjectsInactive.Include);
        if (placer == null) return;

        placer.SetPlacementFrame(this);
    }

    Transform GetOrCreateChild(string childName)
    {
        var child = transform.Find(childName);
        if (child != null) return child;

        GameObject go = new GameObject(childName);
        go.transform.SetParent(transform, false);

        if (hideChildInHierarchy)
            go.hideFlags = HideFlags.HideInHierarchy;

        return go.transform;
    }

    void SetupPiece(Transform t, Vector2 localPos, Vector2 size)
    {
        if (t == null) return;

        t.localPosition = new Vector3(localPos.x, localPos.y, 0f);
        t.localRotation = Quaternion.identity;
        t.localScale = Vector3.one;

        var sr = t.GetComponent<SpriteRenderer>();
        if (sr == null) sr = t.gameObject.AddComponent<SpriteRenderer>();

        sr.sprite = frameSprite;
        sr.color = color;
        sr.sortingOrder = sortingOrder;
        sr.sortingLayerName = sortingLayerName;

        if (material != null)
            sr.sharedMaterial = material;

        sr.drawMode = drawMode;

        if (drawMode == SpriteDrawMode.Simple)
            ApplySimpleScale(sr.transform, frameSprite, size);
        else
            sr.size = size;
    }

    void ApplySimpleScale(Transform target, Sprite sprite, Vector2 targetSize)
    {
        if (sprite == null) return;

        float ppu = sprite.pixelsPerUnit;
        Rect r = sprite.rect;

        float spriteWorldW = r.width / ppu;
        float spriteWorldH = r.height / ppu;

        if (spriteWorldW <= 0f || spriteWorldH <= 0f) return;

        target.localScale = new Vector3(
            targetSize.x / spriteWorldW,
            targetSize.y / spriteWorldH,
            1f
        );
    }

    void TryDestroyChild(string childName)
    {
        var child = transform.Find(childName);
        if (child == null) return;

#if UNITY_EDITOR
        if (!Application.isPlaying)
            DestroyImmediate(child.gameObject);
        else
            Destroy(child.gameObject);
#else
        Destroy(child.gameObject);
#endif
    }

    // =========================================================
    // Placement Check
    // =========================================================

    public Rect GetInnerRectLocal()
    {
        return new Rect(
            placementCenter.x - placementInnerSize.x * 0.5f,
            placementCenter.y - placementInnerSize.y * 0.5f,
            placementInnerSize.x,
            placementInnerSize.y
        );
    }

    public bool ContainsWorldPointInHole(Vector3 worldPoint, float margin = 0f)
    {
        Vector3 local = transform.InverseTransformPoint(worldPoint);
        Rect r = GetInnerRectLocal();

        r.xMin += margin;
        r.yMin += margin;
        r.xMax -= margin;
        r.yMax -= margin;

        return r.Contains(new Vector2(local.x, local.y));
    }

    public bool ContainsAllWorldPointsInHole(IReadOnlyList<Vector3> worldPoints, float margin = 0f)
    {
        if (worldPoints == null || worldPoints.Count == 0)
            return false;

        for (int i = 0; i < worldPoints.Count; i++)
        {
            if (!ContainsWorldPointInHole(worldPoints[i], margin))
                return false;
        }
        return true;
    }

    // =========================================================
    // Gizmos
    // =========================================================

    void OnDrawGizmosSelected()
    {
        Gizmos.matrix = transform.localToWorldMatrix;

        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(placementCenter, placementInnerSize);
    }
}