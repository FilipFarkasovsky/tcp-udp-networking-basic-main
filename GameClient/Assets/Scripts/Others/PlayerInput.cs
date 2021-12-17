using UnityEngine;
using System.Collections.Generic;
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
}

public class SimulationState
{
    public int simulationFrame;
    public Vector3 position;
    public Vector3 velocity;
    public Quaternion rotation;
    public Vector3 angularVelocity;
    public static SimulationState CurrentSimulationState(ClientInputState inputState, PlayerInput player)
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

/// <summary>
/// Processes input, sends input, reconciliates, makes client prediction, controls interpolation of local player
/// </summary>
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

    #region newSystem
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

    #endregion

    static ConvarRef interp = new ConvarRef("interpolation");

    public Player playerManager;
    public PlayerMovement playerMovement;
    public Camera playerCamera;
    public Rigidbody rb;

    [HideInInspector]
    public Vector3 velocity = Vector3.zero;
    public Vector3 angularVelocity = Vector3.zero;

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
    //private const int INPUT_CACHE_SIZE = 32;
    //[SerializeField] int ClientTick;
    //[SerializeField] int ClientLastAckedTick;
    //Queue<Snapshot> ReceivedClientSnapshots;
    //SimulationStep[] SimulationSteps;
    //InputCmd inputCmd;
    //bool vsyncToggle;


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
        //SimulationSteps = new SimulationStep[INPUT_CACHE_SIZE];

        //logicTimer = new LogicTimer(() => FixedTime());
        //logicTimer.Start();

        //Assign id of local player
        Player.myId = playerManager.id;

        playerMovement.SetTransformAndRigidbody(transform, rb);
    }

    private void FixedUpdate()
    {
        // Process inputs
        ProcessInput(inputState);
        //SecondProcessInput(inputState);


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

        // Send inputs so the server can process them
        SendInputToServer();

        // Move next frame
        ++simulationFrame;

        // Add position to interpolate

        if (playerManager.interpolation.implementation == Interpolation.InterpolationImplemenation.alex) playerManager.interpolation.PreviousPosition = rb.position;
        if (playerManager.interpolation.implementation == Interpolation.InterpolationImplemenation.notAGoodUsername) playerManager.interpolation.PlayerUpdate(simulationFrame, rb.position);
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

    private void OnGUI()
    {
        GUI.Box(new Rect(5f, 5f, 180f, 25f), $"Lastframe {lastCorrectedFrame} + {simulationFrame}");
    }

    //    **************   NEW SYSTEM **************
    #region New System
    //private void SecondProcessInput(ClientInputState inputs)
    //{
    //    //RotationCheck(inputs);

    //    rb.isKinematic = false;

    //    //CalculateVelocity(inputs);
    //    //Physics.Simulate(LogicTimer.FixedDelta);

    //    int stateSlot = simulationFrame % INPUT_CACHE_SIZE;

    //    ushort Buttons = 0;

    //    if (Input.GetKey(KeyCode.W)) Buttons |= BTN_FORWARD;
    //    if (Input.GetKey(KeyCode.S)) Buttons |= BTN_BACKWARD;
    //    if (Input.GetKey(KeyCode.A)) Buttons |= BTN_LEFTWARD;
    //    if (Input.GetKey(KeyCode.D)) Buttons |= BTN_RIGHTWARD;

    //    SimulationSteps[stateSlot].Input = Buttons;

    //    SetStateAndRollback(ref SimulationSteps[stateSlot], rb);

    //    playerManager.interpolation.PreviousPosition = SimulationSteps[stateSlot].Position;

    //    //SendInputCommand();

    //    //++ClientTick;
    //}

    // Redundant packages
    //public void SendInputCommand()
    //{
    //    Message message = Message.Create(MessageSendMode.unreliable, (ushort)ClientToServerId.inputCommand);

    //    message.Add(lastCorrectedFrame);

    //    inputCmd.Inputs = new List<Inputs>();

    //    for (int tick = ClientLastAckedTick; tick <= ClientTick; ++tick)
    //        inputCmd.Inputs.Add(SimulationSteps[tick % INPUT_CACHE_SIZE].Input);

    //    ushort countOfCommands = (ushort)inputCmd.Inputs.Count;
    //    message.Add(countOfCommands);

    //    foreach (Inputs input in inputCmd.Inputs)
    //    {
    //        message.Add(input.buttons);
    //    }

    //    NetworkManager.Singleton.Client.Send(message);
    //}

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

    public void ProcessInput(ClientInputState inputs)
    {
        playerMovement.RotationCheck(inputs);

        rb.isKinematic = false;
        rb.velocity = velocity;
        //rb.angularVelocity = velocity;

        playerMovement.CalculateVelocity(inputs);
        Physics.Simulate(LogicTimer.FixedDelta);

        //angularVelocity = rb.velocity;
        velocity = rb.velocity;
        rb.isKinematic = true;
    }

    private void SendInputToServer()
    {
        SendMessages.PlayerInput(inputState);
        // We send all inputs that havent been acked
        for (int frameToSend = lastCorrectedFrame + 1; frameToSend <= serverSimulationState.simulationFrame; frameToSend++)
        {
            // Determine the cache index 
            int cacheIndex = frameToSend % STATE_CACHE_SIZE;

            Debug.Log("Sending input");

            // Obtain the cached input and simulation states.
            ClientInputState cachedInputState = inputStateCache[cacheIndex];

            if (cachedInputState != null) SendMessages.PlayerInput(cachedInputState);
        }
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

        // If the simulation time isnt equal to the server time then return
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