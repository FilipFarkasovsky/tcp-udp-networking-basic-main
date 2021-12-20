using System;
using System.Diagnostics;
using UnityEngine;

namespace Multiplayer
{
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

    /// <summary> It was used for simulating more accurate fixedUpdate, however it might not work well for low FPS players </summary>
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

    /// <summary> Stores the value of lerpAmount, clientTick and serverTick </summary>
    public class GlobalVariables
    {
        public static int clientTick = 0;
        public static int serverTick = 0;
        public static float lerpAmount = 0f;
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
            GlobalVariables.lerpAmount = GlobalVariables.lerpAmount + Time.unscaledDeltaTime / Utils.TickInterval();

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

    /// <summary> Stores positions and rotations at given tick - used for interpolation </summary>
    class TransformUpdate
    {
        public static TransformUpdate zero = new TransformUpdate(0, 0, 0, Vector3.zero, Vector3.zero, Quaternion.identity, Quaternion.identity);

        public int tick;

        public float time;
        public float lastTime;

        public Vector3 position;
        public Vector3 lastPosition;

        public Quaternion rotation;
        public Quaternion lastRotation;

        internal TransformUpdate(int _tick, float _time, float _lastTime, Vector3 _position, Vector3 _lastPosition)
        {
            tick = _tick;
            time = _time;
            lastTime = _lastTime;

            position = _position;
            lastPosition = _lastPosition;

            rotation = Quaternion.identity;
        }

        internal TransformUpdate(int _tick, float _time, float _lastTime, Quaternion _rotation, Quaternion _lastRotation)
        {
            tick = _tick;
            time = _time;
            lastTime = _lastTime;

            position = Vector3.zero;

            rotation = _rotation;
            lastRotation = _lastRotation;
        }

        internal TransformUpdate(int _tick, float _time, float _lastTime, Vector3 _position, Vector3 _lastPosition, Quaternion _rotation, Quaternion _lastRotation)
        {
            tick = _tick;
            time = _time;
            lastTime = _lastTime;

            position = _position;
            lastPosition = _lastPosition;

            rotation = _rotation;
            lastRotation = _lastRotation;
        }
    }

    /// <summary> Client inputs sent to the server </summary>
    public class ClientInputState
    {
        public int tick; // Tick used for backtracking in LagCompensation - (ClientTick - interp.Getvalue)         
        public float lerpAmount; // Place between to points ( 0 - 1 )
        public int simulationFrame; // Used for client prediction and reconciliation

        public ushort buttons; 

        public float HorizontalAxis; 
        public float VerticalAxis;
        public Quaternion rotation; // Rotation of the camera - used mainly for simulation and lag compensation
    }

    /// <summary> Stores byte for each button </summary>
    public struct Button
    {
        public static ushort Jump = 1 << 0;
        public static ushort Fire1 = 1 << 2;
    }

    /// <summary> Simulation state sent to client - used for reconciliation </summary>
    public class SimulationState
    {
        public int simulationFrame;
        public Vector3 position;
        public Vector3 velocity;
        public Quaternion rotation;
        public Vector3 angularVelocity;
        public static SimulationState CurrentSimulationState(ClientInputState inputState, PlayerInput player)
        {
            return new SimulationState
            {
                simulationFrame = inputState.simulationFrame,
                position = player.transform.position,
                velocity = player.velocity,
                rotation = player.transform.rotation,
                angularVelocity = player.angularVelocity,
            };
        }
    }

}