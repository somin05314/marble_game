using System.Collections;
using UnityEngine;

public class TrueEndingMarbleSpawner : MonoBehaviour
{
    [SerializeField] GameObject marblePrefab;
    [SerializeField] float delay = 0.2f;

    [Header("Camera Follow")]
    [SerializeField] MarbleSpawnPoint cameraTargetSpawnPoint;
    [SerializeField] TrueEndingCameraFollow2D cameraFollow;

    bool spawned;

    IEnumerator Start()
    {
        yield return new WaitForSeconds(delay);

        if (spawned) yield break;
        spawned = true;

        GameModeManager.Instance?.EnterEndingPlayMode();

        if (marblePrefab == null)
        {
            Debug.LogWarning("[TrueEndingMarbleSpawner] marblePrefab missing.", this);
            yield break;
        }

        var spawnPoints = FindObjectsOfType<MarbleSpawnPoint>(true);

        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogWarning("[TrueEndingMarbleSpawner] No MarbleSpawnPoint found.", this);
            yield break;
        }

        for (int i = 0; i < spawnPoints.Length; i++)
        {
            var sp = spawnPoints[i];
            if (sp == null) continue;
            if (!sp.gameObject.activeInHierarchy) continue;

            var marble = Instantiate(
                marblePrefab,
                sp.transform.position,
                sp.transform.rotation
            );

            if (sp == cameraTargetSpawnPoint && cameraFollow != null)
                cameraFollow.SetTarget(marble.transform);
        }
    }
}