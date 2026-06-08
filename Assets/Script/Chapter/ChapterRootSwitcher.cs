using UnityEngine;

public class ChapterRootSwitcher : MonoBehaviour
{
    [System.Serializable]
    public class ChapterRoot
    {
        public int chapterIndex = 1;   // 1,2,3...
        public GameObject root;        // 해당 챕터 루트
    }

    [SerializeField] ChapterRoot[] chapterRoots;

    [Header("Fallback")]
    [SerializeField] int defaultChapterIndex = 1;

    void Awake()
    {
        int chap = ChapterContext.ChapterIndex;
        if (chap <= 0) chap = defaultChapterIndex;

        bool found = false;

        if (chapterRoots != null)
        {
            for (int i = 0; i < chapterRoots.Length; i++)
            {
                var entry = chapterRoots[i];
                if (entry == null || entry.root == null) continue;

                bool on = (entry.chapterIndex == chap);
                entry.root.SetActive(on);
                if (on) found = true;
            }
        }

        // ✅ 혹시 설정 실수로 해당 chap이 없으면: default 챕터를 켜줌
        if (!found && chapterRoots != null)
        {
            for (int i = 0; i < chapterRoots.Length; i++)
            {
                var entry = chapterRoots[i];
                if (entry == null || entry.root == null) continue;

                entry.root.SetActive(entry.chapterIndex == defaultChapterIndex);
            }
        }
    }
}