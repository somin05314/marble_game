using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(LineRenderer))]
public class RailLine2D : MonoBehaviour
{
    [Header("Grid")]
    [SerializeField] float gridSize = 1f;
    [SerializeField] Vector2 gridOrigin = Vector2.zero;

    [Header("Endpoints (Grid Integer)")]
    [SerializeField] Vector2Int startGrid = new Vector2Int(0, 0);
    [SerializeField] Vector2Int endGrid = new Vector2Int(4, 0);

    [Header("Length Limit (World Length)")]
    [SerializeField] float minLength = 3f;
    [SerializeField] float maxLength = 12f;

    [Header("LineRenderer")]
    [SerializeField] LineRenderer lineRenderer;
    [SerializeField] bool autoAssignLineRenderer = true;

    [Header("Editor / Debug")]
    [SerializeField] bool drawGizmos = true;
    [SerializeField] Color gizmoLineColor = new Color(0.2f, 0.9f, 1f, 1f);
    [SerializeField] Color gizmoStartColor = new Color(0.2f, 1f, 0.3f, 1f);
    [SerializeField] Color gizmoEndColor = new Color(1f, 0.4f, 0.2f, 1f);

    public float GridSize => gridSize;
    public Vector2 GridOrigin => gridOrigin;


    [Header("RailSpan Output")]
    [SerializeField] RailSpan2D railSpanPrefab;
    [SerializeField] GridManager grid;
    [SerializeField] bool createRailSpanOnStart = true;
    [SerializeField] bool hideMarkerLineOnPlay = true;

    RailSpan2D spawnedRailSpan;

    void Start()
    {
        if (!Application.isPlaying) return;
        if (!createRailSpanOnStart) return;

        CreateRailSpan();
    }

    public RailSpan2D CreateRailSpan()
    {
        if (railSpanPrefab == null)
        {
            Debug.LogWarning("[RailLine2D] railSpanPrefab is missing.", this);
            return null;
        }

        if (spawnedRailSpan != null)
            return spawnedRailSpan;

        spawnedRailSpan = Instantiate(railSpanPrefab, transform.parent);

        // 엔딩 씬에서는 grid가 없어도 됨
        spawnedRailSpan.Initialize(grid, StartWorld, EndWorld);
        spawnedRailSpan.SetSelected(false);
        spawnedRailSpan.SetEditModeVisible(false);

        if (hideMarkerLineOnPlay && lineRenderer != null)
            lineRenderer.enabled = false;

        return spawnedRailSpan;
    }

    public Vector2Int StartGrid
    {
        get => startGrid;
        set
        {
            startGrid = value;
            ClampLengthFromStartFixed();
            RefreshVisual();
        }
    }

    public Vector2Int EndGrid
    {
        get => endGrid;
        set
        {
            endGrid = value;
            ClampLengthFromStartFixed();
            RefreshVisual();
        }
    }

    public float MinLength => minLength;
    public float MaxLength => maxLength;

    public Vector3 StartWorld => GridToWorld(startGrid);
    public Vector3 EndWorld => GridToWorld(endGrid);

    void Reset()
    {
        TryAssignLineRenderer();
        RefreshVisual();
    }

    void Awake()
    {
        TryAssignLineRenderer();
        RefreshVisual();
    }

    void OnEnable()
    {
        TryAssignLineRenderer();
        RefreshVisual();
    }

    void OnValidate()
    {
        if (gridSize <= 0f)
            gridSize = 1f;

        if (minLength < 0f)
            minLength = 0f;

        if (maxLength < minLength)
            maxLength = minLength;

        TryAssignLineRenderer();
        ClampLengthFromStartFixed();
        RefreshVisual();
    }

    public void SetEndpoints(Vector2Int newStartGrid, Vector2Int newEndGrid)
    {
        startGrid = newStartGrid;
        endGrid = newEndGrid;
        ClampLengthFromStartFixed();
        RefreshVisual();
    }

    public void SetStartKeepEnd(Vector2Int newStartGrid)
    {
        startGrid = newStartGrid;
        ClampLengthFromEndFixed();
        RefreshVisual();
    }

    public void SetEndKeepStart(Vector2Int newEndGrid)
    {
        endGrid = newEndGrid;
        ClampLengthFromStartFixed();
        RefreshVisual();
    }

    public Vector2Int WorldToGrid(Vector3 world)
    {
        Vector2 local = new Vector2(world.x, world.y) - gridOrigin;
        return new Vector2Int(
            Mathf.RoundToInt(local.x / gridSize),
            Mathf.RoundToInt(local.y / gridSize)
        );
    }

