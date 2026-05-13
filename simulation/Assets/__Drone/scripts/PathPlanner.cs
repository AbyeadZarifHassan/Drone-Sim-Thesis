using UnityEngine;
using RosMessageTypes.Std;
using Unity.Robotics.ROSTCPConnector;

public class PathPlanner : MonoBehaviour
{
    [Header("Waypoints")]
    public Transform startPoint;
    public Transform target;

    [Header("Movement Settings")]
    public float speed           = 5f;
    public float turnSpeed       = 3f;
    public float obstacleDistance = 5f;

    [Header("Landing Settings")]
    public float descentSpeed       = 2f;   // max vertical drop speed
    public float descentMinSpeed    = 0.2f; // minimum speed so it never fully stalls
    public float descentEaseDistance = 3f;  // within this Y distance, begin slowing down
    public float horizontalOffset   = 1.5f; // XZ radius to consider "above goal"
    public float landedOffset       = 0.2f; // Y distance to consider fully landed

    [Header("Activation")]
    [Tooltip("Tick this to start path planning from the beginning without waiting for ROS message")]
    public bool activateFromStart = false;

    [Header("Obstacle Detection")]
    public LayerMask obstacleLayer;

    [Header("Game View Visualization")]
    public bool  showVisualization = true;
    public Color goalSphereColor   = new Color(0f, 1f, 0f, 0.4f);
    public Color pathLineColor     = Color.cyan;
    public Color descentLineColor  = Color.yellow;
    public Color startPointColor   = Color.blue;
    public int   sphereSegments    = 32;

    // ── Flight phases ────────────────────────────────────────────
    private enum Phase { Idle, FlyToAboveGoal, Descend, Landed }
    private Phase _phase = Phase.Idle;

    // GL material
    private Material _lineMat;

    private ROSConnection ros;
    private const string activateTopic = "/path_plan_activate";

    // ─────────────────────────────────────────────────────────────
    // Unity lifecycle
    // ─────────────────────────────────────────────────────────────
    void Start()
    {
        ros = ROSConnection.GetOrCreateInstance();
        ros.Subscribe<StringMsg>(activateTopic, OnActivationMessageReceived);
        Debug.Log($"[PathPlanner] Subscribed to ROS topic: {activateTopic}");

        if (startPoint != null)
        {
            transform.position = startPoint.position;
            transform.rotation = Quaternion.identity;
        }

        _lineMat = CreateLineMaterial();

        if (activateFromStart)
            ActivatePathPlanning();
    }

    void Update()
    {
        if (target == null) return;

        switch (_phase)
        {
            case Phase.FlyToAboveGoal:
                UpdateFlyToAboveGoal();
                break;

            case Phase.Descend:
                UpdateDescend();
                break;
        }
    }

    // ─────────────────────────────────────────────────────────────
    // Phase: fly horizontally to directly above the goal
    // ─────────────────────────────────────────────────────────────
    void UpdateFlyToAboveGoal()
    {
        // Destination is target XZ but drone's own Y (keep altitude constant)
        Vector3 aboveGoal = new Vector3(target.position.x, transform.position.y, target.position.z);
        Vector3 toAboveGoal = aboveGoal - transform.position;
        float   horizDist   = new Vector2(toAboveGoal.x, toAboveGoal.z).magnitude;

        // ── Obstacle avoidance (horizontal only) ─────────────────
        RaycastHit hit;
        if (Physics.Raycast(transform.position, transform.forward, out hit, obstacleDistance, obstacleLayer))
        {
            Vector3 avoidDir = Vector3.zero;

            if (!Physics.Raycast(transform.position, transform.right,  obstacleDistance, obstacleLayer))
                avoidDir = transform.right;
            else if (!Physics.Raycast(transform.position, -transform.right, obstacleDistance, obstacleLayer))
                avoidDir = -transform.right;
            else
                avoidDir = transform.right; // fallback

            MoveHorizontal(avoidDir);
            return;
        }

        // ── Arrived above goal? ───────────────────────────────────
        if (horizDist <= horizontalOffset)
        {
            // Snap exactly above goal so descent is perfectly vertical
            transform.position = new Vector3(target.position.x, transform.position.y, target.position.z);

            // Level rotation so descent is straight down
            transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);

            Debug.Log("[PathPlanner] Above goal — starting vertical descent.");
            _phase = Phase.Descend;
            return;
        }

