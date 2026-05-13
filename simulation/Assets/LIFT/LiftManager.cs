using UnityEngine;

public class LiftManager : MonoBehaviour
{
    public enum LiftState
    {
        Idle,
        Moving,
        EmergencyStop
    }

    [Header("Configuration")]
    public int totalFloors = 10;
    public int groundFloor = 1;          // configurable
    public float floorHeight = 3.0f;

    [Header("Motion Settings")]
    public float maxSpeed = 2.0f;
    public float accelTime = 0.6f;
    public float stopTolerance = 0.05f;

    [Header("Runtime State (Read Only)")]
    public int currentFloor;
    public int targetFloor;
    public LiftState state = LiftState.Idle;

    private float targetY;
    private float currentSpeed;

    void Start()
    {
        SetFloorInstant(groundFloor);
    }

    void Update()
    {
        HandleKeyboardInput();

        if (state == LiftState.Moving)
        {
            MoveLift();
        }
    }

    // =========================
    // INPUT LAYER
    // =========================
    void HandleKeyboardInput()
    {
        // Floors 1–10 (0 key = 10)
        for (int i = 1; i <= totalFloors; i++)
        {
            KeyCode key = (i == 10) ? KeyCode.Alpha0 : KeyCode.Alpha0 + i;
            if (Input.GetKeyDown(key))
            {
                RequestFloor(i);
            }
        }

        if (Input.GetKeyDown(KeyCode.S))
            EmergencyStop();

        if (Input.GetKeyDown(KeyCode.R))
            Resume();
    }

    // =========================
    // PUBLIC API (UI / ROS / NET)
    // =========================
    public void RequestFloor(int floor)
    {
        if (floor < 1 || floor > totalFloors) return;
        if (state == LiftState.EmergencyStop) return;
        if (floor == currentFloor) return;

        targetFloor = floor;
        targetY = FloorToY(floor);
        state = LiftState.Moving;
    }

    public void EmergencyStop()
    {
        state = LiftState.EmergencyStop;
        currentSpeed = 0f;
    }

    public void Resume()
    {
        if (state == LiftState.EmergencyStop)
            state = LiftState.Idle;
    }

    // =========================
    // MOTION CONTROL
    // =========================
    void MoveLift()
    {
        float direction = Mathf.Sign(targetY - transform.position.y);

        currentSpeed = Mathf.MoveTowards(
            currentSpeed,
            maxSpeed,
            Time.deltaTime * (maxSpeed / accelTime)
        );

        transform.position += Vector3.up * direction * currentSpeed * Time.deltaTime;

        if (Mathf.Abs(transform.position.y - targetY) <= stopTolerance)
        {
            transform.position = new Vector3(
                transform.position.x,
                targetY,
                transform.position.z
            );

            currentSpeed = 0f;
            currentFloor = targetFloor;
            state = LiftState.Idle;
        }
    }

    // =========================
    // UTILS
    // =========================
    float FloorToY(int floor)
    {
        return (floor - groundFloor) * floorHeight;
    }

    void SetFloorInstant(int floor)
    {
        currentFloor = floor;
        targetFloor = floor;
        transform.position = new Vector3(
            transform.position.x,
            FloorToY(floor),
            transform.position.z
        );
    }
}
