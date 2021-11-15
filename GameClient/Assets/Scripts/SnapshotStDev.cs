using System;
using System.Collections.Generic;
using UnityEngine;

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

public class SnapshotStDev : MonoBehaviour
{
    struct Snapshot
    {
        public Vector3 Position;
        public float Time;
        public float DeliveryTime;
    }

    public GameObject Server;
    public GameObject Client;

    float _lastSnapshot;

    float _cTimeLastSnapshotReceived;
    float _cTimeSinceLastSnapshotReceived;

    [SerializeField] float _cDelayTarget;
    [SerializeField] float _cRealDelayTarget;


    [SerializeField] float _cMaxServerTimeReceived;
    [SerializeField] float _cInterpolationTime;
    [SerializeField] float _cInterpTimeScale;

    [SerializeField] int SNAPSHOT_OFFSET_COUNT = 2;

    Queue<Snapshot> _cNetworkSimQueue = new Queue<Snapshot>();
    List<Snapshot> _cSnapshots = new List<Snapshot>();

    const int SNAPSHOT_RATE = 32;
    const float SNAPSHOT_INTERVAL = 1.0f / SNAPSHOT_RATE;

    [SerializeField] float INTERP_NEGATIVE_THRESHOLD;
    [SerializeField] float INTERP_POSITIVE_THRESHOLD;

    void Start()
    {
        _cInterpTimeScale = 1;

        _cSnapshotDeliveryDeltaAvg.Initialize(SNAPSHOT_RATE);
    }

    StdDev _cSnapshotDeliveryDeltaAvg;

    void Update()
    {
        //ServerMovement();
        //ServerSnapshot();

        ClientUpdateInterpolationTime();
        ClientReceiveDataFromServer();
        ClientRenderLatestPostion();
    }

    void ClientReceiveDataFromServer()
    {
        var received = false;

        while (_cNetworkSimQueue.Count > 0 && _cNetworkSimQueue.Peek().DeliveryTime < Time.time)
        {
            if (_cSnapshots.Count == 0)
                _cInterpolationTime = _cNetworkSimQueue.Peek().Time - (SNAPSHOT_INTERVAL * SNAPSHOT_OFFSET_COUNT);

            var snapshot = _cNetworkSimQueue.Dequeue();

            _cSnapshots.Add(snapshot);
            _cMaxServerTimeReceived = Math.Max(_cMaxServerTimeReceived, snapshot.Time);

            received = true;
        }

        if (received)
        {
            _cSnapshotDeliveryDeltaAvg.Integrate(Time.time - _cTimeLastSnapshotReceived);
            _cTimeLastSnapshotReceived = Time.time;
            _cTimeSinceLastSnapshotReceived = 0f;

            _cDelayTarget = (SNAPSHOT_INTERVAL * SNAPSHOT_OFFSET_COUNT) + _cSnapshotDeliveryDeltaAvg.Mean + (_cSnapshotDeliveryDeltaAvg.Value * 2f);
        }

        _cRealDelayTarget = (_cMaxServerTimeReceived + _cTimeSinceLastSnapshotReceived - _cInterpolationTime) - _cDelayTarget;

        if (_cRealDelayTarget > (SNAPSHOT_INTERVAL * INTERP_POSITIVE_THRESHOLD))
            _cInterpTimeScale = 1.05f;
        else if (_cRealDelayTarget < (SNAPSHOT_INTERVAL * -INTERP_NEGATIVE_THRESHOLD))
            _cInterpTimeScale = 0.95f;
        else _cInterpTimeScale = 1.0f;

        _cTimeSinceLastSnapshotReceived += Time.unscaledDeltaTime;
    }

    void ClientUpdateInterpolationTime()
    {
        _cInterpolationTime += (Time.unscaledDeltaTime * _cInterpTimeScale);
    }

    void ClientRenderLatestPostion()
    {
        if (_cSnapshots.Count > 0)
        {
            var interpFrom = default(Vector3);
            var interpTo = default(Vector3);
            var interpAlpha = default(float);

            for (int i = 0; i < _cSnapshots.Count; ++i)
            {
                if (i + 1 == _cSnapshots.Count)
                {
                    if (_cSnapshots[0].Time > _cInterpolationTime)
                    {
                        interpFrom = interpTo = _cSnapshots[0].Position;
                        interpAlpha = 0;
                    }
                    else
                    {
                        interpFrom = interpTo = _cSnapshots[i].Position;
                        interpAlpha = 0;
                    }
                }
                else
                {

                    var f = i;
                    var t = i + 1;

                    if (_cSnapshots[f].Time <= _cInterpolationTime && _cSnapshots[t].Time >= _cInterpolationTime)
                    {
                        interpFrom = _cSnapshots[f].Position;
                        interpTo = _cSnapshots[t].Position;

                        var range = _cSnapshots[t].Time - _cSnapshots[f].Time;
                        var current = _cInterpolationTime - _cSnapshots[f].Time;

                        interpAlpha = Mathf.Clamp01(current / range);

                        break;
                    }
                }
            }
            Client.transform.position = Vector3.Lerp(interpFrom, interpTo, interpAlpha);
        }
    }

    void ServerMovement()
    {
        Vector3 pos;
        pos = Server.transform.position;
        pos.x = Mathf.PingPong(Time.time * 5, 10f) - 5f;

        Server.transform.position = pos;
    }

    //[SerializeField, Range(0, 0.4f)] float random;
    //public void ServerSnapshot()
    //{
    //    if (_lastSnapshot + Time.fixedDeltaTime < Time.time)
    //    {
    //        _lastSnapshot = Time.time;
    //        _cNetworkSimQueue.Enqueue(new Snapshot
    //        {
    //            Time = _lastSnapshot,
    //            Position = Server.transform.position,
    //            DeliveryTime = Time.time + random
    //        });
    //    }
    //}

    public void ServerSnapshot()
    {
        if (_lastSnapshot + Time.fixedDeltaTime < Time.time)
        {
            _lastSnapshot = Time.time;
            _cNetworkSimQueue.Enqueue(new Snapshot
            {
                Time = _lastSnapshot,
                Position = Server.transform.position,
                DeliveryTime = Time.time
            });
        }
    }
}