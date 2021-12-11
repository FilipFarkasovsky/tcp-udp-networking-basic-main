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

    [SerializeField] GameObject ServerSimObject;

    [SerializeField] int ServerTick;

    public Queue<InputCmd> ReceivedServerInputs;

    public InputCmd inputCmd;

    public Rigidbody ServerRb;
    float FixedStepAccumulator;

    public ushort ID;

    void Start()
    {
        Physics.autoSimulation = false;

        ReceivedServerInputs = new Queue<InputCmd>();

        ServerRb.isKinematic = false;

        Cursor.lockState = CursorLockMode.Locked;
    }

    void Update()
    {
        FixedStepAccumulator += Time.deltaTime;

        while (FixedStepAccumulator >= Time.fixedDeltaTime)
        {
            FixedStepAccumulator -= Time.fixedDeltaTime;

            ServerUpdate();
        }
    }

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
                    Physics.Simulate(Time.fixedDeltaTime);

                    ++ServerTick;

                    SendClientSnapchot();
                }
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

    public void SendClientSnapchot()
    {
        Message message = Message.Create(MessageSendMode.unreliable, (ushort)ServerToClientId.clientSnapshot);

        message.Add(ServerTick);
        message.Add(ServerRb.position);
        message.Add(ServerRb.velocity);
        message.Add(ServerRb.rotation);
        message.Add(ServerRb.angularVelocity);


        NetworkManager.Singleton.Server.Send(message, ID);
    }
}