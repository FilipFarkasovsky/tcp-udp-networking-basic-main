using System.Collections.Generic;
using UnityEngine;
using RiptideNetworking;

/// <summary> Stores list of enemys </summary>
class Enemy : NetworkedEntity<Enemy>
{
    public override byte GetNetworkedObjectType { get; set; } = (byte)NetworkedObjectType.enemy;
    public override ushort Id { get => id; }

    public ushort id;

    public static void Spawn(ushort id, Vector3 position)
    {
        Enemy enemy = Instantiate(NetworkManager.Singleton.EnemyPrefab, position, Quaternion.identity).GetComponent<Enemy>();
        Debug.Log($"Spawning enemy with id {id}");

        enemy.name = $"Enemy {id}";
        enemy.id = id;
        list.Add(enemy.id, enemy);
    }
}

