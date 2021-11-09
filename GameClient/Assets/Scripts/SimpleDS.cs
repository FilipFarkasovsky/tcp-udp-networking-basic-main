using UnityEngine;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using RiptideNetworking;

public class SimpleDS : MonoBehaviour
{

    #region TODO

    //Fix rotation jitter
    //Add remote interpolation handling
    //Rewrite snapshot structure for remote players

    #endregion

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

    const int BufferLength = 32;

    [SerializeField, Range(0, 1)] float RTT;
    [SerializeField, Range(0, 1)] float PACKET_LOSS;

    [SerializeField] GameObject ClientSimObject;
    [SerializeField] GameObject ServerSimObject;
    [SerializeField] GameObject SmoothObject;
    [SerializeField] Transform CameraTransform;

    [SerializeField] int ClientTick;
    [SerializeField] int ClientLastAckedTick;
    [SerializeField] int ServerTick;

    Queue<Snapshot> ReceivedClientSnapshots;

    Queue<InputCmd> ReceivedServerInputs;

    SimulationStep[] SimulationSteps;

    LoadSceneParameters sceneParams = new LoadSceneParameters(LoadSceneMode.Additive, LocalPhysicsMode.Physics3D);

    Scene ServerScene, ClientScene;
    PhysicsScene ServerPhysics, ClientPhysics;

    InputCmd inputCmd;

    Rigidbody ServerRb;
    Rigidbody ClientRb;

    [SerializeField] float RotationSpeed = 90;
    float CamRotation;

    float FixedStepAccumulator;

    Vector3 PreviousPosition;

    void Start()
    {
        Physics.autoSimulation = false;

        ReceivedServerInputs = new Queue<InputCmd>();
        ReceivedClientSnapshots = new Queue<Snapshot>();

        SimulationSteps = new SimulationStep[BufferLength];

        ServerScene = SceneManager.LoadScene("PhysicsInstance", sceneParams);
        ClientScene = SceneManager.LoadScene("PhysicsInstance", sceneParams);

        ServerPhysics = ServerScene.GetPhysicsScene();
        ClientPhysics = ClientScene.GetPhysicsScene();

        SceneManager.MoveGameObjectToScene(ServerSimObject, ServerScene);
        SceneManager.MoveGameObjectToScene(ClientSimObject, ClientScene);

        ServerRb = ServerSimObject.GetComponent<Rigidbody>();
        ClientRb = ClientSimObject.GetComponent<Rigidbody>();

        Cursor.lockState = CursorLockMode.Locked;
    }

    void Update()
    {
        FixedStepAccumulator += Time.deltaTime;

        while (FixedStepAccumulator >= Time.fixedDeltaTime)
        {
            FixedStepAccumulator -= Time.fixedDeltaTime;

            ServerUpdate();
            ClientUpdate();
        }

        float _alpha = Mathf.Clamp01(FixedStepAccumulator / Time.fixedDeltaTime);

        SmoothObject.transform.position = Vector3.Lerp(PreviousPosition, ClientSimObject.transform.position, _alpha);

        CamRotation += Input.GetAxisRaw("Mouse X") * RotationSpeed;
        CameraTransform.position = SmoothObject.transform.position;
        CameraTransform.rotation = Quaternion.Euler(0, CamRotation, 0);


        if (Input.GetKeyDown(KeyCode.V))
        {
            vsyncToggle = !vsyncToggle;
            QualitySettings.vSyncCount = vsyncToggle ? 1 : 0;
        }

        if (Time.unscaledTime > _timer)
        {
            fps = (int)(1f / Time.deltaTime);
            _timer = Time.unscaledTime + 1;
        }
    }

    int fps;

    float _timer;

    bool vsyncToggle = false;

