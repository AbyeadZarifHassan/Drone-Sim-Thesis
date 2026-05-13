using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Std;
using System.Diagnostics;

/// <summary>
/// Publishes the Drone GameObject's raw Unity position and rotation
/// as a plain string to a single ROS topic. No coordinate conversion.
///
/// Topic published:
///   /drone/pose   (std_msgs/String)
///   Format: "px:1.2300,py:4.5600,pz:7.8900,rx:0.0000,ry:90.0000,rz:0.0000"
///
/// Setup:
///   1. Attach this script to any GameObject in the scene.
///   2. Assign the Drone GameObject in the Inspector, or leave empty to auto-find by name "Drone".
///   3. Make sure the ROSTCPConnector component is present in the scene.
/// </summary>
public class DronePosePublisher : MonoBehaviour
{
    [Header("ROS Settings")]
    public string poseTopic = "/drone/pose";
    public float publishHz = 20f;

    [Header("Drone Reference")]
    [Tooltip("Drag the Drone GameObject here, or leave empty to auto-find by name")]
    public Transform droneTransform;

    private ROSConnection _ros;
    private float _publishInterval;
    private float _timer;

    void Start()
    {
        if (droneTransform == null)
        {
            GameObject droneGO = GameObject.Find("Drone");
            if (droneGO != null)
            {
                droneTransform = droneGO.transform;
                UnityEngine.Debug.Log("[DronePosePublisher] Auto-found 'Drone' GameObject.");
            }
            else
            {
                UnityEngine.Debug.LogError("[DronePosePublisher] Could not find 'Drone'. Assign it manually.");
                enabled = false;
                return;
            }
        }

        _ros = ROSConnection.GetOrCreateInstance();
        _ros.RegisterPublisher<StringMsg>(poseTopic);

        _publishInterval = 1f / Mathf.Max(publishHz, 0.1f);
        UnityEngine.Debug.Log($"[DronePosePublisher] Publishing to '{poseTopic}' at {publishHz} Hz.");
    }

    void Update()
    {
        _timer += Time.deltaTime;
        if (_timer < _publishInterval) return;
        _timer = 0f;

        PublishPose();
    }

    void PublishPose()
    {
        Vector3 pos   = droneTransform.position;
        Vector3 euler = droneTransform.rotation.eulerAngles;
        UnityEngine.Debug.Log($"[DronePosePublisher] Publishing Pose - Position: ({pos.x:F2}, {pos.y:F2}, {pos.z:F2}), " +
                  $"Rotation (Euler): ({euler.x:F2}, {euler.y:F2}, {euler.z:F2})");

        string data = $"px:{pos.x:F4},py:{pos.y:F4},pz:{pos.z:F4}," +
                      $"rx:{euler.x:F4},ry:{euler.y:F4},rz:{euler.z:F4}";

        _ros.Publish(poseTopic, new StringMsg(data));
    }
}