using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Std;

public class JointAngleSubscriber : MonoBehaviour
{
    [Header("ROS Settings")]
    public string topicName = "/joint_angles";

    [Header("Assign Arm Joints Here")]
    public Transform baseJoint;
    public Transform shoulderJoint;
    public Transform elbowJoint;

    private ROSConnection ros;

    // Store initial rotations to respect prefab rest pose
    private Quaternion baseInitial;
    private Quaternion shoulderInitial;
    private Quaternion elbowInitial;

    void Start()
    {
        // Get ROS connection instance
        ros = ROSConnection.GetOrCreateInstance();
        ros.Subscribe<Float32MultiArrayMsg>(topicName, ReceiveJointAngles);

        Debug.Log($"Subscribed to {topicName}");

        // Save initial rotations
        if (baseJoint) baseInitial = baseJoint.localRotation;
        if (shoulderJoint) shoulderInitial = shoulderJoint.localRotation;
        if (elbowJoint) elbowInitial = elbowJoint.localRotation;
    }

    void ReceiveJointAngles(Float32MultiArrayMsg msg)
    {
        if (msg.data.Length < 3) return;

        // Apply rotations relative to initial rotation
        if (baseJoint) baseJoint.localRotation = baseInitial * Quaternion.Euler(0, (float)msg.data[0], 0);
        if (shoulderJoint) shoulderJoint.localRotation = shoulderInitial * Quaternion.Euler((float)msg.data[1], 0, 0);
        if (elbowJoint) elbowJoint.localRotation = elbowInitial * Quaternion.Euler((float)msg.data[2], 0, 0);

        // Debug log for troubleshooting
        Debug.Log($"Received joint angles: {msg.data[0]}, {msg.data[1]}, {msg.data[2]}");
    }
}
