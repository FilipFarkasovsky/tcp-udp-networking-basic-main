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

    [SerializeField] GameObject ClientSimObject;
    [SerializeField] GameObject SmoothObject;
    [SerializeField] Transform CameraTransform;

    [SerializeField] int ClientTick;
    [SerializeField] int ClientLastAckedTick;

    public Player playerManager;

    Queue<Snapshot> ReceivedClientSnapshots;

    SimulationStep[] SimulationSteps;

    InputCmd inputCmd;

    public Rigidbody ClientRb;

    [SerializeField] float RotationSpeed = 90;

    float FixedStepAccumulator;

    public Vector3 PreviousPosition;

    void Start()
    {
        Player.myId = playerManager.id;

        ClientRb.isKinematic = false;

        Physics.autoSimulation = false;

        ReceivedClientSnapshots = new Queue<Snapshot>();

        SimulationSteps = new SimulationStep[BufferLength];

        Cursor.lockState = CursorLockMode.Locked;
    }

    void Update()
    {
        FixedStepAccumulator += Time.deltaTime;

        while (FixedStepAccumulator >= Time.fixedDeltaTime)
        {
            FixedStepAccumulator -= Time.fixedDeltaTime;

            //ClientUpdate();
        }

        float _alpha = Mathf.Clamp01(FixedStepAccumulator / Time.fixedDeltaTime);

        SmoothObject.transform.position = Vector3.Lerp(PreviousPosition, ClientSimObject.transform.position, _alpha);

        //CamRotation += Input.GetAxisRaw("Mouse X") * RotationSpeed;
        //CameraTransform.position = SmoothObject.transform.position;
        //CameraTransform.rotation = Quaternion.Euler(0, CamRotation, 0);


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

        SendInputCommand();

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

        rb.velocity = direction.normalized * 3f;
    }

    void SetStateAndRollback(ref SimulationStep state, Rigidbody _rb)
    {
        state.Position = _rb.position;
        state.Rotation = _rb.rotation;

        MoveLocalEntity(_rb, state.Input);
        Physics.Simulate(Time.fixedDeltaTime);
    }

    private void OnGUI()
    {
        //GUI.Box(new Rect(5f, 5f, 180f, 25f), $"STORED COMMANDS {inputCmd.Inputs?.Count}");
        //GUI.Box(new Rect(5f, 35f, 180f, 25f), $"LAST TICK {ClientLastAckedTick}");
        //GUI.Box(new Rect(5f, 65f, 180f, 25f), $"PREDICTED TICK {ClientTick}");
        //GUI.Box(new Rect(5f, 95f, 180f, 25f), $"FPS {fps}");
    }

    public void SendInputCommand()
    {
        Message message = Message.Create(MessageSendMode.unreliable, (ushort)ClientToServerId.inputCommand);

        message.Add(ClientLastAckedTick);
        inputCmd.Inputs = new List<Inputs>();

        for (int tick = ClientLastAckedTick; tick <= ClientTick; ++tick)
            inputCmd.Inputs.Add(SimulationSteps[tick % BufferLength].Input);

        ushort countOfCommands = (ushort)inputCmd.Inputs.Count;
        message.Add(countOfCommands);

        //Debug.Log(countOfCommands);
        //Debug.Log(ClientLastAckedTick);
        //Debug.Log(ClientTick);

        foreach (Inputs input in inputCmd.Inputs)
        {
            message.Add(input.buttons);
        }

        NetworkManager.Singleton.Client.Send(message);

        DebugScreen.packetsUp++;
        DebugScreen.bytesUp += message.WrittenLength;
    }

    [MessageHandler((ushort)ServerToClientId.clientSnapshot)]
    public static void ReceiveClientSnapchot(Message message)
    {
        DebugScreen.bytesDown += message.WrittenLength;
        DebugScreen.packetsDown++;

        Snapshot snapshot;
        snapshot.DeliveryTime = Time.time;
        snapshot.Tick = message.GetInt();
        snapshot.Position = message.GetVector3();
        snapshot.Rotation = message.GetQuaternion();
        snapshot.Velocity = message.GetVector3();
        snapshot.AngularVelocity = message.GetVector3();

        if(Player.list.TryGetValue(Player.myId, out Player player))
        {
        //player.interpolation.ReceivedClientSnapshots.Enqueue(snapshot);
        }


    }
}