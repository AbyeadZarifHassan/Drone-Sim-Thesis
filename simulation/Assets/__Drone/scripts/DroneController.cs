using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Geometry;

public class DroneController : MonoBehaviour
{
    private Rigidbody rb;
    private ROSConnection ros;

    [Header("Movement Settings")]
    public float linearSpeed = 5f;
    public float angularSpeed = 90f; // degrees per second
    public float verticalSpeed = 3f;

    [Header("ROS Topic")]
    public string cmdVelTopic = "cmd_vel";

    [Header("Control Mode")]
    [Tooltip("If true, drone listens to ROS commands. If false, keyboard input is used.")]
    public bool useRosControl = true;

    // Current velocity commands (in local space)
    private Vector3 currentLocalVelocity;
    private float currentYawSpeed; // degrees per second

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false; // Drones fly – disable gravity

        ros = ROSConnection.GetOrCreateInstance();
        ros.Subscribe<TwistMsg>(cmdVelTopic, OnCmdVelReceived);
    }

    void Update()
    {
        if (useRosControl)
        {
            // Apply commands received from ROS
            // Convert local velocity to world space and apply
            rb.linearVelocity = transform.TransformDirection(currentLocalVelocity);
            transform.Rotate(0, currentYawSpeed * Time.deltaTime, 0);
        }
        else
        {
            // Keyboard input for local testing
            HandleKeyboardInput();
        }

        // Optional: Press 'M' to toggle control mode
        if (Input.GetKeyDown(KeyCode.M))
        {
            useRosControl = !useRosControl;
            Debug.Log($"Control mode switched to {(useRosControl ? "ROS" : "Keyboard")}");
        }
    }

    void HandleKeyboardInput()
    {
        // Get input axes
        float horizontal = Input.GetAxis("Horizontal"); // A/D or Left/Right (local X)
        float vertical = Input.GetAxis("Vertical");     // W/S or Up/Down (local Z)
        float upDown = 0f;
        if (Input.GetKey(KeyCode.Q)) upDown = -1f; // Down
        if (Input.GetKey(KeyCode.E)) upDown = 1f;  // Up
        float yaw = 0f;
        if (Input.GetKey(KeyCode.LeftArrow)) yaw = -1f; // Rotate left
        if (Input.GetKey(KeyCode.RightArrow)) yaw = 1f; // Rotate right

        // Build local velocity vector
        // forward/back = vertical (W/S) -> local +Z
        // left/right = horizontal (A/D) -> local +X (right)
        // up/down = upDown (Q/E) -> local +Y
        Vector3 localMove = new Vector3(horizontal, upDown, vertical) * linearSpeed;

        // Apply velocity in world space
        rb.linearVelocity = transform.TransformDirection(localMove);

        // Apply yaw rotation
        float yawAngle = yaw * angularSpeed * Time.deltaTime;
        transform.Rotate(0, yawAngle, 0);
    }

    void OnCmdVelReceived(TwistMsg msg)
    {
        // ROS Twist message convention (for a drone):
        //   linear.x : forward/back (+ forward)
        //   linear.y : left/right (+ left)
        //   linear.z : up/down (+ up)
        //   angular.z : yaw (+ left turn)

        // Map to Unity local space:
        //   forward/back -> local +Z (linear.x)
        //   left/right   -> local +X (but ROS +left = Unity -right? We'll use +left = -X for simplicity)
        //   up/down      -> local +Y (linear.z)
        //   yaw          -> rotate around Y (angular.z, converted to degrees)

        float forwardBack = (float)msg.linear.x;
        float leftRight = (float)msg.linear.y; // +left
        float upDown = (float)msg.linear.z;

        // Convert to local velocity vector (Unity: +X = right, +Y = up, +Z = forward)
        // We'll keep leftRight as is: positive left -> move in -X direction (left)
        // If you prefer positive left to move left, that's correct. If you want positive right,
        // change leftRight to -leftRight.
        currentLocalVelocity = new Vector3(-leftRight, upDown, forwardBack) * linearSpeed;

        // Yaw: ROS uses rad/s, convert to deg/s
        currentYawSpeed = (float)msg.angular.z * Mathf.Rad2Deg;
    }
}