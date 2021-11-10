using System.Collections.Generic;
using UnityEngine;
using RiptideNetworking;

public class Player : MonoBehaviour
{
    public static Dictionary<ushort, Player> List { get; private set; } = new Dictionary<ushort, Player>();
    public SimpleDS simpleDS;

    static Convar moveSpeed = new Convar("sv_movespeed", 6.35f, "Movement speed for the player", Flags.NETWORK);
    static Convar runAcceleration = new Convar("sv_accelerate", 14f, "Acceleration for the player when moving", Flags.NETWORK);
    static Convar airAcceleration = new Convar("sv_airaccelerate", 12f, "Air acceleration for the player", Flags.NETWORK);
    static Convar jumpForce = new Convar("sv_jumpforce", 1f, "Jump force for the player", Flags.NETWORK);
    static Convar friction = new Convar("sv_friction", 5.5f, "Player friction", Flags.NETWORK);
    static Convar rotationBounds = new Convar("sv_maxrotation", 89f, "Maximum rotation around the x axis", Flags.NETWORK);

    public GameObject head;
    public Rigidbody rb;

    public LayerMask whatIsGround;
    public GameObject groundCheck;
    public float checkRadius;

    public ushort id;
    public string username;
    public int tick = 0;

    [HideInInspector]
    public Vector3 velocity = Vector3.zero;
    public bool isGrounded;

    private int lastFrame;
    private Queue<ClientInputState> clientInputs = new Queue<ClientInputState>();

    //static LogicTimer logicTimer;

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
    /// <summary>Sends a player's info to the given client.</summary>
    /// <param name="toClient">The client to send the message to.</param>
    public void SendSpawn(ushort toClient)
    {
        NetworkManager.Singleton.Server.Send(GetSpawnData(Message.Create(MessageSendMode.reliable, (ushort)ServerToClientId.spawnPlayer)), toClient);
    }
    /// <summary>Sends a player's info to all clients.</summary>
    private void SendSpawn()
    {
        NetworkManager.Singleton.Server.SendToAll(GetSpawnData(Message.Create(MessageSendMode.reliable, (ushort)ServerToClientId.spawnPlayer)));
    }

    private Message GetSpawnData(Message message)
    {
        message.Add(id);
        message.Add(username);
        message.Add(transform.position);
        return message;
    }


    //-----------
    private void Awake()
    {
        rb.freezeRotation = true;
        rb.isKinematic = true;
        lastFrame = 0;
        //logicTimer = new LogicTimer(() => FixedTime());
    }
    private void OnDestroy()
    {
        List.Remove(id);
    }
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
    private void Start()
    {
        //logicTimer.Start();
    }
    private void Update()
    {
        //logicTimer.Update();
    }
    public void Destroy()
    {
        //logicTimer.Stop();
        Destroy(gameObject);
    }

    
    void FixedUpdate()
    {
        ProcessInputs();
        SendMessages.PlayerTransform(this);
        SendMessages.PlayerAnimation(this);
    }

    #region New System
    private void SecondProcessInput(ClientInputState inputs)
    {
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
        RotationCheck(inputs);

        if ((inputs.buttons & Button.Fire1) == Button.Fire1)
        {
            LagCompensation.Backtrack(id, inputs.tick, inputs.lerpAmount);
        }

        rb.isKinematic = false;
        rb.velocity = velocity;

        CalculateVelocity(inputs);
        Physics.Simulate(LogicTimer.FixedDelta);

        velocity = rb.velocity;
        rb.isKinematic = true;
    }

    // Clamps and sets rotation
    private void RotationCheck(ClientInputState inputs)
    {
        // Set body y rotation
        inputs.rotation.Normalize();
        transform.rotation = new Quaternion(0f, inputs.rotation.y, 0f, inputs.rotation.w);

        // Set x rotation
        head.transform.localRotation = new Quaternion(inputs.rotation.x, 0f, 0f, inputs.rotation.w);

        // Clamp x rotation
        float angle = head.transform.localEulerAngles.x;
        angle = (angle > 180) ? angle - 360 : angle;
        angle = Mathf.Clamp(angle, -rotationBounds.GetValue(), rotationBounds.GetValue());

        // Set clamped angle and normalize
        head.transform.rotation = Quaternion.Euler(angle, head.transform.eulerAngles.y, 0f);
        head.transform.rotation.Normalize();
    }

