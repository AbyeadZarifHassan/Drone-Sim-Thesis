using UnityEngine;
using UnityEngine.EventSystems;

public class UIButtonContinuous : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    public enum ControlType { RollerCW, RollerCCW, GripperOpen, GripperClose }
    public ControlType control;

    public GripperRollerController controller; // Assign your controller here

    public void OnPointerDown(PointerEventData eventData)
    {
        switch (control)
        {
            case ControlType.RollerCW: controller.RollerClockwise(); break;
            case ControlType.RollerCCW: controller.RollerCounterClockwise(); break;
            case ControlType.GripperOpen: controller.GripperOpen(); break;
            case ControlType.GripperClose: controller.GripperClose(); break;
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        switch (control)
        {
            case ControlType.RollerCW:
            case ControlType.RollerCCW: controller.RollerStop(); break;
            case ControlType.GripperOpen:
            case ControlType.GripperClose: controller.GripperStop(); break;
        }
    }
}
