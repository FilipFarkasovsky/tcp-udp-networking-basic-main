using System;
using System.Diagnostics;
using UnityEngine;

public class Utils
{
    public static float TickInterval()
    {
        return ticksToTime(1);
    }

    public static int timeToTicks(float _time)
    {
        return Mathf.FloorToInt(_time / (1f / NetworkManager.tickrate.GetValue()));
    }

    public static float ticksToTime(int _ticks)
    {
        return (float)_ticks * (1f / NetworkManager.tickrate.GetValue());
    }

    public static float roundTimeToTimeStep(float _time)
    {
        return ticksToTime(timeToTicks(_time));
    }
}

public class GlobalVariables
{
    public static int clientTick = 0;
    public static int serverTick = 0;
    public static float lerpAmount = 0f;
}

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

public class LerpManager
{
    /// <summary> Call this in update | Update lerpAmount and clientTick </summary>
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
        GlobalVariables.lerpAmount = (GlobalVariables.lerpAmount * Utils.TickInterval() + Time.deltaTime) / Utils.TickInterval();
        while (GlobalVariables.lerpAmount >= 1f)
        {
            // Client (simulated) tick >= Server (real) tick, break
            if (GlobalVariables.clientTick >= GlobalVariables.serverTick)
                break;

            GlobalVariables.lerpAmount = (GlobalVariables.lerpAmount * Utils.TickInterval() - Utils.TickInterval()) / Utils.TickInterval();
            GlobalVariables.lerpAmount = Mathf.Max(0f, GlobalVariables.lerpAmount);
            GlobalVariables.clientTick++;
        }
    }
}