    // Calculates player velocity with the given inputs
    private void CalculateVelocity(ClientInputState inputs)
    {
        GroundCheck();

        if (isGrounded)
            WalkMove(inputs);
        else
            AirMove(inputs);
    }
    #endregion

    #region Movement
    void GroundCheck()
    {
        // Are we touching something?
        isGrounded = Physics.CheckSphere(groundCheck.transform.position, checkRadius, whatIsGround);

        // We are touching the ground check if it is a slope
        if (isGrounded && 
            Physics.SphereCast(transform.position, checkRadius, Vector3.down, out RaycastHit hit, 100f, whatIsGround))
        {
            isGrounded = Vector3.Angle(Vector3.up, hit.normal) <= 45f;
        }
    }

    void AirMove(ClientInputState inputs)
    {
        Vector2 input = new Vector2(inputs.HorizontalAxis, inputs.VerticalAxis).normalized;

        Vector3 forward = (inputs.rotation * Vector3.forward);
        Vector3 right = (inputs.rotation * Vector3.right);

        forward.y = 0;
        right.y = 0;

        forward.Normalize();
        right.Normalize();

        Vector3 wishdir = right * input.x + forward * input.y;

        float wishspeed = wishdir.magnitude;

        AirAccelerate(wishdir, wishspeed, airAcceleration.GetValue());
    }

    void WalkMove(ClientInputState inputs)
    {
        if ((inputs.buttons & Button.Jump) == Button.Jump)
        {
            Friction(0f);
            rb.velocity += new Vector3(0f, jumpForce.GetValue(), 0f);
            AirMove(inputs);
            return;
        }
        else
            Friction(1f);

        Vector2 input = new Vector2(inputs.HorizontalAxis, inputs.VerticalAxis).normalized;

        var forward = (inputs.rotation * Vector3.forward);
        var right = (inputs.rotation * Vector3.right);

        forward.y = 0;
        right.y = 0;

        forward.Normalize();
        right.Normalize();

        Vector3 wishdir = right * input.x + forward * input.y;

        float wishspeed = wishdir.magnitude;
        wishspeed *= moveSpeed.GetValue();

        Accelerate(wishdir, wishspeed, runAcceleration.GetValue());

        if ((inputs.buttons & Button.Jump) == Button.Jump)
        {
            rb.velocity += new Vector3(0f, jumpForce.GetValue(), 0f);
        }
    }

    private void Accelerate(Vector3 wishdir, float wishspeed, float accel)
    {
        float addspeed;
        float accelspeed;
        float currentspeed;

        currentspeed = Vector3.Dot(rb.velocity, wishdir);
        addspeed = wishspeed - currentspeed;
        if (addspeed <= 0)
            return;
        accelspeed = accel * LogicTimer.FixedDelta * wishspeed;
        if (accelspeed > addspeed)
            accelspeed = addspeed;

        rb.velocity += new Vector3(accelspeed * wishdir.x, 0f, accelspeed * wishdir.z);
    }

    void AirAccelerate(Vector3 wishdir, float wishspeed, float accel)
    {
        float addspeed, accelspeed, currentspeed;

        currentspeed = Vector3.Dot(rb.velocity, wishdir);
        addspeed = wishspeed - currentspeed;
        if (addspeed <= 0)
            return;

        accelspeed = accel * wishspeed * LogicTimer.FixedDelta;

        if (accelspeed > addspeed)
            accelspeed = addspeed;

        rb.velocity += new Vector3(accelspeed * wishdir.x, 0f, accelspeed * wishdir.z);
    }

    void Friction(float t)
    {
        float speed = rb.velocity.magnitude, newspeed, control, drop;

        if (speed < 0.1f)
            return;

        drop = 0;

        if (isGrounded)
        {
            control = speed < runAcceleration.GetValue() ? runAcceleration.GetValue() : speed;
            drop += control * friction.GetValue() * LogicTimer.FixedDelta * t;
        }

        newspeed = speed - drop;
        if (newspeed < 0)
            newspeed = 0;

        newspeed /= speed;

        rb.velocity = new Vector3(rb.velocity.x * newspeed, rb.velocity.y, rb.velocity.z * newspeed);
    }
    #endregion

    public void AddInput(ClientInputState _inputState)
    {
        clientInputs.Enqueue(_inputState);
    }
}
