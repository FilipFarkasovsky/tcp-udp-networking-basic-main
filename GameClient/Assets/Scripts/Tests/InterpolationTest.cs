using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using RiptideNetworking;

public class InterpolationTest : NetworkedEntity<InterpolationTest>
{
    public override byte GetNetworkedObjectType { get; set; } = (byte)NetworkedObjectType.box;
    public override ushort Id { get => id; }

    public ushort id;

    private void Start()
    {
        list.Add(Id, this);

    }
}
