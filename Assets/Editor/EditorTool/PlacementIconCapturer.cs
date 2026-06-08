#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

public static class PlacementIconCapturer
{
    const int DEFAULT_SIZE = 2048;
    const int PADDING_PX = 24;
    const string OUTPUT_DIR = "Assets/PaletteIcons";

    // ✅ 이 레이어에 있는 SpriteRenderer는 캡쳐에서 제외됨
    const string EXCLUDE_LAYER_NAME = "IgnoreIcon";
    const string CAPTURE_LAYER_NAME = "IconCapture";

    [MenuItem("Tools/Palette Icon/Capture Selected PlacementData")]
    static void CaptureSelectedPlacementData()
    {
        var pd = Selection.activeObject as PlacementData;
        if (pd == null)
        {
            Debug.LogWarning("[IconCapture] PlacementData를 선택하고 실행해줘.");
            return;
        }
        if (pd.prefab == null)
        {
            Debug.LogWarning($"[IconCapture] {pd.name} 의 prefab이 비어있어.");
            return;
        }

        CapturePrefabToPng(pd.prefab, pd.id, DEFAULT_SIZE, DEFAULT_SIZE, PADDING_PX);
    }

    static void CapturePrefabToPng(GameObject prefab, string id, int width, int height, int paddingPx)
    {
        if (string.IsNullOrEmpty(id)) id = prefab.name;

        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        go.hideFlags = HideFlags.HideAndDontSave;

        var srsAll = go.GetComponentsInChildren<SpriteRenderer>(true);
        if (srsAll == null || srsAll.Length == 0)
        {
            Object.DestroyImmediate(go);
            Debug.LogWarning("[IconCapture] SpriteRenderer가 없어서 캡쳐 불가");
            return;
        }

        // ✅ 제외 레이어 처리: IgnoreIcon 레이어의 SR은 잠깐 꺼둠
        int excludeLayer = LayerMask.NameToLayer(EXCLUDE_LAYER_NAME);
        if (excludeLayer == -1)
        {
            Debug.LogWarning($"[IconCapture] '{EXCLUDE_LAYER_NAME}' 레이어가 없음. (없으면 제외 기능이 동작하지 않음)");
        }

        for (int i = 0; i < srsAll.Length; i++)
        {
            var sr = srsAll[i];
            if (sr == null) continue;

            if (excludeLayer != -1 && sr.gameObject.layer == excludeLayer)
                sr.enabled = false; // ✅ 캡쳐에서 제외
        }

        // ✅ enabled=true인 렌더러만으로 Bounds 계산
        if (!TryComputeBoundsFromEnabled(srsAll, out Bounds b))
        {
            Object.DestroyImmediate(go);
            Debug.LogWarning("[IconCapture] 캡쳐할 SpriteRenderer가 없음 (전부 제외/비활성)");
            return;
        }

        // 렌더용 카메라 생성
        var camGO = new GameObject("__IconCam");
        camGO.hideFlags = HideFlags.HideAndDontSave;
        var cam = camGO.AddComponent<Camera>();
        cam.orthographic = true;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0, 0, 0, 0);
        cam.nearClipPlane = -10f;
        cam.farClipPlane = 10f;

        // ✅ 캡쳐용 레이어로 옮기기 (포함된 렌더러만)
        int captureLayer = EnsureLayer(CAPTURE_LAYER_NAME);
        cam.cullingMask = 1 << captureLayer;

        // enabled=true인 SR만 캡쳐 레이어로 변경 (복구용 원래 레이어 저장)
        int[] originalLayers = new int[srsAll.Length];
        for (int i = 0; i < srsAll.Length; i++)
        {
            var sr = srsAll[i];
            if (sr == null) continue;

            originalLayers[i] = sr.gameObject.layer;

            if (sr.enabled) // ✅ 제외된 애들은 레이어 변경 안 함
                sr.gameObject.layer = captureLayer;
        }

        // 카메라 위치/사이즈 맞추기
        Vector3 center = b.center;
        cam.transform.position = new Vector3(center.x, center.y, -5f);

        float aspect = (float)width / height;
        float halfW = b.extents.x;
        float halfH = b.extents.y;

        float ortho = Mathf.Max(halfH, halfW / aspect);

        // paddingPx를 월드로 환산해서 orthoSize에 반영
        float worldPerPixel = (ortho * 2f) / height;
        ortho += paddingPx * worldPerPixel;

        cam.orthographicSize = Mathf.Max(0.01f, ortho);

        // RenderTexture 생성 & 렌더
        var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
        rt.antiAliasing = 4;
        cam.targetTexture = rt;

        var prev = RenderTexture.active;
        RenderTexture.active = rt;

        cam.Render();

        // Texture2D로 읽기
        var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        tex.Apply(false);

        // 저장
        if (!Directory.Exists(OUTPUT_DIR))
            Directory.CreateDirectory(OUTPUT_DIR);

        string safeName = MakeSafeFileName(id);
        string path = $"{OUTPUT_DIR}/{safeName}.png";
        File.WriteAllBytes(path, tex.EncodeToPNG());
        AssetDatabase.ImportAsset(path);

        // Import settings
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;

            importer.spritePixelsPerUnit = 256;

            // 부드러운 스타일
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;

            importer.SaveAndReimport();
        }

        Debug.Log($"[IconCapture] Saved: {path}");

        // 정리(복구)
        RenderTexture.active = prev;
        cam.targetTexture = null;

        Object.DestroyImmediate(tex);
        rt.Release();
        Object.DestroyImmediate(rt);

        // 레이어 복구
        for (int i = 0; i < srsAll.Length; i++)
        {
            var sr = srsAll[i];
            if (sr == null) continue;

            sr.gameObject.layer = originalLayers[i];
            sr.enabled = true; // ✅ 캡쳐 끝났으니 다시 켬(임시 인스턴스라 사실상 의미 없지만 안전)
        }

        Object.DestroyImmediate(camGO);
        Object.DestroyImmediate(go);
    }

    static bool TryComputeBoundsFromEnabled(SpriteRenderer[] srs, out Bounds bounds)
    {
        bounds = default;
        bool hasAny = false;

        for (int i = 0; i < srs.Length; i++)
        {
            var sr = srs[i];
            if (sr == null || !sr.enabled) continue;

            if (!hasAny) { bounds = sr.bounds; hasAny = true; }
            else bounds.Encapsulate(sr.bounds);
        }

        return hasAny;
    }

    static int EnsureLayer(string name)
    {
        int layer = LayerMask.NameToLayer(name);
        if (layer == -1)
        {
            layer = 0;
            Debug.LogWarning($"[IconCapture] '{name}' 레이어가 없음. Project Settings > Tags and Layers에 레이어를 추가하면 더 안전해.");
        }
        return layer;
    }

    static string MakeSafeFileName(string s)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
            s = s.Replace(c, '_');
        return s;
    }
}
#endif
