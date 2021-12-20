using System;
using System.Collections.Generic;
using UnityEngine;

namespace Multiplayer
{
    public class SnapshotStDev : MonoBehaviour
    {
        struct Snapshot
        {
            public Vector3 Position;
            public Quaternion Rotation;
            public float Time;
        }

        [Header("Debug properties")]
        [SerializeField] float TimeLastSnapshotReceived;
        [SerializeField] float TimeSinceLastSnapshotReceived;

        [SerializeField] float DelayTarget;
        [SerializeField] float RealDelayTarget;

        [SerializeField] float MaxServerTimeReceived;
        [SerializeField] float ScaledInterpolationTime;
        private float NormalInterpolationTime;

       [Header("Interpolation properties")]
        [SerializeField] float InterpTimeScale = 1;
        [SerializeField] int SNAPSHOT_OFFSET_COUNT = 2;

        Queue<Snapshot> NetworkSimQueue = new Queue<Snapshot>();
        List<Snapshot> Snapshots = new List<Snapshot>();

        private const int SNAPSHOT_RATE = 32;
        private const float SNAPSHOT_INTERVAL = 1.0f / SNAPSHOT_RATE;

         // We will set up tresholds
        [SerializeField] float INTERP_NEGATIVE_THRESHOLD = SNAPSHOT_INTERVAL * 0.5f;
        [SerializeField] float INTERP_POSITIVE_THRESHOLD = SNAPSHOT_INTERVAL * 2f;

        private StandardDeviation SnapshotDeliveryDeltaAvg;

        Vector3 lastPosition;
        Vector3 position;
        Quaternion lastRotation;
        Quaternion rotation;
        float interpAlpha;

        void Start()
        {
            InterpTimeScale = 1;

            SnapshotDeliveryDeltaAvg.Initialize(SNAPSHOT_RATE);
        }


        void Update()
        {
            ClientReceiveDataFromServer();
            ClientRenderLatestPostion();
        }

        /// <summary> Vyratat hlavne timeScale a dat pridat snapshoty do List<Snapshot> </summary>
        void ClientReceiveDataFromServer()
        {
            // time when we are going to interpolate - Current time - interpolation interval
            ScaledInterpolationTime += (Time.unscaledDeltaTime * InterpTimeScale);
            NormalInterpolationTime += (Time.unscaledDeltaTime);
            TimeSinceLastSnapshotReceived += Time.unscaledDeltaTime;


            //checknut
            // re�lne me�kanie === re�lny �as ak� je teraz - �as kedy m� za�a� interpol�cia - me�kanie 
            RealDelayTarget = (MaxServerTimeReceived + TimeSinceLastSnapshotReceived - ScaledInterpolationTime) - DelayTarget;

            // zistit timeScale
            if (RealDelayTarget > (SNAPSHOT_INTERVAL * INTERP_POSITIVE_THRESHOLD))
                InterpTimeScale = 1.05f;
            else if (RealDelayTarget < (SNAPSHOT_INTERVAL * -INTERP_NEGATIVE_THRESHOLD))
                InterpTimeScale = 0.95f;
            else InterpTimeScale = 1.0f;

            // Time since last snapshot received
            // --------------------  presunut na line 60 ku interpolationTime -----------------------
        }

