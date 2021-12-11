using UnityEngine;
using RiptideNetworking;

public class HandleMessages : MonoBehaviour
{
    [MessageHandler((ushort)ServerToClientId.spawnObject)]
    public static void Spawn(Message message)
    {
        switch (message.GetByte())
        {
            case (byte)NetworkedObjectType.player:
                Player.Spawn(message.GetUShort(), message.GetString(), message.GetVector3());
                break;
            case (byte)NetworkedObjectType.enemy:
                Enemy.Spawn(message.GetUShort(), message.GetVector3());
                break;
            default:
                break;
        }
    }

    [MessageHandler((ushort)ServerToClientId.setTransform)]
    public static void SetTransform(Message message)
    {
        DebugScreen.bytesDown += message.WrittenLength;
        DebugScreen.packetsDown++;

        switch (message.GetByte())
        {
            case (byte)NetworkedObjectType.player:
                Player.SetTransform(message);
                break;
            case (byte)NetworkedObjectType.enemy:
                Enemy.SetTransform(message);
                break;
            default:
                break;
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

        simulationState.simulationFrame = message.GetInt();
        simulationState.position = message.GetVector3();
        simulationState.velocity = message.GetVector3();
        //simulationState.rotation = message.GetQuaternion();
        //simulationState.angularVelocity = message.GetVector3();

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
