
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

        //class TransformUpdate
        //{
        //    public static TransformUpdate zero = new TransformUpdate(0, 0, 0, Vector3.zero, Vector3.zero, Quaternion.identity, Quaternion.identity);

        //    public int tick;

        //    public float time;
        //    public float lastTime;

        //    public Vector3 position;
        //    public Vector3 lastPosition;

        //    public Quaternion rotation;
        //    public Quaternion lastRotation;

        //    internal TransformUpdate(int _tick, float _time, float _lastTime, Vector3 _position, Vector3 _lastPosition)
        //    {
        //        tick = _tick;
        //        time = _time;
        //        lastTime = _lastTime;

        //        position = _position;
        //        lastPosition = _lastPosition;

        //        rotation = Quaternion.identity;
        //    }

        //    internal TransformUpdate(int _tick, float _time, float _lastTime, Quaternion _rotation, Quaternion _lastRotation)
        //    {
        //        tick = _tick;
        //        time = _time;
        //        lastTime = _lastTime;

        //        position = Vector3.zero;

        //        rotation = _rotation;
        //        lastRotation = _lastRotation;
        //    }

        //    internal TransformUpdate(int _tick, float _time, float _lastTime, Vector3 _position, Vector3 _lastPosition, Quaternion _rotation, Quaternion _lastRotation)
        //    {
        //        tick = _tick;
        //        time = _time;
        //        lastTime = _lastTime;

        //        position = _position;
        //        lastPosition = _lastPosition;

        //        rotation = _rotation;
        //        lastRotation = _lastRotation;
        //    }
        //}

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

        private TransformUpdate updateFrom, updateTo;
        float interpAlpha;

        void Start()
        {
            updateFrom = new TransformUpdate(0, Time.time, transform.position, transform.rotation);
            updateTo = new TransformUpdate(0, Time.time, transform.position, transform.rotation);

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
            // reálne meškanie === reálny èas aký je teraz - èas kedy má zaèa interpolácia - meškanie 
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
                // Debug.Log(Time.time - TimeLastSnapshotReceived);
                TimeLastSnapshotReceived = Time.time;
                TimeSinceLastSnapshotReceived = 0f;

                // checknut
                // meškanie     ===       dåžka interpolácie + priemer hodnôt + 2 * smerodajná odchýlka
                DelayTarget = (SNAPSHOT_INTERVAL * SNAPSHOT_OFFSET_COUNT) + SnapshotDeliveryDeltaAvg.Mean + (SnapshotDeliveryDeltaAvg.Value * 2f);
            }
        }

        void ClientRenderLatestPostion()
        {
            if (Snapshots.Count > 0)
            {
                // zrefaktorizova
                // možno použi Utils.TransformUpdate

                // zoradime snapchoty
                for (int i = 0; i < Snapshots.Count; ++i)
                {
                    // ak je to naposledy pridany snapchot
                    // a sa nam ziaden iny interpolovat nepodarilo
                    // stane sa to ak je prilis velky lag
                    if (i + 1 == Snapshots.Count)
                    {
                        updateFrom.position = updateTo.position = Snapshots[i].Position;
                        updateFrom.rotation = updateTo.rotation = Snapshots[i].Rotation;
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
                            updateFrom.position = Snapshots[f].Position;
                            updateTo.position = Snapshots[t].Position;

                            updateFrom.rotation = Snapshots[f].Rotation;
                            updateTo.rotation = Snapshots[t].Rotation;

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
                transform.position = Vector3.Lerp(updateFrom.position, updateTo.position, interpAlpha);
                transform.rotation = Quaternion.Slerp(updateFrom.rotation, updateTo.rotation, interpAlpha);
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
    }
}