        private void ReceivingSnapshot()
        {
            var received = false;

            while (NetworkSimQueue.Count > 0)
            {
                if (Snapshots.Count == 0)
                {
                    ScaledInterpolationTime = NetworkSimQueue.Peek().Time - (SNAPSHOT_INTERVAL * SNAPSHOT_OFFSET_COUNT);
                    NormalInterpolationTime = NetworkSimQueue.Peek().Time - (SNAPSHOT_INTERVAL * SNAPSHOT_OFFSET_COUNT);
                }

                var snapshot = NetworkSimQueue.Dequeue();

                Snapshots.Add(snapshot);

                // Max time when we are interpolating
                MaxServerTimeReceived = Math.Max(MaxServerTimeReceived, snapshot.Time);

                received = true;
            }

            // if we had received server snapshot
            if (received)
            {
                // we sample the current time - the time of the last receivaed packet
                SnapshotDeliveryDeltaAvg.Integrate(Time.time - TimeLastSnapshotReceived);
                Debug.Log(Time.time - TimeLastSnapshotReceived);
                TimeLastSnapshotReceived = Time.time;
                TimeSinceLastSnapshotReceived = 0f;

                // checknut
                // me�kanie     ===       d�ka interpol�cie + priemer hodn�t + 2 * smerodajn� odch�lka
                DelayTarget = (SNAPSHOT_INTERVAL * SNAPSHOT_OFFSET_COUNT) + SnapshotDeliveryDeltaAvg.Mean + (SnapshotDeliveryDeltaAvg.Value * 2f);
            }
        }

        void ClientRenderLatestPostion()
        {
            if (Snapshots.Count > 0)
            {
                // zrefaktorizova�
                // mo�no pou�i� Utils.TransformUpdate

                // zoradime snapchoty
                for (int i = 0; i < Snapshots.Count; ++i)
                {
                    // ak je to naposledy pridany snapchot
                    // a sa nam ziaden iny interpolovat nepodarilo
                    // stane sa to ak je prilis velky lag
                    if (i + 1 == Snapshots.Count)
                    {
                        lastPosition = position = Snapshots[i].Position;
                        lastRotation = rotation = Snapshots[i].Rotation;
                        interpAlpha = 0;
                    }
                    else
                    {
                        var f = i;
                        var t = i + 1;

                        // snazime sa najst snapshot ktory je na hranici interpolovanosti

                        // normalInterpolationTime nefunguje dobre ak nestihne dojst snapshot 
                        // lebo potom neexistuje    Snapshots[t].Time >= NormalInterpolationTime
                        if (Snapshots[f].Time <= ScaledInterpolationTime && Snapshots[t].Time >= ScaledInterpolationTime)
                        {
                            lastPosition = Snapshots[f].Position;
                            position = Snapshots[t].Position;

                            lastRotation = Snapshots[f].Rotation;
                            rotation = Snapshots[t].Rotation;

                            // 
                            var current = ScaledInterpolationTime - Snapshots[f].Time;
                            // time between snapshots
                            var range = Snapshots[t].Time - Snapshots[f].Time;      

                            interpAlpha = Mathf.Clamp01(current / range);

                            break;
                        }
                    }
                }

                // Lerping
                transform.position = Vector3.Lerp(lastPosition, position, interpAlpha);
                transform.rotation = Quaternion.Slerp(lastRotation, rotation, interpAlpha);
            }
        }

        public void ServerSnapshot(Vector3 position, Quaternion rotation, float time)
        {
            NetworkSimQueue.Enqueue(new Snapshot
            {
                Time = time,
                Position = position,
                Rotation = rotation,
            });

            ReceivingSnapshot();
        }

        /// <summary>
        /// Smerodajn� odch�lka (in� n�zvy: �tandardn� odch�lka, �tandardn� devi�cia, stredn� kvadratick� odch�lka, stredn�/priemern� odch�lka
        /// </summary>
        /// https://sk.wikipedia.org/wiki/Smerodajn�_odch�lka
        /// https://en.wikipedia.org/wiki/Standard_deviation
        struct StandardDeviation
        {
            float mean; // priemer
            float varianceSum; // s��et rozptylov

            int index; 
            float[] samples; // vzorky

            int maxWindowSize; // po�et vzoriek

            public int Count => samples.Length; // po�et vzoriek 
            public float Mean => mean; // priemer 
            public float Variance => varianceSum / (maxWindowSize - 1); //  varia�n� koeficient
            public float Value => Mathf.Sqrt(Variance); // stredn� kvadratick� odch�lka

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
}