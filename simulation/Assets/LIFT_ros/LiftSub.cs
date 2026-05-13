using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using Unity.Robotics.ROSTCPConnector.ROSGeometry;
using RosMessageTypes.Std;
using System.Collections;

public class LiftSub : MonoBehaviour
{
    public LiftROSController liftController; // Reference to your LiftController script

    ROSConnection ros;

    void Start()
    {
        ros = ROSConnection.GetOrCreateInstance();
        // Subscribe to /lift_cmd topic
        ros.Subscribe<StringMsg>("/lift_cmd", MoveLiftCallback);
    }

    void MoveLiftCallback(StringMsg msg)
    {
        string cmd = msg.data.Trim();

        if (cmd.StartsWith("FLOOR:"))
        {
            if (int.TryParse(cmd.Substring(6), out int targetFloor))
            {
                targetFloor = Mathf.Clamp(targetFloor - 1, 0, 9);
                liftController.MoveToFloor(targetFloor);
            }
        }
        else if (cmd == "s")
        {
            liftController.EmergencyStop();
        }
        else if (cmd == "r")
        {
            liftController.Resume();
        }
    }
}
