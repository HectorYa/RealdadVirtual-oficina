using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// Asistente de voz por megáfono: reproduce frases grabadas (Resources/Asistente/{id}) con su subtítulo,
/// encola para no pisarse, toca el aviso "ding-dong" tras un silencio y repite recordatorios
/// si el jugador no avanza (frases de "inactividad" del paso actual + datos curiosos).
public class VoiceGuide : MonoBehaviour
{
    public static VoiceGuide Instance { get; private set; }

    const string Folder = "Asistente/";
    const float ChimeAfterSilence = 12f;       // aviso de megáfono si llevaba este tiempo callada
    const float GapBetweenLines = 0.35f;
    const float SquelchLead = 1.0f;            // la estática empieza 1 s antes de que termine la frase
    const float SquelchVolume = 0.22f;
    bool squelchFired;

    AudioSource src, fx;
    AudioClip chime, squelch;
    readonly Dictionary<string, string> lines = new Dictionary<string, string>();
    readonly Queue<string> queue = new Queue<string>();
    float nextStart, lastSpeechEnd = -999f, subtitleUntil;
    bool waitingChime;
    string current;

    // Recordatorios del paso actual
    string[] idleIds;
    int idleIndex;
    float idleFirst, idleRepeat, idleTimer;
    bool idleFired;

    static readonly string[] Facts = { "dato_1", "dato_2", "dato_3", "dato_4", "dato_5" };
    readonly List<string> factBag = new List<string>();

    GameObject subPanel;
    TextMeshProUGUI subText;
    CanvasGroup subGroup;

