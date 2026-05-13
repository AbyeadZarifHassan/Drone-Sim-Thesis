using System.Collections;
using UnityEngine;

public class DoorManager : MonoBehaviour
{
    public enum DoorState
    {
        Open,
        Closed,
        Opening,
        Closing,
        Obstructed
    }

    [Header("Door Panels")]
    public Transform leftDoor;
    public Transform rightDoor;

    [Header("Motion Settings")]
    public float openDistance = 0.9f;
    public float openTime = 0.8f;
    public float closeTime = 0.8f;

    [Header("Obstruction")]
    public Collider doorTrigger;   // Trigger collider covering doorway

    public DoorState State { get; private set; } = DoorState.Closed;

    public System.Action OnDoorsOpened;
    public System.Action OnDoorsClosed;

    public LiftLogPanel logPanel;

    Vector3 leftClosedPos;
    Vector3 rightClosedPos;

    Coroutine activeRoutine;
    int obstructionCount = 0;

    void Awake()
    {
        leftClosedPos = leftDoor.localPosition;
        rightClosedPos = rightDoor.localPosition;

        if (doorTrigger != null)
            doorTrigger.isTrigger = true;
    }

    // =============================
    // PUBLIC API
    // =============================
    public bool IsClosed()
    {
        return State == DoorState.Closed;
    }

    public void OpenDoors()
    {
        if (State == DoorState.Open || State == DoorState.Opening)
            return;

        StartDoorRoutine(OpenRoutine());
    }

    public void CloseDoors()
    {
        if (obstructionCount > 0)
        {
            State = DoorState.Obstructed;
            return;
        }

        if (State == DoorState.Closed || State == DoorState.Closing)
            return;

        StartDoorRoutine(CloseRoutine());
    }

    public void AttemptClose()
    {
        if (obstructionCount == 0)
            CloseDoors();
    }

    // =============================
    // OBSTRUCTION HANDLING
    // =============================
    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
        {
            logPanel?.Log("Door obstruction detected");
            return;
        }

        obstructionCount++;
        State = DoorState.Obstructed;
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player"))
        {
            logPanel?.Log("Door obstruction cleared");
            return;
        }

        obstructionCount = Mathf.Max(0, obstructionCount - 1);

        if (obstructionCount == 0)
            AttemptClose();
    }

    // =============================
    // INTERNAL ROUTINES
    // =============================
    void StartDoorRoutine(IEnumerator routine)
    {
        if (activeRoutine != null)
            StopCoroutine(activeRoutine);

        activeRoutine = StartCoroutine(routine);
    }

    IEnumerator OpenRoutine()
    {
        State = DoorState.Opening;

        Vector3 leftTarget = leftClosedPos + Vector3.left * openDistance;
        Vector3 rightTarget = rightClosedPos + Vector3.right * openDistance;

        yield return MoveDoors(leftTarget, rightTarget, openTime);

        State = DoorState.Open;
        OnDoorsOpened?.Invoke();
    }

    IEnumerator CloseRoutine()
    {
        State = DoorState.Closing;

        yield return MoveDoors(leftClosedPos, rightClosedPos, closeTime);

        if (obstructionCount > 0)
        {
            State = DoorState.Obstructed;
            
            yield break;
        }

        State = DoorState.Closed;
        OnDoorsClosed?.Invoke();
    }

    IEnumerator MoveDoors(Vector3 leftTarget, Vector3 rightTarget, float duration)
    {
        float t = 0f;
        Vector3 leftStart = leftDoor.localPosition;
        Vector3 rightStart = rightDoor.localPosition;

        while (t < 1f)
        {
            t += Time.deltaTime / duration;

            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            leftDoor.localPosition = Vector3.Lerp(leftStart, leftTarget, smoothT);
            rightDoor.localPosition = Vector3.Lerp(rightStart, rightTarget, smoothT);

            yield return null;
        }

        leftDoor.localPosition = leftTarget;
        rightDoor.localPosition = rightTarget;
    }
}
