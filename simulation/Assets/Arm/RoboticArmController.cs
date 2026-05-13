using UnityEngine;

public class RoboticArmController : MonoBehaviour
{
    [System.Serializable]
    public class Joint
    {
        public string jointName;
        public Transform jointTransform;

        public Vector3 rotationAxis = Vector3.up; // Local joint axis
        public float minAngle = -180f;
        public float maxAngle = 180f;

        [HideInInspector]
        public float currentAngle;
    }

    public Joint[] joints;

    void Start()
    {
        // Initialize joint angles from current pose
        for (int i = 0; i < joints.Length; i++)
        {
            joints[i].currentAngle = 0f;
            ApplyJointRotation(i);
        }
    }

    /// <summary>
    /// Set joint angle in degrees (absolute)
    /// </summary>
    public void SetJointAngle(int jointIndex, float angle)
    {
        if (jointIndex < 0 || jointIndex >= joints.Length) return;
        if (joints[jointIndex].jointTransform == null) return;

        joints[jointIndex].currentAngle =
            Mathf.Clamp(angle, joints[jointIndex].minAngle, joints[jointIndex].maxAngle);

        ApplyJointRotation(jointIndex);
    }

    /// <summary>
    /// Apply rotation to the joint
    /// </summary>
    private void ApplyJointRotation(int index)
    {
        Joint joint = joints[index];

        Quaternion rotation =
            Quaternion.AngleAxis(joint.currentAngle, joint.rotationAxis.normalized);

        joint.jointTransform.localRotation = rotation;
    }

    /// <summary>
    /// For external systems (Python / ROS / UI)
    /// </summary>
    public void SetAllJointAngles(float[] angles)
    {
        int count = Mathf.Min(angles.Length, joints.Length);
        for (int i = 0; i < count; i++)
        {
            SetJointAngle(i, angles[i]);
        }
    }
}
