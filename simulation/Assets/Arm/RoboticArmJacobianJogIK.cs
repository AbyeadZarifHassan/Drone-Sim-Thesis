using UnityEngine;

public class RoboticArmJacobianJogIK : MonoBehaviour
{
    [System.Serializable]
    public class Joint
    {
        public Transform transform;
        public Vector3 localAxis = Vector3.up;
        public float minAngle = -180f;
        public float maxAngle = 180f;

        [HideInInspector] public float angle;
        [HideInInspector] public Quaternion baseRotation;
    }

    [Header("Arm")]
    public Joint[] joints;           // base → tip
    public Transform endEffector;

    [Header("Jog")]
    public float cartesianSpeed = 0.05f; // m/s
    public float damping = 0.1f;
    public float dt = 0.02f;

    private Vector3 cartesianVelocity;

    void Start()
    {
        foreach (var j in joints)
        {
            j.baseRotation = j.transform.localRotation;
            j.angle = 0f;
        }
    }

    void Update()
    {
        SolveJacobianIK();
    }

    // -------- Button controls --------

    public void JogX(float dir) => cartesianVelocity.x = dir * cartesianSpeed;
    public void JogY(float dir) => cartesianVelocity.y = dir * cartesianSpeed;
    public void JogZ(float dir) => cartesianVelocity.z = dir * cartesianSpeed;

    public void StopX() => cartesianVelocity.x = 0;
    public void StopY() => cartesianVelocity.y = 0;
    public void StopZ() => cartesianVelocity.z = 0;

    // -------- Jacobian IK --------

    void SolveJacobianIK()
    {
        if (cartesianVelocity == Vector3.zero)
            return;

        int n = joints.Length;
        float[,] J = new float[3, n];

        Vector3 pe = endEffector.position;

        for (int i = 0; i < n; i++)
        {
            Vector3 pi = joints[i].transform.position;
            Vector3 zi = joints[i].transform.TransformDirection(joints[i].localAxis);

            Vector3 Ji = Vector3.Cross(zi, pe - pi);

            J[0, i] = Ji.x;
            J[1, i] = Ji.y;
            J[2, i] = Ji.z;
        }

        // Damped Least Squares: dq = J^T (J J^T + λ² I)^-1 dx
        float[,] JJt = new float[3, 3];

        for (int r = 0; r < 3; r++)
            for (int c = 0; c < 3; c++)
                for (int k = 0; k < n; k++)
                    JJt[r, c] += J[r, k] * J[c, k];

        for (int i = 0; i < 3; i++)
            JJt[i, i] += damping * damping;

        float[,] inv = Invert3x3(JJt);

        float[] dx = {
            cartesianVelocity.x * dt,
            cartesianVelocity.y * dt,
            cartesianVelocity.z * dt
        };

        float[] temp = new float[3];
        for (int i = 0; i < 3; i++)
            for (int k = 0; k < 3; k++)
                temp[i] += inv[i, k] * dx[k];

        float[] dq = new float[n];
        for (int i = 0; i < n; i++)
            for (int k = 0; k < 3; k++)
                dq[i] += J[k, i] * temp[k];

        // Apply joint updates
        for (int i = 0; i < n; i++)
        {
            joints[i].angle =
                Mathf.Clamp(
                    joints[i].angle + dq[i] * Mathf.Rad2Deg,
                    joints[i].minAngle,
                    joints[i].maxAngle
                );

            ApplyJoint(i);
        }
    }

    void ApplyJoint(int i)
    {
        Joint j = joints[i];
        Quaternion delta =
            Quaternion.AngleAxis(j.angle, j.localAxis.normalized);
        j.transform.localRotation = j.baseRotation * delta;
    }

    // -------- Small math helper --------

    float[,] Invert3x3(float[,] m)
    {
        float a = m[0,0], b = m[0,1], c = m[0,2];
        float d = m[1,0], e = m[1,1], f = m[1,2];
        float g = m[2,0], h = m[2,1], i = m[2,2];

        float det = a*(e*i - f*h) - b*(d*i - f*g) + c*(d*h - e*g);
        if (Mathf.Abs(det) < 1e-6f)
            return new float[3,3];

        float invDet = 1f / det;

        return new float[,] {
            { (e*i - f*h)*invDet, (c*h - b*i)*invDet, (b*f - c*e)*invDet },
            { (f*g - d*i)*invDet, (a*i - c*g)*invDet, (c*d - a*f)*invDet },
            { (d*h - e*g)*invDet, (b*g - a*h)*invDet, (a*e - b*d)*invDet }
        };
    }
}
