using System.Collections.Generic;
using UnityEngine;

public class BuildingFloorGenerator : MonoBehaviour
{
    [Header("Floor Settings")]
    public int numberOfFloors = 10;
    public float floorHeight = 3.0f;

    [Header("References")]
    public Transform groundReference;
    public GameObject floorPrefab;

    [Header("Generated Floors (Read Only)")]
    public List<Transform> floorMarkers = new List<Transform>();

    void Start()
    {
        GenerateFloors();
    }

    public void GenerateFloors()
    {
        ClearExistingFloors();
        floorMarkers.Clear();

        for (int i = 0; i < numberOfFloors; i++)
        {
            Vector3 pos = groundReference.position + Vector3.up * i * floorHeight;

            GameObject floor = Instantiate(
                floorPrefab,
                pos,
                Quaternion.identity,
                transform
            );

            floor.name = $"Floor_{i + 1}";
            floorMarkers.Add(floor.transform);
        }
    }

    void ClearExistingFloors()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            Destroy(transform.GetChild(i).gameObject);
    }
}
