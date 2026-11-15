using UnityEngine;

/// Genera por código los sonidos (aviso, glitch, sirena): no necesita archivos de audio.
public class SynthAudio : MonoBehaviour
{
    AudioSource fx, loop;
    AudioClip ding, noise, siren, blip, door;

    void Awake()
    {
        fx = gameObject.AddComponent<AudioSource>(); fx.spatialBlend = 0f;
        loop = gameObject.AddComponent<AudioSource>(); loop.spatialBlend = 0f; loop.loop = true; loop.volume = 0.5f;
        ding = Tone(new[] { 880f, 1320f }, 0.6f);
        noise = Noise(0.9f);
        siren = Siren(2f);
        blip = Tone(new[] { 330f, 495f }, 0.3f);
        door = DoorClip(1.4f);
    }

    public void Notify() { fx.PlayOneShot(ding); }
    public void Blip() { fx.PlayOneShot(blip, 0.45f); }
    public void Door() { fx.PlayOneShot(door, 0.8f); }
    public void Glitch() { fx.PlayOneShot(noise, 0.7f); }
    public void StartAlarm() { loop.clip = siren; loop.Play(); }
    public void StopAlarm() { loop.Stop(); }

    static AudioClip Tone(float[] freqs, float dur)
    {
        int sr = 44100; var d = new float[(int)(sr * dur)];
        for (int i = 0; i < d.Length; i++)
        {
            float t = i / (float)sr, s = 0;
            foreach (var f in freqs) s += Mathf.Sin(2 * Mathf.PI * f * t);
            d[i] = s / freqs.Length * Mathf.Exp(-t * 6f) * 0.6f;
        }
        var c = AudioClip.Create("ding", d.Length, 1, sr, false); c.SetData(d, 0); return c;
    }

    static AudioClip Noise(float dur)
    {
        int sr = 44100; var d = new float[(int)(sr * dur)];
        for (int i = 0; i < d.Length; i++)
        {
            float t = i / (float)sr;
            d[i] = (Random.value * 2f - 1f) * 0.5f * (1f - t / dur) * (Mathf.Sin(t * 60f) > 0 ? 1f : 0.3f);
        }
        var c = AudioClip.Create("noise", d.Length, 1, sr, false); c.SetData(d, 0); return c;
    }

    // Bip doble del lector de tarjetas + soplido grave de la puerta al girar
    static AudioClip DoorClip(float dur)
    {
        int sr = 44100; var d = new float[(int)(sr * dur)]; float lp = 0f;
        for (int i = 0; i < d.Length; i++)
        {
            float t = i / (float)sr, s = 0f;
            if (t < 0.12f) s += Mathf.Sin(2 * Mathf.PI * 1000f * t) * 0.35f;
            else if (t > 0.16f && t < 0.28f) s += Mathf.Sin(2 * Mathf.PI * 1500f * t) * 0.35f;
            if (t > 0.35f)
            {
                float k = (t - 0.35f) / (dur - 0.35f);
                lp += 0.04f * ((Random.value * 2f - 1f) - lp);
                s += lp * 2.2f * Mathf.Sin(Mathf.PI * k);
            }
            d[i] = s;
        }
        var c = AudioClip.Create("door", d.Length, 1, sr, false); c.SetData(d, 0); return c;
    }

    static AudioClip Siren(float dur)
    {
        int sr = 44100; var d = new float[(int)(sr * dur)]; float phase = 0;
        for (int i = 0; i < d.Length; i++)
        {
            float t = i / (float)sr;
            float f = 800f + 250f * Mathf.Sin(2 * Mathf.PI * t / dur);
            phase += 2 * Mathf.PI * f / sr;
            d[i] = Mathf.Sin(phase) * 0.35f;
        }
        var c = AudioClip.Create("siren", d.Length, 1, sr, false); c.SetData(d, 0); return c;
    }
}
