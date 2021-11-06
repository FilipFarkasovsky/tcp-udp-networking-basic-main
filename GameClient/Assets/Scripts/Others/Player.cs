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
    public SimpleInterpolation simpleInterpolation;
    public SnapshotStDev snapshotStDev;

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

    #region Messages
    [MessageHandler((ushort)ServerToClientId.spawnPlayer)]
    public static void SpawnPlayer(Message message)
    {
        DebugScreen.bytesDown += message.WrittenLength;
        DebugScreen.packetsDown++;        
        
        Spawn(message.GetUShort(), message.GetString(), message.GetVector3());
    }

    [MessageHandler((ushort)ServerToClientId.playerPosition)]
    public static void PlayerPosition(Message message)
    {
        DebugScreen.bytesDown += message.WrittenLength;
        DebugScreen.packetsDown++;        

        ushort id = message.GetUShort();
        Vector3 position = message.GetVector3();
        int serverTick = message.GetInt();

        if (list.TryGetValue(id, out Player player))
        {
            if (serverTick > GlobalVariables.serverTick)
                GlobalVariables.serverTick = serverTick;

            player.interpolation.NewUpdate(serverTick, position);
        }
    }

    [MessageHandler((ushort)ServerToClientId.playerRotation)]
    public static void PlayerRotation(Message message)
    {
        DebugScreen.bytesDown += message.WrittenLength;
        DebugScreen.packetsDown++;        

        ushort id = message.GetUShort();
        Quaternion _rotation = message.GetQuaternion();
        int serverTick = message.GetInt();

        if (list.TryGetValue(id, out Player player))
        {
            if (serverTick > GlobalVariables.serverTick)
                GlobalVariables.serverTick = serverTick;

            player.interpolation.NewUpdate(serverTick, _rotation);
        }
    }

    [MessageHandler((ushort)ServerToClientId.playerTransform)]
    public static void PlayerTransform(Message message)
    {
        DebugScreen.bytesDown += message.WrittenLength;
        DebugScreen.packetsDown++;        

        ushort id = message.GetUShort();
        Vector3 position = message.GetVector3();
        Quaternion _rotation = message.GetQuaternion();
        int serverTick = message.GetInt();

        if (list.TryGetValue(id, out Player player))
        {
            if (serverTick > GlobalVariables.serverTick)
                GlobalVariables.serverTick = serverTick;

            //player.interpolation.NewUpdate(serverTick, position);
            //player.simpleInterpolation.NewUpdate(serverTick, position);
            Debug.Log(serverTick - GlobalVariables.clientTick);
            player.snapshotStDev.Server.transform.position = position;
            player.snapshotStDev.ServerSnapshot(Time.time + 32,Time.time+ Utils.ticksToTime(serverTick - GlobalVariables.clientTick) );
            if(player.cameraInterpolation)player.cameraInterpolation.NewUpdate(serverTick, _rotation);
        }
    }

    [MessageHandler((ushort)ServerToClientId.playerAnimation)]
    public static void PlayerAnimation(Message message)
    {
        DebugScreen.bytesDown += message.WrittenLength;
        DebugScreen.packetsDown++;

        ushort id = message.GetUShort();
        bool _isFiring = message.GetBool();
        float _lateralSpeed = message.GetFloat();
        float _forwardSpeed = message.GetFloat();
        bool _grounded = message.GetBool();
        bool _jumping = message.GetBool();

        if (list.TryGetValue(id, out Player player))
        {
            player.playerAnimation.IsFiring(_isFiring);
            player.playerAnimation.UpdateAnimatorProperties(_lateralSpeed, _forwardSpeed, _grounded, _jumping);
        }
    }


    [MessageHandler((ushort)ServerToClientId.serverSimulationState)]
    public static void SimulationState(Message message)
    {
        DebugScreen.bytesDown += message.WrittenLength;
        DebugScreen.packetsDown++;

        SimulationState simulationState = new SimulationState();

        simulationState.position = message.GetVector3();
        simulationState.velocity = message.GetVector3();
        simulationState.simulationFrame = message.GetInt();

        if(list.TryGetValue(myId, out Player player))
            player.gameObject.GetComponentInChildren<PlayerInput>().OnServerSimulationStateReceived(simulationState);
    }
    #endregion
}
