using UnityEngine;
using System.Collections;

public class EnemySpawner : MonoBehaviour
{
    public GameObject enemyPrefab;  // 생성할 적 프리팹
    public Transform player;        // 플레이어 위치
    public float spawnInterval = 5f; // 소환 간격 (초)
    public float spawnRadius = 5f;  // 소환 반경

    void Start()
    {
        // 소환 루프 시작
        StartCoroutine(SpawnRoutine());
    }

    IEnumerator SpawnRoutine()
    {
        while (true)
        {
            SpawnEnemies(6); // 6마리 소환
            yield return new WaitForSeconds(spawnInterval);
        }
    }

    void SpawnEnemies(int count)
    {
        for (int i = 0; i < count; i++)
        {
            Vector2 spawnPos;
            int attempts = 0;
            bool canSpawn = false;

            while (!canSpawn && attempts < 10) // 최대 10번 재시도
            {
                spawnPos = (Vector2)player.position + Random.insideUnitCircle.normalized * spawnRadius;
                // 특정 반경(예: 0.5f) 안에 다른 콜라이더가 있는지 체크
                if (Physics2D.OverlapCircle(spawnPos, 0.5f) == null)
                {
                    Instantiate(enemyPrefab, spawnPos, Quaternion.identity);
                    canSpawn = true;
                }
                attempts++;
            }
        }
    }
}