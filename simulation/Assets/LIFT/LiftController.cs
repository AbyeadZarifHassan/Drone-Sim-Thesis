using UnityEngine;

public class LiftController : MonoBehaviour
{
    [Header("Lift Configuration")]
    public int totalFloors = 10;
    public float floorHeight = 3.0f;     // units per floor
    public float maxSpeed = 2.0f;         // units/sec
    public float accelTime = 0.5f;        // soft start
    public float decelTime = 0.5f;        // soft stop

    [Header("Runtime State")]
    public int currentFloor = 1;
    public int targetFloor = 1;
    public bool emergencyStop = false;

    private float currentSpeed = 0f;
    private float targetY;
    private bool moving = false;

    void Start()
    {
        SetFloorInstant(1);
    }

    void Update()
    {
        HandleKeyboardInput();

        if (moving && !emergencyStop)
        {
            MoveLift();
        }
    }

    void HandleKeyboardInput()
    {
        // Floor selection (1–0 keys → floors 1–10)
        for (int i = 1; i <= totalFloors; i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha0 + i % 10))
            {
                RequestFloor(i);
            }
        }

        // Emergency stop
        if (Input.GetKeyDown(KeyCode.S))
        {
            EmergencyStop();
        }

        // Resume
        if (Input.GetKeyDown(KeyCode.R))
        {
            emergencyStop = false;
        }
    }

    public void RequestFloor(int floor)
    {
        if (floor < 1 || floor > totalFloors) return;
        if (floor == currentFloor) return;
        if (emergencyStop) return;

        targetFloor = floor;
        targetY = (floor - 1) * floorHeight;
        moving = true;
    }

    void MoveLift()
    {
        float direction = Mathf.Sign(targetY - transform.position.y);

        // Soft start
        currentSpeed = Mathf.MoveTowards(
            currentSpeed,
            maxSpeed,
            Time.deltaTime * (maxSpeed / accelTime)
        );

        float step = currentSpeed * Time.deltaTime * direction;
        transform.position += new Vector3(0, step, 0);

        // Soft stop near target
        if (Mathf.Abs(transform.position.y - targetY) < 0.1f)
        {
            transform.position = new Vector3(
                transform.position.x,
                targetY,
                transform.position.z
            );

            currentSpeed = 0f;
            moving = false;
            currentFloor = targetFloor;
        }
    }

    void EmergencyStop()
    {
        emergencyStop = true;
        moving = false;
        currentSpeed = 0f;
    }

    void SetFloorInstant(int floor)
    {
        currentFloor = floor;
        targetFloor = floor;
        transform.position = new Vector3(
            transform.position.x,
            (floor - 1) * floorHeight,
            transform.position.z
        );
    }
}
