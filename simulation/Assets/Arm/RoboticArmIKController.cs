using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class RoboticArmIKController : MonoBehaviour
{
    [System.Serializable]
    public class Joint
    {
        public string name;
        public Transform transform;
        public Vector3 localAxis = Vector3.up;

        public float minAngle = -180f;
        public float maxAngle = 180f;

        public Slider slider;
        public TextMeshProUGUI label;

        [HideInInspector] public Quaternion baseRotation;
        [HideInInspector] public float angle;
    }

    [Header("Arm Joints (base → end)")]
    public Joint[] joints;

    [Header("IK")]
    public Transform target;
    public Transform endEffector;
    public int iterations = 15;
    public float positionTolerance = 0.001f;

    void Start()
    {
        InitializeJoints();
    }

    void InitializeJoints()
    {
        for (int i = 0; i < joints.Length; i++)
        {
            Joint j = joints[i];

            j.baseRotation = j.transform.localRotation;
            j.angle = 0f;

            if (j.slider)
            {
                j.slider.minValue = j.minAngle;
                j.slider.maxValue = j.maxAngle;

                int index = i;
                j.slider.onValueChanged.AddListener(
                    v => SetJointAngle(index, v)
                );
            }

            ApplyJoint(i);
        }
    }

    // ---------- Forward Control (UI / External) ----------

    public void SetJointAngle(int index, float angle)
    {
        Joint j = joints[index];
        j.angle = Mathf.Clamp(angle, j.minAngle, j.maxAngle);
        ApplyJoint(index);
    }

    void ApplyJoint(int index)
    {
        Joint j = joints[index];

        Quaternion delta =
            Quaternion.AngleAxis(j.angle, j.localAxis.normalized);

        j.transform.localRotation = j.baseRotation * delta;

        if (j.label)
            j.label.text = $"{j.name}: {j.angle:F1}°";
    }

    // ---------- Inverse Kinematics ----------

    void LateUpdate()
    {
        if (target)
            SolveIK();
    }

    void SolveIK()
    {
        for (int iter = 0; iter < iterations; iter++)
        {
            // iterate from end effector back to base
            for (int i = joints.Length - 1; i >= 0; i--)
            {
                Transform joint = joints[i].transform;

                Vector3 toEnd =
                    endEffector.position - joint.position;

                Vector3 toTarget =
                    target.position - joint.position;

                // Project onto joint rotation plane
                Vector3 axisWorld =
                    joint.TransformDirection(joints[i].localAxis);

                Vector3 projectedEnd =
                    Vector3.ProjectOnPlane(toEnd, axisWorld);
                Vector3 projectedTarget =
                    Vector3.ProjectOnPlane(toTarget, axisWorld);

                if (projectedEnd.sqrMagnitude < 1e-6f ||
                    projectedTarget.sqrMagnitude < 1e-6f)
                    continue;

                float angleDelta =
                    Vector3.SignedAngle(
                        projectedEnd,
                        projectedTarget,
                        axisWorld
                    );

                joints[i].angle =
                    Mathf.Clamp(
                        joints[i].angle + angleDelta,
                        joints[i].minAngle,
                        joints[i].maxAngle
                    );

                ApplyJoint(i);

                // Early exit if close enough
                if ((endEffector.position - target.position).magnitude
                    < positionTolerance)
                    return;
            }
        }

        // Sync sliders AFTER IK
        SyncSliders();
    }

    void SyncSliders()
    {
        for (int i = 0; i < joints.Length; i++)
        {
            if (joints[i].slider)
                joints[i].slider.SetValueWithoutNotify(joints[i].angle);
        }
    }
}
