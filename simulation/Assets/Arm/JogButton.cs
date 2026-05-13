using UnityEngine;
using UnityEngine.EventSystems;

public class JogButton : MonoBehaviour,
    IPointerDownHandler,
    IPointerUpHandler
{
    public RoboticArmJacobianJogIK arm;
    public enum Axis { X, Y, Z }
    public Axis axis;
    public float direction = 1f; // +1 or -1

    public void OnPointerDown(PointerEventData eventData)
    {
        switch (axis)
        {
            case Axis.X:
                arm.JogX(direction);
                break;
            case Axis.Y:
                arm.JogY(direction);
                break;
            case Axis.Z:
                arm.JogZ(direction);
                break;
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        switch (axis)
        {
            case Axis.X:
                arm.StopX();
                break;
            case Axis.Y:
                arm.StopY();
                break;
            case Axis.Z:
                arm.StopZ();
                break;
        }
    }
}
