using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// Datos del reporte final de auditoría (RF-05).
public class ReportData
{
    public string title, subtitle, lesson;
    public Color color;
    public string mode, time, signals;
    public (string name, string status, Color statusColor, string detail)[] evidence;
    public string mailFrom, mailAddr, mailSubject, mailVector;                         // vista del correo sospechoso
    public (string time, string origin, string action, string result, Color color)[] access;   // historial de accesos
}

/// Interfaz táctil: joystick, zona de mirar, botones Sentarse/Levantarse/Abrir puerta, mensajes y reporte final.
public class OverlayUI : MonoBehaviour
{
    public SeatInteraction seat;
    public DoorInteraction door;

    public bool ShowHints = true;

    GameObject canvasGO, hintPanel, controls, sitBtn, standBtn, doorBtn, debrief;
    RectTransform hintRT, canvasRT;
    TextMeshProUGUI hint;

    static readonly Color Cyan = new Color(0.3f, 0.88f, 1f);
    static readonly Color Soft = new Color(0.72f, 0.78f, 0.88f);

    void Awake()
    {
        MobileInput.Stick = Vector2.zero; MobileInput.LookAccum = Vector2.zero;
        Application.targetFrameRate = 60;
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
        Screen.orientation = ScreenOrientation.LandscapeLeft;
        if (FindFirstObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
        Build();
    }

    static void Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos)
    {
        rt.anchorMin = rt.anchorMax = anchor; rt.pivot = pivot; rt.anchoredPosition = pos;
    }

