using UnityEngine;
using RiptideNetworking;
using System.Collections.Generic;

public class Player : MonoBehaviour
{
    public static ushort myId = 0;
    public static Dictionary<ushort, Player> list = new Dictionary<ushort, Player>();

    public ushort id;
    public string username;

    static Convar interpolationScript = new Convar("interpolationScript", 2, "Camera rotation sensitivity", Flags.CLIENT);

    public SnapshotStDev snapshotStDev;
    public Interpolation interpolation;
    public SimpleInterpolation simpleInterpolation;

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

            switch (interpolationScript.GetValue())
            {
                case 1:
                    player.snapshotStDev.enabled = true;
                    player.interpolation.enabled = false;
                    player.simpleInterpolation.enabled = false;
                    player.snapshotStDev.Server.transform.position = position;
                    player.snapshotStDev.ServerSnapshot();
                    break;
                case 2:
                    player.snapshotStDev.enabled = false;
                    player.interpolation.enabled = true;
                    player.simpleInterpolation.enabled = false;
                    player.interpolation.NewUpdate(serverTick, position);
                    break;
                case 3:
                    player.snapshotStDev.enabled = false;
                    player.interpolation.enabled = false;
                    player.simpleInterpolation.enabled = true;
                    player.interpolation.NewUpdate(serverTick, position);
                    break;
                default:
                    break;
            }

            if(player.cameraInterpolation)player.cameraInterpolation.NewUpdate(serverTick, _rotation);

            //player.interpolation.NewUpdate(serverTick, position);
            //player.simpleInterpolation.NewUpdate(serverTick, position);
            //player.snapshotStDev.Server.transform.position = position;
            //player.snapshotStDev.ServerSnapshot();
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
