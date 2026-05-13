using UnityEngine;

public class CamFollow : MonoBehaviour
{
    [Header("Target Settings")]
    public Transform target;
    public Vector3 offset = new Vector3(0, 2, -5);
    
    [Header("Smoothness")]
    public float smoothTime = 0.2f; // Time to reach the target
    public float rotationSpeed = 5f;

    private Vector3 _currentVelocity = Vector3.zero;

    void LateUpdate()
    {
        if (!target) return;

        // 1. Calculate the desired position based on target's position and rotation
        Vector3 desiredPosition = target.TransformPoint(offset);

        // 2. Smoothly move the camera to that position
        transform.position = Vector3.SmoothDamp(
            transform.position, 
            desiredPosition, 
            ref _currentVelocity, 
            smoothTime
        );

        // 3. Smoothly rotate the camera to look at the target (or match target rotation)
        Quaternion lookRotation = Quaternion.LookRotation(target.position - transform.position + Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, rotationSpeed * Time.deltaTime);
    }
}