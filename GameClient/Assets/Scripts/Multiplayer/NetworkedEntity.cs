using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using RiptideNetworking;

/// <summary> NetworkedEntityType </summary>
public enum NetworkedObjectType : byte
{
    player = 1,
    enemy,
    undefined,
}

/// <summary> Represents networked entities - categorizes them, spawns them, moves them. </summary>
/// <typeparam name="NetworkedObject">Class of the networked type</typeparam>
public class NetworkedEntity<NetworkedObject> : MonoBehaviour 
    where NetworkedObject: NetworkedEntity<NetworkedObject> 
{
    /// <summary> List for the objects of the certain networked type </summary>
    public static Dictionary<ushort, NetworkedObject> list { get; private set; } = new Dictionary<ushort, NetworkedObject>();
    /// <summary> Networked type  </summary> 
    public NetworkedObjectType networkedObjectType;
    /// <summary> The id of the object in the list </summary> 
    public ushort id;

    public Interpolation interpolation;
    public Interpolation cameraInterpolation;

    /// <summary> Gets the networked entity </summary> 
    /// <param name="networkedType"> Networked object type or type of the list </param>
    /// <param name="id"> The id of the object in the list </param>
    /// <returns> Returns networked object from a certain list </returns>
    public static T GetNetworkedEntity<T>(byte networkedType, ushort id) where T : NetworkedEntity<NetworkedObject>
    {
        switch (networkedType)
        {
            case (byte)NetworkedObjectType.player:
                Player.list.TryGetValue(id, out Player player);
                return player as T;
            case (byte)NetworkedObjectType.enemy:
                Enemy.list.TryGetValue(id, out Enemy enemy);
                return enemy as T;
            case (byte)NetworkedObjectType.undefined:
            default:
                list.TryGetValue(id, out NetworkedObject networkedObject);
                return networkedObject as T;
        }
    }

    // Moves and rotates object
    public static void SetTransform(Message message)
    {
        // Find object
        ushort id = message.GetUShort();

        if (list.TryGetValue(id, out NetworkedObject networkedObject))
        {
            // Move object
            networkedObject.MoveTrans(message);
        }
    }

    public virtual void MoveTrans(Message message)
    {
        Vector3 position = message.GetVector3();
        Quaternion rotation = message.GetQuaternion();
        int serverTick = message.GetInt();
        float time = message.GetFloat();

        if (serverTick > GlobalVariables.serverTick)
            GlobalVariables.serverTick = serverTick; 

        if(interpolation == null)
        {
            transform.position = position;
            transform.rotation = rotation;
            return;
        }

        switch (interpolation.implementation)
        {
            case Interpolation.InterpolationImplemenation.notAGoodUsername:
                interpolation.NewUpdate(serverTick, position, rotation);
                break;
            case Interpolation.InterpolationImplemenation.alex:
                interpolation.snapshotStDev.ServerSnapshot(position, rotation, time);
                //interpolation.snapshotStDev.ServerSnapshot(position, rotation);
                break;
        }

        if (cameraInterpolation) cameraInterpolation.NewUpdate(serverTick, rotation);
    }

    protected void Destroy()
    {
        Destroy(gameObject);
    }

    protected void OnDestroy()
    {
        list.Remove(id);
    }
}

