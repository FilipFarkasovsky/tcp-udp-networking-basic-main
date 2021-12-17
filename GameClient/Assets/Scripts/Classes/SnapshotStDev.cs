using System;
using System.Collections.Generic;
using UnityEngine;
using RiptideNetworking;

/// <summary> Cool delta snapshot </summary>
public class SnapshotStDev : MonoBehaviour
{
    struct Snapshot
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public float Time;
        public float DeliveryTime;
    }

    float TimeLastSnapshotReceived; // Time when last snapshot receved
    float TimeSinceLastSnapshotReceived; // Time since last snapshot received

    [SerializeField] float DelayTarget; 
    [SerializeField] float RealDelayTarget;


    [SerializeField] float MaxServerTimeReceived;
    [SerializeField] float InterpolationTime;
    [SerializeField] float InterpTimeScale;   // Advvanced | Determines how fast or slow is lerpAmount getting increased

    [SerializeField] int SNAPSHOT_OFFSET_COUNT = 2;

    Queue<Snapshot> NetworkSimQueue = new Queue<Snapshot>(); // Server | vypocty servera
    List<Snapshot> Snapshots = new List<Snapshot>(); // Client | snapshoty ziskane od servera

    const int SNAPSHOT_RATE = 32;   // Tickrate
    const float SNAPSHOT_INTERVAL = 1.0f / SNAPSHOT_RATE;   // Time between snapshots 

    [SerializeField] float INTERP_NEGATIVE_THRESHOLD;
    [SerializeField] float INTERP_POSITIVE_THRESHOLD;

    float lastSnapshot;

    void Start()
    {
        InterpTimeScale = 1;

        SnapshotDeliveryDeltaAvg.Initialize(SNAPSHOT_RATE);
    }

    StdDev SnapshotDeliveryDeltaAvg;

    void Update()
    {
        ClientUpdateInterpolationTime();
        ClientReceiveDataFromServer();
        ClientRenderLatestPostion();
    }

    void ClientUpdateInterpolationTime()
    {
        // Setting interpolation time
        InterpolationTime += (Time.unscaledDeltaTime * InterpTimeScale);
    }

    void ClientReceiveDataFromServer()
    {
        var received = false;

        while (NetworkSimQueue.Count > 0  && NetworkSimQueue.Peek().DeliveryTime < Time.time )
        {
            if (Snapshots.Count == 0)
                InterpolationTime = NetworkSimQueue.Peek().Time - (SNAPSHOT_INTERVAL * SNAPSHOT_OFFSET_COUNT);

            var snapshot = NetworkSimQueue.Dequeue();
            
            Snapshots.Add(snapshot);
            // Get server time
            MaxServerTimeReceived = Math.Max(MaxServerTimeReceived, snapshot.Time);

            received = true;
        }

        // if we had received server snapshot
        if (received)
        {
            SnapshotDeliveryDeltaAvg.Integrate(Time.time - TimeLastSnapshotReceived);
            TimeLastSnapshotReceived = Time.time;
            TimeSinceLastSnapshotReceived = 0f;

            // Compute delay target
            DelayTarget = (SNAPSHOT_INTERVAL * SNAPSHOT_OFFSET_COUNT) + SnapshotDeliveryDeltaAvg.Mean + (SnapshotDeliveryDeltaAvg.Value * 2f);
        }

        // Compute real delay target
        RealDelayTarget = (MaxServerTimeReceived + TimeSinceLastSnapshotReceived - InterpolationTime) - DelayTarget;

        if (RealDelayTarget > (SNAPSHOT_INTERVAL * INTERP_POSITIVE_THRESHOLD))
            InterpTimeScale = 1.05f;
        else if (RealDelayTarget < (SNAPSHOT_INTERVAL * -INTERP_NEGATIVE_THRESHOLD))
            InterpTimeScale = 0.95f;
        else InterpTimeScale = 1.0f;

        // Time since last snapshot received
        TimeSinceLastSnapshotReceived += Time.unscaledDeltaTime;
    }

    void ClientRenderLatestPostion()
    {
        if (Snapshots.Count > 0)
        {
            var prev_Pos = default(Vector3);
            var new_Pos = default(Vector3);

            var prev_Rot = default(Quaternion);
            var new_Rot = default(Quaternion);

            var interpAlpha = default(float);

            for (int i = 0; i < Snapshots.Count; ++i)
            {
                if (i + 1 == Snapshots.Count)
                {
                    if (Snapshots[0].Time > InterpolationTime)
                    {
                        prev_Pos = new_Pos = Snapshots[0].Position;
                        prev_Rot = new_Rot = Snapshots[0].Rotation;
                        interpAlpha = 0;
                    }
                    else
                    {
                        prev_Pos = new_Pos = Snapshots[i].Position;
                        prev_Rot = new_Rot = Snapshots[i].Rotation;
                        interpAlpha = 0;
                    }
                }
                else
                {

                    var f = i;
                    var t = i + 1;

                    if (Snapshots[f].Time <=InterpolationTime && Snapshots[t].Time >= InterpolationTime)
                    {
                        prev_Pos = Snapshots[f].Position;
                        new_Pos = Snapshots[t].Position;

                        prev_Rot = Snapshots[f].Rotation;
                        new_Rot = Snapshots[t].Rotation;

                        var range = Snapshots[t].Time - Snapshots[f].Time;
                        var current = InterpolationTime - Snapshots[f].Time;

                        interpAlpha = Mathf.Clamp01(current / range);

                        break;
                    }
                }
            }

            transform.rotation = Quaternion.Slerp(prev_Rot, new_Rot, interpAlpha);
            transform.position = Vector3.Lerp(prev_Pos, new_Pos, interpAlpha);
        }
    }

    public void ServerSnapshot(Vector3 position, Quaternion rotation, float time)
    {
        NetworkSimQueue.Enqueue(new Snapshot
        {
            Time = Time.time,
            Position = position,
            Rotation = rotation,
            DeliveryTime = Time.time - time,
        });
    }

    public void ServerSnapshot( Vector3 position, Quaternion rotation)
    {
        if (lastSnapshot + Time.fixedDeltaTime < Time.time)
        {
            lastSnapshot = Time.time;
            NetworkSimQueue.Enqueue(new Snapshot
            {
                Time = lastSnapshot,
                Position = position,
                Rotation = rotation,
                DeliveryTime = Time.time,
            });
        }
    }


    struct StdDev
    {
        float mean;
        float varianceSum;

        int index;
        float[] samples;

        int maxWindowSize;

        public int Count => samples.Length;
        public float Mean => mean;
        public float Variance => varianceSum / (maxWindowSize - 1);
        public float Value => Mathf.Sqrt(Variance);

        public void Initialize(int windowSize)
        {
            maxWindowSize = windowSize;
            samples = new float[maxWindowSize];
        }

        public void Integrate(float sample)
        {
            index = (index + 1) % maxWindowSize;
            float samplePrev = samples[index];
            float meanPrev = mean;

            mean += (sample - samplePrev) / maxWindowSize;
            varianceSum += (sample + samplePrev - mean - meanPrev) * (sample - samplePrev);

            samples[index] = sample;
        }
    }
}