using TMPro;
using UnityEngine;

public class BoxSerial : MonoBehaviour
{
    public TextMeshProUGUI serialText;
    public string serialNumber;

    public void SetSerial(string serial)
    {
        serialNumber = serial;
        serialText.text = serial;
    }
}
