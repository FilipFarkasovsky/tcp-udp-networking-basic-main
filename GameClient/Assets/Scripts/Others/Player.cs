using UnityEngine;

/// <summary> Stores list of players, controls their interpolation and animation </summary>
public class Player : Multiplayer.NetworkedEntity
{
    public static ushort myId = 0;

    public string username;

    public PlayerAnimation playerAnimation;

    private void OnDestroy()
    {
        playerList.Remove(id);
        Destroy(gameObject);
    }

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
        playerList.Add(player.id, player);
    }
}

