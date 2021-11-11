using UnityEngine;
using System.Collections.Generic;
using RiptideNetworking;

public class PlayerInput : MonoBehaviour
{
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

    struct InputCmd
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

    static Convar moveSpeed = new Convar("sv_movespeed", 6.35f, "Movement speed for the player", Flags.NETWORK);
    static Convar runAcceleration = new Convar("sv_accelerate", 14f, "Acceleration for the player when moving", Flags.NETWORK);
    static Convar airAcceleration = new Convar("sv_airaccelerate", 12f, "Air acceleration for the player", Flags.NETWORK);
    static Convar jumpForce = new Convar("sv_jumpforce", 1f, "Jump force for the player", Flags.NETWORK);
    static Convar friction = new Convar("sv_friction", 5.5f, "Player friction", Flags.NETWORK);

    static ConvarRef interp = new ConvarRef("interpolation");

    public Player playerManager;
    public Camera playerCamera;
    public Rigidbody rb;

    public GameObject groundCheck;
    public LayerMask whatIsGround;
    public float checkRadius;

    [HideInInspector]
    public Vector3 velocity = Vector3.zero;
    private bool isGrounded;

    // The maximum cache size for both the ClientInputState and SimulationState caches.
    private const int STATE_CACHE_SIZE = 1024;

    // The client's current simulation frame. 
    private int simulationFrame;
    // The last simulationFrame that we Reconciled from the server.
    private int lastCorrectedFrame;

    // The cache that stores all of the client's predicted movement reuslts. 
    private SimulationState[] simulationStateCache;
    // The cache that stores all of the client's inputs. 
    private ClientInputState[] inputStateCache;
    // The last known SimulationState provided by the server.
    private SimulationState serverSimulationState;
    // The client's current ClientInputState.
    private ClientInputState inputState;

    private ConsoleUI consoleUI;
    static LogicTimer logicTimer;


    //          *******       NEW SYSTEM                                  ************
    // The maximum cache size for SimulationStep caches 
    // also it is count of the redundant inputs sent to server
    private const int INPUT_CACHE_SIZE = 32;
    [SerializeField] int ClientTick;
    [SerializeField] int ClientLastAckedTick;
    Queue<Snapshot> ReceivedClientSnapshots;
    SimulationStep[] SimulationSteps;
    InputCmd inputCmd;


    private void Awake()
    {
        rb.freezeRotation = true;
        rb.isKinematic = true;

        lastCorrectedFrame = 0;
        simulationFrame = 0;

        serverSimulationState = new SimulationState();
        simulationStateCache = new SimulationState[STATE_CACHE_SIZE];
        inputStateCache = new ClientInputState[STATE_CACHE_SIZE];
        inputState = new ClientInputState();
    }

    void Start()
    {
        consoleUI = FindObjectOfType<ConsoleUI>();
        SimulationSteps = new SimulationStep[INPUT_CACHE_SIZE];

        //logicTimer = new LogicTimer(() => FixedTime());
        //logicTimer.Start();

        //Assign local
        Player.myId = playerManager.id;
    }

    private void FixedUpdate()
    {
        // Process inputs
        ProcessInput(inputState);
        //SecondProcessInput(inputState);

        // Send inputs so the server can process them
        SendInputToServer();

        // Reconciliate if there's a message from the server
        if (serverSimulationState != null) Reconciliate();

        // Get current simulationState
        SimulationState simulationState =
            SimulationState.CurrentSimulationState(inputState, this);

        // Determine the cache index based on on modulus operator.
        int cacheIndex = simulationFrame % STATE_CACHE_SIZE;

        // Store the SimulationState into the simulationStateCache 
        simulationStateCache[cacheIndex] = simulationState;

        // Store the ClientInputState into the inputStateCache
        inputStateCache[cacheIndex] = inputState;

        // Move next frame
        ++simulationFrame;

        // Add position to interpolate

        if (playerManager.interpolation.implementation == Interpolation.InterpolationImplemenation.alex) playerManager.interpolation.PreviousPosition = rb.position;
        if (playerManager.interpolation.implementation == Interpolation.InterpolationImplemenation.notAGoodUsername) playerManager.interpolation.PlayerUpdate(simulationFrame, rb.position);
        if (playerManager.interpolation.implementation == Interpolation.InterpolationImplemenation.tomWeiland) playerManager.interpolation.tomWeilandInterpolation.NewUpdate(simulationFrame, rb.position);
    }

