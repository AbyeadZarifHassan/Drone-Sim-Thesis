using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LiftUIController : MonoBehaviour
{
    [Header("References")]
    public LiftSystemManager liftManager;
    public LiftLogPanel logPanel;

    [Header("UI")]
    public TMP_InputField floorInput;
    public Button goButton;

    void Start()
    {
        goButton.onClick.AddListener(OnGoPressed);
        floorInput.onSubmit.AddListener(_ => OnGoPressed());
    }

    void OnGoPressed()
    {
        if (!int.TryParse(floorInput.text, out int floor))
        {
            logPanel.Log("Invalid floor input");
            return;
        }

        logPanel.Log($"Floor request received: {floor}");
        liftManager.RequestFloor(floor);

        floorInput.text = "";
    }
}
