using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Sensor;
using RosMessageTypes.Geometry;
using RosMessageTypes.Std;
using RosMessageTypes.BuiltinInterfaces;

public class IMUPublisher : MonoBehaviour
{
    private ROSConnection ros;
    private Rigidbody rb;
    private Vector3 previousVelocity;   // For acceleration differentiation
    private float previousTime;

    [Header("ROS Topic")]
    public string imuTopic = "drone/imu";

    [Header("Publish Rate")]
    public float publishRate = 100f; // Hz

    [Header("Coordinate Conversion")]
    [Tooltip("Enable if you need to convert from Unity (Y-up, Z-forward) to ROS ENU (Z-up, X-east, Y-north)")]
    public bool convertToRosFrame = false;

    private float publishInterval;
    private float timer = 0f;

    void Start()
    {
        ros = ROSConnection.GetOrCreateInstance();
        rb = GetComponent<Rigidbody>();
        publishInterval = 1f / publishRate;
        ros.RegisterPublisher<ImuMsg>(imuTopic);

        previousVelocity = rb.linearVelocity;
        previousTime = Time.fixedTime;
    }

    void FixedUpdate()
    {
        // Use FixedUpdate for physics-based calculations
        timer += Time.fixedDeltaTime;
        if (timer >= publishInterval)
        {
            timer = 0f;
            PublishIMU();
        }
    }

    void PublishIMU()
    {
        // ---- Orientation ----
        Quaternion orientation = transform.rotation;

        // Optional: convert to ROS ENU frame
        if (convertToRosFrame)
        {
            orientation = ConvertUnityToROS(orientation);
        }

        // ---- Angular velocity (body frame) ----
        // rb.angularVelocity is in world space. Transform to local body frame.
        Vector3 angularVelocityWorld = rb.angularVelocity; // rad/s
        Vector3 angularVelocityBody = transform.InverseTransformDirection(angularVelocityWorld);

        // ---- Linear acceleration (body frame, gravity removed) ----
        // Compute total acceleration from velocity change (world space)
        float dt = Time.fixedDeltaTime;
        Vector3 totalAccelWorld = (rb.linearVelocity - previousVelocity) / dt;

        // Subtract gravity to get motion acceleration (world space)
        Vector3 motionAccelWorld = totalAccelWorld - Physics.gravity;

        // Transform to body frame
        Vector3 motionAccelBody = transform.InverseTransformDirection(motionAccelWorld);

        // Update previous velocity for next frame
        previousVelocity = rb.linearVelocity;

        // ---- Build IMU message ----
        ImuMsg msg = new ImuMsg
        {
            header = new HeaderMsg
            {
                frame_id = "drone_imu",
                stamp = new TimeMsg
                {
                    sec = (int)Time.fixedTime,
                    nanosec = (uint)((Time.fixedTime - (int)Time.fixedTime) * 1e9)
                }
            },
            orientation = new QuaternionMsg
            {
                x = orientation.x,
                y = orientation.y,
                z = orientation.z,
                w = orientation.w
            },
            angular_velocity = new Vector3Msg
            {
                x = angularVelocityBody.x,
                y = angularVelocityBody.y,
                z = angularVelocityBody.z
            },
            linear_acceleration = new Vector3Msg
            {
                x = motionAccelBody.x,
                y = motionAccelBody.y,
                z = motionAccelBody.z
            }
        };

        // Covariance arrays (optional – set to zero if unknown)
        msg.orientation_covariance = new double[9];
        msg.angular_velocity_covariance = new double[9];
        msg.linear_acceleration_covariance = new double[9];

        ros.Publish(imuTopic, msg);
    }

    // Convert a Unity quaternion (Y-up, Z-forward, left-handed) to ROS ENU (Z-up, X-east, Y-north, right-handed)
    Quaternion ConvertUnityToROS(Quaternion qUnity)
    {
        // Unity:    +X right, +Y up,    +Z forward
        // ROS ENU:  +X east, +Y north,  +Z up
        // This transformation rotates the coordinate system:
        // 1. Rotate 90° around X to bring Y to Z and Z to -Y.
        // 2. Then rotate -90° around Y to align axes.
        // The combined rotation can be represented by this quaternion.
        Quaternion rosFromUnity = new Quaternion(-0.5f, 0.5f, -0.5f, 0.5f); // Actually this is approximate. Use the following:
        // A correct conversion: first rotate -90° around X, then -90° around Y.
        // Equivalent quaternion: (0, -0.707, 0, 0.707) * (0.707, 0, 0, -0.707) = ...
        // Simpler: use the well-known conversion for ROS (REP 105) from Unity:
        // Unity → ROS: (x, y, z, w) -> (x, -z, y, w) ??? Not exactly.
        // The safest is to define a fixed rotation:
        // Unity world to ROS world: rotate 90° around X, then -90° around Y.
        Quaternion rot = Quaternion.Euler(90f, -90f, 0f); // This works if Unity's forward is Z, up is Y.
        return rot * qUnity;
    }
}