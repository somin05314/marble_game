using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public class RailSpanVisual2D : MonoBehaviour
{
    [Header("Source")]
    [SerializeField] LineRenderer source;

    [Header("Clone Names")]
    [SerializeField] string cloneSameName = "RailLR_Copy";
    [SerializeField] string cloneOffsetNameA = "RailLR_OffsetA";
    [SerializeField] string cloneOffsetNameB = "RailLR_OffsetB";

    [Header("Child Materials (REQUIRED)")]
    [SerializeField] Material copyMaterial;
    [SerializeField] Material offsetMaterialA;
    [SerializeField] Material offsetMaterialB;

    [Header("Offset Settings")]
    [SerializeField] float offsetY = 0.3f;
    [SerializeField] float offsetWidthA = 0.2f;
    [SerializeField] float offsetWidthB = 0.2f;

    [Header("Layer / Sorting")]
    [SerializeField] string sortingLayerName = "Default";
    [SerializeField] bool setChildLayerIgnoreRaycast = true;

    [Header("Sorting Order (Manual)")]
    [SerializeField] int orderSource = 0;
    [SerializeField] int orderCopy = 1;
    [SerializeField] int orderOffsetA = 2;
    [SerializeField] int orderOffsetB = 3;

    [Header("Sync")]
    [Tooltip("기본은 끄는 것을 권장. 정말 필요할 때만 사용")]
    [SerializeField] bool autoSyncEveryFrame = false;

    [Tooltip("Awake/OnEnable/OnValidate에서 전체 동기화")]
    [SerializeField] bool syncOnEnable = true;

    [Tooltip("dirty 처리 시 실제 반영 타이밍. Update보다 LateUpdate 권장")]
    [SerializeField] bool applyDirtyInLateUpdate = true;

    bool _sortingInitialized;

    LineRenderer _copy;
    LineRenderer _offsetA;
    LineRenderer _offsetB;

    struct TintTarget
    {
        public Renderer renderer;
        public string colorProp;
    }

    MaterialPropertyBlock _mpb;
    TintTarget _copyTint;
    TintTarget _offsetATint;
    TintTarget _offsetBTint;

    Vector3[] _tmpPos;
    Vector3[] _offsetPosA;
    Vector3[] _offsetPosB;

    bool _geometryDirty;
    bool _styleDirty;
    bool _tintDirty;
    bool _childrenReady;

    Color _pendingTint = Color.white;

    static readonly AnimationCurve FlatWidthCurve = AnimationCurve.Linear(0f, 1f, 1f, 1f);

    public LineRenderer Source => source;

    void Reset()
    {
        source = GetComponent<LineRenderer>();
    }

    void Awake()
    {
        if (source == null) source = GetComponent<LineRenderer>();
        EnsureInitialized();

        if (syncOnEnable)
            MarkAllDirty();
    }

    void OnEnable()
    {
        if (source == null) source = GetComponent<LineRenderer>();
        EnsureInitialized();

        if (syncOnEnable)
            MarkAllDirty();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        orderCopy = Mathf.Max(0, orderCopy);
        orderOffsetA = Mathf.Max(0, orderOffsetA);
        orderOffsetB = Mathf.Max(0, orderOffsetB);

        if (!Application.isPlaying)
            return;

        EnsureInitialized();
        MarkAllDirty();
    }
#endif

    void Update()
    {
        if (!applyDirtyInLateUpdate)
            FlushDirty();

        if (autoSyncEveryFrame)
            MarkGeometryDirty();
    }

    void LateUpdate()
    {
        if (applyDirtyInLateUpdate)
            FlushDirty();

        if (autoSyncEveryFrame)
            MarkGeometryDirty();
    }

    public void MarkAllDirty()
    {
        _styleDirty = true;
        _geometryDirty = true;
        _tintDirty = true;
    }

    public void MarkGeometryDirty()
    {
        _geometryDirty = true;
    }

    public void MarkStyleDirty()
    {
        _styleDirty = true;
    }

    public void MarkTintDirty()
    {
        _tintDirty = true;
    }

    public void SyncNow()
    {
        EnsureInitialized();

        SyncRenderStyleOnlyInternal();
        SyncGeometryOnlyInternal();
        ApplyTintInternal(_pendingTint);

        _styleDirty = false;
        _geometryDirty = false;
        _tintDirty = false;
    }

    public void SyncGeometryOnly()
    {
        EnsureInitialized();
        SyncGeometryOnlyInternal();
        _geometryDirty = false;
    }

    public void SyncRenderStyleOnly()
    {
        if (source == null)
            return;

        EnsureInitialized();
        InitializeSortingOnce();

        ApplyRendererStyle(_copy, copyMaterial);
        ApplyRendererStyle(_offsetA, offsetMaterialA);

        var matB = offsetMaterialB != null ? offsetMaterialB : offsetMaterialA;
        ApplyRendererStyle(_offsetB, matB);

        RebuildTintBindings();
        _styleDirty = false;
    }

    public void ApplyTint(Color c)
    {
        _pendingTint = c;
        _tintDirty = true;
    }

    void FlushDirty()
    {
        if (!_childrenReady || source == null)
            return;

        if (_styleDirty)
        {
            SyncRenderStyleOnlyInternal();
            _styleDirty = false;
        }

        if (_geometryDirty)
        {
            SyncGeometryOnlyInternal();
            _geometryDirty = false;
        }

        if (_tintDirty)
        {
            ApplyTintInternal(_pendingTint);
            _tintDirty = false;
        }
    }

    void EnsureInitialized()
    {
        EnsureChildren();
        RebuildTintBindings();
        _childrenReady = true;
    }

    public void EnsureChildren()
    {
        _copy = EnsureChildLR(cloneSameName, ref _copy);
        _offsetA = EnsureChildLR(cloneOffsetNameA, ref _offsetA);
        _offsetB = EnsureChildLR(cloneOffsetNameB, ref _offsetB);
    }

    LineRenderer EnsureChildLR(string childName, ref LineRenderer cache)
    {
        Transform t = transform.Find(childName);
        if (t == null)
        {
            var go = new GameObject(childName);
            go.transform.SetParent(transform, false);
            t = go.transform;
        }

        if (setChildLayerIgnoreRaycast)
        {
            int layer = LayerMask.NameToLayer("Ignore Raycast");
            if (layer >= 0)
                t.gameObject.layer = layer;
        }

        var lr = t.GetComponent<LineRenderer>();
        if (lr == null)
            lr = t.gameObject.AddComponent<LineRenderer>();

        cache = lr;
        return lr;
    }

    void SyncRenderStyleOnlyInternal()
    {
        if (source == null)
            return;

        InitializeSortingOnce();

        ApplyRendererStyle(_copy, copyMaterial);
        ApplyRendererStyle(_offsetA, offsetMaterialA);

        var matB = offsetMaterialB != null ? offsetMaterialB : offsetMaterialA;
        ApplyRendererStyle(_offsetB, matB);

        RebuildTintBindings();
    }

    void SyncGeometryOnlyInternal()
    {
        if (source == null)
            return;

        int n = source.positionCount;
        if (n <= 0)
            return;

        EnsureBuffers(n);

        source.GetPositions(_tmpPos);
        BuildOffsetPositions(n);

        SyncCopyGeometry(n);
        SyncOffsetGeometryA(n);
        SyncOffsetGeometryB(n);
    }

    void ApplyRendererStyle(LineRenderer lr, Material mat)
    {
        if (lr == null || source == null)
            return;

        CopyCommonSettingsExceptMaterial(source, lr);

        if (mat != null)
            lr.sharedMaterial = mat;
    }
    void EnsureBuffers(int n)
    {
        if (_tmpPos == null || _tmpPos.Length < n) _tmpPos = new Vector3[n];
        if (_offsetPosA == null || _offsetPosA.Length < n) _offsetPosA = new Vector3[n];
        if (_offsetPosB == null || _offsetPosB.Length < n) _offsetPosB = new Vector3[n];
    }

    void BuildOffsetPositions(int n)
    {
        for (int i = 0; i < n; i++)
        {
            Vector3 dir;

            if (n == 1)
            {
                dir = Vector3.right;
            }
            else if (i == 0)
            {
                dir = _tmpPos[i + 1] - _tmpPos[i];
            }
            else if (i == n - 1)
            {
                dir = _tmpPos[i] - _tmpPos[i - 1];
            }
            else
            {
                dir = _tmpPos[i + 1] - _tmpPos[i - 1];
            }

            float mag = dir.magnitude;
            if (mag < 1e-6f)
                dir = Vector3.right;
            else
                dir /= mag;

            Vector3 normal = new Vector3(-dir.y, dir.x, 0f);

            _offsetPosA[i] = _tmpPos[i] + normal * offsetY;
            _offsetPosB[i] = _tmpPos[i] - normal * offsetY;
        }
    }

    void SyncCopyGeometry(int n)
    {
        if (_copy == null) return;

        _copy.positionCount = n;
        _copy.SetPositions(_tmpPos);

        _copy.widthCurve = source.widthCurve;
        _copy.widthMultiplier = source.widthMultiplier;
        _copy.startWidth = source.startWidth;
        _copy.endWidth = source.endWidth;
    }

    void SyncOffsetGeometryA(int n)
    {
        if (_offsetA == null) return;

        _offsetA.positionCount = n;
        _offsetA.SetPositions(_offsetPosA);

        _offsetA.widthMultiplier = 1f;
        _offsetA.widthCurve = FlatWidthCurve;
        _offsetA.startWidth = offsetWidthA;
        _offsetA.endWidth = offsetWidthA;
    }

    void SyncOffsetGeometryB(int n)
    {
        if (_offsetB == null) return;

        _offsetB.positionCount = n;
        _offsetB.SetPositions(_offsetPosB);

        _offsetB.widthMultiplier = 1f;
        _offsetB.widthCurve = FlatWidthCurve;
        _offsetB.startWidth = offsetWidthB;
        _offsetB.endWidth = offsetWidthB;
    }

    void CopyCommonSettingsExceptMaterial(LineRenderer src, LineRenderer dst)
    {
        dst.useWorldSpace = src.useWorldSpace;
        dst.alignment = src.alignment;
        dst.textureMode = src.textureMode;
        dst.numCornerVertices = src.numCornerVertices;
        dst.numCapVertices = src.numCapVertices;

        dst.shadowBias = src.shadowBias;
        dst.maskInteraction = src.maskInteraction;

        dst.receiveShadows = src.receiveShadows;
        dst.generateLightingData = src.generateLightingData;

        dst.lightProbeUsage = src.lightProbeUsage;
        dst.reflectionProbeUsage = src.reflectionProbeUsage;
        dst.probeAnchor = src.probeAnchor;

        dst.motionVectorGenerationMode = src.motionVectorGenerationMode;

        dst.colorGradient = src.colorGradient;
        dst.startColor = src.startColor;
        dst.endColor = src.endColor;
    }

    void RebuildTintBindings()
    {
        if (_mpb == null)
            _mpb = new MaterialPropertyBlock();

        _copyTint = BuildTintTarget(_copy);
        _offsetATint = BuildTintTarget(_offsetA);
        _offsetBTint = BuildTintTarget(_offsetB);
    }

    TintTarget BuildTintTarget(LineRenderer lr)
    {
        TintTarget target = default;

        if (lr == null)
            return target;

        var r = lr.GetComponent<Renderer>();
        if (r == null)
            return target;

        target.renderer = r;
        target.colorProp = ResolveColorProperty(lr.sharedMaterial);
        return target;
    }

    string ResolveColorProperty(Material mat)
    {
        if (mat == null)
            return null;

        if (mat.HasProperty("_BaseColor")) return "_BaseColor";
        if (mat.HasProperty("_Color")) return "_Color";
        if (mat.HasProperty("_TintColor")) return "_TintColor";
        return null;
    }

    void ApplyTintInternal(Color c)
    {
        if (_copy != null) { _copy.startColor = c; _copy.endColor = c; }
        if (_offsetA != null) { _offsetA.startColor = c; _offsetA.endColor = c; }
        if (_offsetB != null) { _offsetB.startColor = c; _offsetB.endColor = c; }

        ApplyMpbTo(_copyTint, c);
        ApplyMpbTo(_offsetATint, c);
        ApplyMpbTo(_offsetBTint, c);
    }

    void ApplyMpbTo(TintTarget target, Color c)
    {
        if (target.renderer == null || string.IsNullOrEmpty(target.colorProp))
            return;

        target.renderer.GetPropertyBlock(_mpb);
        _mpb.SetColor(target.colorProp, c);
        target.renderer.SetPropertyBlock(_mpb);
    }

    [ContextMenu("Mark All Dirty")]
    void ContextMarkAllDirty()
    {
        MarkAllDirty();
    }

    [ContextMenu("Sync Now")]
    void ContextSyncNow()
    {
        SyncNow();
    }

    [ContextMenu("Sync Geometry Only")]
    void ContextSyncGeometryOnly()
    {
        SyncGeometryOnly();
    }

    [ContextMenu("Sync Render Style Only")]
    void ContextSyncRenderStyleOnly()
    {
        SyncRenderStyleOnly();
    }

    void InitializeSortingOnce()
    {
        if (_sortingInitialized || source == null)
            return;

        string baseLayer = source.sortingLayerName;
        int baseOrder = source.sortingOrder;

        ApplyRendererSorting(_copy, baseLayer, baseOrder + orderCopy);
        ApplyRendererSorting(_offsetA, baseLayer, baseOrder + orderOffsetA);
        ApplyRendererSorting(_offsetB, baseLayer, baseOrder + orderOffsetB);

        _sortingInitialized = true;
    }

    void ApplyRendererSorting(LineRenderer lr, string layerName, int order)
    {
        if (lr == null)
            return;

        lr.sortingLayerName = layerName;
        lr.sortingOrder = order;
    }

    public void CopySettingsFrom(RailSpanVisual2D other, LineRenderer newSource, string overrideSortingLayer = null, int sortingOrderOffset = 0)
    {
        if (other == null || newSource == null)
            return;

        source = newSource;

        copyMaterial = other.copyMaterial;
        offsetMaterialA = other.offsetMaterialA;
        offsetMaterialB = other.offsetMaterialB;

        offsetY = other.offsetY;
        offsetWidthA = other.offsetWidthA;
        offsetWidthB = other.offsetWidthB;

        sortingLayerName = string.IsNullOrEmpty(overrideSortingLayer)
            ? other.sortingLayerName
            : overrideSortingLayer;

        setChildLayerIgnoreRaycast = other.setChildLayerIgnoreRaycast;

        orderSource = other.orderSource + sortingOrderOffset;
        orderCopy = other.orderCopy + sortingOrderOffset;
        orderOffsetA = other.orderOffsetA + sortingOrderOffset;
        orderOffsetB = other.orderOffsetB + sortingOrderOffset;

        autoSyncEveryFrame = false;
        syncOnEnable = true;
        applyDirtyInLateUpdate = true;

        _sortingInitialized = false;

        EnsureInitialized();
        MarkAllDirty();
    }


}