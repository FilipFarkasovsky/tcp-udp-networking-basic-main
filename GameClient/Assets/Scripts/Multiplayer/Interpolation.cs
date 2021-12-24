using System.Collections.Generic;
using UnityEngine;

namespace Multiplayer
{
    /// <summary> Controls interpolation on networked objects</summary>
    public class Interpolation : MonoBehaviour
    {
        #region properties
        [SerializeField] public InterpolationMode mode;
        [SerializeField] public InterpolationImplemenation implementation;
        [SerializeField] public InterpolationTarget target;
        public SnapshotStDev snapshotStDev;

        static public Convar interpolation = new Convar("cl_interp", 0.1f, "Visual delay for received updates", Flags.CLIENT, 0f, 0.5f);

        // SIMULATION
        private Queue<TransformUpdate> NetworkSimulationQueue = new Queue<TransformUpdate>();
        [SerializeField] Transform Server;
        [SerializeField, Range(0, 0.5f)] float random;
        private float lastSimulationSnapshot;

        private List<TransformUpdate> futureTransformUpdates = new List<TransformUpdate>();
        private TransformUpdate current, updateFrom, updateTo;

        private float lastTime;
        private int lastTick;
        private float lastLerpAmount;

        [SerializeField] private float timeElapsed = 0f;
        [SerializeField] private float timeToReachTarget = 0.1f;

        [SerializeField] bool Delay = false;
        [SerializeField] bool WaitForLerp = false;

        // ---------      ALEX

        float FixedStepAccumulator;
        [SerializeField] Transform clientSimObject;
        public Vector3 PreviousPosition;

        #endregion

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

        [SerializeField] float INTERP_NEGATIVE_THRESHOLD = SNAPSHOT_INTERVAL * 0.5f;
        [SerializeField] float INTERP_POSITIVE_THRESHOLD = SNAPSHOT_INTERVAL * 2f;


        private const int SNAPSHOT_RATE = 32;
        private const float SNAPSHOT_INTERVAL = 1.0f / SNAPSHOT_RATE;

        private StandardDeviation SnapshotDeliveryDeltaAvg;

        float lerpAlpha;
        private bool weHadReceivedInterpolationTime;

        private void Start()
        {

            if (target == InterpolationTarget.localPlayer)
            {
                Delay = false;
                WaitForLerp = false;
            }

            timeToReachTarget = Utils.TickInterval();

            // The localPlayer uses a different tick
            int currentTick = target == InterpolationTarget.localPlayer ? 0 : GlobalVariables.clientTick - Utils.timeToTicks(interpolation.GetValue());
            if (currentTick < 0)
                currentTick = 0;

            current = new TransformUpdate(currentTick, Time.time, transform.position, transform.rotation);
            updateFrom = new TransformUpdate(currentTick, Time.time, transform.position, transform.rotation);
            updateTo = new TransformUpdate(currentTick, Time.time, transform.position, transform.rotation);

            lastTick = 0;
            lastLerpAmount = 0f;

            SnapshotDeliveryDeltaAvg.Initialize(SNAPSHOT_RATE);
        }

        private void Update()
        {
            switch (implementation)
            {
                case InterpolationImplemenation.notAGoodUsername:
                    NotAGoodUsername();
                    break;
                case InterpolationImplemenation.alex:
                    Alex();
                    break;
            }
        }

        private void NotAGoodUsername()
        {
            switch (target)
            {
                case InterpolationTarget.localPlayer:
                    LocalPlayerUpdate();
                    break;
                case InterpolationTarget.syncedRemote:
                    SyncedUpdate();
                    break;
                case InterpolationTarget.nonSyncedRemote:
                    NonSyncedUpdate();
                    break;
            }
        }

        private void Alex()
        {
            switch (target)
            {
                case InterpolationTarget.localPlayer:
                    LocalPlayerDeltaSnapshotUpdate();
                    break;
                case InterpolationTarget.syncedRemote:
                    RemotePlayerDeltaSnapshot();
                    break;
                case InterpolationTarget.nonSyncedRemote:
                    RemotePlayerDeltaSnapshot();
                    break;
            }
        }

        // NotAGoodUsername implementation
        // Used for syncing players - every player has same lerp amount
        // Sync is needed for entities that have lag compensation implemented
        private void SyncedUpdate()
        {
            // There is no updates to lerp from, return
            if (futureTransformUpdates.Count <= 0)
                return;

            // Set current tick
            current = futureTransformUpdates[0];

            // It is very new update so we dont interpolate
            if (Time.time - current.time < Utils.roundTimeToTimeStep(interpolation.GetValue()) && Delay)
            {
                return;
            }

            // Lerp amount moved to the next loop but the current target didnt move to the next tick, so dont interpolate
            if (lastTick == current.tick && GlobalVariables.lerpAmount < lastLerpAmount)
                return;
             
            Interpolate(GlobalVariables.lerpAmount);
            lastTick = current.tick;
            lastLerpAmount = GlobalVariables.lerpAmount;

        }

