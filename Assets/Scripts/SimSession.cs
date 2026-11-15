using UnityEngine;

public enum SimMode { None, Attack, Defense }

/// Estado de la jornada en curso (modo, tiempos, señales y resultado) para la lógica y el reporte de auditoría.
public static class SimSession
{
    public static SimMode Mode = SimMode.None;
    /// Si no es None, al recargar la escena el jugador aparece sentado y entra directo en ese modo ("Probar el otro modo").
    public static SimMode QuickRestartMode = SimMode.None;

    public static float TimerStart, EndTime;
    public static int SignalsFound;
    public static bool Infected, Reported, TimedOut;
    public static string Vector = "";        // "enlace" o "adjunto" (cómo se produjo la infección)

    public static void Begin(SimMode mode)
    {
        Mode = mode; TimerStart = Time.time; EndTime = 0f;
        SignalsFound = 0; Infected = Reported = TimedOut = false; Vector = "";
    }

    public static void Finish() { if (EndTime <= 0f) EndTime = Time.time; }

    public static float Elapsed => (EndTime > 0f ? EndTime : Time.time) - TimerStart;

    public static string Format(float seconds)
    {
        int s = Mathf.Max(0, Mathf.CeilToInt(seconds));
        return $"{s / 60:00}:{s % 60:00}";
    }
}
