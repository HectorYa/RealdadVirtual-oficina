using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// Interfaz táctil: joystick, zona de mirar, botones Sentarse/Levantarse, mensajes y resumen final.
public class OverlayUI : MonoBehaviour
{
    public SeatInteraction seat;
    public DoorInteraction door;

    public bool ShowHints = true;

    GameObject canvasGO, hintPanel, controls, sitBtn, standBtn, doorBtn, debrief;
    TextMeshProUGUI hint, dTitle, dBody;

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
        canvasGO = cg;
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
        var hp = UIKit.Panel(t, "HintPanel", 0, 0, 1200, 90, new Color(0.02f, 0.06f, 0.1f, 0.7f), false);
        Place(hp, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -24));
        hint = UIKit.Label(hp, "", 20, 8, 1160, 76, 32, Color.white, TextAlignmentOptions.Center);
        hintPanel = hp.gameObject;

        // Botones
        var sb = UIKit.Btn(t, "SENTARSE  (E)", 0, 0, 440, 120, new Color(0.1f, 0.65f, 0.4f), Color.white, 44, () => seat.Sit());
        Place((RectTransform)sb.transform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-80, 170)); sitBtn = sb.gameObject;
        var eb = UIKit.Btn(t, "LEVANTARSE", 0, 0, 320, 90, new Color(0.2f, 0.3f, 0.5f, 0.9f), Color.white, 34, () => seat.Stand());
        Place((RectTransform)eb.transform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-40, -40)); standBtn = eb.gameObject;
        var db = UIKit.Btn(t, "ABRIR PUERTA  (E)", 0, 0, 480, 120, new Color(0.15f, 0.45f, 0.85f), Color.white, 44, () => { if (door) door.Open(); });
        Place((RectTransform)db.transform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-80, 170)); doorBtn = db.gameObject;

        // Resumen final
        var d = UIKit.Panel(t, "Debrief", 0, 0, 1400, 860, new Color(0.04f, 0.05f, 0.08f, 0.96f));
        Place(d, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero);
        dTitle = UIKit.Label(d, "", 40, 28, 1320, 80, 52, Color.white, TextAlignmentOptions.TopLeft, true);
        dBody = UIKit.Label(d, "", 40, 120, 1320, 600, 32, new Color(0.85f, 0.9f, 0.95f));
        dBody.enableAutoSizing = true; dBody.fontSizeMin = 18; dBody.fontSizeMax = 32;
        UIKit.Btn(d, "Reiniciar simulación", 40, 740, 460, 90, new Color(0.2f, 0.5f, 0.9f), Color.white, 34,
            () => { GameMenu.SkipMenuOnce = true; SceneManager.LoadScene(SceneManager.GetActiveScene().name); });
        UIKit.Btn(d, "Menú principal", 530, 740, 380, 90, new Color(0.18f, 0.22f, 0.32f), Color.white, 34,
            () => { GameMenu.SkipMenuOnce = false; SceneManager.LoadScene(SceneManager.GetActiveScene().name); });
        debrief = d.gameObject; debrief.SetActive(false);
    }

    void Update()
    {
        sitBtn.SetActive(seat.NearSeat);
        standBtn.SetActive(seat.Seated && seat.CanStand);
        doorBtn.SetActive(door && door.NearDoor && !door.IsOpen && !seat.Seated && !debrief.activeSelf);
        hintPanel.SetActive(ShowHints && !string.IsNullOrEmpty(hint.text) && !debrief.activeSelf);
        bool show = !seat.Seated && !debrief.activeSelf;
        controls.SetActive(show);
        if (!show) MobileInput.Stick = Vector2.zero;
    }

    public void SetHint(string s) { hint.text = s; }

    public void SetVisible(bool v) { if (canvasGO) canvasGO.SetActive(v); }

    public void ShowDebrief(string title, Color c, string body)
    {
        dTitle.text = title; dTitle.color = c; dBody.text = body; debrief.SetActive(true);
    }
}
