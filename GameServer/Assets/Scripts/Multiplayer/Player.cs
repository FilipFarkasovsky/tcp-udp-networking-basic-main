using System.Collections.Generic;
using UnityEngine;
using RiptideNetworking;

public class ClientInputState
{
    public int tick; // Tick used for backtracking in LagCompensation - (ClientTick - interp.Getvalue)                    
    public float lerpAmount; 
    public int simulationFrame;

    public ushort buttons;

    public float HorizontalAxis;
    public float VerticalAxis;
    public Quaternion rotation;
}

public class Button
{
    public static ushort Jump = 1 << 0;
    public static ushort Fire1 = 1 << 2;
};

public class SimulationState
{
    public Vector3 position;
    public Vector3 velocity;
    public Quaternion rotation;
    public Vector3 angularVelocity;
    public int simulationFrame;
    public static SimulationState CurrentSimulationState(ClientInputState inputState, Player player)
    {
        return new SimulationState
        {
            simulationFrame = inputState.simulationFrame,
            position = player.transform.position,
            velocity = player.velocity,
            rotation = player.transform.rotation,
            angularVelocity = player.angularVelocity,
        };
    }
}

/// <summary> Responsible for spawning, tracking and making history of players </summary>
public class Player : NetworkedEntity<Player>
{
    public override byte GetNetworkedObjectType { get; set; } = (byte)NetworkedObjectType.player;
    public override ushort Id { get => id; }

    public override Message SetTransform(ref Message message)
    {
        message.Add(GetNetworkedObjectType);
        message.Add(id);
        message.Add(transform.position);
        message.Add(head.transform.rotation);
        message.Add(tick);
        return message;
    }

    public PlayerMovement playerMovement;

    static Convar moveSpeed = new Convar("sv_movespeed", 6.35f, "Movement speed for the player", Flags.NETWORK);

    public GameObject head;
    public Rigidbody rb;

    public ushort id;
    public string username;
    public int tick = 0;

    [HideInInspector]
    public Vector3 velocity = Vector3.zero;
    public Vector3 angularVelocity = Vector3.zero;

    private int lastFrame;
    private Queue<ClientInputState> clientInputs = new Queue<ClientInputState>();

    public bool isFiring;
    public float lateralSpeed;
    public float forwardSpeed;
    public bool jumping;

    public static void Spawn(ushort id, string username)
    {
        // If player with given id exists dont instantiate him 
        // This can sometimes happen, but i have not found out why or when
        if (List.ContainsKey(id))
            return;

        Player player = Instantiate(NetworkManager.Singleton.PlayerPrefab, new Vector3(-100f, 5f, 130f), Quaternion.identity).GetComponent<Player>();
        player.name = $"Player {id} ({(username == "" ? "Guest" : username)})";
        player.id = id;
        player.username = username;

        player.SendSpawn();
        List.Add(player.id, player);
    }

    /// <summary>Sends a player's info to all clients.</summary>
    protected override void SendSpawn()
    {
        Message message = Message.Create(MessageSendMode.reliable, (ushort)ServerToClientId.spawn);
        message.Add(GetNetworkedObjectType);
        message.Add(id);
        message.Add(username);
        message.Add(transform.position);
        NetworkManager.Singleton.Server.SendToAll(message);
    }

    /// <summary>Sends a player's info to the given client.</summary>
    /// <param name="toClient">The client to send the message to.</param>
    public override void SendSpawn(ushort toClient)
    {
        Message message = Message.Create(MessageSendMode.reliable, (ushort)ServerToClientId.spawn);
        message.Add(GetNetworkedObjectType);
        message.Add(id);
        message.Add(username);
        message.Add(transform.position);
        NetworkManager.Singleton.Server.Send(message, toClient);
    }

    private void Awake()
    {
        rb.freezeRotation = true;
        rb.isKinematic = true;
        lastFrame = 0;
    }


    private void Start()
    {
        playerMovement.SetTransformAndRigidbody(transform, rb);
    }

    void FixedUpdate()
    {
        //ProcessInputs();
        rb.isKinematic = true;
        rb.freezeRotation = true;
        SendMessages.SetTransform(this);
        //SendMessages.PlayerAnimation(this);
    }

    public void ProcessInputs()
    {
        // Declare the ClientInputState that we're going to be using.
        ClientInputState inputState = null;

        // Obtain CharacterInputState's from the queue. 
        while (clientInputs.Count > 0  && (inputState = clientInputs.Dequeue()) != null)
        {

            // Player is sending simulation frames that are in the past, dont process them
            if (inputState.simulationFrame <= lastFrame)
                continue;

            lastFrame = inputState.simulationFrame;

            // Process the input.
            ProcessInput(inputState);
            //SecondProcessInput(inputState);

            // Obtain the current SimulationState.
            SimulationState state = SimulationState.CurrentSimulationState(inputState, this);

            // Send the state back to the client.
            SendMessages.SendSimulationState(id, state);

            //Obtain animation data
            isFiring = (inputState.buttons & Button.Fire1) == Button.Fire1;
            Vector3 localVelocity = Quaternion.Euler(0 ,- head.transform.rotation.eulerAngles.y ,0) * new Vector3 (velocity.x, 0, velocity.z);
            lateralSpeed = localVelocity.x/moveSpeed.GetValue();
            forwardSpeed =  localVelocity.z/moveSpeed.GetValue();
            jumping = (inputState.buttons & Button.Jump) == Button.Jump;
        }
    }

    private void ProcessInput(ClientInputState inputs)
    {
        playerMovement.RotationCheck(inputs);

        if ((inputs.buttons & Button.Fire1) == Button.Fire1)
        {
            LagCompensation.Backtrack(id, inputs.tick, inputs.lerpAmount);
        }

        rb.isKinematic = false;
        rb.velocity = velocity;
        rb.angularVelocity = angularVelocity;

        playerMovement.CalculateVelocity(inputs);
        Physics.Simulate(LogicTimer.FixedDelta);

     
        velocity = rb.velocity;
        angularVelocity = rb.angularVelocity;
        rb.isKinematic = true;
    }

    public void AddInput(ClientInputState _inputState)
    {
        clientInputs.Enqueue(_inputState);
    }

    private void OnGUI()
    {
        GUI.Box(new Rect(5f, 5f, 180f, 25f), $"Lastframe {lastFrame} + {tick}");
            GUI.Box(new Rect(5f, 35f, 180f, 25f), $"Lastframe {clientInputs.Count}");
    }
}