    void Build()
    {
        var cg = new GameObject("OverlayCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        cg.transform.SetParent(transform, false);
        canvasGO = cg; canvasRT = (RectTransform)cg.transform;
        var cv = cg.GetComponent<Canvas>(); cv.renderMode = RenderMode.ScreenSpaceOverlay; cv.sortingOrder = 10;
        var sc = cg.GetComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(1920, 1080); sc.matchWidthOrHeight = 0.5f;
        var t = cg.transform;

        // Controles de movimiento (se ocultan al sentarse para no bloquear toques sobre el monitor)
        controls = new GameObject("Controls", typeof(RectTransform)); controls.transform.SetParent(t, false);
        var crt = (RectTransform)controls.transform; crt.anchorMin = Vector2.zero; crt.anchorMax = Vector2.one; crt.offsetMin = crt.offsetMax = Vector2.zero;

        var look = UIKit.Panel(controls.transform, "LookPad", 0, 0, 10, 10, new Color(1, 1, 1, 0));
        look.anchorMin = new Vector2(0.45f, 0); look.anchorMax = Vector2.one; look.offsetMin = look.offsetMax = Vector2.zero;
        look.gameObject.AddComponent<LookPad>();

        var joy = UIKit.Panel(controls.transform, "Joystick", 0, 0, 320, 320, new Color(1, 1, 1, 0.12f));
        joy.GetComponent<Image>().sprite = UIKit.Circle();
        Place(joy, Vector2.zero, new Vector2(0.5f, 0.5f), new Vector2(270, 270));
        var knob = UIKit.Panel(joy, "Knob", 0, 0, 130, 130, new Color(1, 1, 1, 0.45f), false);
        knob.GetComponent<Image>().sprite = UIKit.Circle();
        Place(knob, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero);
        var js = joy.gameObject.AddComponent<VirtualJoystick>(); js.knob = knob; js.radius = 110f;

        // Mensaje superior
        hintRT = UIKit.Panel(t, "HintPanel", 0, 0, 1200, 90, new Color(0.02f, 0.06f, 0.1f, 0.7f), false);
        Place(hintRT, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -24));
        hint = UIKit.Label(hintRT, "", 20, 8, 1160, 76, 32, Color.white, TextAlignmentOptions.Center);
        hint.enableAutoSizing = true; hint.fontSizeMin = 18; hint.fontSizeMax = 32;
        hintPanel = hintRT.gameObject;

        // Botones
        var sb = UIKit.Btn(t, "SENTARSE  (E)", 0, 0, 440, 120, new Color(0.1f, 0.65f, 0.4f), Color.white, 44, () => seat.Sit());
        Place((RectTransform)sb.transform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-80, 170)); sitBtn = sb.gameObject;
        var eb = UIKit.Btn(t, "LEVANTARSE", 0, 0, 280, 70, new Color(0.2f, 0.3f, 0.5f, 0.9f), Color.white, 28, () => seat.Stand());
        Place((RectTransform)eb.transform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-24, -24)); standBtn = eb.gameObject;
        var db = UIKit.Btn(t, "ABRIR PUERTA  (E)", 0, 0, 480, 120, new Color(0.15f, 0.45f, 0.85f), Color.white, 44, () => { if (door) door.Open(); });
        Place((RectTransform)db.transform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-80, 170)); doorBtn = db.gameObject;

        debrief = new GameObject("Report", typeof(RectTransform)); debrief.transform.SetParent(t, false);
        debrief.SetActive(false);
    }

    void Update()
    {
        bool seated = seat.Seated;
        sitBtn.SetActive(seat.NearSeat && !debrief.activeSelf);
        standBtn.SetActive(seated && seat.CanStand && !debrief.activeSelf);
        doorBtn.SetActive(door && door.NearDoor && !door.IsOpen && !seated && !debrief.activeSelf);
        bool cctvOn = cctv && cctv.activeSelf;
        hintPanel.SetActive(ShowHints && !string.IsNullOrEmpty(hint.text) && !debrief.activeSelf && !cctvOn);
        // Sentado: guía más compacta en el borde superior para no tapar el monitor
        hintRT.sizeDelta = seated ? new Vector2(1100, 54) : new Vector2(1200, 90);
        hintRT.anchoredPosition = new Vector2(0, seated ? -2 : -24);
        hint.rectTransform.sizeDelta = seated ? new Vector2(1060, 48) : new Vector2(1160, 76);
        hint.rectTransform.anchoredPosition = new Vector2(20, seated ? -3 : -8);
        hint.fontSizeMax = seated ? 24 : 32;
        bool show = !seated && !debrief.activeSelf && !cctvOn;
        controls.SetActive(show);
        if (!show) MobileInput.Stick = Vector2.zero;
    }

    public void SetHint(string s) { hint.text = s; }

    public void SetVisible(bool v) { if (canvasGO) canvasGO.SetActive(v); }

    // ------------------------------------------------- CÁMARA DE SEGURIDAD (contagio)
    GameObject cctv; TextMeshProUGUI cctvLabel, cctvDot;

    /// Muestra la etiqueta de cámara de seguridad ("● REC  CÁM. 04 · CONTABILIDAD"); null la oculta.
    public void SetCctv(string label)
    {
        if (!cctv)
        {
            var p = UIKit.Panel(canvasRT, "CCTV", 0, 0, 760, 64, new Color(0, 0, 0, 0.55f), false);
            Place(p, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 40));
            cctvDot = UIKit.Label(p, "●", 18, 10, 30, 44, 30, new Color(1f, 0.2f, 0.2f), TextAlignmentOptions.Center);
            UIKit.Label(p, "REC", 50, 18, 60, 30, 22, new Color(1f, 0.3f, 0.3f), TextAlignmentOptions.TopLeft, true);
            cctvLabel = UIKit.Label(p, "", 116, 16, 630, 34, 26, Color.white, TextAlignmentOptions.TopLeft, true);
            cctvLabel.textWrappingMode = TextWrappingModes.NoWrap;
            cctv = p.gameObject;
        }
        cctv.SetActive(label != null);
        if (label != null) cctvLabel.text = label + "   " + System.DateTime.Now.ToString("HH:mm:ss");
    }

    void LateUpdate()
    {
        if (cctv && cctv.activeSelf) cctvDot.alpha = (Time.unscaledTime % 1f) < 0.6f ? 1f : 0.15f;
    }

    // ------------------------------------------------------------------ REPORTE
    static void Reload(SimMode quickMode, bool skipMenu)
    {
        SimSession.QuickRestartMode = quickMode;
        GameMenu.SkipMenuOnce = skipMenu;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void ShowReport(ReportData r)
    {
        foreach (Transform c in debrief.transform) Destroy(c.gameObject);
        var drt = (RectTransform)debrief.transform;
        drt.anchorMin = Vector2.zero; drt.anchorMax = Vector2.one; drt.offsetMin = drt.offsetMax = Vector2.zero;
        var dim = UIKit.Panel(drt, "Dim", 0, 0, 10, 10, new Color(0.01f, 0.02f, 0.05f, 0.7f));
        dim.anchorMin = Vector2.zero; dim.anchorMax = Vector2.one; dim.offsetMin = dim.offsetMax = Vector2.zero;

        var d = UIKit.Panel(drt, "Card", 0, 0, 1560, 960, new Color(0.03f, 0.05f, 0.1f, 0.98f));
        Place(d, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero);
        UIKit.Panel(d, "Top", 0, 0, 1560, 8, r.color, false);

        var k = UIKit.Label(d, "REPORTE DE EVIDENCIAS  ·  AUDITORÍA", 56, 40, 900, 30, 22, Cyan, TextAlignmentOptions.TopLeft, true);
        k.characterSpacing = 6;
        UIKit.Label(d, r.title, 54, 72, 1450, 70, 58, r.color, TextAlignmentOptions.TopLeft, true);
        var sub = UIKit.Label(d, r.subtitle, 56, 146, 1450, 64, 26, Soft);
        sub.enableAutoSizing = true; sub.fontSizeMin = 18; sub.fontSizeMax = 26;

        // Indicadores
        var stats = new[] { ("MODO", r.mode), ("TIEMPO", r.time), ("SEÑALES DETECTADAS", r.signals) };
        for (int i = 0; i < 3; i++)
        {
            var b = UIKit.Panel(d, "Stat", 56 + i * 490, 222, 470, 104, new Color(1, 1, 1, 0.05f), false);
            UIKit.Panel(b, "Bar", 0, 0, 4, 104, r.color, false);
            UIKit.Label(b, stats[i].Item1, 24, 14, 430, 26, 19, Soft, TextAlignmentOptions.TopLeft, true).characterSpacing = 4;
            UIKit.Label(b, stats[i].Item2, 24, 42, 430, 54, 42, Color.white, TextAlignmentOptions.TopLeft, true);
        }

        // Evidencias auditadas (columna izquierda)
        var gold = new Color(0.97f, 0.8f, 0.36f);
        UIKit.Label(d, "EVIDENCIAS AUDITADAS", 56, 352, 800, 28, 20, gold, TextAlignmentOptions.TopLeft, true).characterSpacing = 5;
        for (int i = 0; i < r.evidence.Length; i++)
        {
            var e = r.evidence[i];
            var row = UIKit.Panel(d, "Row", 56, 390 + i * 124, 960, 112, new Color(1, 1, 1, 0.04f), false);
            var num = UIKit.Panel(row, "Num", 18, 30, 48, 48, new Color(1, 1, 1, 0.08f), false);
            num.GetComponent<Image>().sprite = UIKit.Circle();
            UIKit.Label(num, (i + 1).ToString(), 0, 0, 48, 48, 24, Color.white, TextAlignmentOptions.Center, true);
            UIKit.Label(row, e.name, 84, 14, 560, 34, 26, Color.white, TextAlignmentOptions.TopLeft, true);
            var det = UIKit.Label(row, e.detail, 84, 50, 590, 58, 19, Soft);
            det.enableAutoSizing = true; det.fontSizeMin = 13; det.fontSizeMax = 19;
            var chip = UIKit.Panel(row, "Chip", 690, 32, 252, 46, e.statusColor, false);
            var ct = UIKit.Label(chip, e.status, 8, 0, 236, 46, 20, Color.white, TextAlignmentOptions.Center, true);
            ct.characterSpacing = 1; ct.enableAutoSizing = true; ct.fontSizeMin = 12; ct.fontSizeMax = 20;
            ct.textWrappingMode = TextWrappingModes.NoWrap;
        }

        // Columna derecha: correo sospechoso + historial de accesos
        UIKit.Label(d, "CORREO SOSPECHOSO", 1046, 352, 460, 28, 20, gold, TextAlignmentOptions.TopLeft, true).characterSpacing = 5;
        var mail = UIKit.Panel(d, "Mail", 1046, 390, 458, 150, new Color(0.96f, 0.97f, 0.99f), false);
        UIKit.Panel(mail, "Bar", 0, 0, 458, 6, r.color, false);
        UIKit.Label(mail, r.mailSubject, 16, 14, 426, 50, 19, new Color(0.1f, 0.12f, 0.16f), TextAlignmentOptions.TopLeft, true).overflowMode = TextOverflowModes.Ellipsis;
        UIKit.Label(mail, "De: " + r.mailFrom, 16, 64, 426, 22, 15, new Color(0.25f, 0.28f, 0.34f)).overflowMode = TextOverflowModes.Ellipsis;
        UIKit.Label(mail, "<" + r.mailAddr + ">", 16, 86, 426, 22, 15, new Color(0.75f, 0.16f, 0.14f)).overflowMode = TextOverflowModes.Ellipsis;
        UIKit.Label(mail, "Vector: " + r.mailVector, 16, 112, 426, 30, 14, new Color(0.35f, 0.38f, 0.45f)).overflowMode = TextOverflowModes.Ellipsis;

        UIKit.Label(d, "HISTORIAL DE ACCESOS", 1046, 556, 460, 28, 20, gold, TextAlignmentOptions.TopLeft, true).characterSpacing = 5;
        var log = UIKit.Panel(d, "Log", 1046, 592, 458, 170, new Color(1, 1, 1, 0.04f), false);
        for (int i = 0; i < r.access.Length && i < 4; i++)
        {
            var a = r.access[i];
            float y = 8 + i * 40;
            UIKit.Label(log, a.time, 12, y + 4, 60, 24, 16, Color.white, TextAlignmentOptions.TopLeft, true);
            var o = UIKit.Label(log, a.origin + "\n<size=80%>" + a.action + "</size>", 76, y, 228, 38, 14, Soft);
            o.textWrappingMode = TextWrappingModes.NoWrap; o.overflowMode = TextOverflowModes.Ellipsis;
            var rc = UIKit.Panel(log, "Res", 312, y + 4, 136, 28, a.color, false);
            var rt = UIKit.Label(rc, a.result, 4, 0, 128, 28, 13, Color.white, TextAlignmentOptions.Center, true);
            rt.enableAutoSizing = true; rt.fontSizeMin = 9; rt.fontSizeMax = 13; rt.textWrappingMode = TextWrappingModes.NoWrap;
        }

        var lesson = UIKit.Label(d, r.lesson, 56, 770, 1448, 72, 21, new Color(0.85f, 0.9f, 0.96f));
        lesson.enableAutoSizing = true; lesson.fontSizeMin = 15; lesson.fontSizeMax = 21;

        SimMode cur = SimSession.Mode == SimMode.None ? SimMode.Attack : SimSession.Mode;
        SimMode other = cur == SimMode.Attack ? SimMode.Defense : SimMode.Attack;
        string otherName = other == SimMode.Attack ? "MODO ATAQUE" : "MODO DEFENSA";
        UIKit.Btn(d, "PROBAR " + otherName, 56, 856, 520, 80, other == SimMode.Defense ? new Color(0.12f, 0.6f, 0.35f) : new Color(0.75f, 0.2f, 0.18f), Color.white, 30,
            () => Reload(other, true));
        UIKit.Btn(d, "Repetir este modo", 596, 856, 380, 80, new Color(0.2f, 0.45f, 0.85f), Color.white, 28, () => Reload(cur, true));
        UIKit.Btn(d, "Menú principal", 996, 856, 340, 80, new Color(0.18f, 0.22f, 0.32f), Color.white, 28, () => Reload(SimMode.None, false));

        debrief.SetActive(true);
        debrief.transform.SetAsLastSibling();
    }
}
