using System.Collections.Generic;
using UnityEngine;

public class LiftScheduler : MonoBehaviour
{
    public LiftSystemManager lift;
    public DoorManager doors;

    Queue<int> queue = new Queue<int>();

    void Awake()
    {
        lift.OnArrivedAtFloor += HandleArrival;
    }

    void Update()
    {
        if (lift.state == LiftSystemManager.LiftState.Idle &&
            queue.Count > 0 &&
            doors.IsClosed())
        {
            lift.RequestFloor(queue.Dequeue());
        }
    }

    public void CallLift(int floor)
    {
        if (!queue.Contains(floor))
            queue.Enqueue(floor);
    }

    void HandleArrival(int floor)
    {
        doors.OpenDoors();
        Invoke(nameof(CloseDoors), 2.5f);
    }

    void CloseDoors()
    {
        doors.CloseDoors();
    }
}
