using UnityEngine;
using UnityEngine.Tilemaps;

public class StripPatternFiller : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] Tilemap tilemap;

    [Header("Outer Rect (cells)")]
    [SerializeField] Vector2Int origin = Vector2Int.zero;              // 중심 좌표
    [SerializeField] Vector2Int outerSize = new Vector2Int(100, 40);   // 바깥 사각형 크기

    [Header("Ring")]
    [SerializeField, Min(1)] int ringThickness = 3; // 링 두께(=코너 비울 크기)

    [Header("Patterns")]
    [SerializeField] TilePatternSO[] patterns;

    [Header("Density / Gaps")]
    [Range(0f, 1f)] public float placeChance = 0.35f;
    [SerializeField] Vector2Int gapAfterPlace = new Vector2Int(0, 3);
    [SerializeField] Vector2Int gapWhenSkip = new Vector2Int(1, 4);

    [Header("Random")]
    public bool useSeed = true;
    public int seed = 12345;

    System.Random rng;

    [Header("Gizmos")]
    [SerializeField] bool drawGizmos = true;
    [SerializeField] bool drawOuter = true;
    [SerializeField] bool drawInner = true;
    [SerializeField] bool drawForbiddenCorners = true;

    IntRect _outerRect;
    IntRect _innerRect;
    bool _hasInnerRect;

    public Vector2Int Origin
    {
        get => origin;
        set => origin = value;
    }

    public Vector2Int OuterSize
    {
        get => outerSize;
        set => outerSize = value;
    }

    public void SetLayout(Vector2Int newOrigin, Vector2Int newOuterSize, bool refill = false)
    {
        origin = newOrigin;
        outerSize = newOuterSize;

        if (refill)
            FillStrip();
    }

    // ✅ 4코너 비움 영역(각각 ringThickness x ringThickness)
    IntRect[] _forbidden;

    struct IntRect
    {
        public int xMin, yMin, xMax, yMax; // [min, max)
        public IntRect(int x, int y, int w, int h)
        {
            xMin = x; yMin = y;
            xMax = x + w; yMax = y + h;
        }

        public int Width => xMax - xMin;
        public int Height => yMax - yMin;

        public bool Contains(int x, int y)
            => x >= xMin && x < xMax && y >= yMin && y < yMax;

        public static IntRect Shrink(IntRect r, int t)
        {
            return new IntRect(r.xMin + t, r.yMin + t, r.Width - 2 * t, r.Height - 2 * t);
        }
    }

    [ContextMenu("Fill Strip")]
    public void FillStrip()
    {
        if (tilemap == null) tilemap = GetComponent<Tilemap>();
        if (tilemap == null) { Debug.LogError("Tilemap이 필요함"); return; }
        if (patterns == null || patterns.Length == 0) { Debug.LogError("patterns가 비어있음"); return; }

        rng = useSeed ? new System.Random(seed) : new System.Random();

        tilemap.ClearAllTiles();
        tilemap.CompressBounds();

        // origin = 중심
        int left = origin.x - outerSize.x / 2;
        int bottom = origin.y - outerSize.y / 2;
        var outer = new IntRect(left, bottom, outerSize.x, outerSize.y);

        bool hasInner = outerSize.x > ringThickness * 2 && outerSize.y > ringThickness * 2;
        if (!hasInner)
        {
            Debug.LogWarning("outerSize가 너무 작아서 ringThickness를 만들 수 없음");
            tilemap.RefreshAllTiles();
            return;
        }

        var inner = IntRect.Shrink(outer, ringThickness);

        _outerRect = outer;
        _hasInnerRect = hasInner;
        if (hasInner) _innerRect = inner;

        // ✅ 4코너(겹치는 영역) 비우기: ringThickness x ringThickness
        // 좌표계: outer는 [xMin, xMax), inner는 outer를 thickness만큼 줄인 것
        _forbidden = new[]
        {
            // Top-Left
            new IntRect(outer.xMin, inner.yMax, ringThickness, ringThickness),
            // Top-Right
            new IntRect(inner.xMax, inner.yMax, ringThickness, ringThickness),
            // Bottom-Left
            new IntRect(outer.xMin, outer.yMin, ringThickness, ringThickness),
            // Bottom-Right
            new IntRect(inner.xMax, outer.yMin, ringThickness, ringThickness),
        };

        // ✅ 4변을 따로 채운다 (회전 고정)
        FillEdge_Right(outer, inner);   // r=0
        FillEdge_Top(outer, inner);     // r=1
        FillEdge_Left(outer, inner);    // r=2
        FillEdge_Bottom(outer, inner);  // r=3

        tilemap.RefreshAllTiles();
    }

    // ---------------------------
    // Edges (4 sides)
    // ---------------------------

    void FillEdge_Right(IntRect outer, IntRect inner)
    {
        int r = 0;                 // Right=정방향
        int startX = inner.xMax;   // = outer.xMax - ringThickness
        FillVerticalStrip(startX, outer.yMin, outer.yMax, r, outer, inner);
    }

    void FillEdge_Left(IntRect outer, IntRect inner)
    {
        int r = 2;                 // Left=180
        int startX = outer.xMin;   // 왼쪽 띠 시작
        FillVerticalStrip(startX, outer.yMin, outer.yMax, r, outer, inner);
    }

    void FillEdge_Top(IntRect outer, IntRect inner)
    {
        int r = 1;                 // Top
        int startY = inner.yMax;   // = outer.yMax - ringThickness
        FillHorizontalStrip(outer.xMin, outer.xMax, startY, r, outer, inner);
    }

    void FillEdge_Bottom(IntRect outer, IntRect inner)
    {
        int r = 3;                 // Bottom
        int startY = outer.yMin;   // 바닥 띠 시작
        FillHorizontalStrip(outer.xMin, outer.xMax, startY, r, outer, inner);
    }

    // ---------------------------
    // Strip Fillers
    // ---------------------------

    void FillVerticalStrip(int fixedX, int yMin, int yMax, int r, IntRect outer, IntRect inner)
    {
        int y = yMin;

        while (y < yMax)
        {
            if (rng.NextDouble() > placeChance)
            {
                y += RandRange(gapWhenSkip.x, gapWhenSkip.y);
                continue;
            }

            var p = patterns[rng.Next(patterns.Length)];
            Vector2Int rs = RotatedSize(p.size.x, p.size.y, r);

            int anchorX = fixedX;
            int anchorY = y;

            if (TryStampPatternRing(p, r, anchorX, anchorY, outer, inner))
                y += rs.y + RandRange(gapAfterPlace.x, gapAfterPlace.y);
            else
                y += 1;
        }
    }

    void FillHorizontalStrip(int xMin, int xMax, int fixedY, int r, IntRect outer, IntRect inner)
    {
        int x = xMin;

        while (x < xMax)
        {
            if (rng.NextDouble() > placeChance)
            {
                x += RandRange(gapWhenSkip.x, gapWhenSkip.y);
                continue;
            }

            var p = patterns[rng.Next(patterns.Length)];
            Vector2Int rs = RotatedSize(p.size.x, p.size.y, r);

            int anchorX = x;
            int anchorY = fixedY;

            if (TryStampPatternRing(p, r, anchorX, anchorY, outer, inner))
                x += rs.x + RandRange(gapAfterPlace.x, gapAfterPlace.y);
            else
                x += 1;
        }
    }

    // ---------------------------
    // Placement (Ring only + forbid corners)
    // ---------------------------

    bool TryStampPatternRing(TilePatternSO p, int r, int anchorX, int anchorY, IntRect outer, IntRect inner)
    {
        int w = p.size.x;
        int h = p.size.y;
        Vector2Int rs = RotatedSize(w, h, r);

        // bounding이 outer 밖이면 컷
        if (!outer.Contains(anchorX, anchorY)) return false;
        if (!outer.Contains(anchorX + rs.x - 1, anchorY + rs.y - 1)) return false;

        // 1) 검사: outer 안 && inner 밖(=링) && forbidden 코너 밖 && 겹침 금지
        for (int py = 0; py < h; py++)
        {
            for (int px = 0; px < w; px++)
            {
                // ✅ TilePatternSO가 y가 뒤집혀 저장되어 있으니 Get에서 뒤집어서 읽기
                var t = p.Get(px, (h - 1) - py);
                if (t == null) continue;

                Vector2Int rc = RotateCell(px, py, w, h, r);
                int x = anchorX + rc.x;
                int y = anchorY + rc.y;

                if (!outer.Contains(x, y)) return false;
                if (inner.Contains(x, y)) return false;         // 링 내부는 금지
                if (IsForbiddenCorner(x, y)) return false;      // ✅ 코너 비움

                if (tilemap.HasTile(new Vector3Int(x, y, 0))) return false;
            }
        }

        // 2) 찍기: 타일 + 타일 회전(TransformMatrix)
        Matrix4x4 mat = RotationMatrix(r);

        for (int py = 0; py < h; py++)
        {
            for (int px = 0; px < w; px++)
            {
                var t = p.Get(px, (h - 1) - py);
                if (t == null) continue;

                Vector2Int rc = RotateCell(px, py, w, h, r);
                int x = anchorX + rc.x;
                int y = anchorY + rc.y;

                var pos = new Vector3Int(x, y, 0);

                tilemap.SetTile(pos, t);
                tilemap.SetTransformMatrix(pos, mat);
            }
        }

        return true;
    }

    bool IsForbiddenCorner(int x, int y)
    {
        if (_forbidden == null) return false;
        for (int i = 0; i < _forbidden.Length; i++)
            if (_forbidden[i].Contains(x, y))
                return true;
        return false;
    }

    // ---------------------------
    // Rotation helpers
    // ---------------------------

    static Matrix4x4 RotationMatrix(int r)
    {
        // r: 0=Right(0°), 1=Up(90°), 2=Left(180°), 3=Down(270°)
        float angle =
            r == 0 ? 0f :
            r == 1 ? 90f :
            r == 2 ? 180f :
            270f;

        return Matrix4x4.Rotate(Quaternion.Euler(0f, 0f, angle));
    }

    // ✅ 패턴 좌표를 "좌상단 원점(0,0=왼쪽위), y가 아래로 증가"로 간주하고 회전
    static Vector2Int RotateCell(int x, int y, int w, int h, int r)
    {
        switch (r)
        {
            case 0: return new Vector2Int(x, y);
            case 1: return new Vector2Int(h - 1 - y, x);
            case 2: return new Vector2Int(w - 1 - x, h - 1 - y);
            case 3: return new Vector2Int(y, w - 1 - x);
        }
        return new Vector2Int(x, y);
    }

    static Vector2Int RotatedSize(int w, int h, int r)
    {
        return (r == 1 || r == 3) ? new Vector2Int(h, w) : new Vector2Int(w, h);
    }

    // ---------------------------
    // RNG helper
    // ---------------------------

    int RandRange(int minInclusive, int maxInclusive)
    {
        if (maxInclusive < minInclusive) (minInclusive, maxInclusive) = (maxInclusive, minInclusive);
        return rng.Next(minInclusive, maxInclusive + 1);
    }

    void OnDrawGizmosSelected()
    {
        if (!drawGizmos) return;

        // tilemap 참조 확보
        if (tilemap == null) tilemap = GetComponent<Tilemap>();
        if (tilemap == null) return;

        // 아직 FillStrip을 안 눌렀어도, 현재 인스펙터 값으로 계산해서 그리기
        int left = origin.x - outerSize.x / 2;
        int bottom = origin.y - outerSize.y / 2;
        var outer = new IntRect(left, bottom, outerSize.x, outerSize.y);

        bool hasInner = outerSize.x > ringThickness * 2 && outerSize.y > ringThickness * 2;
        IntRect inner = default;
        if (hasInner) inner = IntRect.Shrink(outer, ringThickness);

        // forbidden 코너 4개
        IntRect[] forbidden = null;
        if (hasInner)
        {
            forbidden = new[]
            {
            new IntRect(outer.xMin, inner.yMax, ringThickness, ringThickness), // TL
            new IntRect(inner.xMax, inner.yMax, ringThickness, ringThickness), // TR
            new IntRect(outer.xMin, outer.yMin, ringThickness, ringThickness), // BL
            new IntRect(inner.xMax, outer.yMin, ringThickness, ringThickness), // BR
        };
        }

        // Grid(셀->월드 변환)
        Grid grid = tilemap.layoutGrid != null ? tilemap.layoutGrid : tilemap.GetComponentInParent<Grid>();

        // origin 표시
        Gizmos.color = Color.white;
        Vector3 originWorld = CellCenterWorld(grid, tilemap, new Vector3Int(origin.x, origin.y, 0));
        Gizmos.DrawWireSphere(originWorld, 0.12f);

        // Outer 박스
        if (drawOuter)
        {
            Gizmos.color = new Color(1f, 0.92f, 0.2f, 1f); // 노랑
            DrawCellRectWire(grid, tilemap, outer);
        }

        // Inner(설치 금지 영역)
        if (drawInner && hasInner)
        {
            Gizmos.color = new Color(1f, 0.3f, 0.3f, 1f); // 빨강
            DrawCellRectWire(grid, tilemap, inner);
        }

        // Forbidden corners(설치 금지)
        if (drawForbiddenCorners && hasInner && forbidden != null)
        {
            Gizmos.color = new Color(1f, 0.1f, 0.1f, 1f);
            for (int i = 0; i < forbidden.Length; i++)
                DrawCellRectWire(grid, tilemap, forbidden[i]);
        }
    }

    static Vector3 CellCenterWorld(Grid grid, Tilemap tm, Vector3Int cell)
    {
        // Tilemap 기준 셀 중심 월드
        return tm.GetCellCenterWorld(cell);
    }

    static void DrawCellRectWire(Grid grid, Tilemap tm, IntRect r)
    {
        // IntRect는 [min,max) 이므로, 월드 바운딩은 (min)~(max) 경계
        Vector3Int minCell = new Vector3Int(r.xMin, r.yMin, 0);
        Vector3Int maxCell = new Vector3Int(r.xMax, r.yMax, 0);

        // 경계 코너들의 월드 좌표 (Grid 기준)
        Vector3 p0 = tm.CellToWorld(minCell); // 좌하 모서리
        Vector3 p1 = tm.CellToWorld(new Vector3Int(r.xMax, r.yMin, 0)); // 우하
        Vector3 p2 = tm.CellToWorld(maxCell); // 우상
        Vector3 p3 = tm.CellToWorld(new Vector3Int(r.xMin, r.yMax, 0)); // 좌상

        // 선으로 사각형
        Gizmos.DrawLine(p0, p1);
        Gizmos.DrawLine(p1, p2);
        Gizmos.DrawLine(p2, p3);
        Gizmos.DrawLine(p3, p0);
    }
}