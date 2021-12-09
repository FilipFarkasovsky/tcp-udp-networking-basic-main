using UnityEngine;
using RiptideNetworking;
using System.Collections.Generic;

/// <summary> Stores list of players, controls their interpolation and animation </summary>
public class Player : NetworkedEntity<Player>
{
    public override byte GetNetworkedObjectType { get; set; } = (byte)NetworkedObjectType.enemy;
    public override ushort Id { get => id; }

    public static ushort myId = 0;

    public ushort id;
    public string username;

    public PlayerAnimation playerAnimation;

    public static void Spawn(ushort id, string username, Vector3 position)
    {
        Player player;
        if (id == NetworkManager.Singleton.Client.Id)
            player = Instantiate(NetworkManager.Singleton.LocalPlayerPrefab, position, Quaternion.identity).GetComponent<Player>();
        else
            player = Instantiate(NetworkManager.Singleton.PlayerPrefab, position, Quaternion.identity).GetComponent<Player>();

        player.name = $"Player {id} ({username})";
        player.id = id;
        player.username = username;
        list.Add(player.id, player);
    }
}
