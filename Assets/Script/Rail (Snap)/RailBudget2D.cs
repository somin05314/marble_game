using UnityEngine;

public class RailBudget2D : MonoBehaviour
{
    public static RailBudget2D Instance { get; private set; }

    [Header("Budget")]
    [SerializeField] int maxRails = 0;   // 0이면 무제한
    [SerializeField] int usedRails = 0;

    public void SetMaxRails(int max) => maxRails = Mathf.Max(0, max);

    public int MaxRails => maxRails;
    public int UsedRails => usedRails;
    public int Remaining => (maxRails <= 0) ? int.MaxValue : Mathf.Max(0, maxRails - usedRails);
    public bool IsLimited => maxRails > 0;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void SetLimit(int max)
    {
        maxRails = Mathf.Max(0, max);
        ClampUsed();
    }

    public void ResetUsed(int used = 0)
    {
        usedRails = Mathf.Max(0, used);
        ClampUsed();
    }

    public bool CanSpend(int amount = 1)
    {
        if (amount <= 0) return true;
        if (!IsLimited) return true;
        return usedRails + amount <= maxRails;
    }

    public bool TrySpend(int amount = 1)
    {
        if (!CanSpend(amount)) return false;
        usedRails += amount;
        ClampUsed();
        return true;
    }

    public void Refund(int amount = 1)
    {
        if (amount <= 0) return;
        usedRails = Mathf.Max(0, usedRails - amount);
        ClampUsed();
    }

    public void SyncUsedWithScene()
    {
        int count = 0;

        var registry = StageObjectRegistry.Instance;
        if (registry != null)
        {
            registry.CleanupNulls();

            var rails = registry.Rails;
            for (int i = 0; i < rails.Count; i++)
            {
                var rail = rails[i];
                if (rail == null) continue;
                if (!rail.gameObject.activeInHierarchy) continue;
                count++;
            }
        }
        else
        {
#if UNITY_2022_2_OR_NEWER
            var rails = FindObjectsByType<RailSpan2D>(FindObjectsSortMode.None);
#else
            var rails = FindObjectsOfType<RailSpan2D>();
#endif
            for (int i = 0; i < rails.Length; i++)
            {
                if (rails[i] == null) continue;
                if (!rails[i].gameObject.activeInHierarchy) continue;
                count++;
            }
        }

        usedRails = count;
        ClampUsed();
    }

    void ClampUsed()
    {
        usedRails = Mathf.Max(0, usedRails);

        if (IsLimited)
            usedRails = Mathf.Min(usedRails, maxRails);
    }
}