using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class RoboticArmUIController : MonoBehaviour
{
    [System.Serializable]
    public class JointUI
    {
        [Header("Joint")]
        public string jointName;
        public Transform jointTransform;
        public Vector3 rotationAxis = Vector3.up;

        [Header("Limits (deg)")]
        public float minAngle = -180f;
        public float maxAngle = 180f;

        [Header("UI")]
        public Slider slider;
        public TextMeshProUGUI angleLabel;

        [HideInInspector] public float currentAngle;
        [HideInInspector] public Quaternion neutralLocalRotation;
    }

    public JointUI[] joints;

    void Start()
    {
        Initialize();
    }

    private void Initialize()
    {
        for (int i = 0; i < joints.Length; i++)
        {
            JointUI j = joints[i];

            if (!j.jointTransform || !j.slider)
                continue;

            // This must be the MECHANICAL ZERO pose
            j.neutralLocalRotation = j.jointTransform.localRotation;
            j.currentAngle = 0f;

            j.slider.minValue = j.minAngle;
            j.slider.maxValue = j.maxAngle;
            j.slider.value = j.currentAngle;

            int index = i;
            j.slider.onValueChanged.AddListener(
                v => OnSliderChanged(index, v)
            );

            ApplyJoint(index);
        }
    }

    private void OnSliderChanged(int index, float angle)
    {
        JointUI j = joints[index];
        j.currentAngle = Mathf.Clamp(angle, j.minAngle, j.maxAngle);
        ApplyJoint(index);
    }

    private void ApplyJoint(int index)
    {
        JointUI j = joints[index];

        Quaternion jointRotation =
            Quaternion.AngleAxis(j.currentAngle, j.rotationAxis.normalized);

        // This does NOT touch children incorrectly
        j.jointTransform.localRotation =
            j.neutralLocalRotation * jointRotation;

        if (j.angleLabel)
            j.angleLabel.text = $"{j.jointName}: {j.currentAngle:F1}°";
    }

    // --------- External control safe ---------

    public void SetJointAngle(int index, float angle)
    {
        joints[index].slider.SetValueWithoutNotify(angle);
        OnSliderChanged(index, angle);
    }
}
