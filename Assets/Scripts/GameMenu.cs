using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// Menú principal (sobre un recorrido de cámara cinemático por el banco), configuración, cómo jugar,
/// acerca de y menú de pausa (ESC o botón II). Toda la interfaz se construye por código.
public class GameMenu : MonoBehaviour
{
    public static bool SkipMenuOnce;     // "Reiniciar simulación" vuelve a cargar la escena sin pasar por el menú

    public PlayerController player;
    public OverlayUI overlay;

    enum State { Menu, Playing, Paused }
    State state = State.Menu;

    static readonly Color Navy = new Color(0.02f, 0.045f, 0.10f);
    static readonly Color Gold = new Color(0.97f, 0.80f, 0.36f);
    static readonly Color Cyan = new Color(0.30f, 0.88f, 1f);
    static readonly Color Soft = new Color(0.70f, 0.77f, 0.88f);
    static readonly Color CardC = new Color(0.025f, 0.055f, 0.115f, 0.97f);
    static readonly Color Line = new Color(1f, 1f, 1f, 0.08f);

    static readonly string[] Tips =
    {
        "Verifica siempre el dominio del remitente: un solo carácter cambiado puede delatar un fraude.",
        "Un archivo «.pdf.exe» no es un PDF. Desconfía de las dobles extensiones.",
        "El área de TI nunca te pedirá tu contraseña por correo.",
        "Bloquea tu PC (Windows + L) cada vez que te levantes de tu puesto.",
        "Si un correo te presiona con urgencia, detente y verifica por otro canal.",
        "Reportar un correo sospechoso a tiempo protege a todo el banco.",
    };

    struct Shot { public Vector3 a, b, ta, tb; }
    static readonly Shot[] Shots =
    {
        new Shot { a = new Vector3(0f, 2.3f, 0.8f),   b = new Vector3(0f, 1.9f, 6.5f),   ta = new Vector3(0f, 1.4f, 17f),     tb = new Vector3(0f, 1.4f, 17f) },
        new Shot { a = new Vector3(-1.6f, 1.9f, 2.2f), b = new Vector3(-1.6f, 1.9f, 9.8f), ta = new Vector3(4.5f, 1.2f, 4.5f),  tb = new Vector3(4.5f, 1.2f, 11.5f) },
        new Shot { a = new Vector3(0f, 1.7f, 18.8f),  b = new Vector3(0f, 1.7f, 27.5f),  ta = new Vector3(0f, 1.6f, 32f),     tb = new Vector3(0f, 1.6f, 32f) },
        new Shot { a = new Vector3(4.6f, 2.5f, 32.7f), b = new Vector3(3.0f, 2.2f, 33.8f), ta = new Vector3(-0.5f, 1.6f, 39f), tb = new Vector3(-0.5f, 1.6f, 39f) },
        new Shot { a = new Vector3(-3.8f, 2.6f, 12.8f), b = new Vector3(-1.2f, 2.2f, 16.4f), ta = new Vector3(5.5f, 1.3f, 14.6f), tb = new Vector3(5.5f, 1.3f, 14.6f) },
    };

    Canvas canvas;
    RectTransform menuRoot, pauseRoot, cardHolder, tipCard;
    RectTransform settingsCard, howCard, aboutCard, openCard;
    CanvasGroup menuGroup, pauseGroup, tipGroup;
    Image fade, shotFade;
    RawImage scan;
    TextMeshProUGUI title, ghostA, ghostB, subtitle, tipText, clock, statusDot;
    GameObject pauseBtn;
    MenuButton[] menuButtons;
    CanvasGroup[] introItems;
    Camera menuCam, playerCam;
    AudioSource uiAudio, ambient;
    AudioClip clickClip, hoverClip;
    Coroutine shotsCo;
    float glitchT = 2f, glitchLeft, tipT;
    int tipIndex;
    bool busy, cardFromPause;

    // Controles de configuración (para refrescarlos)
    Slider volSlider, sensSlider;
    TextMeshProUGUI volValue, sensValue;
    Image[] qualityBtns; TextMeshProUGUI[] qualityLbl;
    ToggleSwitch hintsToggle, fullToggle;

