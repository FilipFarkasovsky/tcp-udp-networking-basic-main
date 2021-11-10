using RiptideNetworking;
using RiptideNetworking.Transports.RudpTransport;
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
    clientSnapshot,
}
public enum ClientToServerId : ushort
{
    playerName = 1,
    playerInput,
    playerConvar,
    inputCommand,
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
    
    public int tick = 0;
    [SerializeField] private ushort port;
    public ushort maxClientCount;
    [SerializeField] private GameObject playerPrefab;

    public GameObject PlayerPrefab => playerPrefab;
    public Server Server { get; private set; }

    public Convar tickrate = new Convar("sv_tickrate", 32, "Ticks per second", Flags.NETWORK, 1, 128);


    private void Awake()
    {
        Singleton = this;
    }

    private void Start()
    {
        Application.targetFrameRate = tickrate.GetIntValue();
        QualitySettings.vSyncCount = 0;

        #if UNITY_EDITOR
        RiptideLogger.Initialize(Debug.Log, false);
#else
        Console.Title = "Server";
        Console.Clear();
        Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
        RiptideLogger.Initialize(Debug.Log, true);
#endif

        Server = new Server(new RudpServer());
        Server.ClientConnected += NewPlayerConnected;
        Server.ClientDisconnected += PlayerLeft;

        LagCompensation.Start(maxClientCount);
        Server.Start(port, maxClientCount);
    }

    private void FixedUpdate()
    {
        Server.Tick();
        Application.targetFrameRate = tickrate.GetIntValue();

        ServerTime();
        LagCompensation.UpdatePlayerRecords();
        tick++;
    }

    private void ServerTime()
    {
        for (ushort i = 1; i <= maxClientCount; i++)
        {
            if(Player.List.TryGetValue(i, out Player player))
                player.tick = tick;
        }
        SendMessages.ServerTick();
    }

    private void OnApplicationQuit()
    {
        Server.Stop();
        LagCompensation.Stop();

        Server.ClientConnected -= NewPlayerConnected;
        Server.ClientDisconnected -= PlayerLeft;
    }

    private void NewPlayerConnected(object sender, ServerClientConnectedEventArgs e)
    {
        Debug.Log("Connected");
        foreach (Player player in Player.List.Values)
        {
            if (player.id != e.Client.Id)
                player.SendSpawn(e.Client.Id);
        }
    }

    private void PlayerLeft(object sender, ClientDisconnectedEventArgs e)
    {
        Debug.Log("disconnected");
        Destroy(Player.List[e.Id].gameObject);
    }
}
