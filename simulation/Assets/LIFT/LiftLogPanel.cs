using UnityEngine;
using TMPro;
using System.Text;
using System.Collections.Generic;

public class LiftLogPanel : MonoBehaviour
{
    [Header("UI")]
    public TextMeshProUGUI logText;

    [Header("Settings")]
    public int maxLines = 50;
    public bool autoScroll = true;

    StringBuilder logBuilder = new StringBuilder();
    Queue<string> logLines = new Queue<string>();

    public void Log(string message)
    {
        string timeStamped =
            $"[{System.DateTime.Now:HH:mm:ss}] {message}";

        logLines.Enqueue(timeStamped);

        while (logLines.Count > maxLines)
            logLines.Dequeue();

        logBuilder.Clear();
        foreach (var line in logLines)
            logBuilder.AppendLine(line);

        logText.text = logBuilder.ToString();
    }

    public void Clear()
    {
        logLines.Clear();
        logText.text = "";
    }
}
