using System.Collections.Generic;
using UnityEngine;
using RiptideNetworking;

namespace Multiplayer
{
    /// <summary> NetworkedEntityType </summary>
    public enum NetworkedObjectType : byte
    {
        player = 1,
        enemy,
        undefined,
    }

    /// <summary> Represents networked entities - categorizes them, spawns them, moves them. </summary>
    public class NetworkedEntity : MonoBehaviour
    {
        // List of the objects for the certain networked type 
        public static Dictionary<ushort, Player> playerList { get; private set; } = new Dictionary<ushort, Player>();
        public static Dictionary<ushort, Enemy> enemyList { get; private set; } = new Dictionary<ushort, Enemy>();
        public static Dictionary<ushort, NetworkedEntity> undefinedList { get; private set; } = new Dictionary<ushort, NetworkedEntity>();

        /// <summary> Networked type  </summary> 
        public NetworkedObjectType networkedObjectType;

        /// <summary> The id of the object in the list </summary> 
        public ushort id;

        // Sends all clients to spawn object
        protected virtual void SendSpawn()
        {
            Message message = Message.Create(MessageSendMode.reliable, (ushort)ServerToClientId.spawn);
            message.Add((byte)networkedObjectType);
            message.Add(id);
            message.Add(transform.position);
            NetworkManager.Singleton.Server.SendToAll(message);
        }

        // Sends certain client to spawn object
        public virtual void SendSpawn(ushort toClient)
        {
            Message message = Message.Create(MessageSendMode.reliable, (ushort)ServerToClientId.spawn);
            message.Add((byte)networkedObjectType);
            message.Add(id);
            message.Add(transform.position);
            NetworkManager.Singleton.Server.Send(message, toClient);
        }

        // Moves and rotates object
        public virtual Message SetTransform(ref Message message)
        {
            message.Add((byte)networkedObjectType);
            message.Add(id);
            message.Add(transform.position);
            message.Add(transform.rotation);
            message.Add(NetworkManager.Singleton.tick);
            message.Add(Time.unscaledTime);
            return message;
        }

        /// <summary> Sends players snapshot to clients </summary>
        public Message SendSnapshot(Message message)
        {
            message.Add(id);
            message.Add(transform.position);
            message.Add(transform.rotation);
            message.Add(Time.time);
            return message;
        }
    }
}