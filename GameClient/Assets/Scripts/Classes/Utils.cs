using UnityEngine;

public class Utils
{
    public static float TickInterval()
    {
        return ticksToTime(1);
    }

    public static int timeToTicks(float _time)
    {
        return Mathf.FloorToInt(_time / (1f / Client.tickrate.GetValue()));
    }

    public static float ticksToTime(int _ticks)
    {
        return (float)_ticks * (1f / Client.tickrate.GetValue());
    }

    public static float roundTimeToTimeStep(float _time)
    {
        return ticksToTime(timeToTicks(_time));
    }
}
