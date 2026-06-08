using UnityEngine;
using UnityEngine.Rendering;

[ExecuteAlways]
public class MoveHintShaderHardTest : MonoBehaviour
{
    [Header("Shader")]
    [SerializeField] Shader shader; // 비우면 "MoveHints/InstancedUnlit" 자동 Find
    [SerializeField] Color color = new Color(1, 1, 0, 1);

    [Header("Quad")]
    [SerializeField] float size = 6f;
    [Tooltip("카메라 near clip에서 얼마나 더 앞에 둘지")]
    [SerializeField] float forwardOffset = 0.5f;

    [Header("Target Camera (optional)")]
    [SerializeField] Camera targetCamera; // 비우면 현재 렌더 중인 Camera.current 사용

    Material _mat;
    Mesh _quad;

    bool UsingSRP => GraphicsSettings.currentRenderPipeline != null;

    void OnEnable()
    {
        Ensure();
        Camera.onPostRender += OnPostRenderBuiltIn;
        RenderPipelineManager.endCameraRendering += OnEndCameraRenderingSRP;
    }

    void OnDisable()
    {
        Camera.onPostRender -= OnPostRenderBuiltIn;
        RenderPipelineManager.endCameraRendering -= OnEndCameraRenderingSRP;
    }

    void Ensure()
    {
        if (_quad == null) _quad = CreateQuad();

        if (shader == null)
            shader = Shader.Find("MoveHints/InstancedUnlit");

        if (_mat == null && shader != null)
        {
            _mat = new Material(shader);
            _mat.name = "HardTest_Mat";
        }
    }

    void OnPostRenderBuiltIn(Camera cam)
    {
        if (UsingSRP) return;
        Draw(cam);
    }

    void OnEndCameraRenderingSRP(ScriptableRenderContext ctx, Camera cam)
    {
        if (!UsingSRP) return;
        Draw(cam);
    }

    void Draw(Camera cam)
    {
        Ensure();
        if (_mat == null || _quad == null) return;

        if (targetCamera != null && cam != targetCamera) return;
        if (cam == null) return;

        // ✅ 화면 정중앙(뷰포트 0.5,0.5)을 월드로 변환
        float dist = cam.nearClipPlane + forwardOffset;
        Vector3 center = cam.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, dist));

        // ✅ 색 강제 주입 (MaterialPropertyBlock 없이도 확실히)
        if (_mat.HasProperty("_Color")) _mat.SetColor("_Color", color);

        // ✅ 여기서 Pass가 유효하면 true, 아니면 false (=> 셰이더/패스 문제 확정)
        bool passOk = _mat.SetPass(0);
        if (!passOk)
        {
            Debug.LogError($"[HardTest] SetPass(0) FAILED. shader={(shader ? shader.name : "NULL")}  passCount={_mat.passCount}");
            return;
        }

        // ✅ 즉시 그리기(큐/정렬/렌더큐 영향 최소화)
        Matrix4x4 trs = Matrix4x4.TRS(center, Quaternion.identity, new Vector3(size, size, 1f));
        Graphics.DrawMeshNow(_quad, trs);

#if UNITY_EDITOR
        // 가끔 “정말 실행되나?” 확인용(스팸 방지로 1초에 1번)
        if (Time.frameCount % 60 == 0)
            Debug.Log($"[HardTest] Drew on cam={cam.name} UsingSRP={UsingSRP} center={center} near={cam.nearClipPlane} dist={dist} shader={shader?.name} passCount={_mat.passCount}");
#endif
    }

    static Mesh CreateQuad()
    {
        var m = new Mesh();
        m.name = "HardTestQuad";
        m.vertices = new[]
        {
            new Vector3(-0.5f,-0.5f,0),
            new Vector3( 0.5f,-0.5f,0),
            new Vector3( 0.5f, 0.5f,0),
            new Vector3(-0.5f, 0.5f,0),
        };
        m.uv = new[]
        {
            new Vector2(0,0),
            new Vector2(1,0),
            new Vector2(1,1),
            new Vector2(0,1),
        };
        m.triangles = new[] { 0, 2, 1, 0, 3, 2 };
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }
}
