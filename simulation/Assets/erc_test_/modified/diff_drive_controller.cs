using UnityEngine;
using TMPro;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Geometry;

public class DiffDriveController : MonoBehaviour
{
    [Header("Limits")]
    public float maxLinearSpeed = 2f;
    public float maxAngularSpeed = 2f;

    [Header("Manual Control")]
    public float manualLinearSpeed = 1.0f;
    public float manualAngularSpeed = 1.0f;

    [Header("PID Gains")]
    public float linearKp = 15f;
    public float angularKp = 10f;

    [Header("ROS")]
    public string cmdVelTopic = "/cmd_vel";
    public float cmdVelTimeout = 1.0f;

    public TextMeshProUGUI statusText;

    private Rigidbody rb;
    private ROSConnection ros;

    private float targetLinear;
    private float targetAngular;

    private float cmdLinear;
    private float cmdAngular;
    private float lastCmdTime;
    private bool useCmdVel;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.centerOfMass = Vector3.down * 0.2f;

        ros = ROSConnection.GetOrCreateInstance();
        ros.Subscribe<TwistStampedMsg>(cmdVelTopic, CmdVelCallback);
    }

    void CmdVelCallback(TwistStampedMsg msg)
    {
        cmdLinear = (float)msg.twist.linear.x;
        cmdAngular = (float)msg.twist.angular.z;

        lastCmdTime = Time.time;
        useCmdVel = true;
    }

    void Update()
    {
        if (useCmdVel && Time.time - lastCmdTime > cmdVelTimeout)
        {
            cmdLinear = 0f;
            cmdAngular = 0f;
            useCmdVel = false;
        }

        HandleInput();
        UpdateStatus();
    }

    void FixedUpdate()
    {
        ApplyVelocityControl();
    }

    void HandleInput()
    {
        if (Input.GetKey(KeyCode.W)) targetLinear = manualLinearSpeed;
        else if (Input.GetKey(KeyCode.S)) targetLinear = -manualLinearSpeed;
        else targetLinear = 0f;

        if (Input.GetKey(KeyCode.A)) targetAngular = manualAngularSpeed;
        else if (Input.GetKey(KeyCode.D)) targetAngular = -manualAngularSpeed;
        else targetAngular = 0f;

        if (Input.anyKey)
            useCmdVel = false;

        if (useCmdVel)
        {
            targetLinear = Mathf.Clamp(cmdLinear, -maxLinearSpeed, maxLinearSpeed);
            targetAngular = Mathf.Clamp(cmdAngular, -maxAngularSpeed, maxAngularSpeed);
        }
    }

    void ApplyVelocityControl()
    {
        Vector3 localVel = transform.InverseTransformDirection(rb.linearVelocity);

        float linearError = targetLinear - localVel.z;
        float angularError = targetAngular - rb.angularVelocity.y;

        float linearCmd = linearKp * linearError;
        float angularCmd = angularKp * angularError;

        Vector3 force = transform.forward * linearCmd;
        Vector3 torque = Vector3.up * angularCmd;

        rb.AddForce(force, ForceMode.Acceleration);
        rb.AddTorque(torque, ForceMode.Acceleration);
    }

    void UpdateStatus()
    {
        if (!statusText) return;

        statusText.text =
            $"Mode: {(useCmdVel ? "ROS /cmd_vel" : "Manual")}\n" +
            $"Target Linear: {targetLinear:F2} m/s\n" +
            $"Target Angular: {targetAngular:F2} rad/s\n" +
            $"Actual Linear: {rb.linearVelocity.magnitude:F2} m/s\n" +
            $"Actual Angular: {rb.angularVelocity.y:F2} rad/s";
    }
}