    public void ServerUpdate()
    {
        while (ReceivedServerInputs.Count > 0 && Time.time >= ReceivedServerInputs.Peek().DeliveryTime)
        {
            InputCmd inputCmd = ReceivedServerInputs.Dequeue();

            if ((inputCmd.LastAckedTick + inputCmd.Inputs.Count - 1) >= ServerTick)
            {
                for (int i = (ServerTick > inputCmd.LastAckedTick ? (ServerTick - inputCmd.LastAckedTick) : 0); i < inputCmd.Inputs.Count; ++i)
                {
                    MoveLocalEntity(ServerRb, inputCmd.Inputs[i]);
                    ServerPhysics.Simulate(Time.fixedDeltaTime);

                    ++ServerTick;

                    //if (Random.value > PACKET_LOSS)
                    //{
                    //    Snapshot snapshot;
                    //    snapshot.DeliveryTime = Time.time + RTT;
                    //    snapshot.Tick = ServerTick;
                    //    snapshot.Position = ServerRb.position;
                    //    snapshot.Rotation = ServerRb.rotation;
                    //    snapshot.Velocity = ServerRb.velocity;
                    //    snapshot.AngularVelocity = ServerRb.angularVelocity;

                    //    ReceivedClientSnapshots.Enqueue(snapshot);
                    //}
                }
            }
        }
    }



    void ClientUpdate()
    {
        int stateSlot = ClientTick % BufferLength;

        ushort Buttons = 0;

        if (Input.GetKey(KeyCode.W)) Buttons |= BTN_FORWARD;
        if (Input.GetKey(KeyCode.S)) Buttons |= BTN_BACKWARD;
        if (Input.GetKey(KeyCode.A)) Buttons |= BTN_LEFTWARD;
        if (Input.GetKey(KeyCode.D)) Buttons |= BTN_RIGHTWARD;

        SimulationSteps[stateSlot].Input = Buttons;

        SetStateAndRollback(ref SimulationSteps[stateSlot], ClientRb);

        PreviousPosition = SimulationSteps[stateSlot].Position;

        //if (Random.value > PACKET_LOSS)
        //{
        //    inputCmd.DeliveryTime = Time.time + RTT;
        //    inputCmd.LastAckedTick = ClientLastAckedTick;
        //    inputCmd.Inputs = new List<Inputs>();

        //    for (int tick = inputCmd.LastAckedTick; tick <= ClientTick; ++tick)
        //        inputCmd.Inputs.Add(SimulationSteps[tick % BufferLength].Input);

        //    ReceivedServerInputs.Enqueue(inputCmd);
        //}

        ++ClientTick;

        if (ReceivedClientSnapshots.Count > 0 && Time.time >= ReceivedClientSnapshots.Peek().DeliveryTime)
        {
            Snapshot snapshot = ReceivedClientSnapshots.Dequeue();

            while (ReceivedClientSnapshots.Count > 0 && Time.time >= ReceivedClientSnapshots.Peek().DeliveryTime)
                snapshot = ReceivedClientSnapshots.Dequeue();

            ClientLastAckedTick = snapshot.Tick;

            ClientRb.position = snapshot.Position;
            ClientRb.rotation = snapshot.Rotation;
            ClientRb.velocity = snapshot.Velocity;
            ClientRb.angularVelocity = snapshot.AngularVelocity;

            Debug.Log("REWIND " + snapshot.Tick + " (rewinding " + (ClientTick - snapshot.Tick) + " ticks)");

            int TicksToRewind = snapshot.Tick;

            while (TicksToRewind < ClientTick)
            {
                int rewindTick = TicksToRewind % BufferLength;
                SetStateAndRollback(ref SimulationSteps[rewindTick], ClientRb);
                ++TicksToRewind;
            }
        }
    }

    void MoveLocalEntity(Rigidbody rb, Inputs input)
    {
        Vector3 direction = default;

        if (input.IsDown(BTN_FORWARD)) direction += transform.forward;
        if (input.IsDown(BTN_BACKWARD)) direction -= transform.forward;
        if (input.IsDown(BTN_LEFTWARD)) direction -= transform.right;
        if (input.IsDown(BTN_RIGHTWARD)) direction += transform.right;

        rb.velocity += direction.normalized * 3f;
    }

    void SetStateAndRollback(ref SimulationStep state, Rigidbody _rb)
    {
        state.Position = _rb.position;
        state.Rotation = _rb.rotation;

        MoveLocalEntity(_rb, state.Input);
        ClientPhysics.Simulate(Time.fixedDeltaTime);
    }

