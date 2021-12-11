using System.Collections.Generic;
using UnityEngine;

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
        rotation = Quaternion.identity;
        lastPosition = _lastPosition;
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
        rotation = _rotation;
        lastPosition = _lastPosition;
        lastRotation = _lastRotation;
    }
}

/// <summary> Controls interpolation of networked objects</summary>
public class Interpolation : MonoBehaviour
{
    [SerializeField] public InterpolationMode mode;
    [SerializeField] public InterpolationImplemenation implementation;
    [SerializeField] public InterpolationTarget target;
    public SimpleInterpolation tomWeilandInterpolation;
    public SnapshotStDev snapshotStDev;

    static public Convar interpolation = new Convar("cl_interp", 0.1f, "Visual delay for received updates", Flags.CLIENT, 0f, 0.5f);

    private List<TransformUpdate> futureTransformUpdates = new List<TransformUpdate>();


    private Vector3 lastPosition;
    private Quaternion lastRotation;
    private float lastTime;

    private int lastTick;
    private float lastLerpAmount;

    private TransformUpdate current;


    [SerializeField] private float timeElapsed = 0f;
    [SerializeField] private float timeToReachTarget = 0.1f;


    public bool Sync = false;
    public bool Delay = false;
    public bool WaitForLerp = false;

    // ---------      ALEX

    float FixedStepAccumulator;
    [SerializeField] Transform clientSimObject;
    public Vector3 PreviousPosition;

    private void OnGUI()
    {
        //GUI.Box(new Rect(5f, 5f, 180f, 25f), $"FUTURE COMMANDS {futureTransformUpdates?.Count}");
        //GUI.Box(new Rect(5f, 95f, 180f, 25f), $"FPS {1000f/DebugScreen.framesPerSec}");
        //GUI.Box(new Rect(5f, 125f, 180f, 25f), $"PING {DebugScreen.ping}");
        GUI.Box(new Rect(5f, 65f, 180f, 25f), $"LAST POSITION {current.lastPosition}");
        GUI.Box(new Rect(5f, 95f, 180f, 25f), $"CURRENT POSITION {current.position}");
        GUI.Box(new Rect(5f, 155f, 180f, 25f), $"MISPREDICTIONS {DebugScreen.mispredictions}");
    }

    private void Start()
    {
        if (target == InterpolationTarget.localPlayer)
        {
            Sync = false;
            Delay = false;
            WaitForLerp = false;
        }

        // The localPlayer uses a different tick
        int currentTick = target == InterpolationTarget.localPlayer ? 0 : GlobalVariables.clientTick - Utils.timeToTicks(interpolation.GetValue());
        if (currentTick < 0)
            currentTick = 0;

        lastPosition = transform.position;
        lastRotation = transform.rotation;
        lastTime = Time.time;

        lastTick = 0;
        lastLerpAmount = 0f;

        current = new TransformUpdate(currentTick, Time.time, Time.time, transform.position, transform.position, transform.rotation, transform.rotation);
    }
    
    private void Update()
    {
        switch (implementation)
        {
            case InterpolationImplemenation.notAGoodUsername:
                NotAGoodUsername();
                tomWeilandInterpolation.enabled = false;
                break;
            case InterpolationImplemenation.alex:
                Alex();
                tomWeilandInterpolation.enabled = false;
                break;
            case InterpolationImplemenation.tomWeiland:
                tomWeilandInterpolation.enabled = true;
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
        if (futureTransformUpdates.Count <= 0 || futureTransformUpdates[0] == null)
            return;

        TransformUpdate last = TransformUpdate.zero;
        foreach (TransformUpdate update in futureTransformUpdates.ToArray())
        {
            if (update == null || update == TransformUpdate.zero)
            {
                futureTransformUpdates.Remove(update);
                continue;
            }

            // if tick < current client tick - interpolation amount, then remove
            if (update.tick < GlobalVariables.clientTick - Utils.timeToTicks(interpolation.GetValue()))
            {
                futureTransformUpdates.Remove(update);
                continue;
            }

            if (update.tick <= last?.tick)
            {
                futureTransformUpdates.Remove(update);
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
            return;

        // Set current tick
        current = futureTransformUpdates[0];

        // If (time - time tick) <= interpolation amount, return
        if (Time.time - current.time <= Utils.roundTimeToTimeStep(interpolation.GetValue()) && Delay)
            return;

        // Lerp amount moved to the next loop but the current target didnt move to the next tick, so dont interpolate
        if (lastTick == current.tick && GlobalVariables.lerpAmount < lastLerpAmount)
            return;

        Interpolate(GlobalVariables.lerpAmount);
        lastTick = current.tick;
        lastLerpAmount = GlobalVariables.lerpAmount;
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

        timeElapsed = (timeElapsed * Utils.TickInterval() + Time.deltaTime) / Utils.TickInterval();

        Interpolate(timeElapsed);

        // While we have reached the target, move to the next and repeat
        while (ReachedTarget(timeElapsed))
        {
            timeElapsed = (timeElapsed * Utils.TickInterval() - Utils.TickInterval()) / Utils.TickInterval();
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
    
    // NotAGoodUsername implementation
    // Used for entitities that don't require lag compensation
    private void NonSyncedUpdate()
    {
        // There is no updates to lerp from, return
        if (futureTransformUpdates.Count <= 0 || futureTransformUpdates[0] == null)
            return;

        // Set current tick
        current = futureTransformUpdates[0];

        // If (time - time tick) <= interpolation amount, return
        if (Time.time - current.time <= Utils.roundTimeToTimeStep(interpolation.GetValue()) && Delay)
            return;

        timeElapsed += Time.deltaTime;

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

    // Alex implementation
    private void LocalPlayerDeltaSnapshotUpdate()
    {
        FixedStepAccumulator += Time.deltaTime;

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

    // Interpolates depending on the requested mode
    #region Interpolate
    private void Interpolate(float _lerpAmount)
    {
        switch (mode)
        {
            case InterpolationMode.both:
                InterpolatePosition(_lerpAmount);
                InterpolateRotation(_lerpAmount);
                break;
            case InterpolationMode.position:
                InterpolatePosition(_lerpAmount);
                break;
            case InterpolationMode.rotation:
                InterpolateRotation(_lerpAmount);
                break;
        }
    }

    private void InterpolatePosition(float _lerpAmount)
    {
        transform.position = Vector3.Lerp(current.lastPosition, current.position, _lerpAmount);
    }

    private void InterpolateRotation(float _lerpAmount)
    {
        transform.rotation = Quaternion.Slerp(current.lastRotation, current.rotation, _lerpAmount);
    }
    #endregion

    // Updates are used to add a new tick to the list
    // the list is sorted and then set the last tick info to the respective variables
    #region Updates

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

        futureTransformUpdates.Sort(delegate (TransformUpdate x, TransformUpdate y) {
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
        if(futureTransformUpdates[futureTransformUpdates.Count - 1] != null)
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

        futureTransformUpdates.Sort(delegate (TransformUpdate x, TransformUpdate y) {
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

        futureTransformUpdates.Sort(delegate (TransformUpdate x, TransformUpdate y) {
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
        tomWeiland,
    }

    public enum InterpolationTarget
    {
        localPlayer,
        syncedRemote,
        nonSyncedRemote,
    }
}