using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameModeManager : MonoBehaviour
{
    public static GameModeManager Instance;
    public GameMode currentMode = GameMode.Build;
    public GameObject marblePrefab;
    public Transform spawnPoint;

    List<PlacementSnapshot> snapshot = new List<PlacementSnapshot>();

    void Awake()
    {
        Instance = this;
    }

    private void Update()
    {
        //임시 변환
        if (Input.GetKeyDown(KeyCode.Space))
        {
            GameModeManager.Instance.EnterPlayMode();
        }


        if (Input.GetKeyDown(KeyCode.R))
            EnterBuildMode();
    }

    public void SpawnMarble()
    {
        Instantiate(marblePrefab, spawnPoint.position, Quaternion.identity);
    }

    public void EnterBuildMode()
    {
        currentMode = GameMode.Build;
        RestoreSnapshot();
    }

    public void EnterPlayMode()
    {
        SaveSnapshot();
        currentMode = GameMode.Play;
        ApplyPhysics();
        SpawnMarble();
    }

    void SaveSnapshot()
    {
        snapshot.Clear();

        var placedObjects = FindObjectsOfType<PlacementObject>();

        foreach (var po in placedObjects)
        {
            if (po.gameObject.layer == LayerMask.NameToLayer("Ghost"))
                continue; // 고스트 제외

            snapshot.Add(new PlacementSnapshot
            {
                prefab = po.prefab,   // 프리팹 참조는 아래에서 설명
                position = po.transform.position,
                rotation = po.transform.rotation
            });

            Debug.Log($"SaveSnapshot: {po.name}, prefab = {po.prefab}");
        }


    }

    void RestoreSnapshot()
    {
        // 현재 배치된 오브젝트 전부 제거
        var current = FindObjectsOfType<PlacementObject>();
        foreach (var po in current)
            Destroy(po.gameObject);

        // 구슬 제거
        var marbles = GameObject.FindGameObjectsWithTag("Marble");
        foreach (var m in marbles)
            Destroy(m);

        // 스냅샷으로 다시 생성
        foreach (var snap in snapshot)
        {
            GameObject obj = Instantiate(
                snap.prefab,
                snap.position,
                snap.rotation
            );

            // Build 모드 기본값
            var rb = obj.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.bodyType = RigidbodyType2D.Kinematic;
                rb.gravityScale = 0;
                rb.simulated = true;
            }

            foreach (var col in obj.GetComponentsInChildren<Collider2D>())
            {
                col.enabled = true;
                col.isTrigger = false; // 
            }
        }
    }


    void ApplyPhysics()
    {
        var objects = FindObjectsOfType<PlacementObject>();

        foreach (var po in objects)
        {
            Rigidbody2D rb = po.GetComponent<Rigidbody2D>();
            if (rb == null) continue;

            switch (po.physicsType)
            {
                case PhysicsType.Static:
                    rb.bodyType = RigidbodyType2D.Static;
                    break;

                case PhysicsType.DynamicNoGravity:
                    rb.bodyType = RigidbodyType2D.Dynamic;
                    rb.gravityScale = 0;
                    break;

                case PhysicsType.DynamicGravity:
                    rb.bodyType = RigidbodyType2D.Dynamic;
                    rb.gravityScale = 1;
                    break;
            }
        }
    }
}
