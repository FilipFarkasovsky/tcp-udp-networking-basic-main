using System.Collections.Generic;
using UnityEngine;

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

        public Interpolation interpolation;
        public Interpolation cameraInterpolation;

        private void Start()
        {
            if(networkedObjectType == NetworkedObjectType.undefined)
            {
                undefinedList.Add(id, this);
            }
        }

        /// <summary> Gets the networked entity </summary> 
        /// <param name="networkedType"> Networked object type or type of the list </param>
        /// <param name="id"> The id of the object in the list </param>
        /// <returns> Returns networked object from a certain list </returns>
        public static bool GetNetworkedEntity(byte networkedType, ushort id, out NetworkedEntity networkedEntity)
        {
            switch (networkedType)
            {
                case (byte)NetworkedObjectType.player:
                    playerList.TryGetValue(id, out Player player);
                    return networkedEntity = player;
                case (byte)NetworkedObjectType.enemy:
                    enemyList.TryGetValue(id, out Enemy enemy);
                    return networkedEntity = enemy;
                case (byte)NetworkedObjectType.undefined:
                    undefinedList.TryGetValue(id, out NetworkedEntity networkedObject);
                    return networkedEntity = networkedObject;
                default:
                    networkedEntity = null;
                    return false;
            }
        }

        /// <summary> Moves entity - parameters are in the message </summary> 
        public virtual void MoveTrans(Vector3 position, Quaternion rotation, int serverTick, float time)
        {
            if (serverTick > GlobalVariables.serverTick)
                GlobalVariables.serverTick = serverTick;

            if (interpolation == null)
            {
                transform.position = position;
                transform.rotation = rotation;
                return;
            }

            interpolation.NewUpdate(serverTick, position, rotation);

            if (cameraInterpolation) cameraInterpolation.NewUpdate(serverTick, rotation);
        }

        public static void UndefinedSpawn(ushort id, Vector3 position)
        {
            NetworkedEntity undefined = Instantiate(NetworkManager.Singleton.UndefinedPrefab, position, Quaternion.identity).GetComponent<NetworkedEntity>();
            Debug.Log($"Spawning undefined with id {id}");

            undefined.name = $"Undefined {id}";
            undefined.id = id;
            undefinedList.Add(id, undefined);
        }
    }
}
