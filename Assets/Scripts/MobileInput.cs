using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// Entrada unificada: joystick/arrastre táctil en el móvil y teclado/ratón en el editor.
public static class MobileInput
{
    public static Vector2 Stick;
    public static Vector2 LookAccum;
    public static bool Blocked;     // true mientras el menú principal o la pausa están abiertos

    public static Vector2 Move
    {
        get
        {
            if (Blocked) return Vector2.zero;
            Vector2 v = Stick; var k = Keyboard.current;
            if (k != null)
            {
                v.x += (k.dKey.isPressed ? 1 : 0) - (k.aKey.isPressed ? 1 : 0);
                v.y += (k.wKey.isPressed ? 1 : 0) - (k.sKey.isPressed ? 1 : 0);
            }
            return Vector2.ClampMagnitude(v, 1f);
        }
    }

    public static Vector2 ConsumeLook()
    {
        Vector2 v = LookAccum; LookAccum = Vector2.zero;
        if (Blocked) return Vector2.zero;
        var m = Mouse.current;
        if (m != null && m.rightButton.isPressed) v += m.delta.ReadValue();
        return v;
    }

    public static bool InteractDown => !Blocked && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
}

public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    public RectTransform knob;
    public float radius = 110f;
    RectTransform rt;

    void Awake() { rt = (RectTransform)transform; }
    public void OnPointerDown(PointerEventData e) { OnDrag(e); }
    public void OnDrag(PointerEventData e)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, e.position, e.pressEventCamera, out var p);
        p = Vector2.ClampMagnitude(p, radius);
        knob.anchoredPosition = p; MobileInput.Stick = p / radius;
    }
    public void OnPointerUp(PointerEventData e) { knob.anchoredPosition = Vector2.zero; MobileInput.Stick = Vector2.zero; }
}

public class LookPad : MonoBehaviour, IDragHandler
{
    public void OnDrag(PointerEventData e) { MobileInput.LookAccum += e.delta; }
}
