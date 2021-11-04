using UnityEngine;
using RiptideNetworking;

public class SendMessages : MonoBehaviour
{
    /// <summary>Sends player inputs to the server.</summary>
    /// <param name="_inputState">Inputs of the player.</param>
    public static void PlayerInput(ClientInputState _inputState)
    {
        Message message = Message.Create(MessageSendMode.unreliable, (ushort)ClientToServerId.playerInput);

        message.Add(_inputState.tick);
        message.Add(_inputState.lerpAmount);
        message.Add(_inputState.simulationFrame);

        message.Add(_inputState.buttons);
        
        message.Add(_inputState.HorizontalAxis);
        message.Add(_inputState.VerticalAxis);
        message.Add(_inputState.rotation);

        NetworkManager.Singleton.Client.Send(message);    

        DebugScreen.packetsUp++;        
        DebugScreen.bytesUp += message.WrittenLength;
    }

    /// <summary>Sends request to change convar.</summary>
    public static void PlayerConvar(Convar i, float _value)
    {
        Message message = Message.Create(MessageSendMode.reliable, (ushort)ClientToServerId.playerConvar);

        message.Add(i.name);
        message.Add(_value);

        NetworkManager.Singleton.Client.Send(message);
        
        DebugScreen.packetsUp++;        
        DebugScreen.bytesUp += message.WrittenLength;
    }
}
