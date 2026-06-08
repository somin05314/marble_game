using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class MoveHintOverlay2D : MonoBehaviour
{
    [Header("Render (Instanced)")]
    [SerializeField] Material dotMaterial;        // URP면 MoveHints/InstancedUnlit_URP 권장
    [SerializeField] Camera renderCamera;         // 비우면 Game 카메라 전체(멀티 카메라면 여러번 그림)
    [SerializeField] Color dotColor = Color.yellow;
    [SerializeField, Min(0.0001f)] float dotSize = 0.10f;

    [Header("Depth (HardTest 방식)")]
    [Tooltip("near clip에서 얼마나 더 앞에 둘지 (0.1~1 추천)")]
    [SerializeField] float forwardOffset = 0.5f;

    [Header("Limits")]
    [SerializeField, Min(1)] int maxDots = 800;

    [Header("Perf")]
    [Tooltip("positions/size/z가 안 바뀌면 매 프레임 매트릭스 재생성하지 않음")]
    [SerializeField] bool cacheMatrices = true;

    [Tooltip("dotMaterial이 에셋이면 runtime 인스턴스로 복제해서(renderQueue 등) 에셋을 건드리지 않음")]
    [SerializeField] bool useRuntimeMaterialInstance = true;

    [Header("Debug")]
    [SerializeField] bool drawInSceneViewInEditor = false;

    Vector2[] _positions = new Vector2[0];
    int _count;

    // DrawMeshInstanced는 한 번에 1023개 제한
    static readonly int MaxBatch = 1023;
    readonly Matrix4x4[] _batchMatrices = new Matrix4x4[MaxBatch];

    // ✅ 캐시 (대부분 maxDots<=1023이면 여기만 사용)
    Matrix4x4[] _cachedMatrices = new Matrix4x4[0];
    int _cachedCount;
    int _dataVersion;          // ShowDots/HideAll 될 때 증가
    int _cachedVersion = -1;   // 마지막으로 매트릭스 빌드한 버전
    float _cachedSize = -1f;
    float _cachedZ = float.NaN;
    int _cachedCamId = 0;

    Mesh _quad;
    MaterialPropertyBlock _mpb;

    static readonly int ColorId = Shader.PropertyToID("_Color");

    bool UsingSRP => GraphicsSettings.currentRenderPipeline != null;

    Material _runtimeMat;
    bool _configuredMat = false;

    Color _cachedColor;
    bool _mpbDirty = true;

    void Awake()
    {
        EnsureInit();
    }

    void OnEnable()
    {
        EnsureInit();

        if (UsingSRP)
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRenderingSRP;
        else
            Camera.onPreRender += OnPreRenderBuiltIn;
    }

    void OnDisable()
    {
        if (UsingSRP)
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRenderingSRP;
        else
            Camera.onPreRender -= OnPreRenderBuiltIn;
    }

    void OnDestroy()
    {
        if (_runtimeMat != null)
        {
            Destroy(_runtimeMat);
            _runtimeMat = null;
        }
    }

    void EnsureInit()
    {
        if (_quad == null) _quad = CreateQuad();
        if (_mpb == null) _mpb = new MaterialPropertyBlock();

        // 자동 머티리얼 생성
        if (dotMaterial == null)
        {
            Shader s = Shader.Find("MoveHints/InstancedUnlit_URP");
            if (s == null) s = Shader.Find("MoveHints/InstancedUnlit"); // Built-in fallback
            if (s != null)
                dotMaterial = new Material(s) { name = "MoveHintOverlay_Instanced_AutoMat" };
        }

        // 에셋 머티리얼을 직접 건드리지 않게 런타임 인스턴스 사용(권장)
        if (useRuntimeMaterialInstance && dotMaterial != null && _runtimeMat == null)
        {
            _runtimeMat = new Material(dotMaterial)
            {
                name = dotMaterial.name + "_RuntimeInstance"
            };
            _configuredMat = false;
        }

        var mat = GetActiveMaterial();
        if (mat != null && !_configuredMat)
        {
            mat.enableInstancing = true;
            mat.renderQueue = 4500; // overlay 위로
            _configuredMat = true;
        }
    }

    Material GetActiveMaterial()
    {
        if (useRuntimeMaterialInstance)
            return _runtimeMat != null ? _runtimeMat : dotMaterial;
        return dotMaterial;
    }

    public void HideAll()
    {
        _count = 0;
        _dataVersion++;
        _cachedVersion = -1;
    }

    public void ShowDots(IReadOnlyList<Vector2> positions)
    {
        if (positions == null || positions.Count == 0)
        {
            HideAll();
            return;
        }

        int n = Mathf.Min(positions.Count, maxDots);
        EnsureCapacity(n);

        for (int i = 0; i < n; i++)
            _positions[i] = positions[i];

        _count = n;

        // ✅ “점 목록이 바뀜” 표시 → 다음 Draw에서 한 번만 매트릭스 재빌드
        _dataVersion++;
    }

    void EnsureCapacity(int n)
    {
        if (_positions.Length < n)
            _positions = new Vector2[Mathf.NextPowerOfTwo(n)];

        if (_cachedMatrices.Length < n)
            _cachedMatrices = new Matrix4x4[Mathf.NextPowerOfTwo(n)];
    }

    bool ShouldDrawFor(Camera cam)
    {
        if (cam == null) return false;

        if (renderCamera != null)
            return cam == renderCamera;

#if UNITY_EDITOR
        if (drawInSceneViewInEditor && cam.cameraType == CameraType.SceneView)
            return true;
#endif
        return cam.cameraType == CameraType.Game;
    }

    // Built-in 훅
    void OnPreRenderBuiltIn(Camera cam) => DrawForCamera(cam);

    // URP(SRP) 훅
    void OnBeginCameraRenderingSRP(ScriptableRenderContext ctx, Camera cam) => DrawForCamera(cam);

    void DrawForCamera(Camera cam)
    {
        if (_count <= 0) return;
        if (!ShouldDrawFor(cam)) return;

        EnsureInit();

        var mat = GetActiveMaterial();
        if (mat == null || _quad == null || _mpb == null) return;
        if (!SystemInfo.supportsInstancing) return;

        // ✅ 카메라 앞쪽 z 확보(2D/Ortho에서도 안정적)
        float dist = cam.nearClipPlane + forwardOffset;
        float z = cam.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, dist)).z;

        // MPB는 색이 바뀔 때만 갱신 (매 프레임 Clear 불필요)
        if (_mpbDirty || _cachedColor != dotColor)
        {
            _cachedColor = dotColor;
            _mpb.SetColor(ColorId, dotColor);
            _mpbDirty = false;
        }

        // ✅ 가장 큰 최적화: positions/size/z/camera가 그대로면 “매트릭스 재생성 없음”
        if (cacheMatrices && _count <= MaxBatch)
        {
            int camId = cam.GetInstanceID();

            bool needRebuild =
                _cachedVersion != _dataVersion ||
                _cachedCount != _count ||
                !Mathf.Approximately(_cachedSize, dotSize) ||
                !Mathf.Approximately(_cachedZ, z) ||
                _cachedCamId != camId;

            if (needRebuild)
            {
                RebuildCachedMatrices(z);
                _cachedVersion = _dataVersion;
                _cachedCount = _count;
                _cachedSize = dotSize;
                _cachedZ = z;
                _cachedCamId = camId;
            }

            Graphics.DrawMeshInstanced(
                _quad, 0, mat, _cachedMatrices, _count,
                _mpb,
                ShadowCastingMode.Off,
                false,
                0,
                cam,
                LightProbeUsage.Off,
                null
            );

            return;
        }

        // fallback: 1023 초과 or 캐시 OFF면 기존처럼 배치 생성/드로우
        DrawBatchedNoCache(cam, mat, z);
    }

    void RebuildCachedMatrices(float z)
    {
        float s = Mathf.Max(0.0001f, dotSize);
        Vector3 scale = new Vector3(s, s, 1f);

        for (int i = 0; i < _count; i++)
        {
            Vector2 p = _positions[i];
            _cachedMatrices[i] = Matrix4x4.TRS(new Vector3(p.x, p.y, z), Quaternion.identity, scale);
        }
    }

    void DrawBatchedNoCache(Camera cam, Material mat, float z)
    {
        float s = Mathf.Max(0.0001f, dotSize);
        Vector3 scale = new Vector3(s, s, 1f);

        int remaining = _count;
        int offset = 0;

        while (remaining > 0)
        {
            int batch = Mathf.Min(MaxBatch, remaining);

            for (int i = 0; i < batch; i++)
            {
                Vector2 p = _positions[offset + i];
                _batchMatrices[i] = Matrix4x4.TRS(new Vector3(p.x, p.y, z), Quaternion.identity, scale);
            }

            Graphics.DrawMeshInstanced(
                _quad, 0, mat, _batchMatrices, batch,
                _mpb,
                ShadowCastingMode.Off,
                false,
                0,
                cam,
                LightProbeUsage.Off,
                null
            );

            offset += batch;
            remaining -= batch;
        }
    }

    static Mesh CreateQuad()
    {
        var m = new Mesh();
        m.name = "MoveHintQuad";
        m.vertices = new[]
        {
            new Vector3(-0.5f, -0.5f, 0f),
            new Vector3( 0.5f, -0.5f, 0f),
            new Vector3( 0.5f,  0.5f, 0f),
            new Vector3(-0.5f,  0.5f, 0f),
        };
        m.uv = new[]
        {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0f, 1f),
        };
        m.triangles = new[] { 0, 2, 1, 0, 3, 2 };
        m.RecalculateBounds();
        return m;
    }
}
