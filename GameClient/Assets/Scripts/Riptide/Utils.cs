using System;
using System.Diagnostics;
using UnityEngine;

/// <summary> Contains utils for working with tick and their conversion and etc. </summary>
public class Utils
{
    /// <summary> Returns the time of one tick (in seconds)</summary>
    public static float TickInterval()
    {
        return ticksToTime(1);
    }

    /// <summary> Converts time (in seconds) to tick (floored) </summary>
    public static int timeToTicks(float time)
    {
        return Mathf.FloorToInt(time / (1f / NetworkManager.tickrate.GetValue()));
    }

    /// <summary> Converts tick to time (in seconds) </summary>
    public static float ticksToTime(int ticks)
    {
        return (float)ticks * (1f / NetworkManager.tickrate.GetValue());
    }

    /// <summary> Round time to TimeStep time | so if the Time = 235 and a TimeStep is every 200 it will round(floor) to 200 </summary>
    public static float roundTimeToTimeStep(float time)
    {
        return ticksToTime(timeToTicks(time));
    }
}

/// <summary> Stores the value of lerpAmount, clientTick and serverTick </summary>
public class GlobalVariables
{
    public static int clientTick = 0;
    public static int serverTick = 0;
    public static float lerpAmount = 0f;
}

/// <summary> It was used for simulating more accurate fixedUpdate, however it didnt work well for low fps clients </summary>
public class LogicTimer
{
    public static float FramesPerSecond = NetworkManager.tickrate.GetIntValue();
    public static float FixedDelta = Utils.TickInterval();

    private double _accumulator;
    private long _lastTime;

    private readonly Stopwatch _stopwatch;
    private readonly Action _action;

    public float LerpAlpha => (float)_accumulator / FixedDelta;

    public LogicTimer(Action action)
    {
        _stopwatch = new Stopwatch();
        _action = action;
    }

    public void Start()
    {
        _lastTime = 0;
        _accumulator = 0.0;
        _stopwatch.Restart();
    }

    public void Stop()
    {
        _stopwatch.Stop();
    }

    public void Update()
    {
        FixedDelta = Utils.TickInterval();
        long elapsedTicks = _stopwatch.ElapsedTicks;
        _accumulator += (double)(elapsedTicks - _lastTime) / Stopwatch.Frequency;
        _lastTime = elapsedTicks;

        while (_accumulator >= FixedDelta)
        {
            _action();
            _accumulator -= FixedDelta;
        }
    }
}

/// <summary> Updates lerpAmount and clientTick </summary>
public class LerpManager
{
    /// <summary> Call this in update | Bad for low FPS clients| Call only when fps > tickRate because it is frame dependent so it gives more accurate results</summary>
    public static void Update()
    {
        // We dont want to lag behind the real tick by too much,
        // so just teleport to the next tick
        // The cases where this can happen are high ping/low fps
        GlobalVariables.clientTick = Mathf.Clamp(GlobalVariables.clientTick, GlobalVariables.serverTick - 2, GlobalVariables.serverTick);

        // Client (simulated) tick >= Server (real) tick, return
        if (GlobalVariables.clientTick >= GlobalVariables.serverTick)
            return;

        // While lerp amount is or more than 1, we move to the next clientTick and reset the lerp amount
        GlobalVariables.lerpAmount = GlobalVariables.lerpAmount + Time.deltaTime / Utils.TickInterval();

        while (GlobalVariables.lerpAmount >= 1f)
        {
            // Client (simulated) tick >= Server (real) tick, break
            if (GlobalVariables.clientTick >= GlobalVariables.serverTick)
                break;

            GlobalVariables.clientTick++;
            GlobalVariables.lerpAmount = GlobalVariables.lerpAmount  - 1;
        }
    }

    /// <summary> Call this in fixedUpdate | Good for low FPS clients| Call only when fps < tickRate it is not frame dependent so it gives more accurate results </summary>
    public static void FixedUpdate()
    {
        // We dont want to lag behind the real tick by too much,
        // so just teleport to the next tick
        // The cases where this can happen are high ping/low fps
        GlobalVariables.clientTick = Mathf.Clamp(GlobalVariables.clientTick, GlobalVariables.serverTick - 2, GlobalVariables.serverTick);

        // Client (simulated) tick >= Server (real) tick, return
        if (GlobalVariables.clientTick >= GlobalVariables.serverTick)
            return;

        // While lerp amount is or more than 1, we move to the next clientTick and reset the lerp amount
        GlobalVariables.lerpAmount = GlobalVariables.lerpAmount + Time.fixedDeltaTime / Utils.TickInterval();

        while (GlobalVariables.lerpAmount >= 1f)
        {
            // Client (simulated) tick >= Server (real) tick, break
            if (GlobalVariables.clientTick >= GlobalVariables.serverTick)
                break;

            GlobalVariables.clientTick++;
            GlobalVariables.lerpAmount = GlobalVariables.lerpAmount - 1;
        }
    }
}