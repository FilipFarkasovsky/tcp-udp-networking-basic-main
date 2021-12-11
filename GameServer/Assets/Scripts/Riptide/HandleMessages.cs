using UnityEngine;
using RiptideNetworking;
using System.Collections.Generic;

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

        inputState.buttons = message.GetInt();

        inputState.HorizontalAxis = message.GetFloat();
        inputState.VerticalAxis = message.GetFloat();
        inputState.rotation = message.GetQuaternion();

        if (!Player.List.TryGetValue(_fromClient, out Player player))
            return;

            player.AddInput(inputState);
    }

    [MessageHandler((ushort)ClientToServerId.playerConvar)]
    public static void PlayerConvar(ushort _fromClient, Message message)
    {
        string name = message.GetString();
        float requestedValue = message.GetFloat();

        //Check if admin
        if (!Player.List.TryGetValue(_fromClient, out Player player))
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

    [MessageHandler((ushort)ClientToServerId.inputCommand)]
    public static void ReceiveInputCommands(ushort _fromClient, Message message)
    {
        if (!Player.List.TryGetValue(_fromClient, out Player player))
            return;

        player.simpleDS.inputCmd.DeliveryTime = Time.time;
        player.simpleDS.inputCmd.LastAckedTick = message.GetInt();

        ushort countOfCommands = message.GetUShort();
        List<SimpleDS.Inputs> list = new List<SimpleDS.Inputs>();

        for (ushort i = 0; i < countOfCommands; i++)
        {
            list.Add((SimpleDS.Inputs)(message.GetUShort()));
        }
        player.simpleDS.inputCmd.Inputs = list;
        player.simpleDS.ReceivedServerInputs.Enqueue(player.simpleDS.inputCmd);
    }

    [MessageHandler((ushort)ClientToServerId.carTransfortmToServer)]
    public static void CarTransform(ushort _fromClient, Message message)
    {
        if (!Player.List.TryGetValue(_fromClient, out Player player))
            return;

        player.transform.position = message.GetVector3();
        player.transform.rotation = message.GetQuaternion();
    }

}