        // NotAGoodUsername implementation
        // Used for entitities that don't require lag compensation
        private void NonSyncedUpdate()
        {
            // There is no updates to lerp from, return
            if (futureTransformUpdates.Count <= 0)
                return;

            while (futureTransformUpdates[0].tick < GlobalVariables.clientTick - Utils.timeToTicks(interpolation.GetValue()))
            {
                futureTransformUpdates.RemoveAt(0);

                // There is no updates to lerp from, return
                if (futureTransformUpdates.Count <= 0)
                    return;
            }


            // Set current tick
            current = futureTransformUpdates[0];

            // If (time - time tick) <= interpolation amount, return
            if (Time.time - current.time <= Utils.roundTimeToTimeStep(interpolation.GetValue()) && Delay)
                return;

            timeElapsed += Time.unscaledDeltaTime;

            Interpolate(timeElapsed / timeToReachTarget);

            // While we have reached the target, move to the next and repeat
            while (ReachedTarget(timeElapsed / timeToReachTarget))
            {
                timeElapsed -= timeToReachTarget;
                //timeToReachTarget = Mathf.Abs(current.time - current.lastTime);

                if (futureTransformUpdates.Count <= 0)
                    break;

                futureTransformUpdates.RemoveAt(0);
                if (futureTransformUpdates.Count <= 0)
                    break;

                // Set current tick
                current = futureTransformUpdates[0];
            }
        }

        // Returns if it has reached the targe when interpolating
        // WaitForLerp waits for _lerpAmount to reach 1
        // If it is false it will return true if the target tick
        // is equal to the current interpolated tick
        private bool ReachedTarget(float lerpAmount)
        {
            if (lerpAmount <= 0)
                return false;
            switch (mode)
            {
                case InterpolationMode.both:
                    if (WaitForLerp)
                        return lerpAmount >= 1f;
                    else
                        return (transform.position == current.position && transform.rotation == current.rotation) || lerpAmount >= 1f;
                case InterpolationMode.position:
                    if (WaitForLerp)
                        return lerpAmount >= 1f;
                    else
                        return transform.position == current.position || lerpAmount >= 1f;
                case InterpolationMode.rotation:
                    if (WaitForLerp)
                        return lerpAmount >= 1f;
                    else
                        return transform.rotation == current.rotation || lerpAmount >= 1f;
            }
            return false;
        }

        // NotAGoodUsername implementation
        // Used for LocalPlayer
        private void LocalPlayerUpdate()
        {
            // There is no updates to lerp from, return
            if (futureTransformUpdates.Count <= 0 || futureTransformUpdates[0] == null)
                return;

            // Set current tick
            current = futureTransformUpdates[0];

            // If (time - time tick) <= interpolation amount, return
            if (Time.time - current.time <= Utils.roundTimeToTimeStep(interpolation.GetValue()) && Delay)
                return;

            timeElapsed = timeElapsed + Time.unscaledDeltaTime / Utils.TickInterval();

            Interpolate(timeElapsed);

            // While we have reached the target, move to the next and repeat
            while (ReachedTarget(timeElapsed))
            {
                timeElapsed = timeElapsed - 1;
                timeElapsed = Mathf.Max(0f, timeElapsed);

                if (futureTransformUpdates.Count <= 0)
                    break;

                futureTransformUpdates.RemoveAt(0);
                if (futureTransformUpdates.Count <= 0)
                    break;

                // Set current tick
                current = futureTransformUpdates[0];
            }
        }

        // Alex implementation
        private void LocalPlayerDeltaSnapshotUpdate()
        {
            FixedStepAccumulator += Time.unscaledDeltaTime;

            while (FixedStepAccumulator >= Time.fixedDeltaTime)
            {
                FixedStepAccumulator -= Time.fixedDeltaTime;
            }

            float _alpha = Mathf.Clamp01(FixedStepAccumulator / Time.fixedDeltaTime);

            transform.position = Vector3.Lerp(PreviousPosition, clientSimObject.position, _alpha);
        }

