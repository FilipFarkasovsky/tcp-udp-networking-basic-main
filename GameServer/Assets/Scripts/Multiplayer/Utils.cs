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

    /// <summary> It is used for simulating more accurate fixedUpdate - fixedTime </summary>
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

    /// <summary> Position and rotation at the given tick - used for lag compensation </summary>
    public class PlayerRecord
    {
        public Vector3 position;
        public Quaternion rotation;
        public int playerTick;

        public PlayerRecord()
        {
            position = new Vector3();
            rotation = new Quaternion();
            playerTick = new int();
        }

        public PlayerRecord(Vector3 _position, Quaternion _rotation, int _playerTick)
        {
            position = _position;
            rotation = _rotation;
            playerTick = _playerTick;
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
        public static SimulationState CurrentSimulationState(ClientInputState inputState, Player player)
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