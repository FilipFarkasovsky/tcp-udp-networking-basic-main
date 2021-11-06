using RiptideNetworking;
using RiptideNetworking.Transports.RudpTransport;
using System;
using UnityEngine;

public enum ServerToClientId : ushort
{
    spawnPlayer = 1,
    playerPosition,
    playerRotation,
    playerTransform,
    playerAnimation,
    serverSimulationState,
    serverConvar,
    serverTick,
}
public enum ClientToServerId : ushort
{
    playerName = 1,
    playerInput,
    playerConvar,
}


public class NetworkManager : MonoBehaviour
{
    private static NetworkManager _singleton;
    public static NetworkManager Singleton
    {
        get => _singleton;
        private set
        {
            if (_singleton == null)
                _singleton = value;
            else if (_singleton != value)
            {
                Debug.Log($"{nameof(NetworkManager)} instance already exists, destroying object!");
                Destroy(value);
            }
        }
    }

    public string ip;
    public ushort port;

    [SerializeField] private GameObject localPlayerPrefab;
    [SerializeField] private GameObject playerPrefab;

    public static Convar tickrate = new Convar("sv_tickrate", 32, "Ticks per second", Flags.NETWORK, 1, 128);
    public GameObject LocalPlayerPrefab => localPlayerPrefab;
    public GameObject PlayerPrefab => playerPrefab;

    public Client Client { get; private set; }
    private static LogicTimer logicTimer;

    private void Awake()
    {
        Singleton = this;
    }

    private void Start()
    {
        RiptideLogger.Initialize(Debug.Log, false);

        //logicTimer = new LogicTimer(() => FixedTime());
        //logicTimer.Start();

        Client = new Client(new RudpClient());

        Client.Connected += DidConnect;
        Client.ConnectionFailed += FailedToConnect;
        Client.ClientDisconnected += PlayerLeft;
        Client.Disconnected += DidDisconnect;
    }

    private void Update(){
        //logicTimer.Update();
    }

    private void FixedUpdate()
    {
        Client.Tick();
    }

    private void OnApplicationQuit()
    {
        Client.Disconnect();

        Client.Connected -= DidConnect;
        Client.ConnectionFailed -= FailedToConnect;
        Client.ClientDisconnected -= PlayerLeft;
        Client.Disconnected -= DidDisconnect;
    }

    public void Connect()
    {
        Client.Connect($"{ip}:{port}");
    }

    private void DidConnect(object sender, EventArgs e)
    {
        UIManager.Singleton.SendName();
    }

    private void FailedToConnect(object sender, EventArgs e)
    {
        UIManager.Singleton.BackToMain();
    }

    private void PlayerLeft(object sender, ClientDisconnectedEventArgs e)
    {
        Destroy(Player.list[e.Id].gameObject);
    }

    private void DidDisconnect(object sender, EventArgs e)
    {
        Destroy(Player.list[Client.Id].gameObject);
        UIManager.Singleton.BackToMain();
        
    }
}
