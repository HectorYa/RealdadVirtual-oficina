using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// PC corporativa simulada en el monitor de la estación TI-01 (estilo Windows de oficina):
/// inicio de sesión con selector de modo, escritorio con iconos y post-it, ventanas (correo, navegador,
/// documentos, Excel, papelera), notificaciones, cuarentena + inspección guiada (Modo Defensa)
/// y la secuencia de infección (Modo Ataque). TODO el malware es FICTICIO: solo animaciones de interfaz.
public class MonitorUI : MonoBehaviour
{
    public float screenWidth = 0.8f;                  // ancho del monitor en metros
    public const float InspectionLimit = 180f;        // 3 minutos (RF-03)

    public event Action<SimMode> OnModeSelected;
    public event Action OnMailDelivered, OnInspectionStarted, OnAllSignalsFound, OnMalwareExecuted, OnReported, OnTimeUp;
    public event Action<int> OnSignalFound;

    public bool Infected { get; private set; }
    public bool Finished { get; private set; }
    public SimMode Mode { get; private set; }

    public const string NotePassword = "Admin2026*";
    public const string AttackerIP = "185.220.101.47";

    const float W = 1280f, H = 720f, TB = 46f;
    static readonly Color Ink = new Color(0.1f, 0.12f, 0.16f);
    static readonly Color Soft = new Color(0.42f, 0.47f, 0.55f);
    static readonly Color Gray = new Color(0.94f, 0.95f, 0.97f);
    static readonly Color LineC = new Color(0.85f, 0.87f, 0.9f);
    static readonly Color Accent = new Color(0.13f, 0.42f, 0.86f);
    static readonly Color Gold = new Color(0.97f, 0.80f, 0.36f);
    static readonly Color Red = new Color(0.86f, 0.18f, 0.16f);
    static readonly Color Green = new Color(0.12f, 0.63f, 0.36f);
    static readonly Color Amber = new Color(1f, 0.58f, 0.08f);
    static readonly Color TaskC = new Color(0.035f, 0.06f, 0.11f, 0.95f);
    static readonly Color NavyC = new Color(0.05f, 0.1f, 0.2f);

    /// Escenario de phishing de esta jornada (aleatorio).
    static PhishScenario S => SimSession.Scenario;
    static bool IsFile(PhishScenario s) => s.element == PhishElement.Attachment || s.element == PhishElement.Image;

    class Mail { public string from, addr, subject, body, time, folder; public bool read, phishing; }
    class Win { public string id; public RectTransform rt; public bool max, min; public Vector2 pos, size; }
    class DeskIcon { public RectTransform root; public Image tile; public TextMeshProUGUI glyph, label; public string name; }

    Canvas canvas;
    RectTransform root, desktop, winLayer, toastLayer, popupLayer, startMenu, login;
    TextMeshProUGUI clock, dateTxt, loginClock, loginDate, loginStatus, shieldTxt;
    Image shieldDot;
    GameObject mailBadge, mailTag, taskMailDot;
    RectTransform postIt, postItFrame;
    readonly List<float> iconLastClick = new List<float>();
    readonly List<Image> iconSel = new List<Image>();
    readonly Dictionary<string, Win> wins = new Dictionary<string, Win>();
    readonly Dictionary<string, Image> taskInd = new Dictionary<string, Image>();
    readonly List<DeskIcon> icons = new List<DeskIcon>();
    readonly List<RectTransform> toasts = new List<RectTransform>();
    readonly List<Mail> mails = new List<Mail>();
    Mail phishMail;
    string folder = "inbox";
    RectTransform mailNav, mailList, mailPane;
    bool active, loggedIn, loggingIn;

    // Inspección guiada (Modo Defensa)
    bool inspecting; float inspectStart;
    readonly bool[] found = new bool[3];
    int foundCount;
    readonly RectTransform[] hl = new RectTransform[3];
    TextMeshProUGUI timerBanner, timerNav, explain;
    Button reportBtn; TextMeshProUGUI reportLbl;

    // ================================================================ CICLO
    void Awake()
    {
        var g = new GameObject("MonitorCanvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
        g.transform.SetParent(transform, false);
        canvas = g.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
        var rt = (RectTransform)g.transform;
        rt.sizeDelta = new Vector2(W, H); rt.localScale = Vector3.one * (screenWidth / W);
        rt.localPosition = Vector3.zero; rt.localRotation = Quaternion.identity;
        root = UIKit.Panel(rt, "Root", 0, 0, W, H, NavyC);

        mails.Add(new Mail { from = "Recursos Humanos", addr = "rrhh@bancoandino.com", subject = "Recordatorio: capacitación de seguridad", time = "08:12", folder = "inbox", read = true,
            body = "Estimado equipo:\n\nLes recordamos que la capacitación anual de seguridad de la información será el jueves a las 10:00 en la Sala 2.\n\nSaludos,\nRecursos Humanos" });
        mails.Add(new Mail { from = "Soporte TI", addr = "soporte.ti@bancoandino.com", subject = "Mantenimiento programado del sábado", time = "Ayer", folder = "inbox", read = true,
            body = "Equipo:\n\nEl sábado de 22:00 a 23:00 habrá mantenimiento en los servidores internos. No se requiere ninguna acción de su parte.\n\nRecuerden: TI nunca les pedirá su contraseña por correo.\n\nSoporte TI" });
        mails.Add(new Mail { from = "Gerencia de Operaciones", addr = "operaciones@bancoandino.com", subject = "Reporte mensual de transacciones", time = "Ayer", folder = "inbox", read = true,
            body = "Buenos días:\n\nEl reporte mensual ya está en la carpeta compartida. Por favor revisarlo antes del viernes.\n\nGerencia de Operaciones" });
        mails.Add(new Mail { from = "Juan Pérez", addr = "juan.perez@bancoandino.com", subject = "RE: Horarios de turno", time = "Lun", folder = "sent", read = true,
            body = "Hola, adjunto los horarios actualizados del turno de la próxima semana.\n\nSaludos,\nJuan" });

        BuildDesktop();
        BuildLogin();
    }

    void Start() { canvas.worldCamera = Camera.main; }

    void Update()
    {
        if (!canvas.worldCamera || !canvas.worldCamera.isActiveAndEnabled) canvas.worldCamera = Camera.main;
        var now = DateTime.Now;
        if (clock) clock.text = now.ToString("HH:mm");
        if (dateTxt) dateTxt.text = now.ToString("dd/MM/yyyy");
        if (loginClock) { loginClock.text = now.ToString("HH:mm"); loginDate.text = $"{Days[(int)now.DayOfWeek]}, {now.Day} de {Months[now.Month - 1]}"; }

        if (inspecting && !Finished)
        {
            float rem = InspectionLimit - (Time.time - inspectStart);
            string t = SimSession.Format(rem);
            Color c = rem < 30f ? Red : (rem < 60f ? Amber : Ink);
            if (timerBanner) { timerBanner.text = "Tiempo restante  " + t; timerBanner.color = rem < 30f ? Red : Ink; }
            if (timerNav) { timerNav.text = t; timerNav.color = c; }
            if (rem <= 0f) TimeUp();
        }
        // Rectángulos de alerta: los pendientes laten en naranja
        float pulse = 0.55f + 0.45f * Mathf.Sin(Time.time * 5f);
        if (postItFrame && !Infected)
            foreach (var im in postItFrame.GetComponentsInChildren<Image>())
                if (im.name == "Edge") im.color = new Color(Amber.r, Amber.g, Amber.b, pulse);
        for (int i = 0; i < 3; i++)
        {
            if (!hl[i] || found[i]) continue;
            foreach (var im in hl[i].GetComponentsInChildren<Image>())
                if (im.name == "Edge") im.color = new Color(Amber.r, Amber.g, Amber.b, pulse);
        }
    }

    // Nombres en español sin depender de la configuración regional del dispositivo
    static readonly string[] Days = { "Domingo", "Lunes", "Martes", "Miércoles", "Jueves", "Viernes", "Sábado" };
    static readonly string[] Months = { "enero", "febrero", "marzo", "abril", "mayo", "junio", "julio", "agosto", "septiembre", "octubre", "noviembre", "diciembre" };

    static TextMeshProUGUI NoWrap(TextMeshProUGUI t) { t.textWrappingMode = TextWrappingModes.NoWrap; t.overflowMode = TextOverflowModes.Ellipsis; return t; }

    // Botón transparente que se ilumina al pasar el cursor (el tinte multiplica el color base blanco)
    static void HoverTint(Button b, float a)
    {
        var cb = b.colors;
        cb.normalColor = new Color(1, 1, 1, 0f); cb.highlightedColor = new Color(1, 1, 1, a);
        cb.pressedColor = new Color(1, 1, 1, a * 1.6f); cb.selectedColor = new Color(1, 1, 1, 0f);
        b.colors = cb;
    }

    // ================================================================== API
    /// El jugador se sentó: la pantalla de inicio de sesión acepta interacción.
    public void Activate()
    {
        active = true;
        if (!loggedIn && !loggingIn && loginStatus) loginStatus.text = "Elige tu jornada para iniciar sesión";
    }

    /// Entrar directamente en un modo (reinicio rápido desde el reporte).
    public void AutoLogin(SimMode mode) { active = true; if (!loggedIn && !loggingIn) StartCoroutine(Login(mode)); }

    public void DeliverPhishingMail()
    {
        if (phishMail != null) return;
        bool defense = Mode == SimMode.Defense;
        phishMail = new Mail
        {
            from = S.fromName, addr = S.fromAddr, subject = S.subject, time = DateTime.Now.ToString("HH:mm"),
            folder = defense ? "quarantine" : "inbox", phishing = true,
        };
        mails.Insert(0, phishMail);
        UpdateMailBadge();
        if (defense)
        {
            mailTag.SetActive(true);
            Toast("Filtro de correo", "Se retuvo 1 mensaje sospechoso en CUARENTENA: «" + S.subject + "».", Amber, () => { OpenMail(); SelectFolder("quarantine"); });
        }
        else
        {
            Toast("Nuevo correo recibido", S.fromName + " — «" + S.subject + "»", Accent, () => { OpenMail(); ShowMail(phishMail); });
        }
        if (wins.ContainsKey("mail")) { RefreshNav(); RefreshList(); }
        SimSession.TimerStart = Time.time;
        OnMailDelivered?.Invoke();
    }

    // ============================================================= HELPERS UI
    static void Stretch(RectTransform rt, float top = 0f)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.pivot = new Vector2(0f, 1f);
        rt.offsetMin = Vector2.zero; rt.offsetMax = new Vector2(0f, -top);
    }

