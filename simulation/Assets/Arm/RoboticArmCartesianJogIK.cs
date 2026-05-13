using UnityEngine;

public class RoboticArmCartesianJogIK : MonoBehaviour
{
    [System.Serializable]
    public class Joint
    {
        public Transform transform;
        public Vector3 localAxis = Vector3.up;
        public float minAngle = -180f;
        public float maxAngle = 180f;

        [HideInInspector] public Quaternion baseRotation;
        [HideInInspector] public float angle;
    }

    [Header("Arm")]
    public Joint[] joints;              // base → end
    public Transform endEffector;

    [Header("Jog Control")]
    public float jogStep = 0.01f;        // meters per press
    public float solveTolerance = 0.002f;
    public int iterations = 10;

    private Vector3 cartesianGoal;

    void Start()
    {
        // Capture mechanical zero
        foreach (var j in joints)
        {
            j.baseRotation = j.transform.localRotation;
            j.angle = 0f;
        }

        // Start from current end-effector pose
        cartesianGoal = endEffector.position;
    }

    void LateUpdate()
    {
        SolveIK();
    }

    // ---------------- BUTTON CALLS ----------------

    public void MoveLeft()     => Jog(Vector3.left);
    public void MoveRight()    => Jog(Vector3.right);
    public void MoveUp()       => Jog(Vector3.up);
    public void MoveDown()     => Jog(Vector3.down);
    public void MoveForward()  => Jog(Vector3.forward);
    public void MoveBackward() => Jog(Vector3.back);

    void Jog(Vector3 dir)
    {
        cartesianGoal += dir * jogStep;
    }

    // ---------------- IK SOLVER ----------------

    void SolveIK()
    {
        for (int iter = 0; iter < iterations; iter++)
        {
            for (int i = joints.Length - 1; i >= 0; i--)
            {
                Transform joint = joints[i].transform;

                Vector3 toEnd =
                    endEffector.position - joint.position;

                Vector3 toGoal =
                    cartesianGoal - joint.position;

                Vector3 axisWorld =
                    joint.TransformDirection(joints[i].localAxis);

                Vector3 pe =
                    Vector3.ProjectOnPlane(toEnd, axisWorld);
                Vector3 pt =
                    Vector3.ProjectOnPlane(toGoal, axisWorld);

                if (pe.sqrMagnitude < 1e-6f ||
                    pt.sqrMagnitude < 1e-6f)
                    continue;

                float delta =
                    Vector3.SignedAngle(pe, pt, axisWorld);

                joints[i].angle =
                    Mathf.Clamp(
                        joints[i].angle + delta,
                        joints[i].minAngle,
                        joints[i].maxAngle
                    );

                ApplyJoint(i);

                if ((endEffector.position - cartesianGoal).magnitude
                    < solveTolerance)
                    return;
            }
        }
    }

    void ApplyJoint(int index)
    {
        Joint j = joints[index];
        Quaternion delta =
            Quaternion.AngleAxis(j.angle, j.localAxis.normalized);
        j.transform.localRotation = j.baseRotation * delta;
    }
}
