using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LiftROSController : MonoBehaviour
{
    public Vector3[] floorPositions;
    public float floorTravelTime = 3f; // seconds
    public float softStartTime = 0.5f;
    public float softStopTime = 0.5f;

    private bool emergency = false;
    private bool moving = false;
    private int currentFloor = 0;

    public void MoveToFloor(int targetFloor)
    {
        if (moving || emergency) return;
        if (targetFloor == currentFloor) return;

        StartCoroutine(MoveLift(targetFloor));
    }

    public void EmergencyStop()
    {
        emergency = true;
    }

    public void Resume()
    {
        emergency = false;
    }

    private IEnumerator MoveLift(int targetFloor)
    {
        moving = true;
        Vector3 startPos = transform.position;
        Vector3 endPos = floorPositions[targetFloor];

        float totalTime = floorTravelTime * Mathf.Abs(targetFloor - currentFloor);

        // Soft start (accelerate)
        float elapsed = 0f;
        while (elapsed < softStartTime)
        {
            if (emergency) yield break;
            float t = elapsed / softStartTime;
            transform.position = Vector3.Lerp(startPos, endPos, t * 0.1f); // small fraction
            elapsed += Time.deltaTime;
            yield return null;
        }

        // Constant speed
        elapsed = 0f;
        float mainTime = totalTime - softStartTime - softStopTime;
        while (elapsed < mainTime)
        {
            if (emergency) yield break;
            float t = elapsed / mainTime;
            transform.position = Vector3.Lerp(startPos, endPos, 0.1f + t * 0.8f);
            elapsed += Time.deltaTime;
            yield return null;
        }

        // Soft stop (decelerate)
        elapsed = 0f;
        while (elapsed < softStopTime)
        {
            if (emergency) yield break;
            float t = elapsed / softStopTime;
            transform.position = Vector3.Lerp(startPos, endPos, 0.9f + t * 0.1f);
            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.position = endPos;
        currentFloor = targetFloor;
        moving = false;
    }
}