    private void Update()
    {
        // Console is open, dont move
        if (consoleUI.isActive())
        {
            inputState = new ClientInputState
            {
                tick = GlobalVariables.clientTick - Utils.timeToTicks(interp.GetValue()),
                lerpAmount = GlobalVariables.lerpAmount,
                simulationFrame = simulationFrame,
                buttons = 0,
                HorizontalAxis = 0f,
                VerticalAxis = 0f,
                rotation = playerCamera.transform.rotation,
            };
            //logicTimer.Update();
            return;
        }

        // Set correspoding buttons
        int buttons = 0;
        if (Input.GetButton("Jump"))
            buttons |= Button.Jump;
        if (Input.GetButton("Fire1"))
            buttons |= Button.Fire1;

        // Set new input
        inputState = new ClientInputState
        {
            tick = GlobalVariables.clientTick - Utils.timeToTicks(interp.GetValue()),
            lerpAmount = GlobalVariables.lerpAmount,
            simulationFrame = simulationFrame,
            buttons = buttons,
            HorizontalAxis = Input.GetAxisRaw("Horizontal"),
            VerticalAxis = Input.GetAxisRaw("Vertical"),
            rotation = playerCamera.transform.rotation,
        };

        Vector3 localVelocity = Quaternion.Euler(0 ,transform.rotation.eulerAngles.y - playerCamera.transform.rotation.eulerAngles.y ,0) * new Vector3 (velocity.x, 0, velocity.z);
        //playerManager.playerAnimation.IsFiring(Input.GetButton("Fire1"));
        //playerManager.playerAnimation.UpdateAnimatorProperties(localVelocity.x/moveSpeed.GetValue(), localVelocity.z/moveSpeed.GetValue(), isGrounded, Input.GetButton("Jump"));
        //logicTimer.Update();
    }

    //    **************   NEW SYSTEM **************
    #region New System
    private void SecondProcessInput(ClientInputState inputs)
    {
        //RotationCheck(inputs);

        rb.isKinematic = false;

        //CalculateVelocity(inputs);
        //Physics.Simulate(LogicTimer.FixedDelta);

        int stateSlot = simulationFrame % INPUT_CACHE_SIZE;

        ushort Buttons = 0;

        if (Input.GetKey(KeyCode.W)) Buttons |= BTN_FORWARD;
        if (Input.GetKey(KeyCode.S)) Buttons |= BTN_BACKWARD;
        if (Input.GetKey(KeyCode.A)) Buttons |= BTN_LEFTWARD;
        if (Input.GetKey(KeyCode.D)) Buttons |= BTN_RIGHTWARD;

        SimulationSteps[stateSlot].Input = Buttons;

        SetStateAndRollback(ref SimulationSteps[stateSlot], rb);

        playerManager.interpolation.PreviousPosition = SimulationSteps[stateSlot].Position;

        //SendInputCommand();

        //++ClientTick;
    }

