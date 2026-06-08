using UnityEngine;

public class FixedRootStrengthInitializer : MonoBehaviour
{
    [System.Serializable]
    public class Entry
    {
        public StrengthBasedOccupancyCells target;
        [Min(1)] public int initialLevel = 1;
    }

    [SerializeField] Entry[] entries;

    void Start()
    {
        ApplyAll();
    }

    public void ApplyAll()
    {
        if (entries == null) return;

        for (int i = 0; i < entries.Length; i++)
        {
            var e = entries[i];
            if (e == null || e.target == null) continue;

            // ✅ 초기 강도 적용 후 OnLevelChanged도 발생시킴
            e.target.ApplyExternalInitialLevelOnce(e.initialLevel, true);
        }
    }
}