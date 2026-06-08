public static class RailGraphRevision
{
    public static int Value { get; private set; } = 1;

    // 기존: 인수 없는 bump
    public static void Bump()
    {
        Value++;
        if (Value <= 0) Value = 1; // overflow 방지
    }

    // ✅ 호환: Bump("reason") 형태 호출 허용
    public static void Bump(string _reason)
        => Bump();

    // ✅ 호환: Bump(this) 같은 형태 호출 허용
    public static void Bump(object _any)
        => Bump();
}
