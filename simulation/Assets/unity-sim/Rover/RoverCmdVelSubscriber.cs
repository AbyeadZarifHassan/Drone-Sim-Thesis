using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Geometry;

public class RoverCmdVelSubscriber : MonoBehaviour
{
    public float linearSpeedMultiplier = 1f;   // Unity units per second
    public float angularSpeedMultiplier = 50f; // degrees/sec

    private ROSConnection ros;
    private Rigidbody rb;

    private float linear = 0f;
    private float angular = 0f;

    void Start()
    {
        ros = ROSConnection.GetOrCreateInstance();
        rb = GetComponent<Rigidbody>();

        // Subscribe to /cmd_vel topic
        ros.Subscribe<TwistMsg>("/cmd_vel", CmdVelCallback);
    }

    void CmdVelCallback(TwistMsg msg)
    {
        // Save ROS velocities for FixedUpdate
        linear = (float)msg.linear.x * linearSpeedMultiplier;
        angular = (float)msg.angular.z * angularSpeedMultiplier;
    }

    void FixedUpdate()
    {
        // Move forward/backward smoothly
        Vector3 move = transform.forward * linear * Time.fixedDeltaTime;
        rb.MovePosition(rb.position + move);

        // Rotate smoothly around Y-axis
        Quaternion rot = Quaternion.Euler(0f, angular * Time.fixedDeltaTime, 0f);
        rb.MoveRotation(rb.rotation * rot);
    }
}
