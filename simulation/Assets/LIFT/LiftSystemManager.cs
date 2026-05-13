using UnityEngine;

public class LiftSystemManager : MonoBehaviour
{
    public enum LiftState
    {
        Idle,
        Moving,
        EmergencyStop
    }

    public System.Action<int> OnArrivedAtFloor;

    [Header("Subsystems")]
    public DoorManager doorManager;
    public LiftScheduler scheduler;
    public BuildingFloorGenerator floorGenerator;

    [Header("References (Assignable Models)")]
    public Transform liftCabin;          // Any lift model
    public Transform groundReference;    // Any object marking ground floor

    [Header("Floor Configuration")]
    public int groundFloorIndex = 1;

    [Header("Motion Settings")]
    public float maxSpeed = 2.0f;
    public float accelerationTime = 0.6f;
    public float stopTolerance = 0.05f;

    [Header("Runtime State")]
    public int currentFloor;
    public int targetFloor;
    public LiftState state = LiftState.Idle;

    bool pendingMoveAfterDoorClose = false;

    public LiftLogPanel logPanel;

    float targetY;
    float currentSpeed;

    // =============================
    // INITIALIZATION
    // =============================
    void Start()
    {
        if (floorGenerator == null || liftCabin == null)
        {
            Debug.LogError("LiftSystemManager: Missing references.");
            logPanel.Log("LiftSystemManager: Missing references");
            enabled = false;
            return;
        }

        floorGenerator.GenerateFloors();

        if (floorGenerator.floorMarkers.Count == 0)
        {
            Debug.LogError("LiftSystemManager: No floors generated.");
            enabled = false;
            return;
        }

        SetFloorInstant(
            Mathf.Clamp(groundFloorIndex, 1, floorGenerator.floorMarkers.Count)
        );

        doorManager.OnDoorsClosed += HandleDoorsClosed;
    }

    void Update()
    {
        if (state == LiftState.Moving)
            MoveLift();
    }

    // =============================
    // PUBLIC CONTROL API
    // =============================
    public void RequestFloor(int floor)
    {
        if (state == LiftState.EmergencyStop)
        {
            logPanel.Log("Request ignored: Emergency Stop active");
            return;
        }
            

        if (floor < 1 || floor > floorGenerator.floorMarkers.Count)
        {
            logPanel.Log($"Invalid floor requested: {floor}");
            return;
        }

        // Same floor → open doors
        if (floor == currentFloor && state == LiftState.Idle)
        {
            logPanel.Log($"Lift already at floor {floor}. Opening doors.");
            doorManager.OpenDoors();
            return;
        }
        
        logPanel.Log($"Moving from floor {currentFloor} to floor {floor}");
        targetFloor = floor;
        targetY = FloorToWorldY(floor);

        // 🚪 Doors not ready → wait
        if (!doorManager.IsClosed())
        {
            pendingMoveAfterDoorClose = true;
            doorManager.AttemptClose();
            return;
        }

        StartMoving();
    }

    void StartMoving()
    {
        pendingMoveAfterDoorClose = false;
        state = LiftState.Moving;
    }

    public void EmergencyStop()
    {
        state = LiftState.EmergencyStop;
        currentSpeed = 0f;
    }

    void HandleDoorsClosed()
    {
        if (pendingMoveAfterDoorClose && state == LiftState.Idle)
            StartMoving();
    }

    public void Resume()
    {
        if (state == LiftState.EmergencyStop)
            state = LiftState.Idle;
    }

    // =============================
    // MOTION LOGIC
    // =============================
    void MoveLift()
    {
        float direction = Mathf.Sign(targetY - liftCabin.position.y);

        currentSpeed = Mathf.MoveTowards(
            currentSpeed,
            maxSpeed,
            Time.deltaTime * (maxSpeed / accelerationTime)
        );

        liftCabin.position += Vector3.up * direction * currentSpeed * Time.deltaTime;

        if (Mathf.Abs(liftCabin.position.y - targetY) <= stopTolerance)
        {
            liftCabin.position = new Vector3(
                liftCabin.position.x,
                targetY,
                liftCabin.position.z
            );

            currentSpeed = 0f;
            currentFloor = targetFloor;
            state = LiftState.Idle;

            // 🔔 ARRIVAL EVENT
            OnArrivedAtFloor?.Invoke(currentFloor);

            // 🚪 AUTO OPEN DOORS
            doorManager.OpenDoors();
            logPanel.Log($"Arrived at floor {currentFloor}");
        }
    }

    // =============================
    // FLOOR MAPPING
    // =============================
    float FloorToWorldY(int floor)
    {
        int index = floor - 1;

        if (index < 0 || index >= floorGenerator.floorMarkers.Count)
        {
            Debug.LogError($"Invalid floor requested: {floor}");
            return liftCabin.position.y;
        }

        return floorGenerator.floorMarkers[index].position.y;
    }

    void SetFloorInstant(int floor)
    {
        floor = Mathf.Clamp(floor, 1, floorGenerator.floorMarkers.Count);

        currentFloor = floor;
        targetFloor = floor;

        liftCabin.position = new Vector3(
            liftCabin.position.x,
            FloorToWorldY(floor),
            liftCabin.position.z
        );

        state = LiftState.Idle;
        currentSpeed = 0f;
    }
}
