using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.BuiltinInterfaces;
using RosMessageTypes.Rosgraph;

public class ClockPublisher : MonoBehaviour
{
    [Header("Clock Configuration")]
    public float publishFrequency = 100f;

    private ROSConnection ros;
    private double lastPublishTime = 0;
    private float publishInterval;
    private string clockTopic = "/clock";

    public static TimeMsg LatestSimTime { get; private set; }

    void Start()
    {
        ros = ROSConnection.GetOrCreateInstance();
        ros.RegisterPublisher<ClockMsg>(clockTopic);

        publishInterval = 1.0f / publishFrequency;
    }

    void Update()
    {
        double currentTime = Time.timeAsDouble;
        if (currentTime - lastPublishTime >= publishInterval)
        {
            PublishClock();
            lastPublishTime = currentTime;
        }
    }

    void PublishClock()
    {
        var simTime = new TimeMsg
        {
            sec = (int)Time.time,
            nanosec = (uint)((Time.time - Mathf.Floor(Time.time)) * 1e9f)
        };

        var clockMsg = new ClockMsg
        {
            clock = simTime
        };

        LatestSimTime = simTime; // This makes it globally accessible
        ros.Publish(clockTopic, clockMsg);
    }
}
