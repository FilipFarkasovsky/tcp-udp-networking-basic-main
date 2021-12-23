using System.Collections.Generic;
using UnityEngine;

namespace Multiplayer
{
    /// <summary> Controls interpolation of networked objects</summary>
    public class Interpolation : MonoBehaviour
    {
        #region properties
        [SerializeField] public InterpolationMode mode;
        [SerializeField] public InterpolationImplemenation implementation;
        [SerializeField] public InterpolationTarget target;
        public SnapshotStDev snapshotStDev;

        static public Convar interpolation = new Convar("cl_interp", 0.1f, "Visual delay for received updates", Flags.CLIENT, 0f, 0.5f);

        public List<TransformUpdate> futureTransformUpdates = new List<TransformUpdate>();

        private TransformUpdate current;

        private Vector3 lastPosition;
        private Quaternion lastRotation;
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

        //private void OnGUI()
        //{
        //    GUI.Box(new Rect(5f, 5f, 180f, 25f), $"FUTURE COMMANDS {futureTransformUpdates?.Count}");
        //    GUI.Box(new Rect(5f, 95f, 180f, 25f), $"FPS {1000f/DebugScreen.framesPerSec}");
        //    GUI.Box(new Rect(5f, 125f, 180f, 25f), $"PING {DebugScreen.ping}");
        //    GUI.Box(new Rect(5f, 65f, 180f, 25f), $"LAST POSITION {current.lastPosition}");
        //    GUI.Box(new Rect(5f, 95f, 180f, 25f), $"CURRENT POSITION {current.position}");
        //    GUI.Box(new Rect(5f, 155f, 180f, 25f), $"MISPREDICTIONS {DebugScreen.mispredictions}");
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

        [SerializeField] float INTERP_NEGATIVE_THRESHOLD = SNAPSHOT_INTERVAL * 0.5f;
        [SerializeField] float INTERP_POSITIVE_THRESHOLD = SNAPSHOT_INTERVAL * 2f;

        Queue<Snapshot> NetworkSimQueue = new Queue<Snapshot>();
        List<Snapshot> Snapshots = new List<Snapshot>();

        private const int SNAPSHOT_RATE = 32;
        private const float SNAPSHOT_INTERVAL = 1.0f / SNAPSHOT_RATE;

        private StandardDeviation SnapshotDeliveryDeltaAvg;
        float lerpAlpha;


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

            current = new TransformUpdate(currentTick, Time.time, Time.time, transform.position, transform.position, transform.rotation, transform.rotation);

            lastPosition = transform.position;
            lastRotation = transform.rotation;
            lastTime = Time.time;

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
            if (CanWeInterpolateSyncedUpdate())
            {
                Interpolate(GlobalVariables.lerpAmount);
                lastTick = current.tick;
                lastLerpAmount = GlobalVariables.lerpAmount;
            }
            else
            {
                 // Debug.Log("InterpolationStopped");
            }

        }

        // We find our if there are any TransformUpdates we can interpolate from
        private bool CanWeInterpolateSyncedUpdate()
        {

            // There is no updates to lerp from, return
            if (futureTransformUpdates.Count <= 0 || futureTransformUpdates[0] == null)
                return false;

            // we remove incorrect updates and create new ones if needed
            TransformUpdate last = TransformUpdate.zero;
            foreach (TransformUpdate update in futureTransformUpdates.ToArray())
            {
                if (update == null || update == TransformUpdate.zero)
                {
                    continue;
                }

                // if tick < current client tick - interpolation, then remove
                if (update.tick < GlobalVariables.clientTick - Utils.timeToTicks(interpolation.GetValue()))
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
                    AccountForPacketLoss(update, last);
                }
                last = update;
            }

            // There is no updates to lerp from, return
            if (futureTransformUpdates.Count <= 0 || futureTransformUpdates[0] == null)
                return false;

            // Set current tick
            current = futureTransformUpdates[0];

            // It is very new update so we dont interpolate
            if (Time.time - current.time < Utils.roundTimeToTimeStep(interpolation.GetValue()) && Delay)
            {
                return false;
            }

