using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Multiplayer;

public class DebugScreen : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI FPS;
    [SerializeField] TextMeshProUGUI Tickrate;
    [SerializeField] TextMeshProUGUI Ping;
    [SerializeField] TextMeshProUGUI Mispred;
    [SerializeField] TextMeshProUGUI BytesUp;
    [SerializeField] TextMeshProUGUI BytesDown;
    [SerializeField] TextMeshProUGUI PacketsUp;
    [SerializeField] TextMeshProUGUI PacketsDown;
    [SerializeField] TextMeshProUGUI Gos;
    [SerializeField] TextMeshProUGUI Ram;

    [SerializeField] Image lerpAmount;

    //*********         HELP VARIABLES          ***********//
    private float time;

    public static int ping;
    private int frameCount;
    public static int mispredictions = 0; 
    public static int bytesUp = 0;
    public static int bytesDown = 0;
    public static int packetsUp = 0;
    public static int packetsDown = 0;


    public static int framesPerSec;
    private static int bytesUpPerSec;
    private static int bytesDownPerSec;
    private static int packetsUpPerSec;
    private static int packetsDownPerSec;

    // Update is called once per frame
    void Update()
    {
        time += Time.deltaTime;

        frameCount++;

        lerpAmount.fillAmount = GlobalVariables.lerpAmount;

        if (NetworkManager.Singleton.Client.IsConnected){
            ping = NetworkManager.Singleton.Client.RTT;
        }

        if(time >= 1f){
            framesPerSec = Mathf.RoundToInt(frameCount / time);
            bytesUpPerSec =  Mathf.RoundToInt(bytesUp / time);
            bytesDownPerSec = Mathf.RoundToInt(bytesDown / time);
            packetsUpPerSec =  Mathf.RoundToInt(packetsUp / time);
            packetsDownPerSec = Mathf.RoundToInt(packetsDown / time);

            time -= 1f;

            frameCount = 0;
            bytesUp = 0;
            bytesDown = 0;
            packetsUp = 0;
            packetsDown = 0;
        }
    }

    void FixedTime(){
        FPS.text = $"{1000f/framesPerSec:#.#} ms {framesPerSec} FPS";
        Tickrate.text = $"tickrate: {1 / Utils.TickInterval()}/s {Utils.TickInterval()*1000f} ms";
        Ping.text = $"ping: {ping} m/s";
        Mispred.text = $"mispredictions: {mispredictions} total";
        BytesUp.text = $"byteUp: {bytesUpPerSec}/s";
        BytesDown.text = $"byteDown: {bytesDownPerSec}/s";
        PacketsUp.text = $"byteUp: {packetsUpPerSec}/s";
        PacketsDown.text = $"byteDown: {packetsDownPerSec}/s";
        Gos.text = $"gos active: {GameObject.FindObjectsOfType(typeof(MonoBehaviour)).Length} gos total: {1}";
        Ram.text = $"lerpAmount: {GlobalVariables.lerpAmount}";

        if(1000f/framesPerSec >= Utils.TickInterval() * 1000f){
            Mispred.color = Color.red;
        }
        else{
            Mispred.color = Color.white;
        }

    }

    private void FixedUpdate()
    {
        FixedTime();
    }
}
