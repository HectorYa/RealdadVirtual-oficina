using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// Escritorio simulado en el monitor principal: correo, navegador (simulado), documentos y la simulación de malware.
/// TODO el malware es FICTICIO: solo animaciones de interfaz.
public class MonitorUI : MonoBehaviour
{
    public float screenWidth = 0.8f;                  // ancho del monitor en metros
    public event Action OnMalwareExecuted, OnReported;
    public bool Infected { get; private set; }

    const float W = 1280f, H = 720f;
    static readonly Color Navy = new Color(0.06f, 0.12f, 0.22f);
    static readonly Color BarC = new Color(0.04f, 0.07f, 0.12f, 0.96f);
    static readonly Color WinC = new Color(0.96f, 0.97f, 0.98f);
    static readonly Color TitleC = new Color(0.12f, 0.25f, 0.45f);
    static readonly Color Ink = new Color(0.1f, 0.12f, 0.16f);
    static readonly Color GrayC = new Color(0.88f, 0.9f, 0.93f);
    static readonly Color Soft = new Color(0.4f, 0.45f, 0.52f);

    class Mail { public string from, subject, body; public bool read, phishing; }

    Canvas canvas;
    RectTransform root, appWin, mailList, mailPane;
    TextMeshProUGUI clock, badge, pageLabel;
    GameObject toast, lockScreen;
    readonly List<Mail> mails = new List<Mail>();
    Mail phishMail;
    bool active;
    float t0;

