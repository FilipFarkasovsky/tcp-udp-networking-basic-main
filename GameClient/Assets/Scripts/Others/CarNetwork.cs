using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using RiptideNetworking;

public class CarNetwork : MonoBehaviour
{
    /// <summary>Sends cars transform to the server.</summary>
    public static void CarTransform(Transform transform)
    {
        Message message = Message.Create(MessageSendMode.unreliable, (ushort)ClientToServerId.carTransfortmToServer);

        message.Add(transform.position);
        message.Add(transform.rotation);

        NetworkManager.Singleton.Client.Send(message);
    }

    void FixedUpdate()
    {
        CarTransform(transform);
    }
}
