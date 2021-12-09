using System.Collections.Generic;
using UnityEngine;
using RiptideNetworking;

public class ClientInputState
{
    public int tick;
    public float lerpAmount;
    public int simulationFrame;

    public int buttons;

    public float HorizontalAxis;
    public float VerticalAxis;
    public Quaternion rotation;
}

public class Button
{
    public static int Jump = 1 << 0;
    public static int Fire1 = 1 << 2;
};

public class SimulationState
{
    public Vector3 position;
    public Vector3 velocity;
    public int simulationFrame;
    public static SimulationState CurrentSimulationState(ClientInputState inputState, Player player)
    {
        return new SimulationState
        {
            position = player.transform.position,
            velocity = player.velocity,
            simulationFrame = inputState.simulationFrame,
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

    public SimpleDS simpleDS;
    public PlayerMovement playerMovement;

    static Convar moveSpeed = new Convar("sv_movespeed", 6.35f, "Movement speed for the player", Flags.NETWORK);

    public GameObject head;
    public Rigidbody rb;

    public ushort id;
    public string username;
    public int tick = 0;

    [HideInInspector]
    public Vector3 velocity = Vector3.zero;

    private int lastFrame;
    private Queue<ClientInputState> clientInputs = new Queue<ClientInputState>();

    public bool isFiring;
    public float lateralSpeed;
    public float forwardSpeed;
    public bool jumping;

    // ---------------------- NEW SYSTEM ------------------------
    #region Structs

    #region INPUT SCHEMA

    public const byte BTN_FORWARD = 1 << 1;
    public const byte BTN_BACKWARD = 1 << 2;
    public const byte BTN_LEFTWARD = 1 << 3;
    public const byte BTN_RIGHTWARD = 1 << 4;

    #endregion

    public struct Inputs
    {
        public readonly ushort buttons;

        public Inputs(ushort value) : this() => buttons = value;

        public bool IsUp(ushort button) => IsDown(button) == false;

        public bool IsDown(ushort button) => (buttons & button) == button;

        public static implicit operator Inputs(ushort value) => new Inputs(value);
    }

    public struct InputCmd
    {
        public float DeliveryTime;
        public int LastAckedTick;
        public List<Inputs> Inputs;
    }

    struct SimulationStep
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public Inputs Input;
    }

    struct Snapshot
    {
        public float DeliveryTime;
        public int Tick;
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Velocity;
        public Vector3 AngularVelocity;
    }

    #endregion

    //-----------
    public static void Spawn(ushort id, string username)
    { 
        Player player = Instantiate(NetworkManager.Singleton.PlayerPrefab, new Vector3(0f, 1f, 0f), Quaternion.identity).GetComponent<Player>();
        player.name = $"Player {id} ({(username == "" ? "Guest" : username)})";
        player.id = id;
        player.username = username;
        player.simpleDS.ID = id;

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
        Debug.Log(id);
        message.Add(username);
        message.Add(transform.position);
        NetworkManager.Singleton.Server.Send(message, toClient);
    }

    private void Awake()
    {
        rb.freezeRotation = true;
        rb.isKinematic = true;
        lastFrame = 0;
        //logicTimer = new LogicTimer(() => FixedTime());
    }


    private void Start()
    {
        playerMovement.SetTransformAndRigidbody(transform, rb);
        //logicTimer.Start();
    }

    void FixedUpdate()
    {
        ProcessInputs();
        SendMessages.SetTransform(this);
        SendMessages.PlayerAnimation(this);
    }

    #region New System
    private void SecondProcessInput(ClientInputState inputs)
    {
        //RotationCheck(inputs);

        rb.isKinematic = false;

        Vector3 direction = default;

        if (inputs.VerticalAxis == 1) { direction += transform.forward; }
        else if (inputs.VerticalAxis == -1) { direction -= transform.forward; }
        if (inputs.HorizontalAxis == 1) { direction += transform.right; }
        else if (inputs.HorizontalAxis == -1) { direction -= transform.right; }

        rb.velocity = direction.normalized * 3f;
        Physics.Simulate(Time.fixedDeltaTime);
    }

    void MoveLocalEntity(Rigidbody rb, Inputs input)
    {
        Vector3 direction = default;

        if (input.IsDown(BTN_FORWARD)) direction += transform.forward;
        if (input.IsDown(BTN_BACKWARD)) direction -= transform.forward;
        if (input.IsDown(BTN_LEFTWARD)) direction -= transform.right;
        if (input.IsDown(BTN_RIGHTWARD)) direction += transform.right;

        rb.velocity = direction.normalized * 3f;
    }
    #endregion

    #region Old System
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

        playerMovement.CalculateVelocity(inputs);
        Physics.Simulate(LogicTimer.FixedDelta);

        velocity = rb.velocity;
        rb.isKinematic = true;
    }
    #endregion

    public void AddInput(ClientInputState _inputState)
    {
        clientInputs.Enqueue(_inputState);
    }
}