    public void SendInputCommand()
    {
        Message message = Message.Create(MessageSendMode.unreliable, (ushort)ClientToServerId.inputCommand);

        message.Add(ClientLastAckedTick);
        inputCmd.Inputs = new List<Inputs>();

        for (int tick = ClientLastAckedTick; tick <= ClientTick; ++tick)
            inputCmd.Inputs.Add(SimulationSteps[tick % INPUT_CACHE_SIZE].Input);

        ushort countOfCommands = (ushort)inputCmd.Inputs.Count;
        message.Add(countOfCommands);

        foreach (Inputs input in inputCmd.Inputs)
        {
            message.Add(input.buttons);
        }

        NetworkManager.Singleton.Client.Send(message);

        DebugScreen.packetsUp++;
        DebugScreen.bytesUp += message.WrittenLength;
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

    void SetStateAndRollback(ref SimulationStep state, Rigidbody _rb)
    {
        state.Position = _rb.position;
        state.Rotation = _rb.rotation;

        MoveLocalEntity(_rb, state.Input);
        Physics.Simulate(Time.fixedDeltaTime);
    }
    #endregion

    #region Old System
    private void ProcessInput(ClientInputState inputs)
    {


        RotationCheck(inputs);

        rb.isKinematic = false;
        rb.velocity = velocity;

        CalculateVelocity(inputs);
        Physics.Simulate(LogicTimer.FixedDelta);

        velocity = rb.velocity;
        rb.isKinematic = true;
    }

    // Normalizes rotation
    private void RotationCheck(ClientInputState inputs)
    {
        inputs.rotation.Normalize();
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

    private void SendInputToServer()
    {
        SendMessages.PlayerInput(inputState);
    }

    private void OnApplicationQuit()
    {
        //logicTimer.Stop();
    }

    private void setPlayerToSimulationState(SimulationState state)
    {
        transform.position = state.position;
        velocity = state.velocity;
    }

    public void Reconciliate()
    {
        // -----------------------------------------------------------//
        //          DELETE THIS         OR      COMMENT            //
        /*
        if (ReceivedClientSnapshots.Count > 0 && Time.time >= ReceivedClientSnapshots.Peek().DeliveryTime)
        {
            Snapshot snapshot = ReceivedClientSnapshots.Dequeue();

            while (ReceivedClientSnapshots.Count > 0 && Time.time >= ReceivedClientSnapshots.Peek().DeliveryTime)
                snapshot = ReceivedClientSnapshots.Dequeue();

            ClientLastAckedTick = snapshot.Tick;
            rb.position = snapshot.Position;
            rb.rotation = snapshot.Rotation;
            rb.velocity = snapshot.Velocity;
            rb.angularVelocity = snapshot.AngularVelocity;

            Debug.Log("REWIND " + snapshot.Tick + " (rewinding " + (ClientTick - snapshot.Tick) + " ticks)");

            int TicksToRewind = snapshot.Tick;

            while (TicksToRewind < ClientTick)
            {
                int rewindTick = TicksToRewind % INPUT_CACHE_SIZE;
                SetStateAndRollback(ref SimulationSteps[rewindTick], rb);
                ++TicksToRewind;
            }
        }
        */
        // ------------------------------------------------------------   //


        // Sanity check, don't reconciliate for old states.
        if (serverSimulationState.simulationFrame <= lastCorrectedFrame) return;

        // Determine the cache index 
        int cacheIndex = serverSimulationState.simulationFrame % STATE_CACHE_SIZE;

        // Obtain the cached input and simulation states.
        ClientInputState cachedInputState = inputStateCache[cacheIndex];
        SimulationState cachedSimulationState = simulationStateCache[cacheIndex];

        // If there's missing cache data for either input or simulation 
        // snap the player's position to match the server.
        if (cachedInputState == null || cachedSimulationState == null)
        {
            setPlayerToSimulationState(serverSimulationState);

            // Set the last corrected frame to equal the server's frame.
            lastCorrectedFrame = serverSimulationState.simulationFrame;
            return;
        }

        // If the simulation time isnt equal to the serve time then return
        // this should never happen
        if (cachedInputState.simulationFrame != serverSimulationState.simulationFrame || cachedSimulationState.simulationFrame != serverSimulationState.simulationFrame)
            return;

        // Find the difference between the vector's values. 
        Vector3 difference = cachedSimulationState.position - serverSimulationState.position;

        //  The amount of distance in units that we will allow the client's
        //  prediction to drift from it's position on the server, before a
        //  correction is necessary. 
        float tolerance = 0.1f;

        // A correction is necessary.
        if (difference.sqrMagnitude > tolerance)
        {
            // Show warning about misprediction
            Debug.LogWarning("Client misprediction with a difference of " + difference + " at frame " + serverSimulationState.simulationFrame + ".");
            DebugScreen.mispredictions++;

            // Set the player's position to match the server's state. 
            setPlayerToSimulationState(serverSimulationState);

            // Declare the rewindFrame as we're about to resimulate our cached inputs. 
            int rewindFrame = serverSimulationState.simulationFrame;

            // Loop through and apply cached inputs until we're 
            // caught up to our current simulation frame. 
            while (rewindFrame < simulationFrame)
            {
                // Determine the cache index 
                int rewindCacheIndex = rewindFrame % STATE_CACHE_SIZE;

                // Obtain the cached input and simulation states.
                ClientInputState rewindCachedInputState = inputStateCache[rewindCacheIndex];
                SimulationState rewindCachedSimulationState = simulationStateCache[rewindCacheIndex];

                // If there's no state to simulate, for whatever reason, 
                // increment the rewindFrame and continue.
                if (rewindCachedInputState == null || rewindCachedSimulationState == null)
                {
                    ++rewindFrame;
                    continue;
                }

                // Process the cached inputs. 
                ProcessInput(rewindCachedInputState);
                //SecondProcessInput(rewindCachedInputState);

                // Replace the simulationStateCache index with the new value.
                SimulationState rewoundSimulationState = SimulationState.CurrentSimulationState(rewindCachedInputState, this);
                rewoundSimulationState.simulationFrame = rewindFrame;
                simulationStateCache[rewindCacheIndex] = rewoundSimulationState;

                // Increase the amount of frames that we've rewound.
                ++rewindFrame;
            }
        }

        // Once we're complete, update the lastCorrectedFrame to match.
        // NOTE: Set this even if there's no correction to be made. 
        lastCorrectedFrame = serverSimulationState.simulationFrame;
    }

    // We received a new simualtion state, overwrite it
    public void OnServerSimulationStateReceived(SimulationState simulationState)
    {
        if (serverSimulationState?.simulationFrame < simulationState.simulationFrame)
            serverSimulationState = simulationState;
    }
}