    // ================================================================= CICLO
    void Awake()
    {
        if (FindFirstObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
        MakeAudio();
        Build();
    }

    void Start()
    {
        if (!player) player = FindFirstObjectByType<PlayerController>();
        if (!overlay) overlay = FindFirstObjectByType<OverlayUI>();
        playerCam = player ? player.cam.GetComponent<Camera>() : Camera.main;
        GameSettings.Load();
        GameSettings.Apply(player, overlay);
        RefreshSettingsUI();

        if (SkipMenuOnce) { SkipMenuOnce = false; EnterPlaying(); StartCoroutine(Fade(1f, 0f, 0.6f)); }
        else EnterMenu();
    }

    void Update()
    {
        var k = Keyboard.current;
        if (k != null && k.escapeKey.wasPressedThisFrame && !busy)
        {
            if (state == State.Playing) Pause();
            else if (openCard) CloseCard();
            else if (state == State.Paused) Resume();
        }
        float dt = Time.unscaledDeltaTime;
        if (scan) { var r = scan.uvRect; r.y -= dt * 0.04f; scan.uvRect = r; }

        if (state != State.Menu) return;
        // Efecto glitch del título
        glitchT -= dt;
        if (glitchT <= 0f) { glitchLeft = UnityEngine.Random.Range(0.12f, 0.3f); glitchT = UnityEngine.Random.Range(2.5f, 5f); }
        if (glitchLeft > 0f)
        {
            glitchLeft -= dt;
            ghostA.rectTransform.anchoredPosition = new Vector2(UnityEngine.Random.Range(-9f, 9f), UnityEngine.Random.Range(-3f, 3f));
            ghostB.rectTransform.anchoredPosition = new Vector2(UnityEngine.Random.Range(-9f, 9f), UnityEngine.Random.Range(-3f, 3f));
            ghostA.alpha = ghostB.alpha = 0.75f;
        }
        else { ghostA.rectTransform.anchoredPosition = new Vector2(3f, 0f); ghostB.rectTransform.anchoredPosition = new Vector2(-3f, 0f); ghostA.alpha = ghostB.alpha = 0.18f; }

        // Consejos rotativos
        tipT += dt;
        float cycle = 7f, tt = tipT % cycle;
        if (tipT >= cycle) { tipT -= cycle; tipIndex = (tipIndex + 1) % Tips.Length; tipText.text = Tips[tipIndex]; }
        tipText.alpha = Mathf.Clamp01(Mathf.Min(tt / 0.5f, (cycle - tt) / 0.5f));
        tipGroup.alpha = Mathf.MoveTowards(tipGroup.alpha, openCard ? 0f : 1f, dt * 4f);

        clock.text = DateTime.Now.ToString("HH:mm");
        statusDot.alpha = 0.45f + 0.55f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 2.2f));
    }

    // ============================================================== ESTADOS
    void EnterMenu()
    {
        state = State.Menu;
        MobileInput.Blocked = true;
        if (overlay) overlay.SetVisible(false);
        menuRoot.gameObject.SetActive(true);
        pauseRoot.gameObject.SetActive(false);
        pauseBtn.SetActive(false);

        var g = new GameObject("MenuCamera");
        menuCam = g.AddComponent<Camera>();
        menuCam.nearClipPlane = 0.05f; menuCam.farClipPlane = 60f; menuCam.fieldOfView = 55f;
        menuCam.clearFlags = CameraClearFlags.SolidColor; menuCam.backgroundColor = Navy;
        GameSettings.ApplyCamera(menuCam);
        if (playerCam) playerCam.enabled = false;

        ambient.Play();
        shotsCo = StartCoroutine(Shots_());
        StartCoroutine(Intro());
    }

    void EnterPlaying()
    {
        state = State.Playing;
        MobileInput.Blocked = false;
        MobileInput.Stick = Vector2.zero; MobileInput.LookAccum = Vector2.zero;
        if (shotsCo != null) StopCoroutine(shotsCo);
        if (menuCam) Destroy(menuCam.gameObject);
        if (playerCam) playerCam.enabled = true;
        shotFade.color = Color.clear;
        menuRoot.gameObject.SetActive(false);
        HideCard(true);
        if (overlay) overlay.SetVisible(true);
        pauseBtn.SetActive(true);
        ambient.Stop();
    }

    void StartGame()
    {
        if (busy) return;
        StartCoroutine(StartRoutine());
    }

    IEnumerator StartRoutine()
    {
        busy = true;
        float v0 = ambient.volume;
        for (float t = 0; t < 0.7f; t += Time.unscaledDeltaTime)
        {
            float k = t / 0.7f;
            fade.color = new Color(0, 0, 0, k); ambient.volume = v0 * (1f - k);
            yield return null;
        }
        fade.color = Color.black;
        EnterPlaying();
        ambient.volume = v0;
        yield return new WaitForSecondsRealtime(0.2f);
        yield return Fade(1f, 0f, 0.8f);
        busy = false;
    }

    void Pause()
    {
        state = State.Paused;
        MobileInput.Blocked = true;
        Time.timeScale = 0f; AudioListener.pause = true;
        pauseRoot.gameObject.SetActive(true);
        pauseBtn.SetActive(false);
        StartCoroutine(FadeGroup(pauseGroup, 0f, 1f, 0.2f));
    }

    void Resume()
    {
        HideCard(true);
        state = State.Playing;
        Time.timeScale = 1f; AudioListener.pause = false;
        MobileInput.Blocked = false;
        pauseRoot.gameObject.SetActive(false);
        pauseBtn.SetActive(true);
    }

    void Reload(bool skipMenu)
    {
        Time.timeScale = 1f; AudioListener.pause = false;
        MobileInput.Blocked = false;
        SkipMenuOnce = skipMenu;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ============================================================ ANIMACIÓN
    IEnumerator Fade(float a0, float a1, float dur)
    {
        fade.raycastTarget = true;
        for (float t = 0; t < dur; t += Time.unscaledDeltaTime)
        {
            fade.color = new Color(0, 0, 0, Mathf.Lerp(a0, a1, t / dur));
            yield return null;
        }
        fade.color = new Color(0, 0, 0, a1);
        fade.raycastTarget = a1 > 0.01f;
    }

    static IEnumerator FadeGroup(CanvasGroup g, float a0, float a1, float dur)
    {
        for (float t = 0; t < dur; t += Time.unscaledDeltaTime) { g.alpha = Mathf.Lerp(a0, a1, t / dur); yield return null; }
        g.alpha = a1;
    }

    IEnumerator Intro()
    {
        busy = true;
        fade.color = Color.black;
        foreach (var c in introItems) c.alpha = 0f;
        yield return Fade(1f, 0f, 1.0f);
        busy = false;
        // Título, subtítulo y botones entran escalonados desde la izquierda
        string full = subtitle.text; subtitle.text = "";
        for (int i = 0; i < introItems.Length; i++) StartCoroutine(SlideIn(introItems[i], i * 0.09f));
        yield return new WaitForSecondsRealtime(0.35f);
        for (int i = 0; i <= full.Length; i++) { subtitle.text = full.Substring(0, i); yield return new WaitForSecondsRealtime(0.018f); }
    }

    static IEnumerator SlideIn(CanvasGroup g, float delay)
    {
        var rt = (RectTransform)g.transform; Vector2 p = rt.anchoredPosition;
        rt.anchoredPosition = p + new Vector2(-60f, 0f);
        yield return new WaitForSecondsRealtime(delay);
        for (float t = 0; t < 0.55f; t += Time.unscaledDeltaTime)
        {
            float k = 1f - Mathf.Pow(1f - t / 0.55f, 3f);
            g.alpha = k; rt.anchoredPosition = Vector2.Lerp(p + new Vector2(-60f, 0f), p, k);
            yield return null;
        }
        g.alpha = 1f; rt.anchoredPosition = p;
    }

    // Recorrido cinemático por el banco detrás del menú
    IEnumerator Shots_()
    {
        int i = 0;
        while (true)
        {
            var s = Shots[i % Shots.Length]; i++;
            const float dur = 8f;
            for (float t = 0; t < dur; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / dur);
                menuCam.transform.position = Vector3.Lerp(s.a, s.b, k);
                menuCam.transform.rotation = Quaternion.LookRotation(Vector3.Lerp(s.ta, s.tb, k) - menuCam.transform.position);
                float edge = Mathf.Min(t, dur - t);
                shotFade.color = new Color(0, 0, 0, 1f - Mathf.Clamp01(edge / 0.6f));
                yield return null;
            }
        }
    }

    // ================================================================ AUDIO
    void MakeAudio()
    {
        uiAudio = gameObject.AddComponent<AudioSource>(); uiAudio.ignoreListenerPause = true; uiAudio.spatialBlend = 0f;
        ambient = gameObject.AddComponent<AudioSource>(); ambient.loop = true; ambient.volume = 0.22f; ambient.spatialBlend = 0f;
        clickClip = Blip(1700f, 0.06f, 0.5f);
        hoverClip = Blip(1150f, 0.035f, 0.18f);
        // Zumbido ambiental de sala de servidores (bucle exacto de 4 s)
        int sr = 22050, n = sr * 4; var d = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)sr, lfo = 0.6f + 0.4f * Mathf.Sin(2 * Mathf.PI * 0.25f * t);
            d[i] = (Mathf.Sin(2 * Mathf.PI * 55f * t) * 0.5f + Mathf.Sin(2 * Mathf.PI * 82.5f * t) * 0.3f +
                    Mathf.Sin(2 * Mathf.PI * 110f * t) * 0.2f * lfo + Mathf.Sin(2 * Mathf.PI * 220f * t) * 0.06f) * 0.5f;
        }
        var c = AudioClip.Create("menuHum", n, 1, sr, false); c.SetData(d, 0); ambient.clip = c;
    }

    static AudioClip Blip(float f, float dur, float amp)
    {
        int sr = 44100; var d = new float[(int)(sr * dur)];
        for (int i = 0; i < d.Length; i++) { float t = i / (float)sr; d[i] = Mathf.Sin(2 * Mathf.PI * f * t) * Mathf.Exp(-t * 60f) * amp; }
        var c = AudioClip.Create("blip", d.Length, 1, sr, false); c.SetData(d, 0); return c;
    }

    void Click() { uiAudio.PlayOneShot(clickClip); }
    void Hover() { uiAudio.PlayOneShot(hoverClip); }

    // ======================================================= CONSTRUCCIÓN UI
    static RectTransform RT(Transform p, string n, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        var g = new GameObject(n, typeof(RectTransform)); g.transform.SetParent(p, false);
        var rt = (RectTransform)g.transform;
        rt.anchorMin = aMin; rt.anchorMax = aMax; rt.pivot = pivot; rt.anchoredPosition = pos; rt.sizeDelta = size;
        return rt;
    }

    static Image Img(Transform p, string n, Color c, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 size, bool ray = false)
    {
        var rt = RT(p, n, aMin, aMax, pivot, pos, size);
        var im = rt.gameObject.AddComponent<Image>(); im.color = c; im.raycastTarget = ray;
        return im;
    }

    static TextMeshProUGUI Txt(Transform p, string s, float size, Color c, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 box,
        TextAlignmentOptions al = TextAlignmentOptions.TopLeft, FontStyles st = FontStyles.Normal)
    {
        var rt = RT(p, "Text", aMin, aMax, pivot, pos, box);
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.text = s; t.fontSize = size; t.color = c; t.alignment = al; t.fontStyle = st; t.raycastTarget = false;
        return t;
    }

    static readonly Vector2 TL = new Vector2(0, 1), TR = new Vector2(1, 1), BL = Vector2.zero, BR = new Vector2(1, 0), C = new Vector2(0.5f, 0.5f), ML = new Vector2(0, 0.5f), MR = new Vector2(1, 0.5f);

    static void Stretch(RectTransform rt) { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero; }

    static Sprite GradientSprite(bool horizontal, float a0, float a1)
    {
        int n = 256; var t = new Texture2D(horizontal ? n : 1, horizontal ? 1 : n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int i = 0; i < n; i++)
        {
            float k = i / (n - 1f); k = k * k * (3f - 2f * k);
            var col = new Color(1, 1, 1, Mathf.Lerp(a0, a1, k));
            if (horizontal) t.SetPixel(i, 0, col); else t.SetPixel(0, i, col);
        }
        t.Apply();
        return Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f));
    }

    static void Brackets(RectTransform card, Color c, float len = 30f, float th = 3f)
    {
        foreach (var a in new[] { BL, BR, TL, TR })
        {
            Img(card, "BracketH", c, a, a, a, Vector2.zero, new Vector2(len, th));
            Img(card, "BracketV", c, a, a, a, Vector2.zero, new Vector2(th, len));
        }
    }

    void Build()
    {
        var cg = new GameObject("MenuCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        cg.transform.SetParent(transform, false);
        canvas = cg.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 50;
        var sc = cg.GetComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(1920, 1080); sc.matchWidthOrHeight = 0.6f;
        var root = (RectTransform)cg.transform;

        // ---------------- MENÚ PRINCIPAL
        menuRoot = RT(root, "Menu", Vector2.zero, Vector2.one, C, Vector2.zero, Vector2.zero);
        menuGroup = menuRoot.gameObject.AddComponent<CanvasGroup>();
        shotFade = Img(menuRoot, "ShotFade", Color.black, Vector2.zero, Vector2.one, C, Vector2.zero, Vector2.zero);
        var grad = Img(menuRoot, "GradientL", Navy, Vector2.zero, new Vector2(0.75f, 1f), C, Vector2.zero, Vector2.zero);
        grad.sprite = GradientSprite(true, 0.96f, 0f);
        var gradB = Img(menuRoot, "GradientB", Navy, Vector2.zero, new Vector2(1f, 0.35f), C, Vector2.zero, Vector2.zero);
        gradB.sprite = GradientSprite(false, 0.85f, 0f);
        var gradT = Img(menuRoot, "GradientT", Navy, new Vector2(0f, 0.85f), Vector2.one, C, Vector2.zero, Vector2.zero);
        gradT.sprite = GradientSprite(false, 0f, 0.7f);

        // Líneas de escaneo animadas
        var st = new Texture2D(1, 4, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Point };
        st.SetPixel(0, 0, new Color(0, 0, 0, 0.35f)); st.SetPixel(0, 1, Color.clear); st.SetPixel(0, 2, Color.clear); st.SetPixel(0, 3, Color.clear); st.Apply();
        var sg = RT(menuRoot, "Scanlines", Vector2.zero, Vector2.one, C, Vector2.zero, Vector2.zero);
        scan = sg.gameObject.AddComponent<RawImage>(); scan.texture = st; scan.uvRect = new Rect(0, 0, 1, 270); scan.color = new Color(1, 1, 1, 0.22f); scan.raycastTarget = false;

        // Barra superior: marca y estado
        var top = RT(menuRoot, "TopBar", TL, TR, new Vector2(0.5f, 1f), new Vector2(0, -40), new Vector2(-240, 40));
        Txt(top, "BANCO ANDINO  ·  SUCURSAL CENTRAL", 20, new Color(1, 1, 1, 0.55f), BL, TL, ML, Vector2.zero, new Vector2(600, 40), TextAlignmentOptions.MidlineLeft).characterSpacing = 6;
        statusDot = Txt(top, "●", 22, new Color(0.3f, 1f, 0.55f), BR, TR, MR, new Vector2(-298, 0), new Vector2(30, 40), TextAlignmentOptions.MidlineRight);
        Txt(top, "RED SEGURA", 20, new Color(0.3f, 1f, 0.55f), BR, TR, MR, new Vector2(-130, 0), new Vector2(160, 40), TextAlignmentOptions.MidlineLeft, FontStyles.Bold).characterSpacing = 4;
        clock = Txt(top, "09:00", 24, Color.white, BR, TR, MR, Vector2.zero, new Vector2(110, 40), TextAlignmentOptions.MidlineRight, FontStyles.Bold);
        Img(top, "Sep", new Color(1, 1, 1, 0.25f), BR, TR, MR, new Vector2(-115, 0), new Vector2(2, 26));
        Img(menuRoot, "TopLine", Line, TL, TR, new Vector2(0.5f, 1f), new Vector2(0, -78), new Vector2(-240, 1));

        // Bloque de título + botones (anclado a la izquierda)
        var left = RT(menuRoot, "Left", ML, ML, ML, new Vector2(130, 10), new Vector2(900, 900));
        var tag = RT(left, "Tag", ML, ML, ML, new Vector2(0, 345), new Vector2(800, 40));
        Img(tag, "Dash", Cyan, ML, ML, ML, Vector2.zero, new Vector2(46, 3));
        Txt(tag, "SIMULACIÓN EDUCATIVA DE CIBERSEGURIDAD", 21, Cyan, ML, ML, ML, new Vector2(64, 0), new Vector2(760, 40), TextAlignmentOptions.MidlineLeft, FontStyles.Bold).characterSpacing = 7;

        var tblock = RT(left, "Title", ML, ML, ML, new Vector2(-6, 245), new Vector2(900, 170));
        ghostA = Txt(tblock, "CYBERSIM", 158, new Color(0.2f, 0.9f, 1f), Vector2.zero, Vector2.one, C, Vector2.zero, Vector2.zero, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        ghostB = Txt(tblock, "CYBERSIM", 158, new Color(1f, 0.15f, 0.25f), Vector2.zero, Vector2.one, C, Vector2.zero, Vector2.zero, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        title = Txt(tblock, "CYBER<color=#F7CC5C>SIM</color>", 158, Color.white, Vector2.zero, Vector2.one, C, Vector2.zero, Vector2.zero, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        foreach (var t in new[] { ghostA, ghostB, title }) t.characterSpacing = -3;

        var sub = RT(left, "Subtitle", ML, ML, ML, new Vector2(0, 140), new Vector2(860, 50));
        subtitle = Txt(sub, "Simulador de concientización: phishing y malware en la banca", 30, Soft, Vector2.zero, Vector2.one, C, Vector2.zero, Vector2.zero, TextAlignmentOptions.MidlineLeft);
        var gl = RT(left, "GoldLine", ML, ML, ML, new Vector2(0, 95), new Vector2(140, 4));
        gl.gameObject.AddComponent<Image>().color = Gold;

        var items = new (string label, Action act)[]
        {
            ("INICIAR SIMULACIÓN", StartGame),
            ("CÓMO JUGAR", () => ToggleCard(howCard, false)),
            ("CONFIGURACIÓN", () => ToggleCard(settingsCard, false)),
            ("ACERCA DE", () => ToggleCard(aboutCard, false)),
            ("SALIR", Quit),
        };
        menuButtons = new MenuButton[items.Length];
        introItems = new CanvasGroup[items.Length + 4];
        introItems[0] = tag.gameObject.AddComponent<CanvasGroup>();
        introItems[1] = tblock.gameObject.AddComponent<CanvasGroup>();
        introItems[2] = sub.gameObject.AddComponent<CanvasGroup>();
        introItems[3] = gl.gameObject.AddComponent<CanvasGroup>();
        for (int i = 0; i < items.Length; i++)
        {
            var b = MakeMenuButton(left, new Vector2(0, 20 - i * 84), 600, (i + 1).ToString("00"), items[i].label, items[i].act, i == 0);
            menuButtons[i] = b;
            introItems[4 + i] = b.gameObject.AddComponent<CanvasGroup>();
        }

        Txt(menuRoot, "v1.0  ·  Unity 6  ·  Todo el contenido es ficticio y con fines educativos", 18, new Color(1, 1, 1, 0.4f),
            BL, BL, BL, new Vector2(130, 40), new Vector2(1000, 30), TextAlignmentOptions.BottomLeft);

        // Tarjeta de consejos (abajo a la derecha)
        tipCard = RT(menuRoot, "TipCard", BR, BR, BR, new Vector2(-120, 60), new Vector2(560, 170));
        tipGroup = tipCard.gameObject.AddComponent<CanvasGroup>();
        var tbg = tipCard.gameObject.AddComponent<Image>(); tbg.color = new Color(0.02f, 0.05f, 0.11f, 0.78f); tbg.raycastTarget = false;
        Img(tipCard, "Accent", Cyan, BL, TL, ML, Vector2.zero, new Vector2(4, 0));
        Img(tipCard, "Diamond", Cyan, TL, TL, C, new Vector2(34, -32), new Vector2(10, 10)).rectTransform.localEulerAngles = new Vector3(0, 0, 45);
        Txt(tipCard, "CONSEJO DE SEGURIDAD", 18, Cyan, TL, TL, TL, new Vector2(50, -20), new Vector2(500, 30), TextAlignmentOptions.TopLeft, FontStyles.Bold).characterSpacing = 4;
        tipText = Txt(tipCard, Tips[0], 24, Color.white, TL, TL, TL, new Vector2(28, -56), new Vector2(505, 100));

        // Tarjetas de contenido
        cardHolder = RT(root, "Cards", Vector2.zero, Vector2.one, C, Vector2.zero, Vector2.zero);
        settingsCard = BuildSettingsCard(cardHolder);
        howCard = BuildHowCard(cardHolder);
        aboutCard = BuildAboutCard(cardHolder);
        foreach (var c in new[] { settingsCard, howCard, aboutCard }) c.gameObject.SetActive(false);

        // ---------------- PAUSA
        pauseRoot = RT(root, "Pause", Vector2.zero, Vector2.one, C, Vector2.zero, Vector2.zero);
        pauseRoot.SetSiblingIndex(cardHolder.GetSiblingIndex());   // debajo de las tarjetas
        pauseGroup = pauseRoot.gameObject.AddComponent<CanvasGroup>();
        Img(pauseRoot, "Dim", new Color(0.01f, 0.02f, 0.05f, 0.78f), Vector2.zero, Vector2.one, C, Vector2.zero, Vector2.zero, true);
        var pc = Card(pauseRoot, "PauseCard", C, Vector2.zero, new Vector2(680, 600));
        Txt(pc, "SIMULACIÓN DETENIDA", 20, Cyan, TL, TL, TL, new Vector2(60, -50), new Vector2(560, 30), TextAlignmentOptions.TopLeft, FontStyles.Bold).characterSpacing = 6;
        Txt(pc, "PAUSA", 72, Color.white, TL, TL, TL, new Vector2(56, -80), new Vector2(560, 90), TextAlignmentOptions.TopLeft, FontStyles.Bold);
        Img(pc, "GoldLine", Gold, TL, TL, TL, new Vector2(60, -178), new Vector2(110, 4));
        var pItems = new (string, Action)[]
        {
            ("CONTINUAR", Resume),
            ("CONFIGURACIÓN", () => ToggleCard(settingsCard, true)),
            ("REINICIAR SIMULACIÓN", () => Reload(true)),
            ("MENÚ PRINCIPAL", () => Reload(false)),
        };
        for (int i = 0; i < pItems.Length; i++)
            MakeMenuButton(pc, new Vector2(40, -250 - i * 80), 600, (i + 1).ToString("00"), pItems[i].Item1, pItems[i].Item2, i == 0, TL);
        pauseRoot.gameObject.SetActive(false);

        // Botón de pausa durante la partida (útil en el móvil)
        var pb = Img(root, "PauseButton", new Color(0.02f, 0.05f, 0.11f, 0.7f), TL, TL, TL, new Vector2(30, -30), new Vector2(96, 96), true);
        pb.sprite = UIKit.Circle();
        Txt(pb.transform, "II", 40, Color.white, Vector2.zero, Vector2.one, C, Vector2.zero, Vector2.zero, TextAlignmentOptions.Center, FontStyles.Bold);
        var pbb = pb.gameObject.AddComponent<Button>(); pbb.targetGraphic = pb;
        pbb.onClick.AddListener(() => { if (state == State.Playing && !busy) { Click(); Pause(); } });
        pauseBtn = pb.gameObject; pauseBtn.SetActive(false);

        // Fundido general (siempre encima)
        fade = Img(root, "Fade", Color.black, Vector2.zero, Vector2.one, C, Vector2.zero, Vector2.zero);
    }

    MenuButton MakeMenuButton(Transform p, Vector2 pos, float w, string idx, string label, Action act, bool primary, Vector2? anchor = null)
    {
        Vector2 a = anchor ?? ML;
        var rt = RT(p, "Btn_" + label, a, a, ML, pos, new Vector2(w, 72));
        if (anchor.HasValue) rt.pivot = new Vector2(0f, 1f);
        var hit = rt.gameObject.AddComponent<Image>(); hit.color = new Color(1, 1, 1, 0.001f);
        var bg = Img(rt, "Hover", new Color(Cyan.r, Cyan.g, Cyan.b, 0f), Vector2.zero, Vector2.one, C, Vector2.zero, Vector2.zero);
        bg.sprite = GradientSprite(true, 0.22f, 0f);
        var bar = Img(rt, "Bar", primary ? Gold : new Color(1, 1, 1, 0.35f), ML, ML, ML, Vector2.zero, new Vector2(3, 30)).rectTransform;
        var ix = Txt(rt, idx, 18, Cyan, ML, ML, ML, new Vector2(26, 0), new Vector2(40, 40), TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        var lb = Txt(rt, label, primary ? 38 : 32, primary ? Color.white : Soft, ML, ML, ML, new Vector2(78, 0), new Vector2(w - 90, 60), TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        lb.characterSpacing = 3;
        var mb = rt.gameObject.AddComponent<MenuButton>();
        mb.bar = bar; mb.label = lb; mb.index = ix; mb.hover = bg; mb.primary = primary;
        mb.onClick = () => { if (busy) return; Click(); act(); };
        mb.onHover = Hover;
        return mb;
    }

    RectTransform Card(Transform p, string n, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        var rt = RT(p, n, anchor, anchor, anchor, pos, size);
        var bg = rt.gameObject.AddComponent<Image>(); bg.color = CardC; bg.raycastTarget = true;
        Img(rt, "TopStrip", Gold, TL, TR, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0, 4));
        Brackets(rt, Cyan);
        return rt;
    }

    RectTransform CardHeader(RectTransform card, string kicker, string title)
    {
        Txt(card, kicker, 19, Cyan, TL, TL, TL, new Vector2(56, -44), new Vector2(600, 30), TextAlignmentOptions.TopLeft, FontStyles.Bold).characterSpacing = 6;
        Txt(card, title, 50, Color.white, TL, TL, TL, new Vector2(54, -72), new Vector2(700, 70), TextAlignmentOptions.TopLeft, FontStyles.Bold);
        Img(card, "HeadLine", Line, TL, TR, new Vector2(0.5f, 1f), new Vector2(0, -150), new Vector2(-112, 1));
        return card;
    }

    Button FlatButton(Transform p, string label, Vector2 anchor, Vector2 pos, Vector2 size, bool primary, Action act)
    {
        var im = Img(p, "Btn_" + label, primary ? Gold : new Color(1, 1, 1, 0.06f), anchor, anchor, anchor, pos, size, true);
        if (!primary) { var o = im.gameObject.AddComponent<Outline>(); o.effectColor = new Color(1, 1, 1, 0.3f); o.effectDistance = new Vector2(1, -1); }
        var t = Txt(im.transform, label, 24, primary ? Navy : Color.white, Vector2.zero, Vector2.one, C, Vector2.zero, Vector2.zero, TextAlignmentOptions.Center, FontStyles.Bold);
        t.characterSpacing = 4;
        var b = im.gameObject.AddComponent<Button>(); b.targetGraphic = im;
        var cb = b.colors; cb.highlightedColor = new Color(0.85f, 0.95f, 1f); cb.pressedColor = new Color(0.7f, 0.7f, 0.7f); b.colors = cb;
        b.onClick.AddListener(() => { Click(); act(); });
        return b;
    }

    // ----------------------------------------------------------- CONFIGURACIÓN
    RectTransform BuildSettingsCard(Transform p)
    {
        var c = Card(p, "SettingsCard", MR, new Vector2(-120, 10), new Vector2(780, 760));
        CardHeader(c, "03  /  PREFERENCIAS", "CONFIGURACIÓN");
        float y = -190;
        void Row(string label, string hint)
        {
            Txt(c, label, 28, Color.white, TL, TL, TL, new Vector2(56, y), new Vector2(360, 36), TextAlignmentOptions.TopLeft, FontStyles.Bold);
            Txt(c, hint, 19, Soft, TL, TL, TL, new Vector2(56, y - 36), new Vector2(360, 28));
        }

        Row("Volumen general", "Música, efectos y alarmas");
        volSlider = MakeSlider(c, new Vector2(430, y - 14), 205, 0f, 1f, v => { GameSettings.Volume = v; AudioListener.volume = v; volValue.text = Mathf.RoundToInt(v * 100) + "%"; });
        volValue = Txt(c, "80%", 24, Gold, TR, TR, TR, new Vector2(-56, y - 6), new Vector2(80, 36), TextAlignmentOptions.TopRight, FontStyles.Bold);
        y -= 92;

        Row("Sensibilidad de cámara", "Ratón y arrastre táctil");
        sensSlider = MakeSlider(c, new Vector2(430, y - 14), 205, 0.5f, 2f, v => { GameSettings.Sensitivity = v; if (player) player.lookSensitivity = GameSettings.BaseLookSensitivity * v; sensValue.text = v.ToString("0.0") + "x"; });
        sensValue = Txt(c, "1.0x", 24, Gold, TR, TR, TR, new Vector2(-56, y - 6), new Vector2(80, 36), TextAlignmentOptions.TopRight, FontStyles.Bold);
        y -= 92;

        Row("Calidad gráfica", "Baja mejora el rendimiento en móviles");
        string[] q = { "BAJA", "MEDIA", "ALTA" };
        qualityBtns = new Image[3]; qualityLbl = new TextMeshProUGUI[3];
        for (int i = 0; i < 3; i++)
        {
            int idx = i;
            var im = Img(c, "Q" + i, Color.clear, TL, TL, TL, new Vector2(430 + i * 98, y + 2), new Vector2(92, 48), true);
            var o = im.gameObject.AddComponent<Outline>(); o.effectColor = new Color(1, 1, 1, 0.25f);
            qualityLbl[i] = Txt(im.transform, q[i], 20, Color.white, Vector2.zero, Vector2.one, C, Vector2.zero, Vector2.zero, TextAlignmentOptions.Center, FontStyles.Bold);
            var b = im.gameObject.AddComponent<Button>(); b.targetGraphic = im; b.transition = Selectable.Transition.None;
            b.onClick.AddListener(() => { Click(); GameSettings.Quality = idx; GameSettings.Apply(player, overlay); if (menuCam) GameSettings.ApplyCamera(menuCam); RefreshSettingsUI(); });
            qualityBtns[i] = im;
        }
        y -= 92;

        Row("Guía en pantalla", "Mensajes que indican qué hacer");
        hintsToggle = MakeToggle(c, new Vector2(-56, y), v => { GameSettings.ShowHints = v; if (overlay) overlay.ShowHints = v; });
        y -= 92;

        if (!Application.isMobilePlatform)
        {
            Row("Pantalla completa", "Solo en ordenador");
            fullToggle = MakeToggle(c, new Vector2(-56, y), v => { GameSettings.Fullscreen = v; Screen.fullScreen = v; });
        }

        FlatButton(c, "RESTABLECER", BL, new Vector2(56, 50), new Vector2(250, 64), false, () =>
        {
            GameSettings.ResetDefaults(); GameSettings.Apply(player, overlay); if (menuCam) GameSettings.ApplyCamera(menuCam); RefreshSettingsUI();
        });
        FlatButton(c, "GUARDAR Y VOLVER", BR, new Vector2(-56, 50), new Vector2(330, 64), true, CloseCard);
        return c;
    }

    Slider MakeSlider(Transform p, Vector2 pos, float w, float min, float max, Action<float> onChange)
    {
        var rt = RT(p, "Slider", TL, TL, TL, pos, new Vector2(w, 36));
        rt.gameObject.AddComponent<Image>().color = new Color(1, 1, 1, 0.001f);
        Img(rt, "Track", new Color(1, 1, 1, 0.14f), ML, MR, C, Vector2.zero, new Vector2(0, 6), true);
        var fillArea = RT(rt, "FillArea", ML, MR, C, Vector2.zero, new Vector2(0, 6));
        fillArea.offsetMin = new Vector2(0, fillArea.offsetMin.y); fillArea.offsetMax = new Vector2(-15, fillArea.offsetMax.y);
        var fill = Img(fillArea, "Fill", Gold, Vector2.zero, Vector2.one, C, Vector2.zero, Vector2.zero);
        var handleArea = RT(rt, "HandleArea", Vector2.zero, Vector2.one, C, Vector2.zero, Vector2.zero);
        handleArea.offsetMin = new Vector2(15, 0); handleArea.offsetMax = new Vector2(-15, 0);
        var handle = Img(handleArea, "Handle", Color.white, Vector2.zero, Vector2.one, C, Vector2.zero, new Vector2(30, -6), true);
        handle.sprite = UIKit.Circle();
        var s = rt.gameObject.AddComponent<Slider>();
        s.fillRect = fill.rectTransform; s.handleRect = handle.rectTransform; s.targetGraphic = handle;
        s.direction = Slider.Direction.LeftToRight; s.minValue = min; s.maxValue = max;
        var cb = s.colors; cb.highlightedColor = Gold; cb.pressedColor = Gold; s.colors = cb;
        s.onValueChanged.AddListener(v => onChange(v));
        return s;
    }

    ToggleSwitch MakeToggle(Transform p, Vector2 pos, Action<bool> onChange)
    {
        var bg = Img(p, "Toggle", new Color(1, 1, 1, 0.15f), TR, TR, TR, pos, new Vector2(96, 46), true);
        bg.sprite = UIKit.Circle(); bg.type = Image.Type.Simple;
        var knob = Img(bg.transform, "Knob", Color.white, ML, ML, C, new Vector2(23, 0), new Vector2(36, 36));
        knob.sprite = UIKit.Circle();
        var ts = bg.gameObject.AddComponent<ToggleSwitch>();
        ts.bg = bg; ts.knob = knob.rectTransform; ts.onColor = Gold;
        ts.onChange = v => { Click(); onChange(v); };
        return ts;
    }

    void RefreshSettingsUI()
    {
        if (!volSlider) return;
        volSlider.SetValueWithoutNotify(GameSettings.Volume); volValue.text = Mathf.RoundToInt(GameSettings.Volume * 100) + "%";
        sensSlider.SetValueWithoutNotify(GameSettings.Sensitivity); sensValue.text = GameSettings.Sensitivity.ToString("0.0") + "x";
        for (int i = 0; i < 3; i++)
        {
            bool on = i == GameSettings.Quality;
            qualityBtns[i].color = on ? Gold : new Color(1, 1, 1, 0.04f);
            qualityLbl[i].color = on ? Navy : Color.white;
        }
        hintsToggle.Set(GameSettings.ShowHints, false);
        if (fullToggle) fullToggle.Set(GameSettings.Fullscreen, false);
    }

    // ------------------------------------------------------------- CÓMO JUGAR
    RectTransform BuildHowCard(Transform p)
    {
        var c = Card(p, "HowCard", MR, new Vector2(-120, 10), new Vector2(780, 760));
        CardHeader(c, "02  /  INSTRUCCIONES", "CÓMO JUGAR");
        Txt(c, "OBJETIVO", 19, Gold, TL, TL, TL, new Vector2(56, -180), new Vector2(660, 28), TextAlignmentOptions.TopLeft, FontStyles.Bold).characterSpacing = 5;
        Txt(c, "Recorre el banco, entra a la <b>Zona TI</b>, siéntate en la estación TI-01 y revisa tu correo. " +
               "Detecta el correo de phishing y decide bien: <color=#7CFFA8>repórtalo</color> o descubre qué pasa si abres el adjunto.",
            23, Soft, TL, TL, TL, new Vector2(56, -212), new Vector2(670, 110));

        Txt(c, "CONTROLES", 19, Gold, TL, TL, TL, new Vector2(56, -340), new Vector2(660, 28), TextAlignmentOptions.TopLeft, FontStyles.Bold).characterSpacing = 5;
        var rows = new (string key, string pc, string mobile)[]
        {
            ("W A S D", "Caminar", "Joystick izquierdo"),
            ("CLIC DER.", "Mantener y mover para mirar", "Arrastrar a la derecha"),
            ("E", "Abrir puertas / sentarse", "Botón en pantalla"),
            ("ESC", "Pausa", "Botón II"),
        };
        Txt(c, "ORDENADOR", 16, new Color(1, 1, 1, 0.45f), TL, TL, TL, new Vector2(240, -374), new Vector2(240, 24), TextAlignmentOptions.TopLeft, FontStyles.Bold);
        Txt(c, "MÓVIL", 16, new Color(1, 1, 1, 0.45f), TL, TL, TL, new Vector2(500, -374), new Vector2(240, 24), TextAlignmentOptions.TopLeft, FontStyles.Bold);
        for (int i = 0; i < rows.Length; i++)
        {
            float y = -400 - i * 60;
            var key = Img(c, "Key", new Color(1, 1, 1, 0.08f), TL, TL, TL, new Vector2(56, y), new Vector2(160, 48));
            var o = key.gameObject.AddComponent<Outline>(); o.effectColor = new Color(Cyan.r, Cyan.g, Cyan.b, 0.5f);
            Txt(key.transform, rows[i].key, 20, Cyan, Vector2.zero, Vector2.one, C, Vector2.zero, Vector2.zero, TextAlignmentOptions.Center, FontStyles.Bold);
            Txt(c, rows[i].pc, 21, Color.white, TL, TL, TL, new Vector2(240, y - 4), new Vector2(250, 56));
            Txt(c, rows[i].mobile, 21, Soft, TL, TL, TL, new Vector2(500, y - 4), new Vector2(240, 56));
        }
        FlatButton(c, "VOLVER", BR, new Vector2(-56, 50), new Vector2(220, 64), true, CloseCard);
        return c;
    }

    // -------------------------------------------------------------- ACERCA DE
    RectTransform BuildAboutCard(Transform p)
    {
        var c = Card(p, "AboutCard", MR, new Vector2(-120, 10), new Vector2(780, 760));
        CardHeader(c, "04  /  INFORMACIÓN", "ACERCA DE");
        Txt(c, "<b>CyberSim · Banco Andino</b> es un simulador educativo para aprender a reconocer correos de " +
               "<color=#F7CC5C>phishing</color> y a entender cómo un <color=#FF7A70>malware</color> puede propagarse por toda la red de una entidad financiera " +
               "a partir de un solo clic.\n\n" +
               "La experiencia recorre un hall de ventanillas, las oficinas administrativas y la Zona TI, donde ocurre el incidente.",
            24, Soft, TL, TL, TL, new Vector2(56, -185), new Vector2(670, 300));
        var warn = Img(c, "Notice", new Color(1f, 0.8f, 0.36f, 0.08f), TL, TL, TL, new Vector2(56, -480), new Vector2(668, 110));
        Img(warn.transform, "Bar", Gold, BL, TL, ML, Vector2.zero, new Vector2(4, 0));
        Txt(warn.transform, "Todo el contenido es <b>ficticio</b>. Ningún malware real se ejecuta: el ataque es solo una animación con fines didácticos.",
            21, Color.white, Vector2.zero, Vector2.one, C, new Vector2(10, 0), new Vector2(-50, -20), TextAlignmentOptions.MidlineLeft);
        string[] badges = { "UNITY 6", "URP", "PC Y ANDROID" };
        float x = 56;
        foreach (var b in badges)
        {
            float w = b.Length * 14f + 40f;
            var im = Img(c, "Badge", new Color(Cyan.r, Cyan.g, Cyan.b, 0.12f), TL, TL, TL, new Vector2(x, -600), new Vector2(w, 40));
            Txt(im.transform, b, 17, Cyan, Vector2.zero, Vector2.one, C, Vector2.zero, Vector2.zero, TextAlignmentOptions.Center, FontStyles.Bold).characterSpacing = 3;
            x += w + 12f;
        }
        FlatButton(c, "VOLVER", BR, new Vector2(-56, 50), new Vector2(220, 64), true, CloseCard);
        return c;
    }

    // ---------------------------------------------------------------- TARJETAS
    void ToggleCard(RectTransform card, bool fromPause)
    {
        if (openCard == card) { CloseCard(); return; }
        HideCard(true);
        openCard = card; cardFromPause = fromPause;
        card.gameObject.SetActive(true);
        // En la pausa la tarjeta va centrada; en el menú, a la derecha
        Vector2 a = fromPause ? C : MR;
        card.anchorMin = card.anchorMax = card.pivot = a;
        card.anchoredPosition = fromPause ? Vector2.zero : new Vector2(-120, 10);
        if (fromPause) pauseGroup.alpha = 0f;
        for (int i = 0; i < menuButtons.Length; i++)
            menuButtons[i].selected = !fromPause && ((i == 1 && card == howCard) || (i == 2 && card == settingsCard) || (i == 3 && card == aboutCard));
        RefreshSettingsUI();
        StartCoroutine(CardIn(card));
    }

    IEnumerator CardIn(RectTransform card)
    {
        var g = card.GetComponent<CanvasGroup>();
        if (!g) g = card.gameObject.AddComponent<CanvasGroup>();
        Vector2 p = card.anchoredPosition, from = p + new Vector2(40f, 0f);
        for (float t = 0; t < 0.3f; t += Time.unscaledDeltaTime)
        {
            float k = 1f - Mathf.Pow(1f - t / 0.3f, 3f);
            g.alpha = k; card.anchoredPosition = Vector2.Lerp(from, p, k);
            yield return null;
        }
        g.alpha = 1f; card.anchoredPosition = p;
    }

    void CloseCard()
    {
        bool fromPause = cardFromPause;
        HideCard(false);
        if (fromPause && state == State.Paused) pauseGroup.alpha = 1f;
    }

    void HideCard(bool silent)
    {
        if (openCard == settingsCard || !silent) GameSettings.Save();
        if (openCard) openCard.gameObject.SetActive(false);
        openCard = null;
        if (menuButtons != null) foreach (var b in menuButtons) b.selected = false;
    }
}

/// Botón del menú: barra lateral que crece, fondo con degradado y texto que se desplaza al pasar el cursor.
public class MenuButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    public RectTransform bar;
    public TextMeshProUGUI label, index;
    public Image hover;
    public bool primary, selected;
    public Action onClick, onHover;
    bool over; float h;
    static readonly Color Gold = new Color(0.97f, 0.80f, 0.36f);

    public void OnPointerEnter(PointerEventData e) { over = true; onHover?.Invoke(); }
    public void OnPointerExit(PointerEventData e) { over = false; }
    public void OnPointerClick(PointerEventData e) { onClick?.Invoke(); }

    void Update()
    {
        h = Mathf.MoveTowards(h, over || selected ? 1f : 0f, Time.unscaledDeltaTime * 6f);
        float k = h * h * (3f - 2f * h);
        bar.sizeDelta = new Vector2(Mathf.Lerp(3f, 6f, k), Mathf.Lerp(30f, 72f, k));
        bar.GetComponent<Image>().color = Color.Lerp(primary ? Gold : new Color(1, 1, 1, 0.35f), Gold, k);
        label.rectTransform.anchoredPosition = new Vector2(78f + 18f * k, 0f);
        index.rectTransform.anchoredPosition = new Vector2(26f + 6f * k, 0f);
        label.color = Color.Lerp(primary ? Color.white : new Color(0.70f, 0.77f, 0.88f), primary ? Gold : Color.white, k);
        var c = hover.color; c.a = k; hover.color = c;
    }
}

/// Interruptor deslizante (sí/no) con animación.
public class ToggleSwitch : MonoBehaviour, IPointerClickHandler
{
    public Image bg;
    public RectTransform knob;
    public Color onColor;
    public Action<bool> onChange;
    public bool On { get; private set; }
    float k;

    public void Set(bool v, bool notify) { On = v; if (notify) onChange?.Invoke(v); }
    public void OnPointerClick(PointerEventData e) { Set(!On, true); }

    void Update()
    {
        k = Mathf.MoveTowards(k, On ? 1f : 0f, Time.unscaledDeltaTime * 8f);
        knob.anchoredPosition = new Vector2(Mathf.Lerp(23f, 73f, k), 0f);
        bg.color = Color.Lerp(new Color(1, 1, 1, 0.15f), onColor, k);
    }
}
