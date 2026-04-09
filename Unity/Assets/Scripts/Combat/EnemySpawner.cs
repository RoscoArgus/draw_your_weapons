using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    [Header("References")]
    public Transform playerTarget;
    public GameObject[] enemyPrefabs;
    public Transform[] spawnPoints;

    /// <summary>
    /// Spawns a random enemy at a random spawn point and links it to the player target
    /// </summary>
    public EnemyHealth SpawnEnemy()
    {
        if (playerTarget == null)
        {
            Debug.LogWarning("EnemySpawner has no playerTarget.");
            return null;
        }

        if (enemyPrefabs == null || enemyPrefabs.Length == 0)
        {
            Debug.LogWarning("EnemySpawner has no enemy prefabs.");
            return null;
        }

        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogWarning("EnemySpawner has no spawn points.");
            return null;
        }

        Transform chosenSpawn = spawnPoints[Random.Range(0, spawnPoints.Length)];
        GameObject enemyObj = Instantiate(
            enemyPrefabs[Random.Range(0, enemyPrefabs.Length)],
            chosenSpawn.position,
            chosenSpawn.rotation);

        EnemyMover mover = enemyObj.GetComponent<EnemyMover>();
        if (mover != null)
        {
            mover.target = playerTarget;
        }
        return enemyObj.GetComponent<EnemyHealth>();
    }
}