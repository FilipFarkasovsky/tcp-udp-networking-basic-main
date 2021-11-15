using UnityEngine;
using RiptideNetworking;
using System.Collections.Generic;

public class Player : MonoBehaviour
{
    public static ushort myId = 0;
    public static Dictionary<ushort, Player> list = new Dictionary<ushort, Player>();

    public ushort id;
    public string username;

    public Interpolation interpolation;

    public Interpolation cameraInterpolation;
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
