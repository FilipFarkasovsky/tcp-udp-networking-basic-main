using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary> Responsible for enemy spawning</summary>
public class EnemySpawning : MonoBehaviour
{
    private static EnemySpawning _singleton;
    public static EnemySpawning Singleton
    {
        get => _singleton;
        private set
        {
            if (_singleton == null)
                _singleton = value;
            else if (_singleton != value)
            {
                Debug.Log($"{nameof(EnemySpawning)} instance already exists, destroying object!");
                Destroy(value);
            }
        }
    }

    public ushort maxEnemies;
    public ushort enemiesCount;
    public bool shouldSpawn = true;
    public ushort secondsForRespawning;

    [SerializeField] private GameObject enemyPrefab;
    public GameObject EnemyPrefab => enemyPrefab;

    public IEnumerator StartSpawning()
    {
        while (true)
        {
            if (enemiesCount <= maxEnemies && shouldSpawn)
                Enemy.Spawn(new Vector3(Random.Range(-10,10), 0 , Random.Range(-10, 10)));

            enemiesCount++;
            yield return new WaitForSeconds(secondsForRespawning);
        }
    }

    private void Awake()
    {
        Singleton = this;
    }
}