        // Alex implementation 
        private void RemotePlayerDeltaSnapshot()
        {
            ScaledInterpolationTime += (Time.unscaledDeltaTime * InterpTimeScale);
            NormalInterpolationTime += (Time.unscaledDeltaTime);
            TimeSinceLastSnapshotReceived += Time.unscaledDeltaTime;

            RealDelayTarget = (MaxServerTimeReceived + TimeSinceLastSnapshotReceived - ScaledInterpolationTime) - DelayTarget;

            if (RealDelayTarget > (SNAPSHOT_INTERVAL * INTERP_POSITIVE_THRESHOLD))
                InterpTimeScale = 1.05f;
            else if (RealDelayTarget < (SNAPSHOT_INTERVAL * -INTERP_NEGATIVE_THRESHOLD))
                InterpTimeScale = 0.95f;
            else InterpTimeScale = 1.0f;


            if (futureTransformUpdates.Count > 0)
            {
                for (int i = 0; i < futureTransformUpdates.Count; ++i)
                {
                    if (i + 1 == futureTransformUpdates.Count)
                    {
                        updateFrom.position = updateTo.position = futureTransformUpdates[i].position;
                        updateFrom.rotation = updateTo.rotation = futureTransformUpdates[i].rotation;
                        lerpAlpha = 0;
                    }
                    else
                    {
                        var f = i;
                        var t = i + 1;

                        if (futureTransformUpdates[f].time <= ScaledInterpolationTime && futureTransformUpdates[t].time >= ScaledInterpolationTime)
                        {
                            updateFrom.position = futureTransformUpdates[f].position;
                            updateTo.position = futureTransformUpdates[t].position;

                            updateFrom.rotation = futureTransformUpdates[f].rotation;
                            updateTo.rotation = futureTransformUpdates[t].rotation;

                            var current = ScaledInterpolationTime - futureTransformUpdates[f].time;
                            var range = futureTransformUpdates[t].time - futureTransformUpdates[f].time;

                            lerpAlpha = Mathf.Clamp01(current / range);

                            break;
                        }
                    }
                }
                Interpolate(lerpAlpha);
            }
        }

        // Interpolates depending on the requested mode
        private void Interpolate(float lerpAmount)
        {
            switch (mode)
            {
                case InterpolationMode.both:
                    transform.position = Vector3.Lerp(updateFrom.position, updateTo.position, lerpAmount);
                    transform.rotation = Quaternion.Slerp(updateFrom.rotation, updateTo.rotation, lerpAmount);
                    break;
                case InterpolationMode.position:
                    transform.position = Vector3.Lerp(updateFrom.position, updateTo.position, lerpAmount);
                    break;
                case InterpolationMode.rotation:
                    transform.rotation = Quaternion.Slerp(updateFrom.rotation, updateTo.rotation, lerpAmount);
                    break;
            }
        }

        // Simulates the network traffic and delay
        // For proper working it needs FPS higher than tickrate
        private void SimulateTrafficSending()
        {
            Vector3 pos;
            pos = Server.transform.position;
            pos.x = Mathf.PingPong(Time.unscaledTime * 5, 10f) - 70f;

            Server.position = pos;
            if (lastSimulationSnapshot + Time.fixedDeltaTime < Time.unscaledTime)
            {
                lastSimulationSnapshot = Time.unscaledTime;
                NetworkSimulationQueue.Enqueue(new TransformUpdate(0, Time.unscaledTime, Server.position, Server.rotation));
            }
        }
        // simulates update handling on client
        private void SimulateTrafficReceiving()
        {
            while(NetworkSimulationQueue.Count > 0)
            {
                NewUpdate(NetworkSimulationQueue.Dequeue());
            }
        }

