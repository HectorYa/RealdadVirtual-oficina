using UnityEngine;
using UnityEngine.Rendering.Universal;

/// Preferencias del jugador (se guardan en el dispositivo con PlayerPrefs).
public static class GameSettings
{
    public const float BaseLookSensitivity = 0.12f;

    public static float Volume = 0.8f;        // 0..1
    public static float Sensitivity = 1f;     // 0.5..2 (multiplicador)
    public static int Quality = 2;            // 0 baja, 1 media, 2 alta
    public static bool ShowHints = true;
    public static bool VoiceGuide = true;      // asistente de voz + subtítulos
    public static bool Fullscreen = true;

    public static void Load()
    {
        Volume = PlayerPrefs.GetFloat("cs_volume", 0.8f);
        Sensitivity = PlayerPrefs.GetFloat("cs_sens", 1f);
        Quality = PlayerPrefs.GetInt("cs_quality", Application.isMobilePlatform ? 1 : 2);
        ShowHints = PlayerPrefs.GetInt("cs_hints", 1) == 1;
        VoiceGuide = PlayerPrefs.GetInt("cs_voice", 1) == 1;
        Fullscreen = PlayerPrefs.GetInt("cs_full", Screen.fullScreen ? 1 : 0) == 1;
    }

    public static void Save()
    {
        PlayerPrefs.SetFloat("cs_volume", Volume);
        PlayerPrefs.SetFloat("cs_sens", Sensitivity);
        PlayerPrefs.SetInt("cs_quality", Quality);
        PlayerPrefs.SetInt("cs_hints", ShowHints ? 1 : 0);
        PlayerPrefs.SetInt("cs_voice", VoiceGuide ? 1 : 0);
        PlayerPrefs.SetInt("cs_full", Fullscreen ? 1 : 0);
        PlayerPrefs.Save();
    }

    public static void ResetDefaults()
    {
        Volume = 0.8f; Sensitivity = 1f; Quality = Application.isMobilePlatform ? 1 : 2;
        ShowHints = true; VoiceGuide = true; Fullscreen = true;
    }

    public static void Apply(PlayerController player, OverlayUI overlay)
    {
        AudioListener.volume = Volume;
        if (player)
        {
            player.lookSensitivity = BaseLookSensitivity * Sensitivity;
            if (player.cam) ApplyCamera(player.cam.GetComponent<Camera>());
        }
        if (overlay) overlay.ShowHints = ShowHints;
        foreach (var c in Camera.allCameras) ApplyCamera(c);
        if (!Application.isMobilePlatform && Screen.fullScreen != Fullscreen) Screen.fullScreen = Fullscreen;
    }

    /// Calidad: baja = sin post-procesado; media = post-procesado; alta = post-procesado + antialiasing.
    public static void ApplyCamera(Camera c)
    {
        if (!c) return;
        var d = c.GetComponent<UniversalAdditionalCameraData>();
        if (!d) d = c.gameObject.AddComponent<UniversalAdditionalCameraData>();
        d.renderPostProcessing = Quality > 0;
        d.antialiasing = Quality >= 2 ? AntialiasingMode.FastApproximateAntialiasing : AntialiasingMode.None;
    }
}
