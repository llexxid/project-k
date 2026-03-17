using UnityEngine;
using System.Collections;

public class EnemySpawner : MonoBehaviour
{
    public GameObject enemyPrefab;
    public Transform player;
    public float spawnInterval = 5f;
    public float spawnRadius = 5f;

    void Start()
    {
        StartCoroutine(SpawnRoutine());
    }

    IEnumerator SpawnRoutine()
    {
        while (true)
        {
            SpawnEnemies(6);
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

            while (!canSpawn && attempts < 10)
            {
                spawnPos = (Vector2)player.position + Random.insideUnitCircle.normalized * spawnRadius;
                if (Physics2D.OverlapCircle(spawnPos, 0.5f) == null)
                {
                    Instantiate(enemyPrefab, spawnPos, Quaternion.identity);
                    enemyPrefab.SetActive(true);
                    canSpawn = true;
                }
                attempts++;
            }
        }
    }
}
