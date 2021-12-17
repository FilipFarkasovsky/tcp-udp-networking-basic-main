using RiptideNetworking;
using RiptideNetworking.Transports.RudpTransport;
using System;
using UnityEngine;

public enum ServerToClientId : ushort
{
    spawnObject = 1,
    setTransform,
    playerAnimation,
    serverSimulationState,
    serverConvar,
    serverTick,
    clientSnapshot = 101,


    serverSnapshot = 600,
}
public enum ClientToServerId : ushort
{
    playerName = 1,
    playerInput,
    playerConvar,
    inputCommand = 101,
    carTransfortmToServer = 200,
}

/// <summary> Main core of the networking - conection handling, tick counting, spawning, disconnecting</summary>
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

    public static Convar tickrate = new Convar("sv_tickrate", 32, "Ticks per second", Flags.NETWORK, 1, 128);

    [SerializeField] private GameObject localPlayerPrefab;
    public GameObject LocalPlayerPrefab => localPlayerPrefab;

    [SerializeField] private GameObject playerPrefab;
    public GameObject PlayerPrefab => playerPrefab;

    [SerializeField] private GameObject enemyPrefab;
    public GameObject EnemyPrefab => enemyPrefab;

    public Client Client { get; private set; }
    private static LogicTimer logicTimer;

    private void Awake()
    {
        Application.runInBackground = true;
        Singleton = this;
    }

    private void Start()
    {
        RiptideLogger.Initialize(Debug.Log, false);

        Client = new Client(new RudpClient());

        Client.Connected += DidConnect;
        Client.ConnectionFailed += FailedToConnect;
        Client.ClientDisconnected += PlayerLeft;
        Client.Disconnected += DidDisconnect;
    }

    private void Update()
    {
        // For high FPS clients  [Calculates lerpAmount and clientTick]
        //if(DebugScreen.framesPerSec > tickrate.GetValue()) LerpManager.Update();
        LerpManager.Update();
    }

    private void FixedUpdate()
    {
        // For low FPS clients  [Calculates lerpAmount and clientTick]
        // if (DebugScreen.framesPerSec <= tickrate.GetValue()) LerpManager.FixedUpdate();

        // Execute networking operations (handled messages etc.)
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
