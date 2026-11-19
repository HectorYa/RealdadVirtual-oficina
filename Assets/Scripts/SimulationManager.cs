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

    bool mailScheduled, ended, phishSeen;
    VoiceGuide voice;
    float inspectT0 = -1f; int timeWarn;
    int stage;      // 0 hall, 1 pasillo, 2 frente a la puerta, 3 puerta abierta

    static readonly Color RedC = new Color(1f, 0.32f, 0.3f);
    static readonly Color GreenC = new Color(0.3f, 1f, 0.55f);
    static readonly Color AmberC = new Color(1f, 0.72f, 0.25f);
    static readonly Color ChipRed = new Color(0.78f, 0.16f, 0.14f);
    static readonly Color ChipGreen = new Color(0.12f, 0.58f, 0.32f);
    static readonly Color ChipAmber = new Color(0.85f, 0.5f, 0.05f);

    // Cámaras de seguridad por nodo del mapa (posición, punto al que miran, nombre)
    static readonly (Vector3 pos, Vector3 look, string name)[] Cams =
    {
        (new Vector3(0f, 1.75f, 36.4f), new Vector3(0f, 2.0f, 39.9f), "CÁM. 01 · ZONA TI · MAPA DE RED"),
        (new Vector3(0f, 1.75f, 36.4f), new Vector3(0f, 2.0f, 39.9f), "CÁM. 01 · ZONA TI · MAPA DE RED"),
        (new Vector3(-2.6f, 2.7f, 33.2f), new Vector3(-5.2f, 1.2f, 37.5f), "CÁM. 02 · SALA DE SERVIDORES"),
        (new Vector3(-2.6f, 2.7f, 33.2f), new Vector3(-5.2f, 1.2f, 37.5f), "CÁM. 02 · SALA DE SERVIDORES"),
        (new Vector3(-5.7f, 2.7f, 24.6f), new Vector3(-4.2f, 0.9f, 21.4f), "CÁM. 04 · CONTABILIDAD"),
        (new Vector3(5.7f, 2.7f, 24.6f), new Vector3(4.3f, 0.9f, 21.5f), "CÁM. 05 · CRÉDITOS Y PRÉSTAMOS"),
        (new Vector3(-5.7f, 2.7f, 25.4f), new Vector3(-4.4f, 0.9f, 29.6f), "CÁM. 06 · GERENCIA"),
        (new Vector3(5.7f, 2.7f, 25.4f), new Vector3(4.2f, 0.9f, 28.5f), "CÁM. 07 · RECURSOS HUMANOS"),
        (new Vector3(5.4f, 2.7f, 12.3f), new Vector3(3.6f, 0.9f, 5.5f), "CÁM. 08 · VENTANILLAS"),
        (new Vector3(2.2f, 2.4f, 0.5f), new Vector3(5.42f, 1.45f, 0.9f), "CÁM. 09 · CAJERO AUTOMÁTICO"),
    };
    Camera cctvCam;

    void Start()
    {
        voice = GetComponent<VoiceGuide>();
        if (!voice) voice = gameObject.AddComponent<VoiceGuide>();
        monitor.OnPhishOpened += OnPhishOpened;
        monitor.OnThreatBlocked += () => voice.Say("defensa_bloqueado", true);
        overlay.SetHint("Bienvenido al Banco Andino. Recorre el hall de ventanillas y avanza hacia el pasillo del fondo.");
        seat.OnSat += OnSat;
        if (door) door.OnOpened += OnDoorOpened;
        monitor.OnModeSelected += OnModeSelected;
        monitor.OnMalwareExecuted += OnMalware;
        monitor.OnReported += OnReported;
        monitor.OnInspectionStarted += () =>
        {
            overlay.SetHint("Inspección guiada: toca los 3 rectángulos resaltados del mensaje. Tienes 3 minutos.");
            inspectT0 = Time.time; timeWarn = 0;
            voice.Say("defensa_inspeccion", true);
            voice.SetIdle(40f, 50f, false, "defensa_pista");
        };
        monitor.OnSignalFound += n =>
        {
            if (n < 3) overlay.SetHint($"Señal encontrada ({n}/3). Lee la explicación y busca la siguiente.");
            voice.Say("senal_" + Mathf.Clamp(n, 1, 3), true);
        };
        monitor.OnAllSignalsFound += () =>
        {
            overlay.SetHint("¡3 de 3 señales! Pulsa REPORTAR PHISHING para terminar.");
            inspectT0 = -1f;
            voice.SetIdle(20f, 25f, false, "defensa_idle_reporte");
        };
        monitor.OnTimeUp += OnTimeUp;

        // "Probar el otro modo" desde el reporte: aparece sentado y entra directo
        var quick = SimSession.QuickRestartMode;
        SimSession.QuickRestartMode = SimMode.None;
        if (quick != SimMode.None) StartCoroutine(QuickStart(quick));
        else StartCoroutine(Intro());
    }

    // Saludo de la asistente cuando el jugador sale del menú principal
    IEnumerator Intro()
    {
        while (!GameMenu.IsPlaying) yield return null;
        yield return new WaitForSeconds(1.2f);
        if (stage > 0 || seat.Seated) yield break;
        voice.Say("intro_1");
        voice.Say("intro_2");
        voice.SetIdle(35f, 40f, false, "hall_idle_1", "hall_idle_2", "hall_idle_3");
    }

    void OnPhishOpened()
    {
        if (phishSeen || monitor.Mode != SimMode.Attack) return;
        phishSeen = true;
        voice.SetIdle(40f, 45f, true, "ataque_idle_2", "ataque_idle_3");
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
        if (audioFx) audioFx.DuckAlarm(voice && voice.Busy);
        // Avisos de tiempo de la inspección guiada (3 min)
        if (inspectT0 > 0f && !monitor.Finished)
        {
            float t = Time.time - inspectT0;
            if (timeWarn == 0 && t > 90f) { timeWarn = 1; voice.Say("defensa_mitad"); }
            if (timeWarn == 1 && t > 150f) { timeWarn = 2; voice.Say("defensa_30s", true); }
        }
        if (seat.Seated || monitor.Infected || stage >= 3) return;
        if (stage == 0 && player.transform.position.z > corridorStartZ)
        {
            stage = 1;
            overlay.SetHint("Pasillo de oficinas: Contabilidad, Créditos, Gerencia y RR.HH. Al fondo está la puerta de la ZONA TI.");
            voice.Say("pasillo");
            voice.SetIdle(30f, 35f, false, "pasillo_idle_1", "pasillo_idle_2", "pasillo_idle_3");
        }
        if (stage == 1 && door && door.NearDoor)
        {
            stage = 2;
            overlay.SetHint("Puerta de la ZONA TI. Pulsa ABRIR PUERTA (E) para pasar tu tarjeta y entrar.");
            voice.Say("puerta", true);
            voice.SetIdle(15f, 25f, false, "puerta_idle");
        }
    }

    void OnDoorOpened()
    {
        stage = 3;
        audioFx.Door();
        overlay.SetHint("Acceso concedido. Entra a la Zona TI y siéntate en la estación TI-01 (la que brilla).");
        voice.Say("acceso", true);
        voice.SetIdle(25f, 30f, false, "asiento_idle_1", "asiento_idle_2", "asiento_idle_3");
    }

    void OnSat()
    {
        if (seatMarker) seatMarker.SetActive(false);
        monitor.Activate();
        if (monitor.Mode == SimMode.None)
        {
            overlay.SetHint("Inicia sesión: elige MODO ATAQUE (PC sin protección) o MODO DEFENSA (con controles y guía).");
            if (!mailScheduled)
            {
                voice.Say("elegir_modo", true);
                voice.SetIdle(25f, 30f, false, "elegir_idle");
            }
        }
    }

    void OnModeSelected(SimMode mode)
    {
        overlay.SetHint(mode == SimMode.Defense
            ? "Modo Defensa: tu PC tiene filtro de correo, Sandbox y MFA. Trabaja con normalidad y revisa tu correo."
            : "Es un día normal de trabajo. Explora tu escritorio y revisa tu correo.");
        voice.Say(mode == SimMode.Defense ? "defensa_inicio" : "ataque_inicio", true);
        voice.ClearIdle();
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
        if (mode == SimMode.Defense) { voice.Say("defensa_cuarentena"); voice.SetIdle(25f, 30f, false, "defensa_idle_1"); }
        else { voice.Say("ataque_correo"); if (!phishSeen) voice.SetIdle(25f, 30f, false, "ataque_idle_1"); }
    }

    // ------------------------------------------------------------ MODO ATAQUE
    void OnMalware()
    {
        seat.CanStand = false;
        voice.ClearIdle();
        voice.Say("ataque_infectado", true);
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
        overlay.SetHint("¡ALERTA! Sigue por las cámaras de seguridad cómo el virus salta de un equipo a otro del banco.");
        voice.Say("ataque_llamar_ti");
        voice.Say("ataque_propagacion");
        if (infectionMap)
        {
            StartCctv();
            infectionMap.OnNodeTargeted += CutTo;
            infectionMap.Play();
            while (!infectionMap.Done) yield return null;
            yield return new WaitForSeconds(1.5f);
            CutTo(0);
            yield return new WaitForSeconds(2f);
            StopCctv();
        }
        else yield return new WaitForSeconds(12f);
        ShowReport();
    }

    // La propagación se sigue con una cámara de seguridad que salta al área atacada
    void StartCctv()
    {
        var g = new GameObject("CCTV_Contagio");
        cctvCam = g.AddComponent<Camera>();
        cctvCam.nearClipPlane = 0.05f; cctvCam.farClipPlane = 60f; cctvCam.fieldOfView = 62f;
        cctvCam.clearFlags = CameraClearFlags.SolidColor; cctvCam.backgroundColor = Color.black;
        GameSettings.ApplyCamera(cctvCam);
        if (player.cam) player.cam.GetComponent<Camera>().enabled = false;
    }

    void CutTo(int node)
    {
        if (!cctvCam) return;
        var c = Cams[Mathf.Clamp(node, 0, Cams.Length - 1)];
        cctvCam.transform.position = c.pos;
        cctvCam.transform.rotation = Quaternion.LookRotation(c.look - c.pos);
        overlay.SetCctv(c.name);
    }

    void StopCctv()
    {
        if (infectionMap) infectionMap.OnNodeTargeted -= CutTo;
        if (cctvCam) Destroy(cctvCam.gameObject);
        if (player.cam) player.cam.GetComponent<Camera>().enabled = true;
        overlay.SetCctv(null);
    }

    // ----------------------------------------------------------- REPORTE / FIN
    void OnReported()
    {
        voice.ClearIdle(); inspectT0 = -1f;
        voice.Say(SimSession.Mode == SimMode.Defense ? "defensa_reportado" : "ataque_reportado", true);
        StartCoroutine(EndAfter(3f));
    }

    void OnTimeUp()
    {
        voice.ClearIdle(); inspectT0 = -1f;
        voice.Say("defensa_tiempo", true);
        audioFx.Glitch(); StartCoroutine(EndAfter(3.5f));
    }

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
        voice.ClearIdle();
        if (SimSession.Infected) voice.Say("final_infectado");
        bool defense = SimSession.Mode == SimMode.Defense;
        string time = SimSession.Format(SimSession.Elapsed);
        string signals = defense ? $"{SimSession.SignalsFound} / 3" : (SimSession.Reported ? "Detectado sin ayuda" : "0 / 3");
        string pw = MonitorUI.NotePassword, ip = MonitorUI.AttackerIP;
        var sc = SimSession.Scenario;
        var r = new ReportData
        {
            mode = defense ? "Defensa" : "Ataque", time = time, signals = signals,
            lesson = "<b>Señales de este correo:</b> " + sc.lesson + " Ante la duda: no abras, verifica por otro canal y pulsa «Reportar phishing».",
            mailFrom = sc.fromName, mailAddr = sc.fromAddr, mailSubject = sc.subject,
            mailVector = sc.element switch
            {
                PhishElement.Attachment => "archivo adjunto «" + sc.fileName + "»",
                PhishElement.Image => "imagen que descarga «" + sc.fileName + "»",
                PhishElement.QR => "código QR hacia " + sc.RealHost,
                _ => "enlace hacia " + sc.RealHost,
            },
        };
        string login = System.DateTime.Now.AddMinutes(-Mathf.Max(1f, SimSession.Elapsed / 60f + 2f)).ToString("HH:mm");
        string now = System.DateTime.Now.ToString("HH:mm");
        var okLogin = (login, "PC TI-01 · red interna", "Inicio de sesión de juan.perez", "PERMITIDO", ChipGreen);

        if (SimSession.Infected)
        {
            bool web = SimSession.CredentialTheft;
            r.title = "SISTEMA COMPROMETIDO";
            r.color = RedC;
            r.subtitle = $"Al {sc.attackAction}, el atacante tomó el control de tu PC y la infección se propagó a los 10 equipos de la red del banco.";
            r.evidence = new[]
            {
                ("Correo sospechoso", web ? "ENGAÑO CONSUMADO" : "EJECUTADO", ChipRed,
                    web ? $"«{sc.subject}»: escribiste tus credenciales en la web falsa «{sc.RealHost}»."
                        : $"«{sc.subject}»: se abrió «{sc.fileName}» y se ejecutó el código malicioso."),
                ("Contraseña anotada en el escritorio", "ROBADA", ChipRed,
                    web ? $"Copiaste la clave «{pw}» de tu nota adhesiva en la página falsa: ahora la tiene el atacante."
                        : $"El malware leyó la nota adhesiva «{pw}» (clave del correo y del sistema)."),
                ("Registro de accesos", "NO AUTORIZADO", ChipRed, $"{now} · inicio de sesión como juan.perez desde {ip} con la clave robada. Sin MFA, nada lo impidió."),
            };
            r.access = new[]
            {
                okLogin,
                (now, ip + " · ubicación desconocida", "Inicio de sesión con clave robada", "NO AUTORIZADO", ChipRed),
                (now, ip + " · acceso remoto", "Lectura de correo y archivos", "NO AUTORIZADO", ChipRed),
                (now, "Red interna del banco", "Propagación a 10 equipos", "COMPROMETIDO", ChipRed),
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
            r.access = new[]
            {
                okLogin,
                (now, "Filtro de correo", "Mensaje retenido en cuarentena", "AISLADO", ChipGreen),
                (now, ip + " · ubicación desconocida", "Intento de inicio de sesión", "BLOQUEADO (MFA)", ChipGreen),
            };
        }
        else if (defense)
        {
            r.title = "ATAQUE DETENIDO A TIEMPO";
            r.color = GreenC;
            r.subtitle = $"Identificaste las 3 señales y reportaste el phishing en {time} (límite 03:00). Se evitó la infección por completo.";
            r.evidence = new[]
            {
                ("Correo sospechoso", "AISLADO Y REPORTADO", ChipGreen, $"Retenido en cuarentena, analizado ({sc.scanVerdict}) y reportado al equipo de Seguridad (ticket #SOC-4040)."),
                ("Contraseña anotada en el escritorio", "PROTEGIDA", ChipGreen, "Nunca se escribió en una web falsa ni la leyó un malware. El sistema advirtió del riesgo: muévela al Gestor de Contraseñas y destruye la nota."),
                ("Registro de accesos", "PROTEGIDO", ChipGreen, $"MFA bloqueó 1 intento de inicio de sesión desde {ip}. Historial de accesos intacto."),
            };
            r.access = new[]
            {
                okLogin,
                (now, "Filtro de correo", "Mensaje retenido en cuarentena", "AISLADO", ChipGreen),
                (now, ip + " · ubicación desconocida", "Intento de inicio de sesión", "BLOQUEADO (MFA)", ChipGreen),
                (now, "juan.perez · Seguridad", "Reporte de phishing #SOC-4040", "REGISTRADO", ChipGreen),
            };
        }
        else
        {
            r.title = "PHISHING DETECTADO";
            r.color = GreenC;
            r.subtitle = $"Sin filtros ni ayudas, reconociste el engaño y reportaste el correo en {time}. ¡Excelente criterio!";
            r.evidence = new[]
            {
                ("Correo sospechoso", "REPORTADO", ChipGreen, "No abriste el enlace ni el archivo y lo enviaste al equipo de Seguridad."),
                ("Contraseña anotada en el escritorio", "EN RIESGO", ChipAmber, $"La clave «{pw}» sigue visible: cualquier malware futuro podría leerla."),
                ("Registro de accesos", "SIN INCIDENTES", ChipGreen, "No se registraron accesos no autorizados (aunque esta PC no tiene MFA)."),
            };
            r.access = new[]
            {
                okLogin,
                (now, "juan.perez · Seguridad", "Reporte de phishing #SOC-4040", "REGISTRADO", ChipGreen),
                ("--:--", "Sin accesos externos", "Esta PC no tiene MFA activo", "SIN MFA", ChipAmber),
            };
        }
        overlay.ShowReport(r);
    }
}
