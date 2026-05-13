using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using Unity.Robotics.ROSTCPConnector.ROSGeometry;
using RosMessageTypes.Nav;
using RosMessageTypes.Tf2;
using RosMessageTypes.Geometry;
using RosMessageTypes.Std;
using RosMessageTypes.BuiltinInterfaces;

[RequireComponent(typeof(Rigidbody))]
public class OdometryPublisher : MonoBehaviour
{
    [Header("ROS Topics")]
    public string odomTopic = "/panther/odometry/filtered";
    public string tfTopic = "/tf";
    public string frameId = "panther/odom";
    public string childFrameId = "panther/base_link";
    public float publishRate = 100f;

    private ROSConnection ros;
    private Rigidbody rb;
    private float lastPublishTime;

    void Start()
    {
        ros = ROSConnection.GetOrCreateInstance();
        ros.RegisterPublisher<OdometryMsg>(odomTopic);
        ros.RegisterPublisher<TFMessageMsg>(tfTopic);

        rb = GetComponent<Rigidbody>();
        lastPublishTime = Time.time;
    }

    void Update()
    {
        if (Time.time - lastPublishTime < 1f / publishRate)
            return;
        lastPublishTime = Time.time;

        PublishOdometry();
        PublishTF();
    }

    void PublishOdometry()
    {
        var now = new TimeMsg
        {
            sec = (int)Time.time,
            nanosec = (uint)((Time.time - (int)Time.time) * 1e9)
        };
        var header = new HeaderMsg { stamp = now, frame_id = frameId };

        var posRos = transform.position.To<FLU>();
        var rotRos = transform.rotation.To<FLU>();

        var posFlipped = new Vector3<FLU>(-posRos.x, -posRos.y, posRos.z);
        var flipQuat = new Quaternion<FLU>(0f, 0f, 1f, 0f);
        var rotFlipped = flipQuat * rotRos;

        var linRos = rb.linearVelocity.To<FLU>();
        var angRos = rb.angularVelocity.To<FLU>();

        var odom = new OdometryMsg
        {
            header = header,
            child_frame_id = childFrameId,
            pose = new PoseWithCovarianceMsg
            {
                pose = new PoseMsg
                {
                    position = new PointMsg { x = posFlipped.x, y = posFlipped.y, z = posFlipped.z },
                    orientation = new QuaternionMsg { x = rotFlipped.x, y = rotFlipped.y, z = rotFlipped.z, w = rotFlipped.w }
                },
                covariance = new double[36]
            },
            twist = new TwistWithCovarianceMsg
            {
                twist = new TwistMsg
                {
                    linear = new Vector3Msg { x = linRos.x, y = linRos.y, z = linRos.z },
                    angular = new Vector3Msg { x = angRos.x, y = angRos.y, z = angRos.z }
                },
                covariance = new double[36]
            }
        };

        ros.Publish(odomTopic, odom);
    }

    void PublishTF()
    {
        // Header
        var now = new TimeMsg
        {
            sec = (int)Time.time,
            nanosec = (uint)((Time.time - (int)Time.time) * 1e9)
        };
        var header = new HeaderMsg { stamp = now, frame_id = frameId };

        var posRos = transform.position.To<FLU>();
        var rotRos = transform.rotation.To<FLU>();

        var posFlipped = new Vector3<FLU>(-posRos.x, -posRos.y, posRos.z);
        var flipQuat = new Quaternion<FLU>(0f, 0f, 1f, 0f);
        var rotFlipped = flipQuat * rotRos;

        var tf = new TransformStampedMsg
        {
            header = header,
            child_frame_id = childFrameId,
            transform = new TransformMsg
            {
                translation = new Vector3Msg { x = posFlipped.x, y = posFlipped.y, z = posFlipped.z },
                rotation = new QuaternionMsg { x = rotFlipped.x, y = rotFlipped.y, z = rotFlipped.z, w = rotFlipped.w }
            }
        };

        var tfMsg = new TFMessageMsg
        {
            transforms = new TransformStampedMsg[] { tf }
        };

        ros.Publish(tfTopic, tfMsg);
    }
}