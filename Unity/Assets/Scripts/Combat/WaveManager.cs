using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WaveManager : MonoBehaviour
{
    public enum WaveState
    {
        Prep,
        Spawning,
        WaitingForClear
    }

    [Header("References")]
    public EnemySpawner enemySpawner;

    [Header("Wave Settings")]
    public float prepTime = 8f;
    public float timeBetweenSpawns = 0.75f;
    public float timeBetweenWaves = 2f;

    [Header("Scaling")]
    public int startingEnemies = 3;
    public int extraEnemiesPerWave = 2;

    [Header("Debug")]
    public int currentWave = 0;
    public WaveState currentState;

    private readonly List<EnemyHealth> aliveEnemies = new List<EnemyHealth>();

    private void Start()
    {
        if (enemySpawner == null)
        {
            Debug.LogError("WaveManager has no EnemySpawner assigned.");
            enabled = false;
            return;
        }

        enemySpawner.SetWaveManager(this);
        StartCoroutine(RunWaves());
    }

    /// <summary>
    /// Runs the wave loop
    /// </summary>
    private IEnumerator RunWaves()
    {
        while (true)
        {
            currentWave++;
            int enemyCount = startingEnemies + ((currentWave - 1) * extraEnemiesPerWave);

            currentState = WaveState.Prep;
            Debug.Log($"Wave {currentWave} prep started. Draw your weapon now.");
            yield return new WaitForSeconds(prepTime);

            currentState = WaveState.Spawning;
            Debug.Log($"Wave {currentWave} spawning {enemyCount} enemies.");

            for (int i = 0; i < enemyCount; i++)
            {
                EnemyHealth enemy = enemySpawner.SpawnEnemy();
                if (enemy != null)
                {
                    aliveEnemies.Add(enemy);
                }
                yield return new WaitForSeconds(timeBetweenSpawns);
            }

            currentState = WaveState.WaitingForClear;
            Debug.Log($"Wave {currentWave} active.");

            yield return new WaitUntil(AllEnemiesCleared);

            Debug.Log($"Wave {currentWave} cleared.");
            yield return new WaitForSeconds(timeBetweenWaves);
        }
    }

    /// <summary>
    /// Removes a killed enemy from the list of live enemies
    /// </summary>
    /// <param name="enemy">Killed enemy</param>
    public void NotifyEnemyKilled(EnemyHealth enemy)
    {
        if (aliveEnemies.Contains(enemy))
        {
            aliveEnemies.Remove(enemy);
        }
    }

    /// <summary>
    /// Checks if all enemies are cleared by removing any null or dead entries and verifying the list is empty
    /// </summary>
    /// <returns>True when all enemies are cleared, false otherwise</returns>
    private bool AllEnemiesCleared()
    {
        aliveEnemies.RemoveAll(e => e == null || e.IsDead);
        return aliveEnemies.Count == 0;
    }
}
