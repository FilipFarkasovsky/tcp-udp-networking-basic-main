using UnityEngine;
using RiptideNetworking;

public class HandleMessages : MonoBehaviour
{
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
