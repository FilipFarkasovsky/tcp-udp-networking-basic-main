using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RiptideNetworking;
using UnityEngine;

class Enemy : MonoBehaviour
{
    public static Dictionary<ushort, Enemy> list = new Dictionary<ushort, Enemy>();
    public ushort id;
    public Interpolation interpolation;

    public static void Spawn(ushort id, Vector3 position)
    {
        Enemy enemy = Instantiate(NetworkManager.Singleton.EnemyPrefab, position, Quaternion.identity).GetComponent<Enemy>();
        Debug.Log("Spawning enemy");

        enemy.name = $"Enemy {id}";
        enemy.id = id;
        list.Add(enemy.id, enemy);
    }
}

