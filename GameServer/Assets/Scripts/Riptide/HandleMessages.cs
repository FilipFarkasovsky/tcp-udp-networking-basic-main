using RiptideNetworking;
using Multiplayer;

public class HandleMessages
{
    [MessageHandler((ushort)ClientToServerId.playerName)]
    public static void PlayerName(ushort fromClientId, Message message)
    {
        Player.Spawn(fromClientId, message.GetString());
    }

    [MessageHandler((ushort)ClientToServerId.playerInput)]
    public static void PlayerInput(ushort _fromClient, Message message)
    {
        ClientInputState inputState = new ClientInputState();

        inputState.tick = message.GetInt();
        inputState.lerpAmount = message.GetFloat();
        inputState.simulationFrame = message.GetInt();

        inputState.buttons = message.GetUShort();

        inputState.HorizontalAxis = message.GetFloat();
        inputState.VerticalAxis = message.GetFloat();
        inputState.rotation = message.GetQuaternion();

        if (!NetworkedEntity.playerList.TryGetValue(_fromClient, out Player player))
            return;

            player.AddInput(inputState);
    }

    [MessageHandler((ushort)ClientToServerId.playerConvar)]
    public static void PlayerConvar(ushort _fromClient, Message message)
    {
        string name = message.GetString();
        float requestedValue = message.GetFloat();

        //Check if admin
        if (!NetworkedEntity.playerList.TryGetValue(_fromClient, out Player player))
            return;

        foreach (Convar i in Convars.list)
        {
            if (i.name == name)
            {
                i.SetValue(requestedValue);
                return;
            }
        }
    }

    [MessageHandler((ushort)ClientToServerId.carTransfortmToServer)]
    public static void CarTransform(ushort _fromClient, Message message)
    {
        if (!NetworkedEntity.playerList.TryGetValue(_fromClient, out Player player))
            return;

        player.transform.position = message.GetVector3();
        player.transform.rotation = message.GetQuaternion();
    }

}
