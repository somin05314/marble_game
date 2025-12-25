using System.Collections.Generic;

public class SnapPreviewResult
{
    public List<SnapPair> pairs = new();

    public bool HasNone => pairs.Count == 0;
    public bool HasSingle => pairs.Count == 1;
    public bool HasMultiple => pairs.Count >= 2;
}

public struct SnapPair
{
    public SnapPoint myPoint;
    public SnapPoint otherPoint;
}