            // Lerp amount moved to the next loop but the current target didnt move to the next tick, so dont interpolate
            if (lastTick == current.tick && GlobalVariables.lerpAmount < lastLerpAmount)
                return false;

            // everything is allright we can interpolate
            return true;
        }

        // NotAGoodUsername implementation
        // Adds fake packets between real ones
        private void AccountForPacketLoss(TransformUpdate update, TransformUpdate last)
        {
            // Get tick difference
            int tickDifference = update.tick - last.tick;
            if (tickDifference <= 1)
                return;

            // Loop through every tick till getting to the last tick,
            // which we dont use since it is the current tick
            TransformUpdate lastInForLoop = last;
            for (int j = 1; j < tickDifference; j++)
            {
                // Create new update
                TransformUpdate inBetween = TransformUpdate.zero;

                // Calculate the fraction in between the ticks
                float fraction = (float)j / (float)tickDifference;

                // Set corresponding last values
                inBetween.lastPosition = lastInForLoop.position;
                inBetween.lastRotation = lastInForLoop.rotation;
                inBetween.lastTime = lastInForLoop.time;

                // Lerp with the given fraction
                inBetween.position = Vector3.Lerp(inBetween.lastPosition, update.position, fraction);
                inBetween.rotation = Quaternion.Slerp(inBetween.lastRotation, update.rotation, fraction);
                inBetween.time = Mathf.Lerp(lastInForLoop.time, update.time, fraction);

                // Set new tick
                inBetween.tick = lastInForLoop.tick + 1;

                // Insert new update
                futureTransformUpdates.Insert(futureTransformUpdates.IndexOf(lastInForLoop), inBetween);

                // Last tick is now the inserted tick
                lastInForLoop = inBetween;

                // Set current tick proper last positions
                update.lastPosition = lastInForLoop.lastPosition;
                update.lastRotation = lastInForLoop.rotation;
                update.lastTime = lastInForLoop.time;
            }
        }

        // NotAGoodUsername implementation
        // Used for entitities that don't require lag compensation
        private void NonSyncedUpdate()
        {
            // There is no updates to lerp from, return
            if (futureTransformUpdates.Count <= 0 || futureTransformUpdates[0] == null)
                return;

            while (futureTransformUpdates[0].tick < GlobalVariables.clientTick - Utils.timeToTicks(interpolation.GetValue()))
            {
                futureTransformUpdates.RemoveAt(0);

                // There is no updates to lerp from, return
                if (futureTransformUpdates.Count <= 0 || futureTransformUpdates[0] == null)
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
                timeToReachTarget = Mathf.Abs(current.time - current.lastTime);

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
        private bool ReachedTarget(float _lerpAmount)
        {
            if (_lerpAmount <= 0)
                return false;
            switch (mode)
            {
                case InterpolationMode.both:
                    if (WaitForLerp)
                        return _lerpAmount >= 1f;
                    else
                        return (transform.position == current.position && transform.rotation == current.rotation) || _lerpAmount >= 1f;
                case InterpolationMode.position:
                    if (WaitForLerp)
                        return _lerpAmount >= 1f;
                    else
                        return transform.position == current.position || _lerpAmount >= 1f;
                case InterpolationMode.rotation:
                    if (WaitForLerp)
                        return _lerpAmount >= 1f;
                    else
                        return transform.rotation == current.rotation || _lerpAmount >= 1f;
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
            if (Snapshots.Count > 0)
            {
                for (int i = 0; i < Snapshots.Count; ++i)
                {
                    if (i + 1 == Snapshots.Count)
                    {
                        current.lastPosition = current.position = Snapshots[i].Position;
                        current.lastRotation = current.rotation = Snapshots[i].Rotation;
                        lerpAlpha = 0;
                    }
                    else
                    {
                        var f = i;
                        var t = i + 1;

                        if (Snapshots[f].Time <= ScaledInterpolationTime && Snapshots[t].Time >= ScaledInterpolationTime)
                        {
                            current.lastPosition = Snapshots[f].Position;
                            current.position = Snapshots[t].Position;

                            current.lastRotation = Snapshots[f].Rotation;
                            current.rotation = Snapshots[t].Rotation;

                            var Current = ScaledInterpolationTime - Snapshots[f].Time;
                            var range = Snapshots[t].Time - Snapshots[f].Time;

                            lerpAlpha = Mathf.Clamp01(Current / range);

                            break;
                        }
                    }
                }
                Interpolate(lerpAlpha);
            }
        }

        // Interpolates depending on the requested mode
        private void Interpolate(float _lerpAmount)
        {
            switch (mode)
            {
                case InterpolationMode.both:
                    transform.position = Vector3.Lerp(current.lastPosition, current.position, _lerpAmount);
                    transform.rotation = Quaternion.Slerp(current.lastRotation, current.rotation, _lerpAmount);
                    break;
                case InterpolationMode.position:
                    transform.position = Vector3.Lerp(current.lastPosition, current.position, _lerpAmount);
                    break;
                case InterpolationMode.rotation:
                    transform.rotation = Quaternion.Slerp(current.lastRotation, current.rotation, _lerpAmount);
                    break;
            }
        }

        // Updates are used to add a new tick to the list
        // the list is sorted and then set the last tick info to the respective variables
        #region Updates
        internal void NewUpdate(int _tick, float time, Vector3 _position, Quaternion _rotation)
        {
            MaxServerTimeReceived = Mathf.Max(MaxServerTimeReceived, time);
            SnapshotDeliveryDeltaAvg.Integrate(Time.time - TimeLastSnapshotReceived);
            TimeLastSnapshotReceived = Time.time;
            TimeSinceLastSnapshotReceived = 0f;
            DelayTarget = (SNAPSHOT_INTERVAL * SNAPSHOT_OFFSET_COUNT) + SnapshotDeliveryDeltaAvg.Mean + (SnapshotDeliveryDeltaAvg.Value * 2f);

            futureTransformUpdates.Add(new TransformUpdate(_tick, time, lastTime, _position, lastPosition, _rotation, lastRotation));

            //if (!weHadReceivedInterpolationTime)
            //{
            //    ScaledInterpolationTime = NormalInterpolationTime = time - (SNAPSHOT_INTERVAL * SNAPSHOT_OFFSET_COUNT);
            //    weHadReceivedInterpolationTime = true;
            //}

            if (futureTransformUpdates.Count <= 1)
            {
                lastPosition = _position;
                lastRotation = _rotation;
                lastTime = time;
                return;
            }

            futureTransformUpdates.Sort(delegate (TransformUpdate x, TransformUpdate y)
            {
                return x.tick.CompareTo(y.tick);
            });

            // Purpose: after sorting the updates, we set the last positions/rotations
            // This accounts for packets being out of order

            TransformUpdate last = TransformUpdate.zero;
            foreach (TransformUpdate transformUpdate in futureTransformUpdates)
            {
                if (transformUpdate == null)
                    continue;

                if (last != TransformUpdate.zero)
                {
                    transformUpdate.lastPosition = last.position;
                    transformUpdate.lastRotation = last.rotation;
                    transformUpdate.lastTime = last.time;

                    lastPosition = last.position;
                    lastRotation = last.rotation;
                    lastTime = last.time;
                }

                last = transformUpdate;
            }

            if (futureTransformUpdates[futureTransformUpdates.Count - 1] != null)
            {
                lastPosition = futureTransformUpdates[futureTransformUpdates.Count - 1].position;
                lastRotation = futureTransformUpdates[futureTransformUpdates.Count - 1].rotation;
                lastTime = futureTransformUpdates[futureTransformUpdates.Count - 1].time;
            }
        }
        internal void NewUpdate(int _tick, Vector3 _position, Quaternion _rotation)
        {
            futureTransformUpdates.Add(new TransformUpdate(_tick, Time.time, lastTime, _position, lastPosition, _rotation, lastRotation));

            if (futureTransformUpdates.Count <= 1)
            {
                lastPosition = _position;
                lastRotation = _rotation;
                lastTime = Time.time;
                return;
            }

            futureTransformUpdates.Sort(delegate (TransformUpdate x, TransformUpdate y)
            {
                return x.tick.CompareTo(y.tick);
            });

            // Purpose: after sorting the updates, we set the last positions/rotations
            // This accounts for packets being out of order

            TransformUpdate last = TransformUpdate.zero;
            foreach (TransformUpdate transformUpdate in futureTransformUpdates)
            {
                if (transformUpdate == null)
                    continue;

                if (last != TransformUpdate.zero)
                {
                    transformUpdate.lastPosition = last.position;
                    transformUpdate.lastRotation = last.rotation;
                    transformUpdate.lastTime = last.time;

                    lastPosition = last.position;
                    lastRotation = last.rotation;
                    lastTime = last.time;
                }

                last = transformUpdate;
            }

            if (futureTransformUpdates[futureTransformUpdates.Count - 1] != null)
            {
                lastPosition = futureTransformUpdates[futureTransformUpdates.Count - 1].position;
                lastRotation = futureTransformUpdates[futureTransformUpdates.Count - 1].rotation;
                lastTime = futureTransformUpdates[futureTransformUpdates.Count - 1].time;
            }
        }
        internal void NewUpdate(int _tick, Vector3 _position)
        {
            futureTransformUpdates.Add(new TransformUpdate(_tick, Time.time, lastTime, _position, lastPosition));

            if (futureTransformUpdates.Count <= 1)
            {
                lastPosition = _position;
                lastTime = Time.time;
                return;
            }

            futureTransformUpdates.Sort(delegate (TransformUpdate x, TransformUpdate y)
            {
                return x.tick.CompareTo(y.tick);
            });

            // Purpose: after sorting the updates, we set the last positions/rotations
            // This accounts for packets being out of order

            TransformUpdate last = TransformUpdate.zero;
            foreach (TransformUpdate transformUpdate in futureTransformUpdates)
            {
                if (transformUpdate == null)
                    continue;

                if (last != TransformUpdate.zero)
                {
                    transformUpdate.lastPosition = last.position;
                    transformUpdate.lastRotation = last.rotation;
                    transformUpdate.lastTime = last.time;

                    lastPosition = last.position;
                    lastRotation = last.rotation;
                    lastTime = last.time;
                }

                last = transformUpdate;
            }
            if (futureTransformUpdates[futureTransformUpdates.Count - 1] != null)
            {
                lastPosition = futureTransformUpdates[futureTransformUpdates.Count - 1].position;
                lastTime = futureTransformUpdates[futureTransformUpdates.Count - 1].time;
            }
        }
        internal void NewUpdate(int _tick, Quaternion _rotation)
        {
            futureTransformUpdates.Add(new TransformUpdate(_tick, Time.time, lastTime, _rotation, lastRotation));

            if (futureTransformUpdates.Count <= 1)
            {
                lastRotation = _rotation;
                lastTime = Time.time;
                return;
            }

            futureTransformUpdates.Sort(delegate (TransformUpdate x, TransformUpdate y)
            {
                return x.tick.CompareTo(y.tick);
            });

            // Purpose: after sorting the updates, we set the last positions/rotations
            // This accounts for packets being out of order

            TransformUpdate last = TransformUpdate.zero;
            foreach (TransformUpdate transformUpdate in futureTransformUpdates)
            {
                if (transformUpdate == null)
                    continue;

                if (last != TransformUpdate.zero)
                {
                    transformUpdate.lastPosition = last.position;
                    transformUpdate.lastRotation = last.rotation;
                    transformUpdate.lastTime = last.time;

                    lastPosition = last.position;
                    lastRotation = last.rotation;
                    lastTime = last.time;
                }

                last = transformUpdate;
            }
            if (futureTransformUpdates[futureTransformUpdates.Count - 1] != null)
            {
                lastRotation = futureTransformUpdates[futureTransformUpdates.Count - 1].rotation;
                lastTime = futureTransformUpdates[futureTransformUpdates.Count - 1].time;
            }
        }
        #endregion

        // It is used for localPlayer interpolation, for smooth camera gameplay
        // the reason it is a separete function is to skip some unecessary calls
        internal void PlayerUpdate(int _tick, Vector3 _position)
        {
            futureTransformUpdates.Add(new TransformUpdate(_tick, Time.time, lastTime, _position, lastPosition));

            lastPosition = _position;
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
