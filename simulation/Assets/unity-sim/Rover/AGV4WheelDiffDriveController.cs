using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Geometry;

namespace RosSharp.Control
{
    public enum DriveControlMode
    {
        Keyboard,
        ROS
    }

    public class AGV4WheelDiffDriveController : MonoBehaviour
    {
        [Header("Wheel References")]
        public GameObject frontLeftWheel;
        public GameObject rearLeftWheel;
        public GameObject frontRightWheel;
        public GameObject rearRightWheel;

        [Header("Control Mode")]
        public DriveControlMode mode = DriveControlMode.ROS;

        [Header("Robot Parameters")]
        public float wheelRadius = 0.033f;   // meters
        public float trackWidth = 0.288f;    // meters (distance between left and right wheels)
        public float maxLinearSpeed = 1.5f;  // m/s
        public float maxAngularSpeed = 1.0f; // rad/s

        [Header("Articulation Settings")]
        public float forceLimit = 1000f;
        public float damping = 5f;

        [Header("ROS Settings")]
        public float rosTimeout = 0.5f;

        private ArticulationBody fl;
        private ArticulationBody rl;
        private ArticulationBody fr;
        private ArticulationBody rr;

        private ROSConnection ros;

        private float rosLinear = 0f;
        private float rosAngular = 0f;
        private float lastCmdTime = 0f;

        void Start()
        {
            fl = frontLeftWheel.GetComponent<ArticulationBody>();
            rl = rearLeftWheel.GetComponent<ArticulationBody>();
            fr = frontRightWheel.GetComponent<ArticulationBody>();
            rr = rearRightWheel.GetComponent<ArticulationBody>();

            ConfigureWheel(fl);
            ConfigureWheel(rl);
            ConfigureWheel(fr);
            ConfigureWheel(rr);

            ros = ROSConnection.GetOrCreateInstance();
            ros.Subscribe<TwistMsg>("/cmd_vel", ReceiveROSCmd);
        }

        void FixedUpdate()
        {
            if (mode == DriveControlMode.Keyboard)
            {
                KeyboardUpdate();
            }
            else
            {
                ROSUpdate();
            }
        }

        // ---------------- ROS ----------------

        void ReceiveROSCmd(TwistMsg msg)
        {
            rosLinear = (float)msg.linear.x;
            rosAngular = (float)msg.angular.z;
            lastCmdTime = Time.time;
        }

        void ROSUpdate()
        {
            if (Time.time - lastCmdTime > rosTimeout)
            {
                rosLinear = 0f;
                rosAngular = 0f;
            }

            Drive(rosLinear, rosAngular);
        }

        // ---------------- Keyboard ----------------

        void KeyboardUpdate()
        {
            float linear = Input.GetAxis("Vertical") * maxLinearSpeed;
            float angular = Input.GetAxis("Horizontal") * maxAngularSpeed;
            Drive(linear, angular);
        }

        // ---------------- Core Drive Logic ----------------

        void Drive(float linear, float angular)
        {
            linear = Mathf.Clamp(linear, -maxLinearSpeed, maxLinearSpeed);
            angular = Mathf.Clamp(angular, -maxAngularSpeed, maxAngularSpeed);

            // Differential drive equations
            float leftRadPerSec =
                (linear - angular * trackWidth * 0.5f) / wheelRadius;

            float rightRadPerSec =
                (linear + angular * trackWidth * 0.5f) / wheelRadius;

            // Convert rad/s → deg/s (Unity ArticulationBody expects degrees)
            float leftDegPerSec = leftRadPerSec * Mathf.Rad2Deg;
            float rightDegPerSec = rightRadPerSec * Mathf.Rad2Deg;

            SetWheelSpeed(fl, leftDegPerSec);
            SetWheelSpeed(rl, leftDegPerSec);
            SetWheelSpeed(fr, rightDegPerSec);
            SetWheelSpeed(rr, rightDegPerSec);
        }

        // ---------------- Articulation Utilities ----------------

        void ConfigureWheel(ArticulationBody wheel)
        {
            var drive = wheel.xDrive;
            drive.forceLimit = forceLimit;
            drive.damping = damping;
            drive.stiffness = 0f;
            wheel.xDrive = drive;
        }

        void SetWheelSpeed(ArticulationBody wheel, float targetVelocity)
        {
            var drive = wheel.xDrive;
            drive.targetVelocity = targetVelocity;
            wheel.xDrive = drive;
        }
    }
}
