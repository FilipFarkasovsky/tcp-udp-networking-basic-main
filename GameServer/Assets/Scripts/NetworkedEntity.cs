using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using RiptideNetworking;

/// <summary> Represents networked entities - makes list of them, spawns them, moves them. </summary>
/// <typeparam name="NetworkedObject">Class of the networked type</typeparam>
public abstract class NetworkedEntity<NetworkedObject> : MonoBehaviour 
{
    // List of of objects of the certain networked 
    public static Dictionary<ushort, NetworkedObject> List { get; private set; } = new Dictionary<ushort, NetworkedObject>();
   
    // Networked type 
    public abstract byte GetNetworkedObjectType { get; set; }

    // The id of the object in the list
    public abstract ushort Id { get; }

    // Sends all clients to spawn object
    protected virtual void SendSpawn()
    {
        Message message = Message.Create(MessageSendMode.reliable, (ushort)ServerToClientId.spawn);
        message.Add(GetNetworkedObjectType);
        message.Add(Id);
        Debug.Log(Id);
        message.Add(transform.position);
        NetworkManager.Singleton.Server.SendToAll(message);
    }

    // Sends certain client to spawn object
    public virtual void SendSpawn(ushort toClient)
    {
        Message message = Message.Create(MessageSendMode.reliable, (ushort)ServerToClientId.spawn);
        message.Add(GetNetworkedObjectType);
        message.Add(Id);
        message.Add(transform.position);
        NetworkManager.Singleton.Server.Send(message, toClient);
    }

    // Moves and rotates object
    public virtual Message SetTransform(ref Message message)
    {
        message.Add(GetNetworkedObjectType);
        message.Add(Id);
        message.Add(transform.position);
        message.Add(transform.rotation);
        message.Add(NetworkManager.Singleton.tick);
        return message;
    }


    protected void Destroy()
    {
        Destroy(gameObject);
    }

    protected void OnDestroy()
    {
        List.Remove(Id);
    }
}

public enum NetworkedObjectType : byte
{
    player = 1,
    enemy,
    car,
    box,
    wall,
}
