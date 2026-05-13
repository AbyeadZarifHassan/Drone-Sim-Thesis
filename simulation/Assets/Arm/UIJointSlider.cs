using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIJointSlider : MonoBehaviour
{
    [Header("References")]
    public RoboticArmController armController;
    public int jointIndex;

    [Header("UI")]
    public Slider slider;
    public TextMeshProUGUI angleText;

    void Start()
    {
        if (slider == null)
            slider = GetComponent<Slider>();

        // Configure slider from joint limits
        var joint = armController.joints[jointIndex];
        slider.minValue = joint.minAngle;
        slider.maxValue = joint.maxAngle;

        slider.onValueChanged.AddListener(OnSliderChanged);

        // Initialize UI
        slider.value = joint.currentAngle;
        UpdateText(slider.value);
    }

    public void OnSliderChanged(float angle)
    {
        armController.SetJointAngle(jointIndex, angle);
        UpdateText(angle);
    }

    private void UpdateText(float angle)
    {
        if (angleText != null)
            angleText.text = $"{angle:F1}°";
    }
}
