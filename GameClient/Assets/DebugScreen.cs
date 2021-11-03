using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class DebugScreen : MonoBehaviour
{
    public TextMeshProUGUI FPS;
    private string fps = "{0:#.#} ms {1} FPS";
    public TextMeshProUGUI Tickrate;
    private string tickrate = "tickrate: {0}/s {1} ms";
    public TextMeshProUGUI Ping;
    private string ping = "ping: {0} m/s";
    public TextMeshProUGUI ByteUp;
    private string byteUp = "byteUp: {0}";
    public TextMeshProUGUI ByteDown;
    private string byteDown = "byteDown: {0}";
    public TextMeshProUGUI Gos;
    private string gos = "gos active: {0} gos total: {1}";
    public TextMeshProUGUI Ram;
    private string  ram = "ram: {0} mb";
    public TextMeshProUGUI Mispred;
    private string mispred = "mispredictions: {0}/s {1}total";

    private float time;
    private int frameCount;
    private int frameRate;

    public static int mispredictions = 0; 
    public static int mispredictionsPerSec = 0; 

    static LogicTimer logicTimer;

    void Start()
    {

        logicTimer = new LogicTimer(() => FixedTime());
        logicTimer.Start();
    }

    // Update is called once per frame
    void Update()
    {
        logicTimer.Update();
        time += Time.deltaTime;

        frameCount++;

        if(time >= 1f){
            frameRate = Mathf.RoundToInt(frameCount / time);
            time -= 1f;
            frameCount = 0;
            mispredictionsPerSec = 0;
        }
    }

    void FixedTime(){
        FPS.text = string.Format(fps, (1000f/frameRate),frameRate.ToString());
        Tickrate.text = string.Format(tickrate, 1 / Utils.TickInterval(), Utils.TickInterval());
        //Ping.text = string.Format(ping, )
        //ByteUp.text = string.Format(byteUp, )
        //ByteDown.text = string.Format(byteDown, )
        Gos.text = string.Format(gos,"" , GameObject.FindObjectsOfType(typeof(MonoBehaviour)).Length);
        Mispred.text = string.Format(mispred, mispredictionsPerSec, mispredictions);
    }
}