    private void OnGUI()
    {
        GUI.Box(new Rect(5f, 05f, 180f, 25f), $"RTT SIMULATION {RTT * 1000f}");
        GUI.Box(new Rect(5f, 35f, 180f, 25f), $"PACKET LOSS {PACKET_LOSS * 100f} %");
        GUI.Box(new Rect(5f, 65f, 180f, 25f), $"STORED COMMANDS {inputCmd.Inputs?.Count}");
        GUI.Box(new Rect(5f, 95f, 180f, 25f), $"LAST TICK {ClientLastAckedTick}");
        GUI.Box(new Rect(5f, 125f, 180f, 25f), $"PREDICTED TICK {ClientTick}");
        GUI.Box(new Rect(5f, 155f, 180f, 25f), $"SERVER TICK {ServerTick}");
        GUI.Box(new Rect(5f, 185f, 180f, 25f), $"FPS {fps}");
    }

    public void SendInputCommand()
    {
        Message message = Message.Create(MessageSendMode.unreliable, (ushort)ClientToServerId.inputCommand);

        message.Add(ClientLastAckedTick);
        inputCmd.Inputs = new List<Inputs>();

        for (int tick = inputCmd.LastAckedTick; tick <= ClientTick; ++tick)
            inputCmd.Inputs.Add(SimulationSteps[tick % BufferLength].Input);

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

    [MessageHandler((ushort)ClientToServerId.inputCommand)]
    public void ReceiveInputCommands(Message message)
    {
        DebugScreen.bytesDown += message.WrittenLength;
        DebugScreen.packetsDown++;

        inputCmd.DeliveryTime = Time.time;
        inputCmd.LastAckedTick = message.GetInt();

        ushort countOfCommands = message.GetUShort();
        List<Inputs> list = new List<Inputs>();

        for (ushort i = 0; i < countOfCommands; i++)
        {
            list.Add((Inputs)(message.GetUShort()));
        }
        inputCmd.Inputs = list;
    }

    public void EnqueOnServer()
    {
        inputCmd.DeliveryTime = Time.time + RTT;
        inputCmd.LastAckedTick = ClientLastAckedTick;
        inputCmd.Inputs = new List<Inputs>();

        for (int tick = inputCmd.LastAckedTick; tick <= ClientTick; ++tick)
            inputCmd.Inputs.Add(SimulationSteps[tick % BufferLength].Input);

        ReceivedServerInputs.Enqueue(inputCmd);
    }

    public void SendClientSnapchot()
    {
        Message message = Message.Create(MessageSendMode.unreliable, (ushort)ServerToClientId.clientSnapshot);

        message.Add(Time.time);
        message.Add(ServerTick)

        NetworkManager.Singleton.Client.Send(message);

        DebugScreen.packetsUp++;
        DebugScreen.bytesUp += message.WrittenLength;
    }

    [MessageHandler((ushort)ServerToClientId.clientSnapshot))]
    public void ReceiveClientSnapchot(Message message)
    {
        DebugScreen.bytesDown += message.WrittenLength;
        DebugScreen.packetsDown++;

        inputCmd.DeliveryTime = Time.time;
        inputCmd.LastAckedTick = message.GetInt();

        ushort countOfCommands = message.GetUShort();
        List<Inputs> list = new List<Inputs>();

        for (ushort i = 0; i < countOfCommands; i++)
        {
            list.Add((Inputs)(message.GetUShort()));
        }
        inputCmd.Inputs = list;
    }

    public void EnqueOnClient()
    {
         Snapshot snapshot;
         snapshot.DeliveryTime = Time.time;
         snapshot.Tick = ServerTick;
         snapshot.Position = ServerRb.position;
         snapshot.Rotation = ServerRb.rotation;
         snapshot.Velocity = ServerRb.velocity;
         snapshot.AngularVelocity = ServerRb.angularVelocity;
         
         ReceivedClientSnapshots.Enqueue(snapshot);
    }
}