    void Awake()
    {
        t0 = Time.time;
        var g = new GameObject("MonitorCanvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
        g.transform.SetParent(transform, false);
        canvas = g.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
        var rt = (RectTransform)g.transform;
        rt.sizeDelta = new Vector2(W, H); rt.localScale = Vector3.one * (screenWidth / W);
        rt.localPosition = Vector3.zero; rt.localRotation = Quaternion.identity;
        root = UIKit.Panel(rt, "Root", 0, 0, W, H, Navy);

        mails.Add(new Mail { from = "Recursos Humanos", subject = "Recordatorio: capacitación de seguridad", read = true,
            body = "Estimado equipo,\n\nLes recordamos que la capacitación anual de seguridad de la información será el jueves a las 10:00.\n\nSaludos,\nRecursos Humanos" });
        mails.Add(new Mail { from = "Soporte TI", subject = "Mantenimiento programado del sábado", read = true,
            body = "Equipo,\n\nEl sábado de 22:00 a 23:00 habrá mantenimiento en los servidores internos. No se requiere ninguna acción de su parte.\n\nSoporte TI" });
        mails.Add(new Mail { from = "Gerencia de Operaciones", subject = "Reporte mensual de transacciones", read = true,
            body = "Adjunto en la carpeta compartida el reporte mensual. Por favor revisarlo antes del viernes.\n\nGerencia de Operaciones" });

        BuildDesktop();
        BuildLock();
    }

    void Start() { canvas.worldCamera = Camera.main; }

    void Update()
    {
        if (!canvas.worldCamera) canvas.worldCamera = Camera.main;
        int mins = 9 * 60 + 14 + (int)((Time.time - t0) / 8f);
        if (clock) clock.text = $"{(mins / 60) % 24:00}:{mins % 60:00}";
    }

    // ------------------------------------------------------------------ API
    public void Activate() { if (active) return; active = true; StartCoroutine(Login()); }

    public void DeliverPhishingMail()
    {
        phishMail = new Mail
        {
            from = "Soporte Banco Ejemplo <seguridad@banco-ejemp1o-soporte.com>",
            subject = "URGENTE: Su cuenta será bloqueada en 24 horas",
            body = "Estimado cliente,\n\nHemos detectado actividad inusual en su cuenta. Para evitar el bloqueo definitivo debe verificar su identidad de inmediato.\n\nDescargue y abra el documento adjunto e ingrese sus credenciales de acceso.\n\nAtentamente,\nDepartamento de Seguridad",
            phishing = true
        };
        mails.Insert(0, phishMail);
        UpdateBadge();
        ShowToast();
        if (appWin && mailList) RefreshList();
    }

    // -------------------------------------------------------------- DESKTOP
    void BuildDesktop()
    {
        var wm = new Color(1, 1, 1, 0.10f);
        UIKit.Label(root, "BANCO EJEMPLO", 0, 280, W, 70, 48, wm, TextAlignmentOptions.Center, true);
        UIKit.Label(root, "Estación de trabajo SEC-01", 0, 345, W, 40, 26, wm, TextAlignmentOptions.Center);
        Icon("Correo", "C", new Color(0.2f, 0.55f, 0.95f), 40, OpenMail);
        Icon("Navegador", "N", new Color(0.15f, 0.7f, 0.5f), 170, OpenBrowser);
        Icon("Documentos", "D", new Color(0.95f, 0.7f, 0.2f), 300, OpenDocs);

        var bar = UIKit.Panel(root, "Taskbar", 0, H - 44, W, 44, BarC);
        UIKit.Label(bar, "BANCO EJEMPLO  |  SEC-01", 16, 8, 450, 30, 22, new Color(0.6f, 0.8f, 1f));
        badge = UIKit.Label(bar, "", W - 520, 8, 400, 30, 22, new Color(1f, 0.85f, 0.3f), TextAlignmentOptions.TopRight);
        clock = UIKit.Label(bar, "09:14", W - 110, 8, 100, 30, 24, Color.white, TextAlignmentOptions.TopRight);
    }

    void Icon(string name, string letter, Color c, float y, Action open)
    {
        UIKit.Btn(root, letter, 40, y, 90, 90, c, Color.white, 48, () => open());
        UIKit.Label(root, name, 25, y + 92, 120, 28, 20, Color.white, TextAlignmentOptions.Center);
    }

    void BuildLock()
    {
        var p = UIKit.Panel(root, "Lock", 0, 0, W, H, new Color(0.02f, 0.04f, 0.08f));
        lockScreen = p.gameObject;
        UIKit.Label(p, "BANCO EJEMPLO\nEstación SEC-01\n\n[ Sesión bloqueada ]", 0, 230, W, 260, 40, new Color(0.6f, 0.8f, 1f), TextAlignmentOptions.Center);
    }

    IEnumerator Login()
    {
        var t = lockScreen.GetComponentInChildren<TextMeshProUGUI>();
        t.text = "BANCO EJEMPLO\nEstación SEC-01\n\nIniciando sesión...";
        yield return new WaitForSeconds(1.5f);
        lockScreen.SetActive(false);
    }

    RectTransform OpenWindow(string title)
    {
        CloseWindow();
        appWin = UIKit.Panel(root, "Window", 150, 40, 1000, 610, WinC);
        var tb = UIKit.Panel(appWin, "Title", 0, 0, 1000, 38, TitleC);
        UIKit.Label(tb, title, 14, 5, 800, 30, 22, Color.white);
        UIKit.Btn(tb, "X", 956, 4, 38, 30, new Color(0.8f, 0.2f, 0.2f), Color.white, 22, CloseWindow);
        if (toast) toast.transform.SetAsLastSibling();
        return appWin;
    }

    void CloseWindow() { if (appWin) Destroy(appWin.gameObject); appWin = null; }

    // ----------------------------------------------------------------- MAIL
    void OpenMail()
    {
        var w = OpenWindow("Correo - Bandeja de entrada");
        mailList = UIKit.Panel(w, "List", 0, 38, 330, 572, GrayC);
        mailPane = UIKit.Panel(w, "Pane", 330, 38, 670, 572, Color.white);
        RefreshList();
        UIKit.Label(mailPane, "Selecciona un correo para leerlo.", 30, 30, 600, 40, 24, Soft);
    }

    void RefreshList()
    {
        foreach (Transform c in mailList) Destroy(c.gameObject);
        for (int i = 0; i < mails.Count; i++)
        {
            var m = mails[i];
            var b = UIKit.Btn(mailList, "", 0, i * 76, 330, 74, m.read ? new Color(0.92f, 0.94f, 0.96f) : Color.white, Ink, 20, () => ShowMail(m));
            UIKit.Label(b.transform, (m.read ? "" : "* ") + m.from, 12, 6, 306, 26, 19, Ink, TextAlignmentOptions.TopLeft, !m.read);
            UIKit.Label(b.transform, m.subject, 12, 34, 306, 38, 17, Soft);
        }
    }

    void ShowMail(Mail m)
    {
        m.read = true;
        foreach (Transform c in mailPane) Destroy(c.gameObject);
        UIKit.Label(mailPane, m.subject, 24, 14, 620, 70, 27, Ink, TextAlignmentOptions.TopLeft, true);
        UIKit.Label(mailPane, "De: " + m.from, 24, 90, 620, 30, 19, Soft);
        UIKit.Panel(mailPane, "Line", 24, 124, 622, 2, GrayC);
        UIKit.Label(mailPane, m.body, 24, 136, 620, 270, 22, Ink);
        if (m.phishing)
        {
            UIKit.Btn(mailPane, "Abrir adjunto:  Verificacion_Cuenta.pdf.exe", 24, 420, 620, 56, new Color(0.85f, 0.9f, 1f), Ink, 22, () => StartCoroutine(Infection()));
            UIKit.Btn(mailPane, "Reportar como phishing", 24, 490, 320, 52, new Color(0.15f, 0.6f, 0.35f), Color.white, 20, Report);
        }
        RefreshList(); UpdateBadge();
    }

    void UpdateBadge()
    {
        int n = 0; foreach (var m in mails) if (!m.read) n++;
        badge.text = n > 0 ? $"Correos sin leer: {n}" : "";
    }

    void ShowToast()
    {
        if (toast) Destroy(toast);
        var b = UIKit.Btn(root, "", 1000, 556, 270, 100, new Color(1, 1, 1, 0.97f), Ink, 20, () => { if (toast) Destroy(toast); OpenMail(); ShowMail(phishMail); });
        toast = b.gameObject;
        UIKit.Label(b.transform, "Nuevo correo", 12, 6, 250, 24, 18, TitleC, TextAlignmentOptions.TopLeft, true);
        UIKit.Label(b.transform, "Soporte Banco Ejemplo", 12, 30, 250, 26, 20, Ink);
        UIKit.Label(b.transform, "URGENTE: Su cuenta será bloq...", 12, 58, 250, 36, 17, Soft);
        toast.transform.SetAsLastSibling();
    }

    void Report()
    {
        if (Infected) return;
        CloseWindow(); if (toast) Destroy(toast);
        var p = UIKit.Panel(root, "Reported", 340, 250, 600, 200, WinC);
        UIKit.Label(p, "Correo reportado al equipo de Seguridad.\nGracias por mantener la red segura.", 20, 40, 560, 120, 26, Ink, TextAlignmentOptions.Center);
        OnReported?.Invoke();
    }

    // ------------------------------------------------------ BROWSER / DOCS
    void OpenBrowser()
    {
        var w = OpenWindow("Navegador (simulado)");
        var addr = UIKit.Panel(w, "Addr", 12, 48, 976, 40, GrayC);
        var url = UIKit.Label(addr, "https://intranet.bancoejemplo.com", 14, 6, 900, 28, 20, Ink);
        pageLabel = UIKit.Label(w, "", 24, 160, 950, 430, 24, Ink);
        string[] names = { "Intranet", "Noticias", "Buscador" };
        string[] urls = { "https://intranet.bancoejemplo.com", "https://noticias.ejemplo.com", "https://buscador.ejemplo.com" };
        string[] pages =
        {
            "INTRANET - Banco Ejemplo\n\nAvisos:\n- Recuerde: el equipo de TI nunca le pedirá su contraseña por correo.\n- Capacitación de ciberseguridad: jueves 10:00.\n- Guardia del SOC activa 24/7.",
            "NOTICIAS DEL SECTOR\n\nAumentan los ataques de ransomware contra entidades financieras.\n\nLos expertos recomiendan verificar siempre el remitente y no abrir adjuntos inesperados.",
            "BUSCADOR\n\nResultados para \"phishing\":\n- Cómo reconocer un correo fraudulento\n- Qué hacer si abrí un adjunto sospechoso\n- Buenas prácticas de seguridad en la banca"
        };
        pageLabel.text = pages[0];
        for (int i = 0; i < 3; i++)
        {
            int idx = i;
            UIKit.Btn(w, names[i], 12 + i * 170, 100, 160, 44, TitleC, Color.white, 20, () => { url.text = urls[idx]; pageLabel.text = pages[idx]; });
        }
    }

    void OpenDocs()
    {
        var w = OpenWindow("Documentos");
        var view = UIKit.Label(w, "Selecciona un documento.", 360, 60, 620, 500, 22, Ink);
        string[] names = { "Balance_Q3.xlsx", "Clientes_Premium.csv", "Politica_Seguridad.pdf" };
        string[] texts =
        {
            "Balance_Q3.xlsx\n\nIngresos: 12.4 M\nEgresos: 9.1 M\nUtilidad neta: 3.3 M\n\n(datos ficticios)",
            "Clientes_Premium.csv\n\nID, Nombre, Saldo\n1001, A. Rojas, 54,200\n1002, M. Díaz, 120,900\n1003, L. Vega, 87,450\n...\n\n(datos ficticios)",
            "Politica_Seguridad.pdf\n\n1. No abrir adjuntos inesperados.\n2. No compartir credenciales.\n3. Reportar correos sospechosos al SOC.\n4. Bloquear la pantalla al ausentarse."
        };
        for (int i = 0; i < 3; i++)
        {
            int idx = i;
            UIKit.Btn(w, names[i], 20, 60 + i * 70, 320, 58, GrayC, Ink, 20, () => view.text = texts[idx]);
        }
    }

    // ------------------------------------------------------------ INFECTION
    IEnumerator Infection()
    {
        if (Infected) yield break;
        Infected = true;
        CloseWindow(); if (toast) Destroy(toast);

        var dlg = UIKit.Panel(root, "Download", 300, 250, 680, 220, new Color(0.93f, 0.93f, 0.93f));
        UIKit.Label(dlg, "Verificacion_Cuenta.pdf.exe", 24, 16, 630, 36, 26, Ink, TextAlignmentOptions.TopLeft, true);
        var status = UIKit.Label(dlg, "Descargando componente de seguridad...", 24, 60, 630, 30, 20, Soft);
        var track = UIKit.Panel(dlg, "Track", 24, 110, 632, 28, new Color(0.75f, 0.75f, 0.78f));
        var fill = UIKit.Panel(track, "Fill", 0, 0, 0, 28, new Color(0.2f, 0.6f, 0.95f));
        var pct = UIKit.Label(dlg, "0%", 24, 150, 630, 30, 22, Ink, TextAlignmentOptions.Center);
        for (float t = 0; t < 2.6f; t += Time.deltaTime)
        {
            float k = t / 2.6f;
            fill.sizeDelta = new Vector2(632f * k, 28f);
            pct.text = Mathf.RoundToInt(k * 100f) + "%";
            if (k > 0.7f) status.text = "Ejecutando...";
            yield return null;
        }

        OnMalwareExecuted?.Invoke();

        var gl = UIKit.Panel(root, "Glitch", 0, 0, W, H, Color.black);
        var gi = gl.GetComponent<Image>();
        Color[] gc = { Color.black, new Color(0.8f, 0, 0), Color.white, new Color(0.2f, 0, 0) };
        for (int i = 0; i < 14; i++)
        {
            gi.color = gc[UnityEngine.Random.Range(0, gc.Length)];
            gl.anchoredPosition = new Vector2(UnityEngine.Random.Range(-20f, 20f), UnityEngine.Random.Range(-10f, 10f));
            yield return new WaitForSeconds(0.07f);
        }
        Destroy(gl.gameObject); Destroy(dlg.gameObject);
        yield return MalwareScreen();
    }

    IEnumerator MalwareScreen()
    {
        var m = UIKit.Panel(root, "Malware", 0, 0, W, H, new Color(0.1f, 0, 0));
        var warn = UIKit.Label(m, "SU SISTEMA HA SIDO COMPROMETIDO", 0, 24, W, 70, 50, new Color(1f, 0.15f, 0.15f), TextAlignmentOptions.Center, true);
        var term = UIKit.Label(m, "", 60, 120, 1160, 270, 24, new Color(1f, 0.5f, 0.45f));
        var stats = UIKit.Label(m, "", 60, 410, 700, 200, 26, Color.white);
        var timer = UIKit.Label(m, "", 740, 410, 480, 200, 40, new Color(1f, 0.2f, 0.2f), TextAlignmentOptions.Center, true);
        UIKit.Label(m, "SIMULACIÓN EDUCATIVA - ESTO NO ES UN ATAQUE REAL", 0, 676, W, 30, 20, new Color(1, 1, 1, 0.6f), TextAlignmentOptions.Center);

        string[] lines =
        {
            "> Conexión remota establecida desde 185.220.101.47",
            "> Acceso al centro de datos principal ........... OK",
            "> Credenciales de administrador capturadas ...... OK",
            "> Desactivando copias de seguridad .............. OK",
            "> Cifrando servidores de la red bancaria ......... EN CURSO"
        };
        foreach (var l in lines) { term.text += l + "\n"; yield return new WaitForSeconds(0.6f); }

        float files = 0, recs = 0, accts = 0, sec = 72f * 3600f;
        while (true)
        {
            files = Mathf.MoveTowards(files, 48213f, Time.deltaTime * 5000f);
            recs = Mathf.MoveTowards(recs, 1250000f, Time.deltaTime * 90000f);
            accts = Mathf.MoveTowards(accts, 31540f, Time.deltaTime * 2500f);
            stats.text = $"Archivos cifrados: {files:N0}\nRegistros de clientes robados: {recs:N0}\nCuentas bancarias expuestas: {accts:N0}";
            sec -= Time.deltaTime * 900f;
            int s = Mathf.Max(0, (int)sec);
            timer.text = $"DATOS CIFRADOS\nTiempo restante\n{s / 3600:00}:{(s / 60) % 60:00}:{s % 60:00}";
            warn.color = Color.Lerp(new Color(0.6f, 0, 0), new Color(1f, 0.2f, 0.2f), 0.5f + 0.5f * Mathf.Sin(Time.time * 6f));
            yield return null;
        }
    }
}
