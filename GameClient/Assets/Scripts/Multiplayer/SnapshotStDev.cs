using System;
using System.Collections.Generic;
using UnityEngine;

namespace Multiplayer
{
    public class SnapshotStDev : MonoBehaviour
    {

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

        private TransformUpdate update;
        float interpAlpha;

        void Start()
        {
            update = new TransformUpdate(0 , Time.time, Time.time, transform.position, transform.position, transform.rotation, transform.rotation);

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
            // re·lne meökanie === re·lny Ëas ak˝ je teraz - Ëas kedy m· zaËaù interpol·cia - meökanie 
            RealDelayTarget = (MaxServerTimeReceived + TimeSinceLastSnapshotReceived - ScaledInterpolationTime) - DelayTarget;

            // zistit timeScale
            //if (RealDelayTarget > (SNAPSHOT_INTERVAL * INTERP_POSITIVE_THRESHOLD))
            if (RealDelayTarget > 1/16f)
                InterpTimeScale = 1.05f;
            //else if (RealDelayTarget < (SNAPSHOT_INTERVAL * -INTERP_NEGATIVE_THRESHOLD))
            else if (RealDelayTarget < - 1 /32f)
                InterpTimeScale = 0.95f;
            else InterpTimeScale = 1.0f;

            // Time since last snapshot received
            // --------------------  presunut na line 60 ku interpolationTime -----------------------
        }

        private void ReceivingSnapshot()
        {
            while (NetworkSimQueue.Count > 0)
            {
                // zadame ScaledInterpolationTime iba raz za hru
                if (Snapshots.Count == 0)
                {
                    ScaledInterpolationTime = NormalInterpolationTime = NetworkSimQueue.Peek().Time - (SNAPSHOT_INTERVAL * SNAPSHOT_OFFSET_COUNT);
                    Debug.Log(NetworkSimQueue.Peek().Time);
                }

                var snapshot = NetworkSimQueue.Dequeue();

                Snapshots.Add(snapshot);

                // Max time when we are interpolating
                MaxServerTimeReceived = Math.Max(MaxServerTimeReceived, snapshot.Time);

                // we sample the current time - the time of the last receivaed packet
                SnapshotDeliveryDeltaAvg.Integrate(Time.time - TimeLastSnapshotReceived);
                // Debug.Log(Time.time - TimeLastSnapshotReceived);
                TimeLastSnapshotReceived = Time.time;
                TimeSinceLastSnapshotReceived = 0f;

                // checknut
                // meökanie     ===       dÂûka interpol·cie + priemer hodnÙt + 2 * smerodajn· odch˝lka
                DelayTarget = (SNAPSHOT_INTERVAL * SNAPSHOT_OFFSET_COUNT) + SnapshotDeliveryDeltaAvg.Mean + (SnapshotDeliveryDeltaAvg.Value * 2f);
            }
        }

        void ClientRenderLatestPostion()
        {
            if (Snapshots.Count > 0)
            {
                // zrefaktorizovaù
                // moûno pouûiù Utils.TransformUpdate

                // zoradime snapchoty
                for (int i = 0; i < Snapshots.Count; ++i)
                {
                    // ak je to naposledy pridany snapchot
                    // a sa nam ziaden iny interpolovat nepodarilo
                    // stane sa to ak je prilis velky lag
                    if (i + 1 == Snapshots.Count)
                    {
                        update.lastPosition = update.position = Snapshots[i].Position;
                        update.lastRotation = update.rotation = Snapshots[i].Rotation;
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
                            update.lastPosition = Snapshots[f].Position;
                            update.position = Snapshots[t].Position;

                            update.lastRotation = Snapshots[f].Rotation;
                            update.rotation = Snapshots[t].Rotation;

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
                transform.position = Vector3.Lerp(update.lastPosition, update.position, interpAlpha);
                transform.rotation = Quaternion.Slerp(update.lastRotation, update.rotation, interpAlpha);
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