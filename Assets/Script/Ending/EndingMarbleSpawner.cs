using System.Collections;
using UnityEngine;

public class EndingMarbleSpawner : MonoBehaviour
{
    [SerializeField] GameObject marblePrefab;
    [SerializeField] float delay = 0.2f;

    bool spawned;

    IEnumerator Start()
    {
        yield return new WaitForSeconds(delay);

        if (spawned) yield break;
        spawned = true;

        if (GameModeManager.Instance != null)
            GameModeManager.Instance.EnterEndingPlayMode();

        if (marblePrefab == null)
        {
            Debug.LogWarning("[EndingMarbleSpawner] marblePrefab missing.", this);
            yield break;
        }

        var spawnPoints = FindObjectsOfType<MarbleSpawnPoint>(true);

        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogWarning("[EndingMarbleSpawner] No MarbleSpawnPoint found.", this);
            yield break;
        }

        for (int i = 0; i < spawnPoints.Length; i++)
        {
            var sp = spawnPoints[i];
            if (sp == null) continue;
            if (!sp.gameObject.activeInHierarchy) continue;

            Instantiate(marblePrefab, sp.transform.position, sp.transform.rotation);
        }
    }
}