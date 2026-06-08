using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Stage Save Revision Database", fileName = "StageSaveRevisionDatabase")]
public class StageSaveRevisionDatabase : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        public string stageId;
        public int revision = 0;
    }

    [SerializeField] Entry[] entries;

    public int GetRevision(string stageId)
    {
        if (string.IsNullOrWhiteSpace(stageId))
            return 0;

        if (entries == null || entries.Length == 0)
            return 0;

        for (int i = 0; i < entries.Length; i++)
        {
            var e = entries[i];
            if (e == null || string.IsNullOrWhiteSpace(e.stageId))
                continue;

            if (string.Equals(e.stageId, stageId, StringComparison.OrdinalIgnoreCase))
                return e.revision;
        }

        return 0;
    }
}