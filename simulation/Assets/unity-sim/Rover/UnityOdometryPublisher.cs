using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Nav;
using RosMessageTypes.Geometry;
using RosMessageTypes.Sensor;
using RosMessageTypes.Std;
using RosMessageTypes.BuiltinInterfaces;

public class UnityOdometryPublisher : MonoBehaviour
{
    [Header("ROS")]
    public string odomTopic = "/odom";
    public string jointStateTopic = "/joint_states";
    public string wheelRotationTopic = "/wheel_rotation";
    public string frameId = "odom";
    public string childFrameId = "base_link";
    public float publishRate = 30f;

    [Header("Robot")]
    public Transform baseLink; // Must be the moving base_link
    public Rigidbody baseRigidbody;

    [Header("Wheels (Articulation Bodies)")]
    public ArticulationBody frontLeft;
    public ArticulationBody frontRight;
    public ArticulationBody rearLeft;
    public ArticulationBody rearRight;

    private ROSConnection ros;
    private float publishTimer;

    private Vector3 lastPosition;
    private Quaternion lastRotation;

    private string[] jointNames;
    private double[] jointPositions;
    private double[] jointVelocities;

    private float[] lastWheelPositions;
    private float[] wheelRotations;

    /* -------------------- ROS TIME -------------------- */
    private TimeMsg GetROSTime()
    {
        double t = Time.timeAsDouble;
        int sec = (int)t;
        uint nanosec = (uint)((t - sec) * 1e9);
        return new TimeMsg(sec, nanosec);
    }

    /* -------------------- INIT -------------------- */
    void Start()
    {
        ros = ROSConnection.GetOrCreateInstance();
        ros.RegisterPublisher<OdometryMsg>(odomTopic);
        ros.RegisterPublisher<JointStateMsg>(jointStateTopic);
        ros.RegisterPublisher<Float32MultiArrayMsg>(wheelRotationTopic);

        if (baseLink == null)
            baseLink = transform;

        lastPosition = baseLink.position;
        lastRotation = baseLink.rotation;

        jointNames = new string[]
        {
            frontLeft.name,
            frontRight.name,
            rearLeft.name,
            rearRight.name
        };

        jointPositions = new double[4];
        jointVelocities = new double[4];

        lastWheelPositions = new float[4];
        wheelRotations = new float[4];

        ArticulationBody[] wheels = { frontLeft, frontRight, rearLeft, rearRight };
        for (int i = 0; i < wheels.Length; i++)
        {
            lastWheelPositions[i] = wheels[i].jointPosition[0];
            wheelRotations[i] = 0f;
        }
    }

    /* -------------------- UPDATE -------------------- */
    void FixedUpdate()
    {
        publishTimer += Time.fixedDeltaTime;
        if (publishTimer >= 1f / publishRate)
        {
            publishTimer = 0f;
            PublishOdometry();
            PublishJointStates();
            PublishWheelRotation();
        }
    }

    /* -------------------- ODOMETRY -------------------- */
    void PublishOdometry()
    {
        float dt = Time.fixedDeltaTime;

        Vector3 currentPosition = baseLink.position;
        Quaternion currentRotation = baseLink.rotation;

        // Pose delta
        Vector3 deltaPos = currentPosition - lastPosition;
        Quaternion deltaRot = currentRotation * Quaternion.Inverse(lastRotation);

        // Local linear velocity
        Vector3 localVel = baseLink.InverseTransformDirection(deltaPos / dt);

        // Local angular velocity (yaw only)
        deltaRot.ToAngleAxis(out float angleDeg, out Vector3 axis);
        if (angleDeg > 180f) angleDeg -= 360f;
        float yawRate = Mathf.Deg2Rad * angleDeg / dt * Mathf.Sign(axis.y);

        lastPosition = currentPosition;
        lastRotation = currentRotation;

        OdometryMsg odom = new OdometryMsg
        {
            header = new HeaderMsg
            {
                stamp = GetROSTime(),
                frame_id = frameId
            },
            child_frame_id = childFrameId,
            pose = new PoseWithCovarianceMsg
            {
                pose = new PoseMsg
                {
                    position = new PointMsg(
                        currentPosition.z,
                        -currentPosition.x,
                        currentPosition.y
                    ),
                    orientation = new QuaternionMsg(
                        -currentRotation.z,
                        currentRotation.x,
                        -currentRotation.y,
                        currentRotation.w
                    )
                },
                covariance = new double[36]
            },
            twist = new TwistWithCovarianceMsg
            {
                twist = new TwistMsg
                {
                    linear = new Vector3Msg(
                        localVel.z,
                        -localVel.x,
                        0.0
                    ),
                    angular = new Vector3Msg(
                        0.0,
                        0.0,
                        yawRate
                    )
                },
                covariance = new double[36]
            }
        };

        ros.Publish(odomTopic, odom);
    }

    /* -------------------- JOINT STATES -------------------- */
    void PublishJointStates()
    {
        ArticulationBody[] wheels = { frontLeft, frontRight, rearLeft, rearRight };
        for (int i = 0; i < wheels.Length; i++)
        {
            jointPositions[i] = wheels[i].jointPosition[0];
            jointVelocities[i] = wheels[i].jointVelocity[0];
        }

        JointStateMsg jointState = new JointStateMsg
        {
            header = new HeaderMsg
            {
                stamp = GetROSTime(),
                frame_id = childFrameId
            },
            name = jointNames,
            position = jointPositions,
            velocity = jointVelocities,
            effort = new double[4]
        };

        ros.Publish(jointStateTopic, jointState);
    }

    /* -------------------- WHEEL ROTATION -------------------- */
    void PublishWheelRotation()
    {
        ArticulationBody[] wheels = { frontLeft, frontRight, rearLeft, rearRight };
        float[] delta = new float[4];

        for (int i = 0; i < wheels.Length; i++)
        {
            float pos = wheels[i].jointPosition[0];
            delta[i] = pos - lastWheelPositions[i];
            lastWheelPositions[i] = pos;

            // Handle wrap-around
            if (delta[i] > Mathf.PI) delta[i] -= 2f * Mathf.PI;
            if (delta[i] < -Mathf.PI) delta[i] += 2f * Mathf.PI;

            wheelRotations[i] += delta[i];
        }

        Float32MultiArrayMsg msg = new Float32MultiArrayMsg
        {
            data = new float[4] { wheelRotations[0], wheelRotations[1], wheelRotations[2], wheelRotations[3] }
        };

        ros.Publish(wheelRotationTopic, msg);
    }
}
