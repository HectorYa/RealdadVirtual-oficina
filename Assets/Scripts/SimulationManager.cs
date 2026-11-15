using System.Collections;
using UnityEngine;

/// Orquesta la jornada: hall -> pasillo -> puerta ZONA TI -> sentarse -> inicio de sesión (Modo Ataque / Modo Defensa)
/// -> correo de phishing -> infección + propagación (Ataque) o cuarentena + inspección guiada (Defensa) -> reporte de auditoría.
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
    [Range(3f, 30f)] public float mailDelay = 8f;
    public float corridorStartZ = 18.3f;

    bool mailScheduled, ended;
    int stage;      // 0 hall, 1 pasillo, 2 frente a la puerta, 3 puerta abierta

    static readonly Color RedC = new Color(1f, 0.32f, 0.3f);
    static readonly Color GreenC = new Color(0.3f, 1f, 0.55f);
    static readonly Color AmberC = new Color(1f, 0.72f, 0.25f);
    static readonly Color ChipRed = new Color(0.78f, 0.16f, 0.14f);
    static readonly Color ChipGreen = new Color(0.12f, 0.58f, 0.32f);
    static readonly Color ChipAmber = new Color(0.85f, 0.5f, 0.05f);

    const string Lesson =
        "Las 3 señales de este correo: <b>remitente falso</b> («banco-andlno.com» en lugar de «bancoandino.com»), " +
        "<b>urgencia artificial</b> («HOY antes de las 17:00 o se suspende el pago») y <b>enlace / archivo malicioso</b> " +
        "(Excel con macro oculta y portal falso). Ante la duda: no abras, verifica por otro canal y pulsa «Reportar phishing».";

    void Start()
    {
        overlay.SetHint("Bienvenido al Banco Andino. Recorre el hall de ventanillas y avanza hacia el pasillo del fondo.");
        seat.OnSat += OnSat;
        if (door) door.OnOpened += OnDoorOpened;
        monitor.OnModeSelected += OnModeSelected;
        monitor.OnMalwareExecuted += OnMalware;
        monitor.OnReported += OnReported;
        monitor.OnInspectionStarted += () => overlay.SetHint("Inspección guiada: toca los 3 rectángulos resaltados del mensaje. Tienes 3 minutos.");
        monitor.OnSignalFound += n => { if (n < 3) overlay.SetHint($"Señal encontrada ({n}/3). Lee la explicación y busca la siguiente."); };
        monitor.OnAllSignalsFound += () => overlay.SetHint("¡3 de 3 señales! Pulsa REPORTAR PHISHING para terminar.");
        monitor.OnTimeUp += OnTimeUp;

        // "Probar el otro modo" desde el reporte: aparece sentado y entra directo
        var quick = SimSession.QuickRestartMode;
        SimSession.QuickRestartMode = SimMode.None;
        if (quick != SimMode.None) StartCoroutine(QuickStart(quick));
    }

    IEnumerator QuickStart(SimMode mode)
    {
        yield return null;
        stage = 3;
        if (door) door.OpenInstant();
        seat.SitInstant();
        monitor.AutoLogin(mode);
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
        if (monitor.Mode == SimMode.None)
            overlay.SetHint("Inicia sesión: elige MODO ATAQUE (PC sin protección) o MODO DEFENSA (con controles y guía).");
    }

    void OnModeSelected(SimMode mode)
    {
        overlay.SetHint(mode == SimMode.Defense
            ? "Modo Defensa: tu PC tiene filtro de correo, Sandbox y MFA. Trabaja con normalidad y revisa tu correo."
            : "Es un día normal de trabajo. Explora tu escritorio y revisa tu correo.");
        if (!mailScheduled) { mailScheduled = true; StartCoroutine(MailTimer(mode)); }
    }

    IEnumerator MailTimer(SimMode mode)
    {
        yield return new WaitForSeconds(mailDelay);
        if (monitor.Finished) yield break;
        monitor.DeliverPhishingMail();
        audioFx.Notify();
        overlay.SetHint(mode == SimMode.Defense
            ? "El filtro retuvo un correo en CUARENTENA. Abre el Correo y revísalo."
            : "Tienes un correo nuevo.");
    }

    // ------------------------------------------------------------ MODO ATAQUE
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
        overlay.SetHint("¡ALERTA! Mira el MAPA DE RED frente a ti: así se propaga el virus por los equipos del banco.");
        if (infectionMap)
        {
            infectionMap.Play();
            while (!infectionMap.Done) yield return null;
            yield return new WaitForSeconds(3f);
        }
        else yield return new WaitForSeconds(12f);
        ShowReport();
    }

    // ----------------------------------------------------------- REPORTE / FIN
    void OnReported() { StartCoroutine(EndAfter(3f)); }
    void OnTimeUp() { audioFx.Glitch(); StartCoroutine(EndAfter(3.5f)); }

    IEnumerator EndAfter(float delay)
    {
        audioFx.Notify();
        if (SimSession.Mode == SimMode.Defense && infectionMap) infectionMap.SetContained();
        if (lighting && lighting.wallStatus && !SimSession.Infected)
        {
            lighting.wallStatus.text = "ESTADO DE RED: PROTEGIDA";
            lighting.wallStatus.color = new Color(0.2f, 1f, 0.45f);
        }
        overlay.SetHint("");
        yield return new WaitForSeconds(delay);
        ShowReport();
    }

    void ShowReport()
    {
        if (ended) return;
        ended = true;
        SimSession.Finish();
        bool defense = SimSession.Mode == SimMode.Defense;
        string time = SimSession.Format(SimSession.Elapsed);
        string signals = defense ? $"{SimSession.SignalsFound} / 3" : (SimSession.Reported ? "Detectado sin ayuda" : "0 / 3");
        string pw = MonitorUI.NotePassword, ip = MonitorUI.AttackerIP;
        var r = new ReportData { mode = defense ? "Defensa" : "Ataque", time = time, signals = signals, lesson = Lesson };

        if (SimSession.Infected)
        {
            string via = SimSession.Vector == "enlace" ? "abrir el enlace del portal falso" : "descargar y abrir el Excel adjunto";
            r.title = "SISTEMA COMPROMETIDO";
            r.color = RedC;
            r.subtitle = $"Al {via}, el malware tomó el control de tu PC y se propagó a los 10 equipos de la red del banco.";
            r.evidence = new[]
            {
                ("Correo sospechoso", "EJECUTADO", ChipRed, $"«{MonitorUI.PhishSubject}» de {MonitorUI.PhishAddr}: se abrió el {(SimSession.Vector == "enlace" ? "enlace" : "adjunto")} y se ejecutó la macro maliciosa."),
                ("Contraseña anotada en el escritorio", "ROBADA", ChipRed, $"El malware leyó la nota adhesiva «{pw}» (clave del correo y del sistema)."),
                ("Registro de accesos", "ACCESO NO AUTORIZADO", ChipRed, $"{System.DateTime.Now:HH:mm} · inicio de sesión como juan.perez desde {ip} con la clave robada. Sin MFA, nada lo impidió."),
            };
        }
        else if (defense && SimSession.TimedOut)
        {
            r.title = "TIEMPO AGOTADO";
            r.color = AmberC;
            r.subtitle = $"El filtro evitó la infección, pero solo identificaste {SimSession.SignalsFound} de 3 señales en 3 minutos. Vuelve a intentarlo.";
            r.evidence = new[]
            {
                ("Correo sospechoso", "AISLADO", ChipGreen, "Retenido en cuarentena por el filtro; enlaces y adjunto bloqueados. No se reportó a tiempo."),
                ("Contraseña anotada en el escritorio", "EN RIESGO", ChipAmber, $"La clave «{pw}» sigue visible en una nota. Guárdala en el Gestor de Contraseñas."),
                ("Registro de accesos", "PROTEGIDO", ChipGreen, $"MFA bloqueó un intento de inicio de sesión desde {ip}."),
            };
        }
        else if (defense)
        {
            r.title = "ATAQUE DETENIDO A TIEMPO";
            r.color = GreenC;
            r.subtitle = $"Identificaste las 3 señales y reportaste el phishing en {time} (límite 03:00). Se evitó la infección por completo.";
            r.evidence = new[]
            {
                ("Correo sospechoso", "AISLADO Y REPORTADO", ChipGreen, "Retenido en cuarentena, analizado en Sandbox (macro maliciosa) y reportado al equipo de Seguridad (ticket #SOC-4040)."),
                ("Contraseña anotada en el escritorio", "PROTEGIDA", ChipGreen, "El malware nunca se ejecutó. Recomendación: mover la clave al Gestor de Contraseñas y destruir la nota."),
                ("Registro de accesos", "PROTEGIDO", ChipGreen, $"MFA bloqueó 1 intento de inicio de sesión desde {ip}. Historial de accesos intacto."),
            };
        }
        else
        {
            r.title = "PHISHING DETECTADO";
            r.color = GreenC;
            r.subtitle = $"Sin filtros ni ayudas, reconociste el engaño y reportaste el correo en {time}. ¡Excelente criterio!";
            r.evidence = new[]
            {
                ("Correo sospechoso", "REPORTADO", ChipGreen, "No abriste el enlace ni el adjunto y lo enviaste al equipo de Seguridad."),
                ("Contraseña anotada en el escritorio", "EN RIESGO", ChipAmber, $"La clave «{pw}» sigue visible: cualquier malware futuro podría leerla."),
                ("Registro de accesos", "SIN INCIDENTES", ChipGreen, "No se registraron accesos no autorizados (aunque esta PC no tiene MFA)."),
            };
        }
        overlay.ShowReport(r);
    }
}
