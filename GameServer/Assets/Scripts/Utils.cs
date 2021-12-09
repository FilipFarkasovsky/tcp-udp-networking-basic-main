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
        return Mathf.FloorToInt(_time / (1f / NetworkManager.Singleton.tickrate.GetValue()));
    }

    public static float ticksToTime(int _ticks)
    {
        return (float)_ticks * (1f / NetworkManager.Singleton.tickrate.GetValue());
    }

}

public class LogicTimer
{
    public float FramesPerSecond = NetworkManager.Singleton.tickrate.GetIntValue();
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