    void Awake()
    {
        Instance = this;
        src = gameObject.AddComponent<AudioSource>();
        src.spatialBlend = 0f; src.playOnAwake = false; src.volume = 1f;
        fx = gameObject.AddComponent<AudioSource>();
        fx.spatialBlend = 0f; fx.playOnAwake = false;
        chime = BuildChime();
        squelch = BuildSquelch();
        var ta = Resources.Load<TextAsset>(Folder + "guion");
        if (ta)
            foreach (var raw in ta.text.Split('\n'))
            {
                var l = raw.Trim();
                if (l.Length == 0 || l[0] == '#') continue;
                int i = l.IndexOf('|');
                if (i > 0) lines[l.Substring(0, i)] = l.Substring(i + 1);
            }
        BuildSubtitles();
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    // ------------------------------------------------------------------ API
    /// Dice una frase. interrupt: corta lo que suene y vacía la cola (eventos importantes).
    public void Say(string id, bool interrupt = false)
    {
        if (interrupt) { queue.Clear(); src.Stop(); fx.Stop(); current = null; waitingChime = false; }
        queue.Enqueue(id);
        idleTimer = 0f;
    }

    /// Recordatorios para el paso actual: primero tras 'first' s sin avanzar, luego cada 'repeat' s.
    /// withFacts intercala datos curiosos entre los recordatorios.
    public void SetIdle(float first, float repeat, bool withFacts, params string[] ids)
    {
        var list = new List<string>();
        foreach (var id in ids)
        {
            list.Add(id);
            if (withFacts) list.Add("*fact");
        }
        idleIds = list.Count > 0 ? list.ToArray() : null;
        idleIndex = 0; idleFirst = first; idleRepeat = repeat; idleTimer = 0f; idleFired = false;
    }

    public void ClearIdle() { idleIds = null; }

    /// El jugador hizo algo útil: reinicia la cuenta del recordatorio.
    public void Poke() { idleTimer = 0f; }

    public bool Busy => src.isPlaying || fx.isPlaying || queue.Count > 0 || waitingChime;

    // --------------------------------------------------------------- BUCLE
    void Update()
    {
        if (Time.timeScale == 0f) return;   // juego en pausa: la voz queda congelada junto con el audio
        bool enabledVoice = GameSettings.VoiceGuide;
        if (!enabledVoice)
        {
            if (src.isPlaying) src.Stop();
            if (fx.isPlaying) fx.Stop();
            queue.Clear(); waitingChime = false; current = null;
            subGroup.alpha = 0f;
            return;
        }

        // Cierre de walkie-talkie: una estática suave entra 1 s antes del final de la frase
        if (current != null && src.isPlaying && !squelchFired && src.clip &&
            src.time >= src.clip.length - SquelchLead)
        {
            squelchFired = true;
            fx.PlayOneShot(squelch, SquelchVolume);
        }

        if (!src.isPlaying && current != null)
        {
            current = null; lastSpeechEnd = Time.time;
            nextStart = Time.time + (squelchFired ? squelch.length - SquelchLead : 0f) + GapBetweenLines;
            subtitleUntil = Time.time + 1.2f;
        }

        if (!src.isPlaying && current == null && Time.time >= nextStart)
        {
            if (waitingChime) PlayNext();
            else if (queue.Count > 0)
            {
                if (Time.time - lastSpeechEnd > ChimeAfterSilence)
                {
                    fx.PlayOneShot(chime, 0.6f);
                    waitingChime = true;
                    nextStart = Time.time + chime.length * 0.85f;
                }
                else PlayNext();
            }
        }

        // Recordatorios por inactividad (solo con el tiempo de juego corriendo)
        if (idleIds != null && !Busy)
        {
            idleTimer += Time.deltaTime;
            if (idleTimer >= (idleFired ? idleRepeat : idleFirst))
            {
                idleTimer = 0f; idleFired = true;
                string id = idleIds[idleIndex % idleIds.Length]; idleIndex++;
                if (id == "*fact") id = NextFact();
                queue.Enqueue(id);
            }
        }

        // Subtítulo
        float target = (current != null || Time.time < subtitleUntil) ? 1f : 0f;
        subGroup.alpha = Mathf.MoveTowards(subGroup.alpha, target, Time.unscaledDeltaTime * 4f);
        subPanel.SetActive(subGroup.alpha > 0.01f);
    }

    void PlayNext()
    {
        waitingChime = false;
        if (queue.Count == 0) return;
        string id = queue.Dequeue();
        var clip = Resources.Load<AudioClip>(Folder + id);
        lines.TryGetValue(id, out var text);
        if (!clip) { Debug.LogWarning("VoiceGuide: falta el audio " + id); return; }
        current = id; squelchFired = false;
        src.clip = clip; src.Play();
        subText.text = "<color=#F8CC5C><b>ASISTENTE ·</b></color> " + (text ?? "");
        subGroup.alpha = Mathf.Max(subGroup.alpha, 0.2f);
    }

    string NextFact()
    {
        if (factBag.Count == 0)
        {
            factBag.AddRange(Facts);
            for (int i = 0; i < factBag.Count; i++)
            {
                int j = Random.Range(i, factBag.Count);
                (factBag[i], factBag[j]) = (factBag[j], factBag[i]);
            }
        }
        string f = factBag[0]; factBag.RemoveAt(0);
        return f;
    }

    // ------------------------------------------------------------ SUBTÍTULOS
    void BuildSubtitles()
    {
        var cg = new GameObject("VoiceSubtitles", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
        cg.transform.SetParent(transform, false);
        var cv = cg.GetComponent<Canvas>(); cv.renderMode = RenderMode.ScreenSpaceOverlay; cv.sortingOrder = 9;
        var sc = cg.GetComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(1920, 1080); sc.matchWidthOrHeight = 0.5f;
        subGroup = cg.GetComponent<CanvasGroup>(); subGroup.alpha = 0f; subGroup.blocksRaycasts = false; subGroup.interactable = false;

        var p = UIKit.Panel(cg.transform, "Subtitle", 0, 0, 1240, 84, new Color(0.02f, 0.05f, 0.1f, 0.82f), false);
        p.anchorMin = p.anchorMax = new Vector2(0.5f, 0f); p.pivot = new Vector2(0.5f, 0f); p.anchoredPosition = new Vector2(0, 118);
        p.GetComponent<Image>().raycastTarget = false;
        var bar = UIKit.Panel(p, "Bar", 0, 0, 5, 84, new Color(0.97f, 0.8f, 0.36f), false);
        bar.GetComponent<Image>().raycastTarget = false;
        subText = UIKit.Label(p, "", 24, 8, 1196, 68, 26, Color.white, TextAlignmentOptions.MidlineLeft);
        subText.enableAutoSizing = true; subText.fontSizeMin = 16; subText.fontSizeMax = 26; subText.raycastTarget = false;
        subPanel = p.gameObject; subPanel.SetActive(false);
    }

    // Estática suave de walkie-talkie (1,2 s): crece despacio durante 1 s bajo el final de la voz
    // y se apaga en 0,2 s, sin golpe.
    static AudioClip BuildSquelch()
    {
        int sr = 44100; float dur = 1.2f; var d = new float[(int)(sr * dur)];
        float lp = 0f, lp2 = 0f, hpPrev = 0f, hpOut = 0f;
        var rng = new System.Random(4);
        for (int i = 0; i < d.Length; i++)
        {
            float t = i / (float)sr;
            float n = (float)(rng.NextDouble() * 2.0 - 1.0);
            lp += 0.35f * (n - lp); lp2 += 0.35f * (lp - lp2);             // pasa-bajos suave ~3 kHz
            hpOut = 0.9f * (hpOut + lp2 - hpPrev); hpPrev = lp2;            // pasa-altos ~700 Hz
            float env = t < 1.0f ? (t / 1.0f) * (t / 1.0f) : Mathf.Max(0f, (dur - t) / 0.2f);
            d[i] = hpOut * env * 1.4f;
        }
        var c = AudioClip.Create("squelch", d.Length, 1, sr, false); c.SetData(d, 0); return c;
    }

    // "Ding-dong" de megáfono de oficina
    static AudioClip BuildChime()
    {
        int sr = 44100; float dur = 1.4f; var d = new float[(int)(sr * dur)];
        for (int i = 0; i < d.Length; i++)
        {
            float t = i / (float)sr, s = 0f;
            if (t < 0.5f) s += (Mathf.Sin(2 * Mathf.PI * 784f * t) + 0.3f * Mathf.Sin(2 * Mathf.PI * 1568f * t)) * Mathf.Exp(-5f * t);
            if (t >= 0.45f) { float u = t - 0.45f; s += (Mathf.Sin(2 * Mathf.PI * 622f * u) + 0.3f * Mathf.Sin(2 * Mathf.PI * 1244f * u)) * Mathf.Exp(-4f * u); }
            d[i] = s * 0.3f;
        }
        var c = AudioClip.Create("chime", d.Length, 1, sr, false); c.SetData(d, 0); return c;
    }
}
