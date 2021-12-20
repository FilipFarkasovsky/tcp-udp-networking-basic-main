using UnityEngine;

/// <summary> Stores list of enemys </summary>
public class Enemy : Multiplayer.NetworkedEntity
{
    public static void Spawn(ushort id, Vector3 position)
    {
        Enemy enemy = Instantiate(NetworkManager.Singleton.EnemyPrefab, position, Quaternion.identity).GetComponent<Enemy>();
        Debug.Log($"Spawning enemy with id {id}");

        enemy.name = $"Enemy {id}";
        enemy.id = id;
        enemyList.Add(enemy.id, enemy);
    }
}