    public Vector3 GridToWorld(Vector2Int grid)
    {
        return new Vector3(
            gridOrigin.x + grid.x * gridSize,
            gridOrigin.y + grid.y * gridSize,
            transform.position.z
        );
    }

    public float GetCurrentLength()
    {
        return Vector2.Distance(StartWorld, EndWorld);
    }

    public void RefreshVisual()
    {
        if (lineRenderer == null)
            TryAssignLineRenderer();

        if (lineRenderer == null)
            return;

        lineRenderer.positionCount = 2;
        lineRenderer.useWorldSpace = true;
        lineRenderer.SetPosition(0, StartWorld);
        lineRenderer.SetPosition(1, EndWorld);
    }

    public void ClampLengthFromStartFixed()
    {
        Vector2 start = StartWorld;
        Vector2 end = EndWorld;

        Vector2 delta = end - start;
        float len = delta.magnitude;

        if (len <= 0.0001f)
        {
            // 방향이 없으면 기본적으로 오른쪽 방향으로 최소 길이 확보 시도
            float fallbackLen = Mathf.Max(minLength, gridSize);
            Vector2 fallbackEnd = start + Vector2.right * fallbackLen;
            endGrid = WorldToGrid(fallbackEnd);
            EnsureNotSamePointFromStart();
            return;
        }

        float clampedLen = Mathf.Clamp(len, minLength, maxLength);
        if (Mathf.Approximately(clampedLen, len))
            return;

        Vector2 dir = delta.normalized;
        Vector2 adjustedEnd = start + dir * clampedLen;
        endGrid = WorldToGrid(adjustedEnd);

        // 정수 스냅 후 길이가 다시 틀어질 수 있으니 한 번 더 미세 보정
        endGrid = FindBestGridPointAroundDirection(startGrid, dir, clampedLen, preferFartherWhenBelowMin: clampedLen <= minLength + 0.0001f);
        EnsureNotSamePointFromStart();
    }

    public void ClampLengthFromEndFixed()
    {
        Vector2 start = StartWorld;
        Vector2 end = EndWorld;

        Vector2 delta = start - end;
        float len = delta.magnitude;

        if (len <= 0.0001f)
        {
            float fallbackLen = Mathf.Max(minLength, gridSize);
            Vector2 fallbackStart = end + Vector2.left * fallbackLen;
            startGrid = WorldToGrid(fallbackStart);
            EnsureNotSamePointFromEnd();
            return;
        }

        float clampedLen = Mathf.Clamp(len, minLength, maxLength);
        if (Mathf.Approximately(clampedLen, len))
            return;

        Vector2 dir = delta.normalized;
        Vector2 adjustedStart = end + dir * clampedLen;
        startGrid = WorldToGrid(adjustedStart);

        startGrid = FindBestGridPointAroundDirectionFromEnd(endGrid, dir, clampedLen, preferFartherWhenBelowMin: clampedLen <= minLength + 0.0001f);
        EnsureNotSamePointFromEnd();
    }

    void EnsureNotSamePointFromStart()
    {
        if (startGrid != endGrid) return;

        endGrid = startGrid + Vector2Int.right * Mathf.Max(1, Mathf.RoundToInt(minLength / gridSize));
        if (startGrid == endGrid)
            endGrid = startGrid + Vector2Int.right;
    }

    void EnsureNotSamePointFromEnd()
    {
        if (startGrid != endGrid) return;

        startGrid = endGrid + Vector2Int.left * Mathf.Max(1, Mathf.RoundToInt(minLength / gridSize));
        if (startGrid == endGrid)
            startGrid = endGrid + Vector2Int.left;
    }

