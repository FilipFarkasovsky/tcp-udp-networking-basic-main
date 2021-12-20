using UnityEngine;
using RiptideNetworking;
using Multiplayer;

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
    }

    /// <summary>Sends request to change convar.</summary>
    public static void PlayerConvar(Convar i, float _value)
    {
        Message message = Message.Create(MessageSendMode.reliable, (ushort)ClientToServerId.playerConvar);

        message.Add(i.name);
        message.Add(_value);

        NetworkManager.Singleton.Client.Send(message);
    }

    /// <summary> Client autoritative - Sends cars transform to the server.</summary>
    public static void CarTransform(Transform transform)
    {
        Message message = Message.Create(MessageSendMode.unreliable, (ushort)ClientToServerId.carTransfortmToServer);

        message.Add(transform.position);
        message.Add(transform.rotation);

        NetworkManager.Singleton.Client.Send(message);
    }
}