        // ── Fly toward the point above the goal ──────────────────
        MoveHorizontal(toAboveGoal.normalized);
    }

    // ─────────────────────────────────────────────────────────────
    // Phase: descend straight down to target Y (smooth eased)
    // ─────────────────────────────────────────────────────────────
    void UpdateDescend()
    {
        float currentY = transform.position.y;
        float targetY  = target.position.y;

        if (currentY <= targetY + landedOffset)
        {
            // Snap to exact landing position
            transform.position = new Vector3(target.position.x, targetY, target.position.z);
            transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
            _phase = Phase.Landed;
            OnGoalReached();
            return;
        }

        // Ease-in-out: slow down as we approach the ground
        float remaining  = currentY - targetY;
        float easeFactor = Mathf.Clamp01(remaining / descentEaseDistance);   // 1 = full speed, 0 = stopped
        float smoothEase = Mathf.SmoothStep(0f, 1f, easeFactor);             // S-curve
        float thisSpeed  = Mathf.Max(descentSpeed * smoothEase, descentMinSpeed); // never fully stall

        transform.position += Vector3.down * thisSpeed * Time.deltaTime;
    }

    // ─────────────────────────────────────────────────────────────
    // Horizontal movement helper (no pitch/roll, yaw only)
    // ─────────────────────────────────────────────────────────────
    void MoveHorizontal(Vector3 dir)
    {
        if (dir == Vector3.zero) return;

        // Flatten direction — never tilt up or down
        dir.y = 0f;
        if (dir == Vector3.zero) return;
        dir.Normalize();

        // Rotate only around Y axis
        Quaternion targetYaw = Quaternion.LookRotation(dir, Vector3.up);
        transform.rotation   = Quaternion.Slerp(transform.rotation, targetYaw, turnSpeed * Time.deltaTime);

        // Move in the horizontal direction directly (not transform.forward, avoids pitch drift)
        transform.position  += dir * speed * Time.deltaTime;
    }

    // ─────────────────────────────────────────────────────────────
    // Activation / goal
    // ─────────────────────────────────────────────────────────────
    void ActivatePathPlanning()
    {
        if (_phase == Phase.Landed)
        {
            Debug.LogWarning("[PathPlanner] Goal already reached. Reset before re-activating.");
            return;
        }

        _phase = Phase.FlyToAboveGoal;
        Debug.Log("[PathPlanner] Path planning ACTIVATED — flying to above goal.");
    }

    void OnGoalReached()
    {
        Debug.Log("[PathPlanner] *** GOAL REACHED *** Drone has landed.");

        var msg = new StringMsg("GOAL_REACHED");
        ros.Publish("/path_plan_status", msg);
        Debug.Log("[PathPlanner] Published 'GOAL_REACHED' to /path_plan_status");
    }

    public void ResetMission()
    {
        _phase = Phase.Idle;

        if (startPoint != null)
        {
            transform.position = startPoint.position;
            transform.rotation = Quaternion.identity;
        }

        Debug.Log("[PathPlanner] Mission reset.");

        if (activateFromStart)
            ActivatePathPlanning();
    }

    void OnActivationMessageReceived(StringMsg message)
    {
        if (message.data.Trim().ToUpper() == "DO")
        {
            Debug.Log("[PathPlanner] Received 'DO' — activating path planning.");
            ActivatePathPlanning();
        }
        else
        {
            Debug.LogWarning($"[PathPlanner] Unknown message on {activateTopic}: '{message.data}'");
        }
    }

    // ─────────────────────────────────────────────────────────────
    // Game view GL visualization
    // ─────────────────────────────────────────────────────────────
    void OnRenderObject()
    {
        if (!showVisualization || _lineMat == null || target == null) return;

        _lineMat.SetPass(0);

        Vector3 aboveGoal = new Vector3(target.position.x, transform.position.y, target.position.z);

        // Horizontal path line: drone → directly above goal
        GL.Begin(GL.LINES);
        GL.Color(pathLineColor);
        GL.Vertex(transform.position);
        GL.Vertex(aboveGoal);
        GL.End();

        // Vertical descent line: above goal → target
        GL.Begin(GL.LINES);
        GL.Color(descentLineColor);
        GL.Vertex(aboveGoal);
        GL.Vertex(target.position);
        GL.End();

        // Goal radius circle + cross
        DrawCircle(target.position, horizontalOffset, goalSphereColor, sphereSegments);
        DrawCross(target.position,  horizontalOffset, goalSphereColor);

        // "Above goal" hover marker
        DrawCircle(aboveGoal, horizontalOffset, new Color(1f, 1f, 0f, 0.4f), 16);

        if (startPoint != null)
        {
            DrawCircle(startPoint.position, 0.4f, startPointColor, 16);
            DrawCross(startPoint.position,  0.4f, startPointColor);
        }
    }

    // ─────────────────────────────────────────────────────────────
    // GL helpers
    // ─────────────────────────────────────────────────────────────
    void DrawCircle(Vector3 center, float radius, Color color, int segments)
    {
        GL.Begin(GL.LINE_STRIP);
        GL.Color(color);
        for (int i = 0; i <= segments; i++)
        {
            float a = i * Mathf.PI * 2f / segments;
            GL.Vertex(center + new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius));
        }
        GL.End();

        GL.Begin(GL.LINE_STRIP);
        GL.Color(color);
        for (int i = 0; i <= segments; i++)
        {
            float a = i * Mathf.PI * 2f / segments;
            GL.Vertex(center + new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f));
        }
        GL.End();
    }

    void DrawCross(Vector3 center, float size, Color color)
    {
        GL.Begin(GL.LINES);
        GL.Color(color);
        GL.Vertex(center + Vector3.left    * size); GL.Vertex(center + Vector3.right   * size);
        GL.Vertex(center + Vector3.down    * size); GL.Vertex(center + Vector3.up      * size);
        GL.Vertex(center + Vector3.back    * size); GL.Vertex(center + Vector3.forward * size);
        GL.End();
    }

    static Material CreateLineMaterial()
    {
        Shader shader = Shader.Find("Hidden/Internal-Colored") ?? Shader.Find("Unlit/Color");
        var mat = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_Cull",     (int)UnityEngine.Rendering.CullMode.Off);
        mat.SetInt("_ZWrite",   0);
        mat.SetInt("_ZTest",    (int)UnityEngine.Rendering.CompareFunction.Always);
        return mat;
    }

    // ─────────────────────────────────────────────────────────────
    // Scene Gizmos
    // ─────────────────────────────────────────────────────────────
    void OnDrawGizmos()
    {
        if (target == null) return;

        Vector3 aboveGoal = new Vector3(target.position.x, transform.position.y, target.position.z);

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(transform.position, aboveGoal);

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(aboveGoal, target.position);

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(target.position, horizontalOffset);

        if (startPoint != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(startPoint.position, 0.3f);
        }
    }
}