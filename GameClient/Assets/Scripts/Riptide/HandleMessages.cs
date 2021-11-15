using UnityEngine;
using RiptideNetworking;

public class HandleMessages : MonoBehaviour
{
    [MessageHandler((ushort)ServerToClientId.spawnPlayer)]
    public static void SpawnPlayer(Message message)
    {
        DebugScreen.bytesDown += message.WrittenLength;
        DebugScreen.packetsDown++;

        Player.Spawn(message.GetUShort(), message.GetString(), message.GetVector3());
    }

    [MessageHandler((ushort)ServerToClientId.spawnEnemy)]
    public static void SpawnEnemy(Message message)
    {
        DebugScreen.bytesDown += message.WrittenLength;
        DebugScreen.packetsDown++;

        Enemy.Spawn(message.GetUShort(), message.GetVector3());
    }

    [MessageHandler((ushort)ServerToClientId.setPosition)]
    public static void PlayerPosition(Message message)
    {
        DebugScreen.bytesDown += message.WrittenLength;
        DebugScreen.packetsDown++;

        ushort id = message.GetUShort();
        Vector3 position = message.GetVector3();
        int serverTick = message.GetInt();

        if (Player.list.TryGetValue(id, out Player player))
        {
            if (serverTick > GlobalVariables.serverTick)
                GlobalVariables.serverTick = serverTick;

            player.interpolation.NewUpdate(serverTick, position);
        }
    }

    [MessageHandler((ushort)ServerToClientId.setRotation)]
    public static void PlayerRotation(Message message)
    {
        DebugScreen.bytesDown += message.WrittenLength;
        DebugScreen.packetsDown++;

        ushort id = message.GetUShort();
        Quaternion _rotation = message.GetQuaternion();
        int serverTick = message.GetInt();

        if (Player.list.TryGetValue(id, out Player player))
        {
            if (serverTick > GlobalVariables.serverTick)
                GlobalVariables.serverTick = serverTick;

            player.interpolation.NewUpdate(serverTick, _rotation);
        }
    }

    [MessageHandler((ushort)ServerToClientId.setTransform)]
    public static void PlayerTransform(Message message)
    {
        DebugScreen.bytesDown += message.WrittenLength;
        DebugScreen.packetsDown++;

        bool isPlayer = message.GetBool();
        ushort id = message.GetUShort();
        Vector3 position = message.GetVector3();
        Quaternion _rotation = message.GetQuaternion();
        int serverTick = message.GetInt();

        if (isPlayer)
        {
            if (Player.list.TryGetValue(id, out Player player))
            {
                if (serverTick > GlobalVariables.serverTick)
                    GlobalVariables.serverTick = serverTick;

                switch (player.interpolation.implementation)
                {
                    case Interpolation.InterpolationImplemenation.notAGoodUsername:
                        player.interpolation.NewUpdate(serverTick, position);
                        break;
                    case Interpolation.InterpolationImplemenation.alex:
                        player.interpolation.snapshotStDev.Server.transform.position = position;
                        player.interpolation.snapshotStDev.ServerSnapshot();
                        break;
                    case Interpolation.InterpolationImplemenation.tomWeiland:
                        player.interpolation.tomWeilandInterpolation.NewUpdate(serverTick, position);
                        break;
                }

                if (player.cameraInterpolation) player.cameraInterpolation.NewUpdate(serverTick, _rotation);
            }
        }
        else
        {
            if(Enemy.list.TryGetValue(id, out Enemy enemy))
            {
                if (serverTick > GlobalVariables.serverTick)
                    GlobalVariables.serverTick = serverTick;

                switch (enemy.interpolation.implementation)
                {
                    case Interpolation.InterpolationImplemenation.notAGoodUsername:
                        enemy.interpolation.NewUpdate(serverTick, position);
                        break;
                    case Interpolation.InterpolationImplemenation.alex:
                        enemy.interpolation.snapshotStDev.Server.transform.position = position;
                        enemy.interpolation.snapshotStDev.ServerSnapshot();
                        break;
                    case Interpolation.InterpolationImplemenation.tomWeiland:
                        enemy.interpolation.tomWeilandInterpolation.NewUpdate(serverTick, position);
                        break;
                }
            }
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

        if (Player.list.TryGetValue(id, out Player player))
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

        if (Player.list.TryGetValue(Player.myId, out Player player))
            player.gameObject.GetComponentInChildren<PlayerInput>().OnServerSimulationStateReceived(simulationState);
    }

    [MessageHandler((ushort)ServerToClientId.serverConvar)]
    public static void ServerConvar(Message message)
    {
        DebugScreen.bytesDown += message.WrittenLength;
        DebugScreen.packetsDown++;        

        string name =message.GetString();
        float value = message.GetFloat();
        string helpString = message.GetString();
        foreach(Convar i in Convars.list)
        {
            if(i.name == name)
            {
                i.ReceiveResponse(value);
                return;
            }
        }

        // We should have returned, but since the convar doesnt exist in the client
        // we need to create it although the client cant know what it is used for
        // Defaultvalue might be wrong, but it doesnt matter too much
        Convar newConvar = new Convar(name, value, helpString, Flags.NETWORK);
    }

    [MessageHandler((ushort)ServerToClientId.serverTick)]
    public static void ServerTick(Message message)
    {
        DebugScreen.bytesDown += message.WrittenLength;
        DebugScreen.packetsDown++;                

        int _serverTick = message.GetInt();
        if(_serverTick > GlobalVariables.serverTick)
            GlobalVariables.serverTick = _serverTick;
    }
}
