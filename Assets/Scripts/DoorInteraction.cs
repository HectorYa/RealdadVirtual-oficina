using System;
using System.Collections;
using UnityEngine;

/// Puerta con bisagra (este transform es la bisagra; la hoja se extiende hacia +X local).
/// Al acercarse se puede abrir con E o con el botón táctil "ABRIR PUERTA".
public class DoorInteraction : MonoBehaviour
{
    public Transform player;
    public Renderer led;                 // LED del lector de tarjetas
    public Material ledOpen;             // material del LED con acceso concedido
    public float width = 1.1f;           // ancho de la hoja (para calcular el centro)
    public float radius = 1.7f;          // distancia para poder abrir
    public float openAngle = -100f;      // giro de apertura en Y
    public float duration = 1.2f;

    public bool NearDoor { get; private set; }
    public bool IsOpen { get; private set; }
    public event Action OnOpened;

    bool busy;
    float closedYaw;

    void Awake() { closedYaw = transform.localEulerAngles.y; }

    Vector3 Center => transform.TransformPoint(new Vector3(width * 0.5f, 0f, 0f));

    void Update()
    {
        if (IsOpen || busy || !player) { NearDoor = false; return; }
        Vector3 d = player.position - Center; d.y = 0f;
        NearDoor = d.magnitude < radius;
        if (NearDoor && MobileInput.InteractDown) Open();
    }

    public void Open()
    {
        if (IsOpen || busy) return;
        StartCoroutine(Swing());
    }

    IEnumerator Swing()
    {
        busy = true; NearDoor = false;
        if (led && ledOpen) led.sharedMaterial = ledOpen;
        OnOpened?.Invoke();
        yield return new WaitForSeconds(0.35f);   // "bip" del lector antes de abrir
        for (float t = 0; t < duration; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / duration);
            transform.localRotation = Quaternion.Euler(0f, closedYaw + openAngle * k, 0f);
            yield return null;
        }
        transform.localRotation = Quaternion.Euler(0f, closedYaw + openAngle, 0f);
        IsOpen = true; busy = false;
    }
}
