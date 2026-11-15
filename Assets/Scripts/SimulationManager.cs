using System.Collections;
using UnityEngine;

/// Orquesta el escenario: hall de ventanillas -> pasillo de oficinas -> puerta ZONA TI -> sentarse ->
/// correo (10-15 s) -> abrir adjunto -> alerta roja + mapa de propagación -> resumen.
public class SimulationManager : MonoBehaviour
{
    public PlayerController player;
    public SeatInteraction seat;
    public MonitorUI monitor;
    public LightingController lighting;
    public OverlayUI overlay;
    public SynthAudio audioFx;
    public DoorInteraction door;
    public InfectionMap infectionMap;
    public GameObject seatMarker;
    [Range(5f, 30f)] public float mailDelay = 12f;
    public float corridorStartZ = 18.3f;

    bool mailScheduled;
    int stage;      // 0 hall, 1 pasillo, 2 frente a la puerta, 3 puerta abierta

    const string FlagsText =
        "Señales de alerta que debías detectar:\n" +
        "1. Remitente falso: el dominio \"banco-ejemp1o-soporte.com\" imita al real (un \"1\" en lugar de \"l\").\n" +
        "2. Urgencia y amenaza: \"su cuenta será bloqueada en 24 horas\".\n" +
        "3. Adjunto peligroso: \"Verificacion_Cuenta.pdf.exe\" tiene doble extensión; un .exe no es un PDF.\n" +
        "4. Pide verificar credenciales por correo y usa un saludo genérico.\n\n" +
        "Qué hacer: no abrir el adjunto, reportar el correo a Seguridad y verificar por un canal oficial.";

    const string SpreadText =
        "Cómo se propagó: el adjunto instaló un malware en tu PC. Con las credenciales robadas saltó al servidor de correo " +
        "y al servidor central; desde ahí llegó a Contabilidad, Créditos, Gerencia, RR.HH., las cajas y los cajeros. " +
        "Un solo clic puede comprometer a todo el banco.\n\n";

    void Start()
    {
        overlay.SetHint("Bienvenido al Banco Andino. Recorre el hall de ventanillas y avanza hacia el pasillo del fondo.");
        seat.OnSat += OnSat;
        monitor.OnMalwareExecuted += OnMalware;
        monitor.OnReported += OnReported;
        if (door) door.OnOpened += OnDoorOpened;
    }

    void Update()
    {
        if (seat.Seated || monitor.Infected || stage >= 3) return;
        if (stage == 0 && player.transform.position.z > corridorStartZ)
        {
            stage = 1;
            overlay.SetHint("Pasillo de oficinas: Contabilidad, Créditos, Gerencia y RR.HH. Al fondo está la puerta de la ZONA TI.");
        }
        if (stage == 1 && door && door.NearDoor)
        {
            stage = 2;
            overlay.SetHint("Puerta de la ZONA TI. Pulsa ABRIR PUERTA (E) para pasar tu tarjeta y entrar.");
        }
    }

    void OnDoorOpened()
    {
        stage = 3;
        audioFx.Door();
        overlay.SetHint("Acceso concedido. Entra a la Zona TI y siéntate en la estación TI-01 (la que brilla).");
    }

    void OnSat()
    {
        if (seatMarker) seatMarker.SetActive(false);
        monitor.Activate();
        overlay.SetHint("Estás en tu puesto. Revisa tus documentos y tu correo con calma...");
        if (!mailScheduled) { mailScheduled = true; StartCoroutine(MailTimer()); }
    }

    IEnumerator MailTimer()
    {
        yield return new WaitForSeconds(mailDelay);
        if (monitor.Infected) yield break;
        monitor.DeliverPhishingMail();
        audioFx.Notify();
        overlay.SetHint("Tienes un correo nuevo. Toca la notificación para abrirlo.");
    }

    void OnMalware()
    {
        seat.CanStand = false;
        lighting.SetAlert(true);
        audioFx.Glitch();
        StartCoroutine(Aftermath());
    }

    IEnumerator Aftermath()
    {
        overlay.SetHint("");
        yield return new WaitForSeconds(1.6f);
        audioFx.StartAlarm();
        player.Shake(1.2f, 0.02f);
        yield return new WaitForSeconds(6f);
        seat.CanStand = true;
        seat.Stand();
        yield return new WaitForSeconds(1.5f);
        overlay.SetHint("¡ALERTA! Mira el MAPA DE RED frente a ti: así se propaga el virus por todo el banco.");
        if (infectionMap)
        {
            infectionMap.Play();
            while (!infectionMap.Done) yield return null;
            yield return new WaitForSeconds(3.5f);
        }
        else yield return new WaitForSeconds(12f);
        overlay.ShowDebrief("SIMULACIÓN FINALIZADA: equipo infectado", new Color(1f, 0.3f, 0.3f),
            "Abriste un adjunto de un correo fraudulento (phishing) y el atacante tomó el control de la red.\n\n" + SpreadText + FlagsText);
    }

    void OnReported()
    {
        StartCoroutine(Reported());
    }

    IEnumerator Reported()
    {
        audioFx.Notify();
        yield return new WaitForSeconds(2.5f);
        overlay.ShowDebrief("¡BIEN HECHO!", new Color(0.3f, 1f, 0.55f),
            "Reportaste el correo sospechoso sin abrir el adjunto. Así se evita el ataque: el mapa de red de la Zona TI sigue en verde.\n\n" + FlagsText);
    }
}
