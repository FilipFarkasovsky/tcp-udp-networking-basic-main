using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using RiptideNetworking;

/// <summary> Represents networked entities - categorizes them, spawns them, moves them. </summary>
/// <typeparam name="NetworkedObject">Class of the networked type</typeparam>
public abstract class NetworkedEntity<NetworkedObject> : MonoBehaviour where NetworkedObject: NetworkedEntity<NetworkedObject> 
{
    // List of of objects of the certain networked 
    public static Dictionary<ushort, NetworkedObject> list { get; private set; } = new Dictionary<ushort, NetworkedObject>();

    // Networked type 
    public abstract byte GetNetworkedObjectType { get; set; }

    // The id of the object in the list
    public abstract ushort Id { get; }

    public Interpolation interpolation;
    public Interpolation cameraInterpolation;

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
        list.Remove(Id);
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
