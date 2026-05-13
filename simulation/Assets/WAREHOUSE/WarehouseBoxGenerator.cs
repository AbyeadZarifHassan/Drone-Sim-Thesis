using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WarehouseBoxGenerator : MonoBehaviour
{
    [Header("Box Settings")]
    public GameObject boxPrefab;

    [Header("Stack Settings")]
    public int totalBoxes = 50;
    public int maxColumnsPerLayer = 3;
    public int maxRowsPerLayer = 4;
    public Vector3 pileCenter = Vector3.zero;

    [Header("Orientation")]
    [Tooltip("Yaw rotation (Y axis) of the entire stack")]
    public float stackYawDegrees = 0f;   // <<< YOU CONTROL THIS

    [Header("Physics Safety")]
    [Range(0f, 0.05f)] public float spacingGap = 0.01f;

    [Header("Serial Settings")]
    public string serialPrefix = "WH-BOX-";

    void Start()
    {
        StartCoroutine(GenerateStackCoroutine());
    }

    IEnumerator GenerateStackCoroutine()
    {
        if (!boxPrefab) yield break;

        BoxCollider refCollider = boxPrefab.GetComponentInChildren<BoxCollider>();
        if (!refCollider) yield break;

        float boxWidth  = refCollider.size.x * boxPrefab.transform.localScale.x + spacingGap;
        float boxDepth  = refCollider.size.z * boxPrefab.transform.localScale.z + spacingGap;
        float boxHeight = refCollider.size.y * boxPrefab.transform.localScale.y;

        Quaternion stackRotation = Quaternion.Euler(0f, stackYawDegrees, 0f);

        int remaining = totalBoxes;
        int serial = 0;
        int layer = 0;

        while (remaining > 0)
        {
            int boxesThisLayer = Mathf.Min(remaining, maxColumnsPerLayer * maxRowsPerLayer);
            List<Rigidbody> layerRBs = new List<Rigidbody>();

            for (int i = 0; i < boxesThisLayer; i++)
            {
                int col = i % maxColumnsPerLayer;
                int row = i / maxColumnsPerLayer;

                Vector3 localOffset = new Vector3(
                    (col - (maxColumnsPerLayer - 1) * 0.5f) * boxWidth,
                    boxHeight * 0.5f + layer * boxHeight,
                    (row - (maxRowsPerLayer - 1) * 0.5f) * boxDepth
                );

                Vector3 worldPos = pileCenter + stackRotation * localOffset;

                GameObject box = Instantiate(boxPrefab, worldPos, stackRotation);

                Rigidbody rb = box.GetComponent<Rigidbody>();
                rb.isKinematic = true;
                //rb.velocity = Vector3.zero;
                //rb.angularVelocity = Vector3.zero;

                layerRBs.Add(rb);

                BoxSerial bs = box.GetComponent<BoxSerial>();
                if (bs)
                    bs.SetSerial(serialPrefix + serial.ToString("D4"));

                serial++;
            }

            // Activate physics safely
            foreach (var rb in layerRBs)
            {
                rb.isKinematic = false;
                rb.WakeUp();
            }

            yield return StartCoroutine(WaitForLayerToSettle(layerRBs));

            remaining -= boxesThisLayer;
            layer++;
        }
    }

    IEnumerator WaitForLayerToSettle(List<Rigidbody> rbs)
    {
        while (true)
        {
            bool settled = true;
            foreach (var rb in rbs)
            {
                if (rb && !rb.IsSleeping())
                {
                    settled = false;
                    break;
                }
            }
            if (settled) yield break;
            yield return null;
        }
    }

    // =========================
    // SCENE VISUALIZATION
    // =========================
    void OnDrawGizmos()
    {
        if (!boxPrefab) return;

        BoxCollider refCollider = boxPrefab.GetComponentInChildren<BoxCollider>();
        if (!refCollider) return;

        float boxW = refCollider.size.x * boxPrefab.transform.localScale.x + spacingGap;
        float boxD = refCollider.size.z * boxPrefab.transform.localScale.z + spacingGap;
        float boxH = refCollider.size.y * boxPrefab.transform.localScale.y;

        int boxesPerLayer = maxColumnsPerLayer * maxRowsPerLayer;
        int layers = Mathf.CeilToInt((float)totalBoxes / boxesPerLayer);

        float totalWidth  = maxColumnsPerLayer * boxW;
        float totalDepth  = maxRowsPerLayer * boxD;
        float totalHeight = layers * boxH;

        Quaternion rot = Quaternion.Euler(0f, stackYawDegrees, 0f);
        Matrix4x4 oldMatrix = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(
            pileCenter + Vector3.up * totalHeight * 0.5f,
            rot,
            Vector3.one
        );

        Gizmos.color = new Color(0f, 1f, 0f, 0.25f);
        Gizmos.DrawCube(Vector3.zero, new Vector3(totalWidth, totalHeight, totalDepth));

        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(totalWidth, totalHeight, totalDepth));

        Gizmos.matrix = oldMatrix;
    }
}
