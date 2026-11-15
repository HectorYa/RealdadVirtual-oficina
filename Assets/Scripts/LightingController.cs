using TMPro;
using UnityEngine;

/// Pasa el laboratorio de iluminación normal a alerta roja (luces, LEDs, pantallas, balizas).
public class LightingController : MonoBehaviour
{
    public Renderer[] strips, screens, leds;
    public Material stripAlert, screenAlert, ledAlert;
    public Light[] lights;
    public Transform[] beacons;
    public TMP_Text wallStatus;

    Color[] baseCol; float[] baseInt;
    Color baseAmbient;
    float blend, flicker;
    bool alert;
    Material stripInst, screenInst;

    void Start()
    {
        baseCol = new Color[lights.Length]; baseInt = new float[lights.Length];
        for (int i = 0; i < lights.Length; i++) { baseCol[i] = lights[i].color; baseInt[i] = lights[i].intensity; }
        baseAmbient = RenderSettings.ambientLight;
        foreach (var b in beacons) b.gameObject.SetActive(false);
    }

    public void SetAlert(bool on)
    {
        alert = on;
        if (!on) return;
        stripInst = new Material(stripAlert); screenInst = new Material(screenAlert);
        foreach (var r in strips) r.sharedMaterial = stripInst;
        foreach (var r in screens) r.sharedMaterial = screenInst;
        foreach (var r in leds) r.sharedMaterial = ledAlert;
        foreach (var b in beacons) b.gameObject.SetActive(true);
        flicker = 1.4f;
        if (wallStatus) { wallStatus.text = "ALERTA: RED COMPROMETIDA"; wallStatus.color = new Color(1f, 0.15f, 0.15f); }
    }

    void Update()
    {
        blend = Mathf.MoveTowards(blend, alert ? 1f : 0f, Time.deltaTime / 1.5f);
        if (blend <= 0f) return;
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 5f);
        bool off = false;
        if (flicker > 0f) { flicker -= Time.deltaTime; off = Random.value < 0.5f; }
        for (int i = 0; i < lights.Length; i++)
        {
            lights[i].color = Color.Lerp(baseCol[i], new Color(1f, 0.05f, 0.05f), blend);
            lights[i].intensity = off ? 0.1f : Mathf.Lerp(baseInt[i], baseInt[i] * (0.7f + 0.9f * pulse), blend);
        }
        RenderSettings.ambientLight = Color.Lerp(baseAmbient, new Color(0.22f, 0.02f, 0.02f), blend);
        if (stripInst) stripInst.SetColor("_EmissionColor", new Color(1f, 0.05f, 0.05f) * (1.2f + 2.5f * pulse));
        if (screenInst) screenInst.SetColor("_EmissionColor", new Color(1f, 0.05f, 0.05f) * (0.8f + 1.2f * pulse));
        foreach (var b in beacons) b.Rotate(0, 240f * Time.deltaTime, 0, Space.World);
    }
}
