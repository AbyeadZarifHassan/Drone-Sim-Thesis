using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class GripperRollerController : MonoBehaviour
{
    [Header("Roller Settings")]
    public Transform roller;          // The roller part
    public float rollerSpeed = 100f;  // degrees per second

    [Header("Gripper Settings")]
    public Transform leftFinger;      // Left gripper finger
    public Transform rightFinger;     // Right gripper finger
    public float gripperSpeed = 0.01f;  // meters per second
    public float gripperMaxDistance = 0.06f; // maximum opening (meters)
    public float gripperMinDistance = 0.0f;  // fully closed
    public LayerMask grippableLayer;       // layer for grippable objects

    private float rollerDir = 0f;     // +1 clockwise, -1 counterclockwise
    private int gripperDir = 0;       // +1 open, -1 close
    private float currentDistance = 0f;  // current distance between fingers

    private Vector3 leftStartLocalPos;
    private Vector3 rightStartLocalPos;

    private FixedJoint joint; // currently gripped object joint

    private Rigidbody gripperRb;

    void Start()
    {
        gripperRb = GetComponent<Rigidbody>();
        gripperRb.isKinematic = true; // moved via IK

        // Store initial positions as reference
        if (leftFinger != null) leftStartLocalPos = leftFinger.localPosition;
        if (rightFinger != null) rightStartLocalPos = rightFinger.localPosition;

        currentDistance = Vector3.Distance(leftFinger.position, rightFinger.position);
    }

    void Update()
    {
        // 1. Rotate roller along Y axis
        if (roller && rollerDir != 0f)
        {
            roller.Rotate(Vector3.up, rollerDir * rollerSpeed * Time.deltaTime, Space.Self);
        }

        // 2. Move gripper fingers along X axis using physics if possible
        float delta = gripperDir * gripperSpeed * Time.deltaTime;
        currentDistance = Mathf.Clamp(currentDistance + delta, gripperMinDistance, gripperMaxDistance);

        if (leftFinger != null && rightFinger != null)
        {
            Rigidbody leftRb = leftFinger.GetComponent<Rigidbody>();
            Rigidbody rightRb = rightFinger.GetComponent<Rigidbody>();

            Vector3 leftTarget = leftStartLocalPos + Vector3.left * (currentDistance / 2f);
            Vector3 rightTarget = rightStartLocalPos + Vector3.right * (currentDistance / 2f);

            if (leftRb != null && rightRb != null)
            {
                leftRb.MovePosition(leftFinger.parent.TransformPoint(leftTarget));
                rightRb.MovePosition(rightFinger.parent.TransformPoint(rightTarget));
            }
            else
            {
                leftFinger.localPosition = leftTarget;
                rightFinger.localPosition = rightTarget;
            }
        }
    }

    // -------- Roller Controls --------
    public void RollerClockwise() => rollerDir = 1f;
    public void RollerCounterClockwise() => rollerDir = -1f;
    public void RollerStop() => rollerDir = 0f;

    // -------- Gripper Controls --------
    public void GripperOpen() => gripperDir = 1;
    public void GripperClose() => gripperDir = -1;
    public void GripperStop() => gripperDir = 0;

    // -------- Physics Gripping via Trigger --------
    private void OnTriggerEnter(Collider other)
    {
        if (((1 << other.gameObject.layer) & grippableLayer) != 0 && joint == null)
        {
            Rigidbody rb = other.attachedRigidbody;
            if (rb != null)
            {
                joint = rb.gameObject.AddComponent<FixedJoint>();
                joint.connectedBody = gripperRb;
                joint.breakForce = 1000f;
                joint.breakTorque = 1000f;
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (joint != null)
        {
            Destroy(joint);
            joint = null;
        }
    }

    // Optional: visualize gripper detection
    void OnDrawGizmosSelected()
    {
        if (leftFinger == null || rightFinger == null) return;

        Gizmos.color = Color.green;
        Vector3 center = (leftFinger.position + rightFinger.position) / 2f;
        Vector3 size = new Vector3(currentDistance / 2f + 0.01f, 0.05f, 0.05f) * 2f;
        Gizmos.DrawWireCube(center, size);
    }
}
