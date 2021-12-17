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
        return Mathf.FloorToInt(time / (1f / NetworkManager.Singleton.tickrate.GetValue()));
    }

    /// <summary> Converts tick to time (in seconds) </summary>
    public static float ticksToTime(int ticks)
    {
        return (float)ticks * (1f / NetworkManager.Singleton.tickrate.GetValue());
    }

    /// <summary> Round time to TimeStep time | so if the Time = 235 and a TimeStep is every 200 it will round(floor) to 200 </summary>
    public static float roundTimeToTimeStep(float time)
    {
        return ticksToTime(timeToTicks(time));
    }
}

/// <summary> It was used for simulating more accurate fixedUpdate </summary>
public class LogicTimer
{
    public static float FramesPerSecond = NetworkManager.Singleton.tickrate.GetIntValue();
    public static float FixedDelta = Utils.TickInterval();

    private double accumulator;
    private long lastTime;

    private readonly Stopwatch stopwatch;
    private readonly Action action;

    public float LerpAlpha => (float)accumulator / FixedDelta;

    public LogicTimer(Action fixedTime)
    {
        stopwatch = new Stopwatch();
        action = fixedTime;
    }

    public void Start()
    {
        lastTime = 0;
        accumulator = 0.0;
        stopwatch.Restart();
    }

    public void Stop()
    {
        stopwatch.Stop();
    }

    public void Update()
    {
        FixedDelta = Utils.TickInterval();
        long elapsedTicks = stopwatch.ElapsedTicks;
        accumulator += (double)(elapsedTicks - lastTime) / Stopwatch.Frequency;
        lastTime = elapsedTicks;

        while (accumulator >= FixedDelta)
        {
            action();
            accumulator -= FixedDelta;
        }
    }
}