using UnityEngine;

public class MultiObjectRotationController : MonoBehaviour
{
    [System.Serializable]
    public class RotatableObject
    {
        public GameObject target;
        public Vector3 rotationAxis = Vector3.up; // Local axis
        public float rotationSpeed = 30f;          // Degrees per second
    }

    public RotatableObject[] objects;

    void Update()
    {
        // Example: Rotate all objects continuously
        for (int i = 0; i < objects.Length; i++)
        {
            RotateObject(i, Time.deltaTime);
        }
    }

    /// <summary>
    /// Rotates a specific object by index
    /// </summary>
    public void RotateObject(int index, float deltaTime)
    {
        if (index < 0 || index >= objects.Length) return;
        if (objects[index].target == null) return;

        objects[index].target.transform.Rotate(
            objects[index].rotationAxis.normalized * objects[index].rotationSpeed * deltaTime,
            Space.Self
        );
    }

    /// <summary>
    /// Sets absolute rotation (useful for robotic joints)
    /// </summary>
    public void SetRotation(int index, Vector3 eulerAngles)
    {
        if (index < 0 || index >= objects.Length) return;
        if (objects[index].target == null) return;

        objects[index].target.transform.localEulerAngles = eulerAngles;
    }

    /// <summary>
    /// Rotates an object by a given angle instantly
    /// </summary>
    public void RotateByAngle(int index, float angle)
    {
        if (index < 0 || index >= objects.Length) return;
        if (objects[index].target == null) return;

        objects[index].target.transform.Rotate(
            objects[index].rotationAxis.normalized * angle,
            Space.Self
        );
    }
}
