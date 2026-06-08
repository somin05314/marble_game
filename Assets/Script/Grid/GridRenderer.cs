using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GridRenderer : MonoBehaviour
{
    public GridManager grid;
    public int range = 50;

    [Header("Visual Offset")]
    [SerializeField] Vector2 visualOffsetCells = new Vector2(0.5f, 0.5f);

    [Header("Visibility")]
    [Tooltip("Play 모드에서는 그리드(가이드 라인)를 숨깁니다.")]
    public bool hideInPlayMode = true;

    [Tooltip("사용자 옵션으로 그리드를 강제 표시/숨김합니다. (hideInPlayMode와 별개)")]
    public bool userVisible = true;

    [Tooltip("비워두면 GameModeManager.Instance 또는 씬에서 자동 탐색")]
    public GameModeManager gameMode;

    [Header("Target Camera")]
    public Camera targetCam;

    [Header("Placement Area Source (Auto Find)")]
    [SerializeField] bool limitToPlacementArea = true;
    [SerializeField] HollowRectSpriteFrame placementFrame;

    [Header("Line Style (PX)")]
    public Color normalColor = new Color(1f, 1f, 1f, 0.15f);
    public Color majorColor = new Color(1f, 1f, 1f, 0.40f);

    [Tooltip("화면에서 보이는 선 굵기 (픽셀)")]
    public float normalWidthPx = 1.0f;

    [Tooltip("화면에서 보이는 선 굵기 (픽셀)")]
    public float majorWidthPx = 2.0f;

    [Tooltip("10칸마다 Major 라인")]
    public int majorStep = 10;

    [Header("Sorting")]
    public string sortingLayerName = "Default";
    public int normalOrder = 0;
    public int majorOrder = 10;

    [Header("Perf")]
    public bool updateWidthEveryFrame = true;

    Material _mat;
    readonly List<LineRenderer> _normalLines = new();
    readonly List<LineRenderer> _majorLines = new();

    float _lastOrtho = -1f;
    int _lastPixelH = -1;
    float _lastAspect = -1f;

    bool _lastPlay;

    void Awake()
    {
        if (targetCam == null) targetCam = Camera.main;
        if (grid == null) grid = FindFirstObjectByType<GridManager>();
        if (gameMode == null) gameMode = GameModeManager.Instance ?? FindFirstObjectByType<GameModeManager>();
        ResolveRefs();
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene s, LoadSceneMode m)
    {
        ResolveRefs();
        DrawGrid();
        UpdateLineWidths(force: true);
    }

    void ResolveRefs()
    {
        if (grid == null)
            grid = FindFirstObjectByType<GridManager>();

        if (targetCam == null)
            targetCam = Camera.main;

        if (targetCam == null)
            targetCam = FindFirstObjectByType<Camera>();

        if (placementFrame == null)
            placementFrame = FindFirstObjectByType<HollowRectSpriteFrame>(FindObjectsInactive.Include);
    }

    void Start()
    {
        EnsureMaterial();
        DrawGrid();
        UpdateLineWidths(force: true);
        _lastPlay = IsPlayMode();
        ApplyVisibility(_lastPlay);
    }

    void LateUpdate()
    {
        if (hideInPlayMode)
        {
            if (gameMode == null) gameMode = GameModeManager.Instance ?? FindFirstObjectByType<GameModeManager>();
            bool isPlay = IsPlayMode();
            if (isPlay != _lastPlay)
            {
                _lastPlay = isPlay;
                ApplyVisibility(isPlay);
            }

            if (isPlay) return;
        }

        if (!updateWidthEveryFrame) return;

        if (targetCam == null) ResolveRefs();

        UpdateLineWidths(force: false);
    }

    bool IsPlayMode()
    {
        if (gameMode == null) return false;
        return gameMode.currentMode == GameMode.Play;
    }

    void ApplyVisibility(bool isPlay)
    {
        bool show = userVisible && !(hideInPlayMode && isPlay);

        for (int i = 0; i < transform.childCount; i++)
        {
            var ch = transform.GetChild(i);
            if (ch != null && ch.gameObject.activeSelf != show)
                ch.gameObject.SetActive(show);
        }
    }

    public void SetUserVisible(bool visible)
    {
        userVisible = visible;

        bool isPlay = IsPlayMode();
        _lastPlay = isPlay;
        ApplyVisibility(isPlay);
    }

    void EnsureMaterial()
    {
        if (_mat != null) return;
        _mat = new Material(Shader.Find("Sprites/Default"));
        _mat.hideFlags = HideFlags.DontSave;
    }

    void DrawGrid()
    {
        EnsureMaterial();

        for (int i = transform.childCount - 1; i >= 0; i--)
            Destroy(transform.GetChild(i).gameObject);

        _normalLines.Clear();
        _majorLines.Clear();

        if (grid == null)
            return;

        if (limitToPlacementArea)
        {
            ResolveRefs();

            if (placementFrame != null)
            {
                DrawGridInsidePlacementFrame();
                return;
            }
        }

        DrawGridByRangeFallback();
    }

    void DrawGridByRangeFallback()
    {
        for (int x = -range; x <= range; x++)
        {
            bool isMajor = (majorStep > 0) && (x % majorStep == 0);

            var lr = CreateLine(
                grid.origin + new Vector2(x * grid.cellSize, -range * grid.cellSize),
                grid.origin + new Vector2(x * grid.cellSize, range * grid.cellSize),
                isMajor
            );

            (isMajor ? _majorLines : _normalLines).Add(lr);
        }

        for (int y = -range; y <= range; y++)
        {
            bool isMajor = (majorStep > 0) && (y % majorStep == 0);

            var lr = CreateLine(
                grid.origin + new Vector2(-range * grid.cellSize, y * grid.cellSize),
                grid.origin + new Vector2(range * grid.cellSize, y * grid.cellSize),
                isMajor
            );

            (isMajor ? _majorLines : _normalLines).Add(lr);
        }
    }

    void DrawGridInsidePlacementFrame()
    {
        Rect localRect = placementFrame.GetInnerRectLocal();

        Vector3 worldBL3 = placementFrame.transform.TransformPoint(new Vector3(localRect.xMin, localRect.yMin, 0f));
        Vector3 worldTR3 = placementFrame.transform.TransformPoint(new Vector3(localRect.xMax, localRect.yMax, 0f));

        float xMin = Mathf.Min(worldBL3.x, worldTR3.x);
        float xMax = Mathf.Max(worldBL3.x, worldTR3.x);
        float yMin = Mathf.Min(worldBL3.y, worldTR3.y);
        float yMax = Mathf.Max(worldBL3.y, worldTR3.y);

        float cell = grid.cellSize;
        Vector2 origin = grid.origin;

        int startX = Mathf.CeilToInt((xMin - origin.x) / cell);
        int endX = Mathf.FloorToInt((xMax - origin.x) / cell);

        int startY = Mathf.CeilToInt((yMin - origin.y) / cell);
        int endY = Mathf.FloorToInt((yMax - origin.y) / cell);

        for (int x = startX; x <= endX; x++)
        {
            bool isMajor = (majorStep > 0) && (x % majorStep == 0);
            float wx = origin.x + x * cell;

            var lr = CreateLine(
                new Vector2(wx, yMin),
                new Vector2(wx, yMax),
                isMajor
            );

            (isMajor ? _majorLines : _normalLines).Add(lr);
        }

        for (int y = startY; y <= endY; y++)
        {
            bool isMajor = (majorStep > 0) && (y % majorStep == 0);
            float wy = origin.y + y * cell;

            var lr = CreateLine(
                new Vector2(xMin, wy),
                new Vector2(xMax, wy),
                isMajor
            );

            (isMajor ? _majorLines : _normalLines).Add(lr);
        }
    }

    LineRenderer CreateLine(Vector2 start, Vector2 end, bool isMajor)
    {
        var lineObj = new GameObject(isMajor ? "GridLine_Major" : "GridLine");
        lineObj.transform.SetParent(transform, worldPositionStays: true);

        var lr = lineObj.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.positionCount = 2;

        Vector2 visualOffset = visualOffsetCells * grid.cellSize;

        lr.SetPosition(0, start + visualOffset);
        lr.SetPosition(1, end + visualOffset);

        lr.sharedMaterial = _mat;

        Color c = isMajor ? majorColor : normalColor;
        lr.startColor = c;
        lr.endColor = c;

        lr.sortingLayerName = sortingLayerName;
        lr.sortingOrder = isMajor ? majorOrder : normalOrder;

        lr.numCapVertices = 6;
        lr.numCornerVertices = 0;
        lr.alignment = LineAlignment.TransformZ;

        return lr;
    }

    float PixelsToWorld(float px)
    {
        if (targetCam == null) targetCam = Camera.main;
        if (targetCam == null) return 0.02f;

        float worldPerPixel = (targetCam.orthographicSize * 2f) / Mathf.Max(1, targetCam.pixelHeight);
        return px * worldPerPixel;
    }

    void UpdateLineWidths(bool force)
    {
        if (targetCam == null) return;

        if (!force)
        {
            if (Mathf.Approximately(_lastOrtho, targetCam.orthographicSize) &&
                _lastPixelH == targetCam.pixelHeight &&
                Mathf.Approximately(_lastAspect, targetCam.aspect))
                return;
        }

        _lastOrtho = targetCam.orthographicSize;
        _lastPixelH = targetCam.pixelHeight;
        _lastAspect = targetCam.aspect;

        float wNormal = PixelsToWorld(normalWidthPx);
        float wMajor = PixelsToWorld(majorWidthPx);

        for (int i = 0; i < _normalLines.Count; i++)
        {
            var lr = _normalLines[i];
            if (lr == null) continue;
            lr.startWidth = wNormal;
            lr.endWidth = wNormal;
        }

        for (int i = 0; i < _majorLines.Count; i++)
        {
            var lr = _majorLines[i];
            if (lr == null) continue;
            lr.startWidth = wMajor;
            lr.endWidth = wMajor;
        }
    }

    void OnDestroy()
    {
        if (_mat != null)
        {
            Destroy(_mat);
            _mat = null;
        }
    }
}