    static RectTransform TopRight(RectTransform rt, float x, float y)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 1f); rt.anchoredPosition = new Vector2(-x, -y); return rt;
    }

    static Sprite gradSprite;
    static Sprite Gradient()
    {
        if (gradSprite) return gradSprite;
        var t = new Texture2D(2, 128, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < 128; y++)
        {
            float k = y / 127f;
            var c = Color.Lerp(new Color(0.02f, 0.06f, 0.13f), new Color(0.05f, 0.22f, 0.36f), k * k);
            t.SetPixel(0, y, c); t.SetPixel(1, y, c);
        }
        t.Apply();
        return gradSprite = Sprite.Create(t, new Rect(0, 0, 2, 128), new Vector2(0.5f, 0.5f));
    }

    // Marco de 4 bordes (para resaltar)
    static RectTransform Frame(Transform p, string n, float x, float y, float w, float h, Color c, float th, Color fill)
    {
        var f = UIKit.Panel(p, n, x, y, w, h, fill);
        UIKit.Panel(f, "Edge", 0, 0, w, th, c, false);
        UIKit.Panel(f, "Edge", 0, h - th, w, th, c, false);
        UIKit.Panel(f, "Edge", 0, 0, th, h, c, false);
        UIKit.Panel(f, "Edge", w - th, 0, th, h, c, false);
        return f;
    }

    // Icono de aplicación dibujado con formas simples
    static void AppGlyph(Transform p, string app, float x, float y, float s)
    {
        switch (app)
        {
            case "mail":
            {
                var t = UIKit.Panel(p, "Tile", x, y, s, s, new Color(0.13f, 0.45f, 0.9f), false);
                UIKit.Panel(t, "Env", s * 0.18f, s * 0.3f, s * 0.64f, s * 0.42f, Color.white, false);
                var v = UIKit.Label(t, "V", 0, s * 0.22f, s, s * 0.4f, s * 0.42f, new Color(0.13f, 0.45f, 0.9f), TextAlignmentOptions.Center, true);
                v.transform.localScale = new Vector3(1.6f, 0.7f, 1f);
                break;
            }
            case "browser":
            {
                var c = UIKit.Panel(p, "Tile", x, y, s, s, new Color(0.1f, 0.62f, 0.55f), false);
                c.GetComponent<Image>().sprite = UIKit.Circle();
                UIKit.Panel(c, "Eq", s * 0.12f, s * 0.48f, s * 0.76f, s * 0.05f, Color.white, false);
                UIKit.Panel(c, "Mer", s * 0.475f, s * 0.12f, s * 0.05f, s * 0.76f, Color.white, false);
                var r = UIKit.Panel(c, "Ring", s * 0.3f, s * 0.12f, s * 0.4f, s * 0.76f, new Color(1, 1, 1, 0.35f), false);
                r.GetComponent<Image>().sprite = UIKit.Circle();
                break;
            }
            case "docs":
                UIKit.Panel(p, "Tab", x + s * 0.06f, y + s * 0.14f, s * 0.38f, s * 0.14f, new Color(0.9f, 0.62f, 0.12f), false);
                UIKit.Panel(p, "Folder", x + s * 0.06f, y + s * 0.24f, s * 0.88f, s * 0.6f, new Color(1f, 0.76f, 0.24f), false);
                break;
            case "excel":
            {
                var t = UIKit.Panel(p, "Tile", x, y, s, s, new Color(0.08f, 0.5f, 0.27f), false);
                UIKit.Label(t, "X", 0, 0, s, s, s * 0.6f, Color.white, TextAlignmentOptions.Center, true);
                break;
            }
            case "bin":
                UIKit.Panel(p, "Lid", x + s * 0.2f, y + s * 0.14f, s * 0.6f, s * 0.08f, new Color(0.75f, 0.8f, 0.86f), false);
                UIKit.Panel(p, "Can", x + s * 0.26f, y + s * 0.24f, s * 0.48f, s * 0.62f, new Color(0.85f, 0.89f, 0.94f), false);
                for (int i = 0; i < 3; i++)
                    UIKit.Panel(p, "Rib", x + s * (0.34f + i * 0.12f), y + s * 0.32f, s * 0.04f, s * 0.46f, new Color(0.6f, 0.66f, 0.74f), false);
                break;
            case "lock":
            {
                var t = UIKit.Panel(p, "Tile", x, y, s, s, new Color(0.18f, 0.18f, 0.2f), false);
                UIKit.Panel(t, "ShL", s * 0.3f, s * 0.16f, s * 0.08f, s * 0.3f, Gold, false);
                UIKit.Panel(t, "ShR", s * 0.62f, s * 0.16f, s * 0.08f, s * 0.3f, Gold, false);
                UIKit.Panel(t, "ShT", s * 0.3f, s * 0.14f, s * 0.4f, s * 0.08f, Gold, false);
                UIKit.Panel(t, "Body", s * 0.22f, s * 0.44f, s * 0.56f, s * 0.42f, Gold, false);
                break;
            }
        }
    }

    // ============================================================ ESCRITORIO
    void BuildDesktop()
    {
        desktop = UIKit.Panel(root, "Desktop", 0, 0, W, H, Color.white);
        desktop.GetComponent<Image>().sprite = Gradient();
        UIKit.Label(desktop, "BANCO ANDINO", 0, 270, W, 80, 64, new Color(1, 1, 1, 0.07f), TextAlignmentOptions.Center, true);
        UIKit.Label(desktop, "Seguridad · Confianza · Servicio", 0, 350, W, 40, 24, new Color(1, 1, 1, 0.07f), TextAlignmentOptions.Center);

        var defs = new (string app, string name, Action open)[]
        {
            ("mail", "Correo Corporativo", OpenMail),
            ("browser", "Navegador Web", () => OpenBrowser(0)),
            ("docs", "Mis Documentos", OpenDocs),
            ("excel", "Excel", OpenExcel),
            ("bin", "Papelera", OpenBin),
        };
        for (int i = 0; i < defs.Length; i++)
        {
            var d = defs[i];
            float y = 20 + i * 106;
            int idx = i;
            var b = UIKit.Btn(desktop, "", 14, y, 120, 100, Color.white, Color.white, 10, () => IconClicked(idx, d.open));
            HoverTint(b, 0.15f);
            var sel = UIKit.Panel(b.transform, "Selected", 0, 0, 120, 100, new Color(0.55f, 0.75f, 1f, 0.28f), false).GetComponent<Image>();
            sel.enabled = false; iconSel.Add(sel); iconLastClick.Add(-10f);
            var holder = UIKit.Panel(b.transform, "Glyph", 0, 0, 120, 70, Color.clear, false);
            AppGlyph(holder, d.app, 30, 6, 60);
            var lb = UIKit.Label(b.transform, d.name, 0, 70, 120, 30, 16, Color.white, TextAlignmentOptions.Center);
            lb.fontStyle = FontStyles.Bold;
            icons.Add(new DeskIcon { root = holder, label = lb, name = d.name });
            if (d.app == "mail")
            {
                var badge = UIKit.Panel(b.transform, "Badge", 80, 0, 28, 28, Red, false);
                badge.GetComponent<Image>().sprite = UIKit.Circle();
                UIKit.Label(badge, "1", 0, 0, 28, 28, 18, Color.white, TextAlignmentOptions.Center, true);
                mailBadge = badge.gameObject; mailBadge.SetActive(false);
                var tag = UIKit.Panel(b.transform, "Tag", 10, 50, 100, 18, Amber, false);
                UIKit.Label(tag, "CUARENTENA", 0, 0, 100, 18, 12, Color.white, TextAlignmentOptions.Center, true).textWrappingMode = TextWrappingModes.NoWrap;
                mailTag = tag.gameObject; mailTag.SetActive(false);
            }
        }

        // Post-it con la contraseña (vulnerabilidad humana, RF-05)
        postIt = UIKit.Panel(desktop, "PostIt", W - 262, 26, 220, 172, new Color(1f, 0.9f, 0.36f));
        postIt.localEulerAngles = new Vector3(0, 0, -3f);
        UIKit.Panel(postIt, "Tape", 70, -10, 80, 22, new Color(1, 1, 1, 0.45f), false);
        var note = UIKit.Label(postIt, "Recordatorio:\nClave correo / sistema:\n<size=130%><b>" + NotePassword + "</b></size>\n(No olvidar cambiar)", 16, 20, 190, 140, 20, new Color(0.15f, 0.2f, 0.5f));
        note.fontStyle = FontStyles.Italic;
        var pb = postIt.gameObject.AddComponent<Button>(); pb.transition = Selectable.Transition.None;
        pb.onClick.AddListener(PostItClicked);

        winLayer = UIKit.Panel(root, "Windows", 0, 0, W, H - TB, Color.clear, false);
        BuildTaskbar();
        toastLayer = UIKit.Panel(root, "Toasts", 0, 0, W, H, Color.clear, false);
        popupLayer = UIKit.Panel(root, "Popups", 0, 0, W, H, Color.clear, false);
    }

    void BuildTaskbar()
    {
        var bar = UIKit.Panel(root, "Taskbar", 0, H - TB, W, TB, TaskC);
        var st = UIKit.Btn(bar, "", 0, 0, 56, TB, new Color(1, 1, 1, 0.001f), Color.white, 10, ToggleStart);
        var logo = UIKit.Panel(st.transform, "Logo", 12, 7, 32, 32, Gold, false);
        UIKit.Label(logo, "BA", 0, 0, 32, 32, 16, NavyC, TextAlignmentOptions.Center, true);

        var pins = new (string id, string app, Action open)[]
        {
            ("mail", "mail", OpenMail), ("browser", "browser", () => OpenBrowser(0)), ("docs", "docs", OpenDocs), ("excel", "excel", OpenExcel),
        };
        for (int i = 0; i < pins.Length; i++)
        {
            var p = pins[i];
            var b = UIKit.Btn(bar, "", 64 + i * 52, 0, 50, TB, Color.white, Color.white, 10, () => TaskClick(p.id, p.open));
            HoverTint(b, 0.12f);
            var g = UIKit.Panel(b.transform, "G", 0, 0, 50, TB, Color.clear, false);
            AppGlyph(g, p.app, 11, 8, 28);
            var ind = UIKit.Panel(b.transform, "Ind", 15, TB - 4, 20, 3, new Color(0.5f, 0.75f, 1f), false).GetComponent<Image>();
            ind.enabled = false; taskInd[p.id] = ind;
            if (p.id == "mail")
            {
                var dot = UIKit.Panel(b.transform, "Dot", 34, 6, 10, 10, Red, false);
                dot.GetComponent<Image>().sprite = UIKit.Circle();
                taskMailDot = dot.gameObject; taskMailDot.SetActive(false);
            }
        }

        // Bandeja del sistema: escudo de seguridad, red, reloj y fecha
        var shield = UIKit.Panel(bar, "Shield", W - 560, 8, 330, 30, new Color(1, 1, 1, 0.06f), false);
        shieldDot = UIKit.Panel(shield, "Dot", 10, 8, 14, 14, new Color(0.5f, 0.5f, 0.55f), false).GetComponent<Image>();
        shieldDot.sprite = UIKit.Circle();
        shieldTxt = UIKit.Label(shield, "Protección: --", 32, 5, 296, 22, 15, new Color(0.75f, 0.78f, 0.85f));
        // Red corporativa (barras)
        for (int i = 0; i < 4; i++)
            UIKit.Panel(bar, "Net", W - 210 + i * 7, 30 - i * 5, 5, 6 + i * 5, Color.white, false);
        UIKit.Label(bar, "RED-BANCO", W - 175, 15, 80, 20, 12, new Color(0.7f, 0.75f, 0.82f));
        clock = UIKit.Label(bar, "09:00", W - 92, 3, 84, 22, 17, Color.white, TextAlignmentOptions.TopRight, true);
        dateTxt = UIKit.Label(bar, "01/01/2026", W - 112, 23, 104, 20, 13, new Color(0.75f, 0.8f, 0.88f), TextAlignmentOptions.TopRight);

        // Menú Inicio
        startMenu = UIKit.Panel(root, "StartMenu", 0, H - TB - 380, 340, 380, new Color(0.06f, 0.09f, 0.15f, 0.98f));
        var av = UIKit.Panel(startMenu, "Avatar", 20, 20, 64, 64, Gold, false); av.GetComponent<Image>().sprite = UIKit.Circle();
        UIKit.Label(av, "JP", 0, 0, 64, 64, 26, NavyC, TextAlignmentOptions.Center, true);
        UIKit.Label(startMenu, "Juan Pérez", 98, 24, 230, 30, 22, Color.white, TextAlignmentOptions.TopLeft, true);
        UIKit.Label(startMenu, "Área Administrativa\njuan.perez@bancoandino.com", 98, 54, 236, 40, 13, new Color(0.7f, 0.76f, 0.85f));
        UIKit.Panel(startMenu, "Sep", 20, 100, 300, 1, new Color(1, 1, 1, 0.12f), false);
        var items = new (string t, Action a)[]
        {
            ("Correo Corporativo", OpenMail), ("Navegador Web", () => OpenBrowser(0)), ("Mis Documentos", OpenDocs), ("Excel", OpenExcel),
            ("Bloquear sesión", () => Toast("Sesión", "No es posible bloquear la sesión durante la simulación.", Soft, null)),
        };
        for (int i = 0; i < items.Length; i++)
        {
            var it = items[i];
            var b = UIKit.Btn(startMenu, it.t, 12, 112 + i * 50, 316, 46, Color.white, Color.white, 18, () => { CloseStart(); it.a(); });
            HoverTint(b, 0.1f);
            b.GetComponentInChildren<TextMeshProUGUI>().alignment = TextAlignmentOptions.MidlineLeft;
            b.GetComponentInChildren<TextMeshProUGUI>().margin = new Vector4(16, 0, 0, 0);
        }
        startMenu.gameObject.SetActive(false);
    }

    // Como en Windows: doble clic abre (un clic selecciona). En el móvil basta un toque.
    void IconClicked(int i, Action open)
    {
        if (!loggedIn) return;
        bool dbl = Time.unscaledTime - iconLastClick[i] < 0.45f;
        for (int k = 0; k < iconSel.Count; k++) iconSel[k].enabled = k == i;
        if (Application.isMobilePlatform || dbl) { iconLastClick[i] = -10f; iconSel[i].enabled = false; open(); }
        else iconLastClick[i] = Time.unscaledTime;
    }

    void ToggleStart() { if (loggedIn) { startMenu.gameObject.SetActive(!startMenu.gameObject.activeSelf); startMenu.SetAsLastSibling(); } }
    void CloseStart() { if (startMenu) startMenu.gameObject.SetActive(false); }

    void TaskClick(string id, Action open)
    {
        if (!loggedIn) return;
        CloseStart();
        if (wins.TryGetValue(id, out var w) && !w.min && w.rt.GetSiblingIndex() == winLayer.childCount - 1) { Minimize(id); return; }
        open();
    }

    void SetShield()
    {
        bool def = Mode == SimMode.Defense;
        shieldDot.color = def ? new Color(0.25f, 0.9f, 0.45f) : new Color(0.5f, 0.5f, 0.55f);
        shieldTxt.text = def ? "Protección activa: Filtro + Sandbox + MFA" : "Protección desactivada";
        shieldTxt.color = def ? new Color(0.55f, 1f, 0.7f) : new Color(0.65f, 0.68f, 0.74f);
    }

    void UpdateMailBadge()
    {
        int n = 0; foreach (var m in mails) if (!m.read && m.folder == "inbox") n++;
        mailBadge.SetActive(n > 0);
        if (n > 0) mailBadge.GetComponentInChildren<TextMeshProUGUI>().text = n.ToString();
        taskMailDot.SetActive(n > 0 || (phishMail != null && phishMail.folder == "quarantine" && !phishMail.read));
    }

    void PostItClicked()
    {
        if (!loggedIn) return;
        if (Mode == SimMode.Defense)
            Toast("Riesgo detectado", "Tienes una contraseña visible en una nota. Guárdala en el Gestor de Contraseñas y destruye el papel.", Amber, () => OpenBrowser(2));
        else
            Toast("Nota adhesiva", "Recordatorio personal de la contraseña del correo y del sistema.", Soft, null);
    }

    // ===================================================== INICIO DE SESIÓN
    void BuildLogin()
    {
        login = UIKit.Panel(root, "Login", 0, 0, W, H, Color.white);
        login.GetComponent<Image>().sprite = Gradient();
        UIKit.Panel(login, "Dim", 0, 0, W, H, new Color(0, 0, 0, 0.35f), false);
        loginClock = UIKit.Label(login, "09:00", 0, 40, W, 100, 92, Color.white, TextAlignmentOptions.Center, true);
        loginDate = UIKit.Label(login, "", 0, 140, W, 36, 26, new Color(0.85f, 0.9f, 1f), TextAlignmentOptions.Center);
        var av = UIKit.Panel(login, "Avatar", W / 2 - 56, 200, 112, 112, Gold, false); av.GetComponent<Image>().sprite = UIKit.Circle();
        UIKit.Label(av, "JP", 0, 0, 112, 112, 46, NavyC, TextAlignmentOptions.Center, true);
        UIKit.Label(login, "Juan Pérez", 0, 322, W, 44, 36, Color.white, TextAlignmentOptions.Center, true);
        UIKit.Label(login, "Área Administrativa · Banco Andino", 0, 366, W, 30, 20, new Color(0.8f, 0.86f, 0.95f), TextAlignmentOptions.Center);

        ModeButton("INICIAR JORNADA", "MODO ATAQUE", "PC sin controles de seguridad ni ayudas", Red, W / 2 - 400, SimMode.Attack);
        ModeButton("INICIAR JORNADA", "MODO DEFENSA", "Filtro de correo, Sandbox, MFA y guía visual", Green, W / 2 + 20, SimMode.Defense);
        loginStatus = UIKit.Label(login, "Siéntate en la estación para iniciar sesión", 0, 590, W, 30, 18, new Color(0.75f, 0.8f, 0.9f), TextAlignmentOptions.Center);
        UIKit.Label(login, "Banco Andino  ·  Estación TI-01  ·  Uso exclusivo del personal autorizado", 0, 680, W, 26, 15, new Color(1, 1, 1, 0.45f), TextAlignmentOptions.Center);
    }

    void ModeButton(string kicker, string title, string sub, Color accent, float x, SimMode mode)
    {
        var b = UIKit.Btn(login, "", x, 420, 380, 150, new Color(0.04f, 0.08f, 0.16f, 0.85f), Color.white, 10, () => { if (active && !loggedIn && !loggingIn) StartCoroutine(Login(mode)); });
        var cb = b.colors; cb.highlightedColor = new Color(1.3f, 1.3f, 1.3f); b.colors = cb;
        UIKit.Panel(b.transform, "Accent", 0, 0, 380, 6, accent, false);
        UIKit.Label(b.transform, kicker, 24, 26, 340, 24, 16, new Color(0.75f, 0.8f, 0.9f), TextAlignmentOptions.TopLeft, true);
        UIKit.Label(b.transform, title, 24, 50, 340, 46, 36, accent == Red ? new Color(1f, 0.45f, 0.42f) : new Color(0.4f, 1f, 0.6f), TextAlignmentOptions.TopLeft, true);
        UIKit.Label(b.transform, sub, 24, 102, 340, 40, 17, Color.white);
    }

    IEnumerator Login(SimMode mode)
    {
        loggingIn = true;
        Mode = mode;
        SimSession.Begin(mode);
        loginStatus.text = "Iniciando sesión...";
        yield return new WaitForSeconds(1.2f);
        login.gameObject.SetActive(false);
        loggedIn = true; loggingIn = false;
        SetShield();
        OnModeSelected?.Invoke(mode);
        yield return new WaitForSeconds(0.8f);
        string tip = Application.isMobilePlatform ? "" : " Haz doble clic en un icono para abrirlo.";
        if (mode == SimMode.Defense)
            Toast("Seguridad", "Protección activa: filtro de correo, análisis en Sandbox y doble factor (MFA)." + tip, Green, null);
        else
            Toast("Bienvenido", "Buenos días, Juan. Tienes una jornada normal por delante." + tip, Accent, null);
        if (mode == SimMode.Defense) { yield return new WaitForSeconds(2.5f); PostItWarning(); }
    }

    // Modo Defensa: el sistema advierte por sí solo de la contraseña visible en la nota (RF-05)
    void PostItWarning()
    {
        if (postItFrame || Finished) return;
        postItFrame = Frame(desktop, "PostItRisk", W - 270, 18, 236, 188, Amber, 4f, new Color(1f, 0.6f, 0.1f, 0.06f));
        postItFrame.localEulerAngles = new Vector3(0, 0, -3f);
        postItFrame.GetComponent<Image>().raycastTarget = false;
        postItFrame.SetSiblingIndex(postIt.GetSiblingIndex() + 1);
        var badge = UIKit.Panel(postItFrame, "Badge", 206, -14, 34, 34, Amber, false);
        badge.GetComponent<Image>().sprite = UIKit.Circle();
        UIKit.Label(badge, "!", 0, 0, 34, 34, 24, Color.white, TextAlignmentOptions.Center, true);
        var lbl = UIKit.Panel(postItFrame, "Risk", 18, 192, 200, 24, Amber, false);
        UIKit.Label(lbl, "RIESGO: CLAVE VISIBLE", 0, 2, 200, 20, 13, Color.white, TextAlignmentOptions.Center, true);
        Toast("Riesgo detectado", "Hay una contraseña visible en una nota adhesiva. Guárdala en el Gestor de Contraseñas y destruye el papel.", Amber, () => OpenBrowser(2), 9f);
    }

    // ============================================================= VENTANAS
    class Focus : MonoBehaviour, IPointerDownHandler { public void OnPointerDown(PointerEventData e) => transform.SetAsLastSibling(); }

    RectTransform OpenWindow(string id, string title, string app, float x, float y, float w, float h, bool canMin = true)
    {
        CloseStart();
        if (wins.TryGetValue(id, out var ex))
        {
            ex.min = false; ex.rt.gameObject.SetActive(true); ex.rt.SetAsLastSibling(); UpdateTask();
            return null;   // ya existía
        }
        var rt = UIKit.Panel(winLayer, "Win_" + id, x, y, w, h, Color.clear, false);
        rt.gameObject.AddComponent<Focus>();
        var sh = UIKit.Panel(rt, "Shadow", 6, 8, w, h, new Color(0, 0, 0, 0.35f), false); Stretch(sh); sh.offsetMin = new Vector2(6, -8); sh.offsetMax = new Vector2(6, -8);
        var frame = UIKit.Panel(rt, "Frame", 0, 0, w, h, Color.white); Stretch(frame);
        var tb = UIKit.Panel(frame, "TitleBar", 0, 0, w, 34, new Color(0.95f, 0.96f, 0.98f));
        tb.anchorMax = new Vector2(1f, 1f); tb.sizeDelta = new Vector2(0, 34); tb.anchoredPosition = Vector2.zero;
        var g = UIKit.Panel(tb, "Ic", 8, 0, 34, 34, Color.clear, false); AppGlyph(g, app, 6, 7, 20);
        UIKit.Label(tb, title, 42, 7, w - 200, 24, 16, Ink);
        var bx = UIKit.Btn(tb, "X", 0, 0, 46, 34, new Color(0.95f, 0.96f, 0.98f), Ink, 18, () => CloseWin(id)); TopRight((RectTransform)bx.transform, 0, 0);
        var cbx = bx.colors; cbx.highlightedColor = new Color(1f, 0.45f, 0.45f); bx.colors = cbx;
        var bm = UIKit.Btn(tb, "[ ]", 0, 0, 46, 34, new Color(0.95f, 0.96f, 0.98f), Ink, 14, () => ToggleMax(id)); TopRight((RectTransform)bm.transform, 46, 0);
        if (canMin) { var bn = UIKit.Btn(tb, "_", 0, 0, 46, 34, new Color(0.95f, 0.96f, 0.98f), Ink, 18, () => Minimize(id)); TopRight((RectTransform)bn.transform, 92, 0); }
        var body = UIKit.Panel(frame, "Body", 0, 34, w, h - 34, Color.white, false); Stretch(body, 34);
        wins[id] = new Win { id = id, rt = rt, pos = new Vector2(x, y), size = new Vector2(w, h) };
        UpdateTask();
        return body;
    }

    void CloseWin(string id)
    {
        if (!wins.TryGetValue(id, out var w)) return;
        Destroy(w.rt.gameObject); wins.Remove(id);
        if (id == "mail") { mailNav = mailList = mailPane = null; timerBanner = timerNav = explain = null; reportBtn = null; for (int i = 0; i < 3; i++) hl[i] = null; }
        UpdateTask();
    }

    void Minimize(string id) { if (wins.TryGetValue(id, out var w)) { w.min = true; w.rt.gameObject.SetActive(false); UpdateTask(); } }

    void ToggleMax(string id)
    {
        if (!wins.TryGetValue(id, out var w)) return;
        w.max = !w.max;
        w.rt.anchoredPosition = w.max ? Vector2.zero : new Vector2(w.pos.x, -w.pos.y);
        w.rt.sizeDelta = w.max ? new Vector2(W, H - TB) : w.size;
    }

    void UpdateTask()
    {
        foreach (var kv in taskInd) kv.Value.enabled = wins.ContainsKey(kv.Key);
    }

    // ======================================================== NOTIFICACIONES
    void Toast(string title, string body, Color accent, Action onClick, float life = 7f)
    {
        var b = UIKit.Btn(toastLayer, "", W - 384, H - TB - 110, 370, 98, new Color(0.08f, 0.11f, 0.17f, 0.97f), Color.white, 10, null);
        var rt = (RectTransform)b.transform;
        UIKit.Panel(rt, "Accent", 0, 0, 5, 98, accent, false);
        UIKit.Label(rt, title, 18, 10, 340, 24, 17, Color.white, TextAlignmentOptions.TopLeft, true);
        var t = UIKit.Label(rt, body, 18, 36, 340, 56, 14, new Color(0.8f, 0.85f, 0.92f));
        t.enableAutoSizing = true; t.fontSizeMin = 10; t.fontSizeMax = 14;
        b.onClick.AddListener(() => { onClick?.Invoke(); RemoveToast(rt); });
        toasts.Insert(0, rt);
        LayoutToasts();
        StartCoroutine(ToastLife(rt, life));
    }

    IEnumerator ToastLife(RectTransform rt, float life)
    {
        yield return new WaitForSeconds(life);
        RemoveToast(rt);
    }

    void RemoveToast(RectTransform rt)
    {
        if (!rt || !toasts.Contains(rt)) return;
        toasts.Remove(rt); Destroy(rt.gameObject); LayoutToasts();
    }

    void LayoutToasts()
    {
        for (int i = 0; i < toasts.Count; i++)
        {
            toasts[i].anchoredPosition = new Vector2(W - 384, -(H - TB - 110 - i * 106));
            toasts[i].gameObject.SetActive(i < 4);
        }
    }

    // ================================================================ CORREO
    void OpenMail()
    {
        if (!loggedIn) return;
        var body = OpenWindow("mail", "Correo Corporativo — juan.perez@bancoandino.com", "mail", 40, 22, 1060, 616);
        if (body == null) return;
        mailNav = UIKit.Panel(body, "Nav", 0, 0, 200, 582, new Color(0.96f, 0.97f, 0.99f), false);
        mailList = UIKit.Panel(body, "List", 200, 0, 300, 582, Gray, false);
        mailPane = UIKit.Panel(body, "Pane", 500, 0, 560, 582, Color.white, false);
        UIKit.Panel(body, "SepA", 200, 0, 1, 582, LineC, false);
        UIKit.Panel(body, "SepB", 500, 0, 1, 582, LineC, false);
        if (phishMail != null && phishMail.folder == "quarantine" && !phishMail.read) folder = "quarantine";
        RefreshNav(); RefreshList();
        UIKit.Label(mailPane, "Selecciona un mensaje para leerlo.", 30, 30, 500, 40, 20, Soft);
    }

    void SelectFolder(string f) { folder = f; if (!wins.ContainsKey("mail")) return; RefreshNav(); RefreshList(); }

    void RefreshNav()
    {
        if (!mailNav) return;
        foreach (Transform c in mailNav) Destroy(c.gameObject);
        UIKit.Btn(mailNav, "+  Nuevo mensaje", 14, 14, 172, 40, Accent, Color.white, 16, () => Toast("Correo", "Función no disponible en la simulación.", Soft, null));
        var folders = new (string id, string name)[] { ("inbox", "Bandeja de entrada"), ("quarantine", "Cuarentena"), ("sent", "Enviados"), ("trash", "Eliminados") };
        for (int i = 0; i < folders.Length; i++)
        {
            var f = folders[i];
            int n = 0; foreach (var m in mails) if (m.folder == f.id && !m.read) n++;
            bool sel = folder == f.id;
            bool warn = f.id == "quarantine" && phishMail != null && phishMail.folder == "quarantine";
            var b = UIKit.Btn(mailNav, "", 8, 68 + i * 40, 184, 36, sel ? new Color(0.86f, 0.91f, 1f) : new Color(0.96f, 0.97f, 0.99f), Ink, 10, () => SelectFolder(f.id));
            UIKit.Label(b.transform, f.name, 12, 8, 140, 22, 14, warn ? new Color(0.75f, 0.38f, 0f) : Ink, TextAlignmentOptions.TopLeft, sel || warn).textWrappingMode = TextWrappingModes.NoWrap;
            if (n > 0) UIKit.Label(b.transform, n.ToString(), 140, 8, 34, 22, 15, warn ? Amber : Accent, TextAlignmentOptions.TopRight, true);
        }

        // Panel de inspección guiada (Modo Defensa)
        if (Mode == SimMode.Defense && (inspecting || foundCount > 0))
        {
            var p = UIKit.Panel(mailNav, "Inspect", 8, 236, 184, 336, new Color(1f, 0.96f, 0.88f), false);
            UIKit.Panel(p, "Top", 0, 0, 184, 4, Amber, false);
            UIKit.Label(p, "INSPECCIÓN GUIADA", 10, 12, 170, 20, 13, new Color(0.65f, 0.35f, 0f), TextAlignmentOptions.TopLeft, true);
            timerNav = UIKit.Label(p, "03:00", 10, 30, 164, 44, 38, Ink, TextAlignmentOptions.TopLeft, true);
            UIKit.Label(p, $"Señales: {foundCount} / 3", 10, 76, 170, 20, 14, Ink, TextAlignmentOptions.TopLeft, true);
            for (int i = 0; i < 3; i++)
            {
                var dot = UIKit.Panel(p, "N", 10, 102 + i * 28, 22, 22, found[i] ? Green : Amber, false);
                dot.GetComponent<Image>().sprite = UIKit.Circle();
                UIKit.Label(dot, (i + 1).ToString(), 0, 0, 22, 22, 13, Color.white, TextAlignmentOptions.Center, true);
                { var sn = UIKit.Label(p, S.signalNames[i], 38, 104 + i * 28, 142, 22, 12, found[i] ? Green : Soft, TextAlignmentOptions.TopLeft, found[i]); sn.textWrappingMode = TextWrappingModes.NoWrap; sn.overflowMode = TextOverflowModes.Ellipsis; }
            }
            explain = UIKit.Label(p, foundCount == 0 ? "Toca cada rectángulo resaltado en el mensaje para descubrir por qué es peligroso." : "", 10, 192, 166, 140, 12, Ink);
            explain.enableAutoSizing = true; explain.fontSizeMin = 9; explain.fontSizeMax = 13;
            for (int i = 2; i >= 0; i--) if (found[i]) { explain.text = "<b>" + S.signalNames[i] + ":</b> " + S.signalText[i]; break; }
        }
        else timerNav = null;
    }

    void RefreshList()
    {
        if (!mailList) return;
        foreach (Transform c in mailList) Destroy(c.gameObject);
        int row = 0;
        foreach (var m in mails)
        {
            if (m.folder != folder) continue;
            var mm = m;
            var b = UIKit.Btn(mailList, "", 0, row * 80, 300, 78, m.read ? Gray : Color.white, Ink, 10, () => ShowMail(mm));
            if (m.phishing && m.folder == "quarantine") UIKit.Panel(b.transform, "Warn", 0, 0, 5, 78, Amber, false);
            else if (!m.read) UIKit.Panel(b.transform, "New", 0, 0, 4, 78, Accent, false);
            NoWrap(UIKit.Label(b.transform, m.from, 14, 8, 210, 22, 16, Ink, TextAlignmentOptions.TopLeft, !m.read));
            UIKit.Label(b.transform, m.time, 222, 8, 66, 22, 13, Soft, TextAlignmentOptions.TopRight);
            NoWrap(UIKit.Label(b.transform, m.subject, 14, 32, 276, 22, 14, m.read ? Soft : Ink, TextAlignmentOptions.TopLeft, !m.read));
            NoWrap(UIKit.Label(b.transform, m.phishing && m.folder == "quarantine" ? "Retenido por el filtro de seguridad" : Preview(m), 14, 54, 276, 20, 12,
                m.phishing && m.folder == "quarantine" ? new Color(0.75f, 0.38f, 0f) : Soft));
            UIKit.Panel(b.transform, "Line", 0, 77, 300, 1, LineC, false);
            row++;
        }
        if (row == 0) UIKit.Label(mailList, "No hay mensajes en esta carpeta.", 20, 24, 260, 40, 15, Soft);
    }

    static string Preview(Mail m)
    {
        if (m.phishing) return S.preview;
        string s = m.body.Replace("\n", " ");
        return s.Length > 40 ? s.Substring(0, 40) + "..." : s;
    }

    void ShowMail(Mail m)
    {
        if (!mailPane) { OpenMail(); if (!mailPane) return; }
        if (m.folder != folder) { folder = m.folder; RefreshNav(); }
        m.read = true;
        foreach (Transform c in mailPane) Destroy(c.gameObject);
        for (int i = 0; i < 3; i++) hl[i] = null;
        reportBtn = null; timerBanner = null;
        if (m.phishing) ShowPhish(m);
        else
        {
            UIKit.Label(mailPane, m.subject, 24, 16, 510, 60, 24, Ink, TextAlignmentOptions.TopLeft, true);
            UIKit.Label(mailPane, $"{m.from}  <{m.addr}>", 24, 80, 510, 24, 15, Soft);
            UIKit.Label(mailPane, "Para: juan.perez@bancoandino.com  ·  " + m.time, 24, 104, 510, 20, 13, Soft);
            UIKit.Panel(mailPane, "Line", 24, 130, 512, 1, LineC, false);
            UIKit.Label(mailPane, m.body, 24, 144, 510, 360, 17, Ink);
        }
        RefreshList(); UpdateMailBadge();
    }

    void ShowPhish(Mail m)
    {
        bool quarantine = m.folder == "quarantine";
        float oy = 0f;
        if (quarantine)
        {
            var ban = UIKit.Panel(mailPane, "Banner", 0, 0, 560, 46, new Color(1f, 0.93f, 0.8f), false);
            UIKit.Panel(ban, "Bar", 0, 0, 5, 46, Amber, false);
            UIKit.Label(ban, "AISLADO POR EL FILTRO DE SEGURIDAD", 16, 6, 340, 20, 14, new Color(0.65f, 0.33f, 0f), TextAlignmentOptions.TopLeft, true);
            UIKit.Label(ban, "Enlaces y adjuntos deshabilitados", 16, 25, 340, 18, 12, new Color(0.55f, 0.35f, 0.1f));
            timerBanner = UIKit.Label(ban, "", 330, 13, 216, 24, 16, Ink, TextAlignmentOptions.TopRight, true);
            oy = 50f;
            if (!inspecting && !Finished) StartInspection();
        }
        var s = S;
        float y = 12f + oy;
        UIKit.Label(mailPane, m.subject, 24, y, 510, 56, 22, Ink, TextAlignmentOptions.TopLeft, true); y += 58f;
        float senderY = y;
        UIKit.Label(mailPane, $"<b>{m.from}</b>  <{m.addr}>", 24, y, 510, 24, 15, Ink).overflowMode = TextOverflowModes.Ellipsis; y += 26f;
        UIKit.Label(mailPane, "Para: juan.perez@bancoandino.com  ·  Hoy " + m.time, 24, y, 510, 20, 13, Soft); y += 24f;
        UIKit.Panel(mailPane, "Line", 24, y, 512, 1, LineC, false); y += 9f;
        UIKit.Label(mailPane, s.greeting, 24, y, 510, 22, 15, Ink); y += 24f;
        UIKit.Label(mailPane, s.intro, 24, y, 510, 42, 15, Ink); y += 44f;

        float urgY = 0f, elemY = 0f, elemH = 0f;
        void Urgency()
        {
            urgY = y;
            UIKit.Label(mailPane, "<b>" + s.urgency + "</b>", 24, y + 2, 510, 46, 15, new Color(0.6f, 0.08f, 0.08f)); y += 54f;
        }
        void Element()
        {
            UIKit.Label(mailPane, s.beforeElement, 24, y, 510, 22, 14, Ink); y += 24f;
            elemY = y - 2f;
            float h = ElementBlock(s, y, quarantine);
            y += h + 4f;
            if (quarantine)
            {
                UIKit.Label(mailPane, "<b>" + s.scanVerdict + "</b>", 24, y, 510, 20, 13, new Color(0.78f, 0.16f, 0.14f)); y += 22f;
            }
            elemH = y - elemY;
            y += 4f;
        }
        if (s.elementBeforeUrgency) { Element(); Urgency(); } else { Urgency(); Element(); }
        UIKit.Label(mailPane, s.signature, 24, y, 510, 40, 14, Ink);

        // Barra inferior
        UIKit.Panel(mailPane, "BarLine", 0, 526, 560, 1, LineC, false);
        // Reportar a la izquierda: las notificaciones aparecen abajo a la derecha y lo taparían
        UIKit.Btn(mailPane, "Responder", quarantine ? 300 : 240, 536, 120, 36, Gray, Ink, 15, () => Toast("Correo", "Función no disponible en la simulación.", Soft, null));
        if (quarantine)
        {
            reportBtn = UIKit.Btn(mailPane, "", 24, 536, 260, 36, new Color(0.8f, 0.82f, 0.85f), Color.white, 10, () => { if (foundCount >= 3) Report(); });
            reportLbl = UIKit.Label(reportBtn.transform, "", 0, 0, 260, 36, 15, Color.white, TextAlignmentOptions.Center, true);
            UpdateReportButton();
            // Los 3 rectángulos sobresalientes (RF-03)
            MakeHighlight(0, 14, senderY - 4f, 532, 32);
            MakeHighlight(1, 14, urgY - 2f, 532, 54);
            MakeHighlight(2, 14, elemY - 2f, 532, elemH + 2f);
        }
        else
        {
            var rb = UIKit.Btn(mailPane, "Reportar phishing", 24, 536, 200, 36, new Color(0.9f, 0.93f, 0.97f), Ink, 15, () => Report());
            rb.GetComponentInChildren<TextMeshProUGUI>().fontStyle = FontStyles.Bold;
        }
    }

    // Dibuja el elemento malicioso del correo y devuelve su alto
    float ElementBlock(PhishScenario s, float y, bool quarantine)
    {
        switch (s.element)
        {
            case PhishElement.Attachment:
            {
                float h = 0f;
                if (!string.IsNullOrEmpty(s.linkText))
                {
                    var link = UIKit.Btn(mailPane, "", 20, y, 520, 24, Color.white, Accent, 10, () => ElementClicked(true));
                    UIKit.Label(link.transform, "<u>" + s.linkText + "</u>", 4, 2, 512, 20, 14, Accent).overflowMode = TextOverflowModes.Ellipsis;
                    h += 28f;
                }
                var att = UIKit.Panel(mailPane, "Attachment", 24, y + h, 512, 54, quarantine ? new Color(1f, 0.95f, 0.95f) : Gray, false);
                string glyph = s.fileName.EndsWith(".xlsx") ? "excel" : (s.fileName.EndsWith(".zip") ? "docs" : "lockless");
                if (glyph == "lockless") { var t = UIKit.Panel(att, "Doc", 12, 9, 30, 36, Color.white, false); UIKit.Panel(t, "Band", 0, 0, 30, 9, new Color(0.8f, 0.2f, 0.2f), false); }
                else AppGlyph(att, glyph, 10, 9, 36);
                UIKit.Label(att, s.fileName, 56, 7, 320, 22, 15, Ink, TextAlignmentOptions.TopLeft, true).overflowMode = TextOverflowModes.Ellipsis;
                UIKit.Label(att, s.fileInfo, 56, 29, 320, 20, 12, Soft).overflowMode = TextOverflowModes.Ellipsis;
                if (quarantine) UIKit.Btn(att, "Bloqueado", 392, 10, 110, 34, new Color(0.85f, 0.86f, 0.88f), Soft, 15, () => ElementClicked(false));
                else UIKit.Btn(att, "Descargar", 392, 10, 110, 34, Accent, Color.white, 15, () => ElementClicked(false));
                return h + 54f;
            }
            case PhishElement.Link:
            {
                var link = UIKit.Btn(mailPane, "", 20, y, 520, 24, Color.white, Accent, 10, () => ElementClicked(true));
                UIKit.Label(link.transform, "<u>" + s.linkText + "</u>", 4, 2, 512, 20, 14, Accent).overflowMode = TextOverflowModes.Ellipsis;
                return 26f;
            }
            case PhishElement.Button:
            {
                var b = UIKit.Btn(mailPane, s.linkText, 24, y, 300, 42, quarantine ? new Color(0.72f, 0.75f, 0.8f) : Accent, Color.white, 17, () => ElementClicked(true));
                b.GetComponentInChildren<TextMeshProUGUI>().fontStyle = FontStyles.Bold;
                return 42f;
            }
            case PhishElement.Image:
            {
                var img = UIKit.Btn(mailPane, "", 24, y, 330, 96, Color.white, Ink, 10, () => ElementClicked(false));
                var card = UIKit.Panel(img.transform, "Receipt", 0, 0, 330, 96, new Color(0.97f, 0.98f, 0.97f), false);
                UIKit.Panel(card, "Head", 0, 0, 330, 24, new Color(0.1f, 0.55f, 0.32f), false);
                UIKit.Label(card, "TRANSFERENCIA EXITOSA", 10, 4, 310, 18, 13, Color.white, TextAlignmentOptions.TopLeft, true);
                UIKit.Label(card, "Operación N.º 0921  ·  S/ 18 450,00\nBeneficiario: BANCO ANDINO S.A.\nFecha: hoy  ·  Estado: PENDIENTE DE CONFIRMAR", 10, 30, 310, 62, 12, Ink);
                if (quarantine)
                {
                    var blk = UIKit.Panel(card, "Blocked", 0, 0, 330, 96, new Color(0.15f, 0.15f, 0.18f, 0.82f), false);
                    UIKit.Label(blk, "IMAGEN BLOQUEADA\nenlaza a un archivo .jpg.html", 0, 26, 330, 50, 14, Color.white, TextAlignmentOptions.Center, true);
                }
                return 96f;
            }
            case PhishElement.QR:
            {
                var b = UIKit.Btn(mailPane, "", 24, y, 96, 96, Color.white, Ink, 10, () => ElementClicked(true));
                var qr = UIKit.Panel(b.transform, "QR", 4, 4, 88, 88, Color.white, false);
                qr.gameObject.GetComponent<Image>().sprite = QrSprite();
                UIKit.Label(mailPane, quarantine ? "Lector de QR del filtro:\n<color=#C62828><b>" + s.RealHost + "</b></color>" : "Validación obligatoria\nde doble factor (MFA)", 136, y + 24, 380, 50, 14, Ink);
                return 96f;
            }
        }
        return 0f;
    }

    // Código QR decorativo (patrón aleatorio con los 3 cuadros de posición)
    static Sprite qrSprite;
    static Sprite QrSprite()
    {
        if (qrSprite) return qrSprite;
        const int n = 25;
        var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        var rnd = new System.Random(2026);
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
                t.SetPixel(x, y, rnd.Next(0, 2) == 0 ? Color.black : Color.white);
        void Finder(int ox, int oy)
        {
            for (int y = -1; y < 8; y++)
                for (int x = -1; x < 8; x++)
                {
                    int px = ox + x, py = oy + y;
                    if (px < 0 || py < 0 || px >= n || py >= n) continue;
                    bool ring = x == 0 || y == 0 || x == 6 || y == 6, core = x >= 2 && x <= 4 && y >= 2 && y <= 4;
                    bool inside = x >= 0 && y >= 0 && x <= 6 && y <= 6;
                    t.SetPixel(px, py, inside && (ring || core) ? Color.black : Color.white);
                }
        }
        Finder(0, 0); Finder(n - 7, 0); Finder(0, n - 7);
        t.Apply();
        return qrSprite = Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
    }

    void MakeHighlight(int i, float x, float y, float w, float h)
    {
        var f = Frame(mailPane, "Signal" + i, x, y, w, h, found[i] ? Green : Amber, 3f, found[i] ? new Color(0.2f, 0.8f, 0.4f, 0.06f) : new Color(1f, 0.6f, 0.1f, 0.06f));
        var tag = UIKit.Panel(f, "Tag", w - 28, -12, 26, 26, found[i] ? Green : Amber, false);
        tag.GetComponent<Image>().sprite = UIKit.Circle();
        UIKit.Label(tag, (i + 1).ToString(), 0, 0, 26, 26, 15, Color.white, TextAlignmentOptions.Center, true);
        var b = f.gameObject.AddComponent<Button>(); b.transition = Selectable.Transition.None;
        int idx = i; b.onClick.AddListener(() => FoundSignal(idx));
        hl[i] = f;
    }

    void UpdateReportButton()
    {
        if (!reportBtn) return;
        bool ready = foundCount >= 3 && !Finished;
        reportBtn.GetComponent<Image>().color = ready ? Green : new Color(0.78f, 0.8f, 0.84f);
        reportLbl.text = ready ? "REPORTAR PHISHING" : $"Reportar phishing  ({foundCount}/3 señales)";
    }

    // ============================================== INSPECCIÓN (MODO DEFENSA)
    void StartInspection()
    {
        inspecting = true; inspectStart = Time.time;
        SimSession.TimerStart = Time.time;
        RefreshNav();
        Toast(IsFile(S) ? "Sandbox" : (S.element == PhishElement.QR ? "Lector de QR" : "Análisis de enlaces"), S.scanToast, Red, null, 9f);
        OnInspectionStarted?.Invoke();
    }

    void FoundSignal(int i)
    {
        if (found[i] || Finished || !inspecting) return;
        found[i] = true; foundCount++;
        SimSession.SignalsFound = foundCount;
        if (hl[i])
        {
            foreach (var im in hl[i].GetComponentsInChildren<Image>())
            {
                if (im.name == "Edge") im.color = Green;
                else if (im.name == "Tag") im.color = Green;
            }
            var fillIm = hl[i].GetComponent<Image>();
            fillIm.color = new Color(0.2f, 0.8f, 0.4f, 0.08f);
            fillIm.raycastTarget = false;   // deja pulsar el enlace / adjunto que hay debajo
        }
        RefreshNav();
        UpdateReportButton();
        if (foundCount == 1)
            Toast("Doble factor (MFA)", $"Se bloqueó un intento de inicio de sesión en tu cuenta desde {AttackerIP}. Sin tu segundo factor, nadie puede entrar.", Green, null, 9f);
        else if (foundCount == 2)
            Toast("Gestor de Contraseñas", (string.IsNullOrEmpty(S.RealHost) ? "No completará tus claves en sitios falsos" : "No completará tus claves en «" + S.RealHost + "»") +
                ": solo autocompleta en dominios verificados de bancoandino.com.", Green, () => OpenBrowser(2), 9f);
        OnSignalFound?.Invoke(foundCount);
        if (foundCount >= 3) OnAllSignalsFound?.Invoke();
    }

    void Report()
    {
        if (Finished) return;
        Finished = true; inspecting = false;
        SimSession.Reported = true; SimSession.Finish();
        if (phishMail != null) phishMail.folder = "trash";
        foreach (var id in new List<string>(wins.Keys)) CloseWin(id);
        var p = UIKit.Panel(popupLayer, "Reported", 340, 230, 600, 220, Color.white);
        UIKit.Panel(p, "Top", 0, 0, 600, 6, Green, false);
        UIKit.Label(p, "Correo reportado al equipo de Seguridad", 0, 40, 600, 36, 26, Ink, TextAlignmentOptions.Center, true);
        UIKit.Label(p, Mode == SimMode.Defense ? "El mensaje fue eliminado de la cuarentena. Amenaza contenida." : "Gracias por mantener la red segura.", 0, 90, 600, 60, 19, Soft, TextAlignmentOptions.Center);
        UIKit.Label(p, "Tiempo: " + SimSession.Format(SimSession.Elapsed), 0, 150, 600, 30, 20, Green, TextAlignmentOptions.Center, true);
        Toast("Seguridad", "Reporte recibido. Ticket #SOC-4040 abierto.", Green, null);
        OnReported?.Invoke();
    }

    void TimeUp()
    {
        if (Finished) return;
        Finished = true; inspecting = false;
        SimSession.TimedOut = true; SimSession.Finish();
        foreach (var id in new List<string>(wins.Keys)) CloseWin(id);
        var p = UIKit.Panel(popupLayer, "TimeUp", 340, 230, 600, 220, Color.white);
        UIKit.Panel(p, "Top", 0, 0, 600, 6, Amber, false);
        UIKit.Label(p, "Tiempo agotado", 0, 40, 600, 36, 28, Ink, TextAlignmentOptions.Center, true);
        UIKit.Label(p, $"El filtro mantuvo el correo aislado, pero solo encontraste {foundCount} de 3 señales en 3 minutos.", 30, 90, 540, 80, 19, Soft, TextAlignmentOptions.Center);
        OnTimeUp?.Invoke();
    }

    /// Clic sobre el elemento malicioso. asLink = enlace / botón / QR (web falsa); si no, archivo descargable.
    void ElementClicked(bool asLink)
    {
        if (Finished) return;
        if (Mode == SimMode.Defense)
        {
            if (asLink) OpenBrowser(3);
            else Toast("Sandbox", "Descarga bloqueada: «" + S.fileName + "» es malicioso. El archivo se mantiene aislado.", Red, null, 8f);
            return;
        }
        StartCoroutine(Breach(asLink));
    }

    // ============================================================ NAVEGADOR
    // tab: 0 intranet, 1 noticias, 2 gestor de contraseñas, 3 enlace del correo (bloqueado en Defensa), 4 página falsa (Ataque)
    TextMeshProUGUI urlTxt; RectTransform browserPage; int browserTab;

    void OpenBrowser(int tab)
    {
        if (!loggedIn) return;
        var body = OpenWindow("browser", "Navegador Web", "browser", 170, 40, 940, 590);
        if (body != null)
        {
            var bar = UIKit.Panel(body, "Bar", 0, 0, 940, 88, new Color(0.93f, 0.94f, 0.96f), false);
            string[] names = { "Intranet", "Noticias", "Gestor de Contraseñas" };
            for (int i = 0; i < names.Length; i++) { int k = i; UIKit.Btn(bar, names[i], 10 + i * 210, 6, 200, 34, Color.white, Ink, 15, () => ShowTab(k)); }
            var addr = UIKit.Panel(bar, "Addr", 10, 48, 920, 32, Color.white, false);
            urlTxt = UIKit.Label(addr, "", 14, 6, 890, 22, 15, Ink);
            browserPage = UIKit.Panel(body, "Page", 0, 88, 940, 468, Color.white, false);
        }
        ShowTab(tab);
    }

    void ShowTab(int tab)
    {
        if (!browserPage) return;
        browserTab = tab;
        foreach (Transform c in browserPage) Destroy(c.gameObject);
        bool def = Mode == SimMode.Defense;
        switch (tab)
        {
            case 0:
                urlTxt.text = "<color=#1E8E3E>[candado]</color>  https://intranet.bancoandino.com";
                Page("INTRANET · Banco Andino", "Avisos de hoy:\n\n- Recuerda: TI y RR.HH. nunca te pedirán tu contraseña por correo.\n- Capacitación de seguridad de la información: jueves 10:00, Sala 2.\n- Ante un correo sospechoso usa el botón «Reportar phishing».\n- El portal oficial de nómina es: https://nomina.bancoandino.com", NavyC);
                break;
            case 1:
                urlTxt.text = "<color=#1E8E3E>[candado]</color>  https://noticias.bancoandino.com";
                Page("NOTICIAS INTERNAS", "Aumentan los intentos de phishing que suplantan a Recursos Humanos con supuestos «bonos» y «ajustes de nómina».\n\nLos atacantes registran dominios casi idénticos al nuestro y adjuntan hojas de cálculo con macros. Verifica siempre el remitente antes de abrir un adjunto.", NavyC);
                break;
            case 2:
                urlTxt.text = "<color=#1E8E3E>[candado]</color>  https://claves.bancoandino.com";
                if (def) Page("GESTOR DE CONTRASEÑAS · Activo", "Bóveda protegida con doble factor (MFA).\n\n- 12 credenciales guardadas y cifradas.\n- Autocompletado SOLO en dominios verificados: *.bancoandino.com\n- En sitios falsos o con dominios parecidos al nuestro, el gestor no escribe ninguna clave.\n\nRecomendación: guarda aquí la clave de tu nota adhesiva y destruye el papel.", Green);
                else Page("GESTOR DE CONTRASEÑAS · No instalado", "Este equipo no tiene un gestor de contraseñas configurado.\n\nLas claves se escriben a mano en cualquier sitio, incluso en páginas falsas, y no hay doble factor (MFA) que bloquee un acceso con una clave robada.", Red);
                break;
            case 3:
                urlTxt.text = "<color=#C62828>[no seguro]</color>  " + S.realUrl;
                Page("SITIO BLOQUEADO POR LA POLÍTICA DE SEGURIDAD", "El enlace del correo en cuarentena apunta a «" + S.RealHost + "», un dominio que no pertenece a Banco Andino y que fue reportado por suplantación.\n\nGestor de Contraseñas: no se autocompletará ninguna credencial en este sitio.\nMFA: aunque alguien obtuviera tu clave, no podría entrar sin tu segundo factor.", Red);
                break;
        }
    }

    void Page(string title, string text, Color accent)
    {
        UIKit.Panel(browserPage, "Head", 0, 0, 940, 70, accent, false);
        UIKit.Label(browserPage, title, 30, 18, 880, 40, 26, Color.white, TextAlignmentOptions.TopLeft, true);
        UIKit.Label(browserPage, text, 30, 96, 880, 360, 19, Ink);
    }

    // ================================================= DOCUMENTOS / EXCEL / PAPELERA
    void OpenDocs()
    {
        if (!loggedIn) return;
        var body = OpenWindow("docs", "Mis Documentos", "docs", 220, 70, 820, 500);
        if (body == null) return;
        UIKit.Panel(body, "Side", 0, 0, 180, 466, Gray, false);
        UIKit.Label(body, "Acceso rápido\n\nEscritorio\nDescargas\nDocumentos\nReportes", 20, 16, 150, 200, 15, Ink);
        var view = UIKit.Label(body, "Selecciona un archivo para ver su descripción.", 500, 20, 300, 420, 15, Soft);
        string[] files = { "Presupuesto_2026.pdf", "Horarios_Turno.docx", "Informe_Ventas_Q3.xlsx", "Politica_Seguridad.pdf", "Manual_Atencion_Cliente.docx" };
        string[] info =
        {
            "Presupuesto_2026.pdf\nPDF · 1,2 MB\n\nPresupuesto anual del área administrativa (datos ficticios).",
            "Horarios_Turno.docx\nWord · 86 KB\n\nTurnos de la semana para ventanillas y plataforma.",
            "Informe_Ventas_Q3.xlsx\nExcel · 240 KB\n\nColocaciones de créditos del tercer trimestre.",
            "Politica_Seguridad.pdf\nPDF · 410 KB\n\n1. No abrir adjuntos inesperados.\n2. No anotar contraseñas en papel.\n3. Reportar correos sospechosos al SOC (anexo 4040).\n4. Bloquear la PC al ausentarse.",
            "Manual_Atencion_Cliente.docx\nWord · 1,8 MB\n\nProtocolos de atención en plataforma y cajas.",
        };
        for (int i = 0; i < files.Length; i++)
        {
            int k = i;
            var b = UIKit.Btn(body, "", 190, 14 + i * 50, 300, 46, Color.white, Ink, 10, () => view.text = info[k]);
            AppGlyph(b.transform, files[i].EndsWith(".xlsx") ? "excel" : "docs", 8, 9, 28);
            UIKit.Label(b.transform, files[i], 46, 12, 250, 24, 15, Ink);
        }
    }

    void OpenExcel()
    {
        if (!loggedIn) return;
        var body = OpenWindow("excel", "Excel — Horarios_Turno.xlsx", "excel", 160, 60, 900, 520);
        if (body == null) return;
        UIKit.Panel(body, "Ribbon", 0, 0, 900, 40, new Color(0.08f, 0.5f, 0.27f), false);
        UIKit.Label(body, "Archivo    Inicio    Insertar    Fórmulas    Datos    Revisar", 16, 10, 860, 24, 15, Color.white);
        string[] cols = { "", "A", "B", "C", "D", "E", "F" };
        string[,] data =
        {
            { "Colaborador", "Lun", "Mar", "Mié", "Jue", "Vie" },
            { "J. Pérez", "08-16", "08-16", "08-16", "10-18", "08-16" },
            { "M. Díaz", "10-18", "10-18", "08-16", "08-16", "10-18" },
            { "L. Vega", "08-16", "Libre", "10-18", "10-18", "08-16" },
            { "A. Rojas", "Libre", "08-16", "08-16", "08-16", "10-18" },
            { "C. Torres", "10-18", "10-18", "Libre", "08-16", "08-16" },
        };
        for (int c = 0; c < 7; c++)
        {
            float x = c == 0 ? 0 : 40 + (c - 1) * 140;
            UIKit.Panel(body, "H", x, 44, c == 0 ? 40 : 139, 26, new Color(0.92f, 0.93f, 0.95f), false);
            UIKit.Label(body, cols[c], x, 48, c == 0 ? 40 : 139, 22, 14, Soft, TextAlignmentOptions.Center);
        }
        for (int r = 0; r < 12; r++)
        {
            float y = 72 + r * 30;
            UIKit.Label(body, (r + 1).ToString(), 0, y + 5, 40, 22, 13, Soft, TextAlignmentOptions.Center);
            for (int c = 0; c < 6; c++)
            {
                UIKit.Panel(body, "Cell", 40 + c * 140, y + 29, 140, 1, LineC, false);
                if (r < 6) UIKit.Label(body, data[r, c], 48 + c * 140, y + 5, 130, 22, 14, Ink, TextAlignmentOptions.TopLeft, r == 0);
            }
        }
    }

    void OpenBin()
    {
        if (!loggedIn) return;
        var body = OpenWindow("bin", "Papelera de reciclaje", "bin", 380, 160, 520, 300, false);
        if (body == null) return;
        AppGlyph(body, "bin", 220, 50, 80);
        UIKit.Label(body, "La papelera está vacía.", 0, 150, 520, 30, 18, Soft, TextAlignmentOptions.Center);
    }

    // ===================================================== INFECCIÓN (MODO ATAQUE)
    /// viaWeb = el usuario abrió la web falsa (enlace / botón / QR) y escribió su clave; si no, abrió un archivo malicioso.
    IEnumerator Breach(bool viaWeb)
    {
        if (Infected || Finished) yield break;
        Infected = true; Finished = true;
        var s = S;
        SimSession.Infected = true; SimSession.CredentialTheft = viaWeb;
        CloseStart();

        if (viaWeb)
        {
            if (s.element == PhishElement.QR)
            {
                Toast("Celular", "Escaneaste el código QR: se abrió «" + s.RealHost + "» y la misma página se abre en tu PC para continuar.", Soft, null, 5f);
                yield return new WaitForSeconds(1.2f);
            }
            // Página falsa: el usuario escribe la clave de su post-it
            OpenBrowser(0);
            urlTxt.text = "<color=#C62828>[no seguro]</color>  " + s.realUrl;
            foreach (Transform c in browserPage) Destroy(c.gameObject);
            UIKit.Panel(browserPage, "Head", 0, 0, 940, 70, NavyC, false);
            UIKit.Label(browserPage, s.fakeSite, 30, 18, 880, 40, 26, Color.white, TextAlignmentOptions.TopLeft, true);
            UIKit.Label(browserPage, s.fakePrompt, 230, 100, 480, 50, 18, Ink, TextAlignmentOptions.Center);
            UIKit.Label(browserPage, "Usuario", 280, 160, 380, 22, 15, Soft);
            var u = UIKit.Panel(browserPage, "User", 280, 184, 380, 40, Gray, false);
            UIKit.Label(u, "juan.perez", 12, 9, 350, 24, 17, Ink);
            UIKit.Label(browserPage, "Contraseña", 280, 236, 380, 22, 15, Soft);
            var pw = UIKit.Panel(browserPage, "Pass", 280, 260, 380, 40, Gray, false);
            var pwt = UIKit.Label(pw, "", 12, 9, 350, 24, 17, Ink);
            var go = UIKit.Panel(browserPage, "Go", 280, 320, 380, 46, Accent, false);
            var got = UIKit.Label(go, "Confirmar datos", 0, 11, 380, 26, 18, Color.white, TextAlignmentOptions.Center, true);
            yield return new WaitForSeconds(0.8f);
            var pf = Frame(desktop, "PostItRead", W - 268, 20, 232, 184, Red, 4f, new Color(1f, 0f, 0f, 0.05f));
            pf.localEulerAngles = new Vector3(0, 0, -3f);
            for (int i = 0; i <= NotePassword.Length; i++) { pwt.text = new string('*', i); yield return new WaitForSeconds(0.12f); }
            yield return new WaitForSeconds(0.4f);
            got.text = "Verificando...";
            yield return new WaitForSeconds(1.2f);
            Toast("Inicio de sesión", "Nuevo inicio de sesión en tu cuenta desde " + AttackerIP + " (ubicación desconocida).", Red, null, 6f);
            yield return new WaitForSeconds(1.0f);
        }
        else
        {
            // Descarga y apertura del archivo malicioso
            var dl = UIKit.Panel(popupLayer, "Download", 340, 250, 600, 170, new Color(0.96f, 0.96f, 0.97f));
            UIKit.Label(dl, "Descargando " + s.fileName, 24, 18, 560, 26, 19, Ink, TextAlignmentOptions.TopLeft, true);
            var track = UIKit.Panel(dl, "Track", 24, 66, 552, 22, new Color(0.8f, 0.82f, 0.85f), false);
            var fill = UIKit.Panel(track, "Fill", 0, 0, 0, 22, new Color(0.08f, 0.5f, 0.27f), false);
            var st = UIKit.Label(dl, "0%", 24, 100, 552, 26, 17, Soft, TextAlignmentOptions.Center);
            for (float t = 0; t < 1.8f; t += Time.deltaTime)
            {
                float k = t / 1.8f; fill.sizeDelta = new Vector2(552 * k, 22); st.text = Mathf.RoundToInt(k * 100) + "%";
                yield return null;
            }
            st.text = s.openingText;
            yield return new WaitForSeconds(1.0f);
            Destroy(dl.gameObject);
        }

        // Ventanas emergentes de error del sistema
        string[] errs =
        {
            viaWeb ? "Se instaló un componente remoto sin tu autorización.\nEl atacante entró con tu contraseña." : "El programa dejó de funcionar.\nSe cerró de forma inesperada.",
            "Error 0x80070570: el archivo o directorio está dañado y no se puede leer.",
            "Acceso denegado: un proceso desconocido está modificando tus archivos.",
        };
        for (int i = 0; i < errs.Length; i++)
        {
            var e = UIKit.Panel(popupLayer, "Error", 300 + i * 70, 160 + i * 70, 460, 170, Color.white);
            UIKit.Panel(e, "Title", 0, 0, 460, 32, new Color(0.95f, 0.96f, 0.98f), false);
            UIKit.Label(e, "Error del sistema", 12, 6, 400, 22, 15, Ink);
            var x = UIKit.Panel(e, "Ico", 20, 54, 44, 44, Red, false); x.GetComponent<Image>().sprite = UIKit.Circle();
            UIKit.Label(x, "X", 0, 0, 44, 44, 24, Color.white, TextAlignmentOptions.Center, true);
            UIKit.Label(e, errs[i], 80, 50, 360, 70, 15, Ink);
            UIKit.Btn(e, "Aceptar", 340, 124, 100, 32, Gray, Ink, 15, null);
            yield return new WaitForSeconds(0.45f);
        }

        OnMalwareExecuted?.Invoke();
        SimSession.Finish();

        // Los iconos del escritorio se cifran uno a uno
        foreach (var ic in icons)
        {
            foreach (Transform c in ic.root) Destroy(c.gameObject);
            AppGlyph(ic.root, "lock", 30, 6, 60);
            ic.label.text = ic.name.Replace(" ", "_") + ".locked";
            ic.label.color = new Color(1f, 0.55f, 0.5f);
            yield return new WaitForSeconds(0.22f);
        }
        var stamp = UIKit.Panel(postIt, "Stamp", 10, 60, 200, 46, new Color(0.85f, 0.1f, 0.1f, 0.92f), false);
        stamp.localEulerAngles = new Vector3(0, 0, 12f);
        UIKit.Label(stamp, viaWeb ? "CLAVE ROBADA" : "LEÍDA POR EL MALWARE", 0, 10, 200, 28, 15, Color.white, TextAlignmentOptions.Center, true);
        yield return new WaitForSeconds(0.8f);

        // Fallo general
        var gl = UIKit.Panel(root, "Glitch", 0, 0, W, H, Color.black, false);
        var gi = gl.GetComponent<Image>();
        Color[] gc = { Color.black, new Color(0.8f, 0, 0), Color.white, new Color(0.2f, 0, 0) };
        for (int i = 0; i < 14; i++)
        {
            gi.color = gc[UnityEngine.Random.Range(0, gc.Length)];
            gl.anchoredPosition = new Vector2(UnityEngine.Random.Range(-20f, 20f), UnityEngine.Random.Range(-10f, 10f));
            yield return new WaitForSeconds(0.07f);
        }
        Destroy(gl.gameObject);
        foreach (Transform c in popupLayer) Destroy(c.gameObject);
        yield return MalwareScreen();
    }

    IEnumerator MalwareScreen()
    {
        var m = UIKit.Panel(root, "Malware", 0, 0, W, H, new Color(0.1f, 0, 0));
        var warn = UIKit.Label(m, "SU SISTEMA HA SIDO COMPROMETIDO", 0, 24, W, 70, 50, new Color(1f, 0.15f, 0.15f), TextAlignmentOptions.Center, true);
        var term = UIKit.Label(m, "", 60, 110, 1160, 290, 22, new Color(1f, 0.5f, 0.45f));
        var stats = UIKit.Label(m, "", 60, 420, 700, 200, 26, Color.white);
        var timer = UIKit.Label(m, "", 740, 420, 480, 200, 40, new Color(1f, 0.2f, 0.2f), TextAlignmentOptions.Center, true);
        UIKit.Label(m, "SIMULACIÓN EDUCATIVA - ESTO NO ES UN ATAQUE REAL", 0, 676, W, 30, 20, new Color(1, 1, 1, 0.6f), TextAlignmentOptions.Center);

        var s = S;
        string[] lines = SimSession.CredentialTheft
            ? new[]
            {
                "> Credenciales capturadas en " + s.RealHost + ": juan.perez / " + NotePassword,
                "> Inicio de sesión remoto desde " + AttackerIP + " (sin MFA) ...... OK",
                "> Instalando acceso remoto en la estación TI-01 ............ OK",
                "> Robando correos y archivos compartidos .................... OK",
                "> Propagando a los equipos de la red del banco .............. EN CURSO",
            }
            : new[]
            {
                "> Código ejecutado desde " + s.fileName + " ............ OK",
                "> Conexión remota establecida desde " + AttackerIP,
                "> Leyendo nota adhesiva del escritorio: \"" + NotePassword + "\" ..... OK",
                "> Inicio de sesión como juan.perez (sin MFA) ............ OK",
                "> Propagando a los equipos de la red del banco .......... EN CURSO",
            };
        foreach (var l in lines) { term.text += l + "\n"; yield return new WaitForSeconds(0.6f); }

        float files = 0, recs = 0, pcs = 0, sec = 72f * 3600f;
        while (true)
        {
            files = Mathf.MoveTowards(files, 48213f, Time.deltaTime * 5000f);
            recs = Mathf.MoveTowards(recs, 1250000f, Time.deltaTime * 90000f);
            pcs = Mathf.MoveTowards(pcs, 10f, Time.deltaTime * 0.8f);
            stats.text = $"Archivos cifrados: {files:N0}\nRegistros de clientes robados: {recs:N0}\nEquipos infectados: {pcs:0} / 10";
            sec -= Time.deltaTime * 900f;
            int secs = Mathf.Max(0, (int)sec);
            timer.text = $"DATOS CIFRADOS\nTiempo restante\n{secs / 3600:00}:{(secs / 60) % 60:00}:{secs % 60:00}";
            warn.color = Color.Lerp(new Color(0.6f, 0, 0), new Color(1f, 0.2f, 0.2f), 0.5f + 0.5f * Mathf.Sin(Time.time * 6f));
            yield return null;
        }
    }
}