    Vector2Int FindBestGridPointAroundDirection(Vector2Int fixedStartGrid, Vector2 desiredDir, float targetLength, bool preferFartherWhenBelowMin)
    {
        Vector2 start = GridToWorld(fixedStartGrid);
        float searchRadiusCells = Mathf.Ceil((maxLength / gridSize) + 2f);

        Vector2Int best = endGrid;
        float bestScore = float.MaxValue;

        for (int y = -Mathf.CeilToInt(searchRadiusCells); y <= Mathf.CeilToInt(searchRadiusCells); y++)
        {
            for (int x = -Mathf.CeilToInt(searchRadiusCells); x <= Mathf.CeilToInt(searchRadiusCells); x++)
            {
                Vector2Int candidate = fixedStartGrid + new Vector2Int(x, y);
                if (candidate == fixedStartGrid) continue;

                Vector2 candidateWorld = GridToWorld(candidate);
                Vector2 v = candidateWorld - start;
                float len = v.magnitude;
                if (len <= 0.0001f) continue;

                Vector2 dir = v / len;

                float dirPenalty = 1f - Mathf.Clamp01(Vector2.Dot(desiredDir.normalized, dir));
                float lenPenalty = Mathf.Abs(len - targetLength);

                float validityPenalty = 0f;
                if (len < minLength)
                    validityPenalty += preferFartherWhenBelowMin ? 1000f : 100f;
                if (len > maxLength)
                    validityPenalty += 1000f;

                float score = validityPenalty + lenPenalty + dirPenalty * 0.35f;

                if (score < bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }
        }

        return best;
    }

    Vector2Int FindBestGridPointAroundDirectionFromEnd(Vector2Int fixedEndGrid, Vector2 desiredDir, float targetLength, bool preferFartherWhenBelowMin)
    {
        Vector2 end = GridToWorld(fixedEndGrid);
        float searchRadiusCells = Mathf.Ceil((maxLength / gridSize) + 2f);

        Vector2Int best = startGrid;
        float bestScore = float.MaxValue;

        for (int y = -Mathf.CeilToInt(searchRadiusCells); y <= Mathf.CeilToInt(searchRadiusCells); y++)
        {
            for (int x = -Mathf.CeilToInt(searchRadiusCells); x <= Mathf.CeilToInt(searchRadiusCells); x++)
            {
                Vector2Int candidate = fixedEndGrid + new Vector2Int(x, y);
                if (candidate == fixedEndGrid) continue;

                Vector2 candidateWorld = GridToWorld(candidate);
                Vector2 v = candidateWorld - end;
                float len = v.magnitude;
                if (len <= 0.0001f) continue;

                Vector2 dir = v / len;

                float dirPenalty = 1f - Mathf.Clamp01(Vector2.Dot(desiredDir.normalized, dir));
                float lenPenalty = Mathf.Abs(len - targetLength);

                float validityPenalty = 0f;
                if (len < minLength)
                    validityPenalty += preferFartherWhenBelowMin ? 1000f : 100f;
                if (len > maxLength)
                    validityPenalty += 1000f;

                float score = validityPenalty + lenPenalty + dirPenalty * 0.35f;

                if (score < bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }
        }

        return best;
    }

    void TryAssignLineRenderer()
    {
        if (!autoAssignLineRenderer && lineRenderer == null)
            return;

        if (lineRenderer == null)
            lineRenderer = GetComponent<LineRenderer>();
    }

    public bool IsValidLengthBetween(Vector2Int aGrid, Vector2Int bGrid)
    {
        Vector2 a = GridToWorld(aGrid);
        Vector2 b = GridToWorld(bGrid);
        float len = Vector2.Distance(a, b);
        return len >= minLength && len <= maxLength;
    }

    public bool TrySetStartKeepEnd(Vector2Int newStartGrid)
    {
        if (!IsValidLengthBetween(newStartGrid, endGrid))
            return false;

        startGrid = newStartGrid;
        RefreshVisual();
        return true;
    }

    public bool TrySetEndKeepStart(Vector2Int newEndGrid)
    {
        if (!IsValidLengthBetween(startGrid, newEndGrid))
            return false;

        endGrid = newEndGrid;
        RefreshVisual();
        return true;
    }

    public void TranslateBy(Vector2Int deltaGrid)
    {
        if (deltaGrid == Vector2Int.zero) return;

        startGrid += deltaGrid;
        endGrid += deltaGrid;
        RefreshVisual();
    }

    public void SetBoth(Vector2Int newStartGrid, Vector2Int newEndGrid)
    {
        startGrid = newStartGrid;
        endGrid = newEndGrid;
        RefreshVisual();
    }

    void OnDrawGizmos()
    {
        if (!drawGizmos) return;

        Vector3 a = StartWorld;
        Vector3 b = EndWorld;

        Gizmos.color = gizmoLineColor;
        Gizmos.DrawLine(a, b);

        Gizmos.color = gizmoStartColor;
        Gizmos.DrawSphere(a, gridSize * 0.12f);

        Gizmos.color = gizmoEndColor;
        Gizmos.DrawSphere(b, gridSize * 0.12f);
    }
}