        // Updates are used to add a new tick to the list
        // the list is sorted and then set the last tick info to the respective variables
        #region Updates
        internal void NewUpdate(TransformUpdate update)
        {
            if (!weHadReceivedInterpolationTime)
            {
                ScaledInterpolationTime = NormalInterpolationTime = update.time - (SNAPSHOT_INTERVAL * SNAPSHOT_OFFSET_COUNT);
                weHadReceivedInterpolationTime = true;
            }

            futureTransformUpdates.Add(update);

            MaxServerTimeReceived = Mathf.Max(MaxServerTimeReceived, update.time);

            SnapshotDeliveryDeltaAvg.Integrate(Time.time - TimeLastSnapshotReceived);
            TimeLastSnapshotReceived = Time.time;
            TimeSinceLastSnapshotReceived = 0f;
            DelayTarget = (SNAPSHOT_INTERVAL * SNAPSHOT_OFFSET_COUNT) + SnapshotDeliveryDeltaAvg.Mean + (SnapshotDeliveryDeltaAvg.Value * 2f);

            futureTransformUpdates.Sort(delegate (TransformUpdate x, TransformUpdate y)
            {
                return x.tick.CompareTo(y.tick);
            });

            AccountForPacketLoss();
        }
        internal void NewUpdate(int tick, float time, Vector3 position, Quaternion rotation)
        {
            if (!weHadReceivedInterpolationTime)
            {
                ScaledInterpolationTime = NormalInterpolationTime = time - (SNAPSHOT_INTERVAL * SNAPSHOT_OFFSET_COUNT);
                weHadReceivedInterpolationTime = true;
            }

            futureTransformUpdates.Add(new TransformUpdate(tick, time, position, rotation));
            
            MaxServerTimeReceived = Mathf.Max(MaxServerTimeReceived, time);

            SnapshotDeliveryDeltaAvg.Integrate(Time.time - TimeLastSnapshotReceived);
            TimeLastSnapshotReceived = Time.time;
            TimeSinceLastSnapshotReceived = 0f;
            DelayTarget = (SNAPSHOT_INTERVAL * SNAPSHOT_OFFSET_COUNT) + SnapshotDeliveryDeltaAvg.Mean + (SnapshotDeliveryDeltaAvg.Value * 2f);

            futureTransformUpdates.Sort(delegate (TransformUpdate x, TransformUpdate y)
            {
                return x.tick.CompareTo(y.tick);
            });

            AccountForPacketLoss();
        }
        internal void NewUpdate(int tick, Vector3 position, Quaternion rotation)
        {
            futureTransformUpdates.Add(new TransformUpdate(tick, Time.time, lastTime, position, rotation));

            futureTransformUpdates.Sort(delegate (TransformUpdate x, TransformUpdate y)
            {
                return x.tick.CompareTo(y.tick);
            });

            AccountForPacketLoss();
        }
        internal void NewUpdate(int tick, Vector3 position)
        {
            futureTransformUpdates.Add(new TransformUpdate(tick, Time.time, position, Quaternion.identity));

            futureTransformUpdates.Sort(delegate (TransformUpdate x, TransformUpdate y)
            {
                return x.tick.CompareTo(y.tick);
            });

            AccountForPacketLoss();

        }
        internal void NewUpdate(int tick, Quaternion rotation)
        {
            futureTransformUpdates.Add(new TransformUpdate(tick, Time.time, Vector3.zero ,rotation));

            futureTransformUpdates.Sort(delegate (TransformUpdate x, TransformUpdate y)
            {
                return x.tick.CompareTo(y.tick);
            });

            AccountForPacketLoss();

        }
        #endregion

        // NotAGoodUsername implementation
        // Adds fake packets between real ones and remove very old updates
        private void AccountForPacketLoss()
        {
            // There is no updates to lerp from, return
            if (futureTransformUpdates.Count <= 0 || futureTransformUpdates[0] == null)
                return;

            // we remove incorrect updates and create new ones if needed
            TransformUpdate last = TransformUpdate.zero;
            foreach (TransformUpdate update in futureTransformUpdates.ToArray())
            {
                if (update == null || update == TransformUpdate.zero)
                {
                    continue;
                }

                // if tick < current client tick - interpolation, then remove
                if (update.tick < GlobalVariables.clientTick - Utils.timeToTicks(interpolation.maxValue))
                {
                    futureTransformUpdates.Remove(update);
                    continue;
                }

                // We want to get last tick
                if (update.tick <= last?.tick)
                {
                    continue;
                }

                // Purpose: Add fake packets in between real ones, to account for packet loss
                if (last != TransformUpdate.zero)
                {
                    // Get tick difference
                    int tickDifference = update.tick - last.tick;
                    if (tickDifference <= 1)
                        continue;

                    // Loop through every tick till getting to the last tick,
                    // which we dont use since it is the current tick
                    TransformUpdate lastInForLoop = last;
                    for (int j = 1; j < tickDifference; j++)
                    {
                        // Create new update
                        TransformUpdate inBetween = TransformUpdate.zero;

                        // Calculate the fraction in between the ticks
                        float fraction = (float)j / (float)tickDifference;

                        // Lerp with the given fraction
                        inBetween.position = Vector3.Lerp(lastInForLoop.position, update.position, fraction);
                        inBetween.rotation = Quaternion.Slerp(lastInForLoop.rotation, update.rotation, fraction);
                        inBetween.time = Mathf.Lerp(lastInForLoop.time, update.time, fraction);

                        // Set new tick
                        inBetween.tick = lastInForLoop.tick + 1;

                        // Insert new update
                        futureTransformUpdates.Insert(futureTransformUpdates.IndexOf(lastInForLoop), inBetween);

                        // Last tick is now the inserted tick
                        lastInForLoop = inBetween;
                    }
                }
                last = update;
            }
        }

        // It is used for localPlayer interpolation, for smooth camera gameplay
        // the reason it is a separete function is to skip some unecessary calls
        internal void PlayerUpdate(int tick, Vector3 position)
        {
            futureTransformUpdates.Add(new TransformUpdate(tick, Time.time, position, Quaternion.identity));

            lastTime = Time.time;
        }

        public enum InterpolationMode
        {
            both,
            position,
            rotation,
        }

        public enum InterpolationImplemenation
        {
            notAGoodUsername,
            alex,
        }

        public enum InterpolationTarget
        {
            localPlayer,
            syncedRemote,
            nonSyncedRemote,
        }
    }
}
