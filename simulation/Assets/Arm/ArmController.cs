using UnityEngine;

public class ArmMovement : MonoBehaviour
{
    [Header("Setup")]
    public Transform targetTransform;
    
    [Header("Movement Settings")]
    public float moveSpeed = 10f;
    public float smoothTime = 0.15f; // Adjust for more/less "floaty" feel

    private Vector3 _currentVelocity;
    private Vector3 _targetPos;

    void Start()
    {
        // Initialize target position to where the target is currently
        if (targetTransform != null)
            _targetPos = targetTransform.position;
    }

    void Update()
    {
        // 1. Capture Inputs
        // Use Arrow keys or WASD for X and Y
        //float moveX = Input.GetAxis("Horizontal"); 
        //float moveY = Input.GetAxis("Vertical");   
        
        // Use R and F (or PageUp/Down) for Z (Forward/Backward)
        float moveX = 0f;
        float moveY = 0f;
        float moveZ = 0f;
        if (Input.GetKey(KeyCode.W)) moveZ = 1f;
        if (Input.GetKey(KeyCode.S)) moveZ = -1f;
        if (Input.GetKey(KeyCode.A)) moveX = 1f;
        if (Input.GetKey(KeyCode.D)) moveX = -1f;
        if (Input.GetKey(KeyCode.Q)) moveY = 1f;
        if (Input.GetKey(KeyCode.E)) moveY = -1f;

        // 2. Calculate the destination
        Vector3 direction = new Vector3(moveX, moveY, moveZ);
        _targetPos += direction * moveSpeed * Time.deltaTime;

        // 3. Apply Smooth Motion
        targetTransform.position = Vector3.SmoothDamp(
            targetTransform.position, 
            _targetPos, 
            ref _currentVelocity, 
            smoothTime
        );
    }
}