#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// Menú: Cyber Sim > 3 - Configurar app / 4 - Compilar Windows / 5 - Compilar Android (APK)
public static class CyberSimBuildTools
{
    public const string ProductName = "CyberSim Banco Andino";
    public const string CompanyName = "CyberSim";
    public const string Version = "1.0.0";
    public const string AndroidId = "com.cybersim.bancoandino";
    const string ScenePath = "Assets/Scenes/CyberSim_Lab.unity";
    const string IconPath = "Assets/Branding/AppIcon.png";
    const string LogoPath = "Assets/Branding/SplashLogo.png";
    static readonly Color Navy = new Color(0.02f, 0.045f, 0.10f);

    // ------------------------------------------------------------- MARCA
    [MenuItem("Cyber Sim/3 - Configurar app (icono, splash, versión)")]
    public static void ApplyBranding()
    {
        Directory.CreateDirectory("Assets/Branding");
        WriteIcon(IconPath, 1024, true);
        WriteIcon(LogoPath, 512, false);
        AssetDatabase.ImportAsset(IconPath); AssetDatabase.ImportAsset(LogoPath);
        var li = (TextureImporter)AssetImporter.GetAtPath(LogoPath);
        li.textureType = TextureImporterType.Sprite; li.alphaIsTransparency = true; li.SaveAndReimport();
        var ii = (TextureImporter)AssetImporter.GetAtPath(IconPath);
        ii.textureType = TextureImporterType.Default; ii.alphaIsTransparency = true; ii.mipmapEnabled = false; ii.SaveAndReimport();

        PlayerSettings.productName = ProductName;
        PlayerSettings.companyName = CompanyName;
        PlayerSettings.bundleVersion = Version;
        PlayerSettings.Android.bundleVersionCode = 1;
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, AndroidId);
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, AndroidId);

        var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
        PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);

        // Pantalla de carga: fondo azul marino con el escudo (Unity 6 permite ocultar el logo de Unity)
        var logo = AssetDatabase.LoadAssetAtPath<Sprite>(LogoPath);
        PlayerSettings.SplashScreen.show = true;
        PlayerSettings.SplashScreen.showUnityLogo = false;
        PlayerSettings.SplashScreen.backgroundColor = Navy;
        PlayerSettings.SplashScreen.unityLogoStyle = PlayerSettings.SplashScreen.UnityLogoStyle.LightOnDark;
        PlayerSettings.SplashScreen.drawMode = PlayerSettings.SplashScreen.DrawMode.AllSequential;
        if (logo) PlayerSettings.SplashScreen.logos = new[] { PlayerSettings.SplashScreenLogo.Create(2.5f, logo) };

        // Ventana de PC
        PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
        PlayerSettings.defaultScreenWidth = 1920; PlayerSettings.defaultScreenHeight = 1080;
        AssetDatabase.SaveAssets();
        Debug.Log($"Cyber Sim: app configurada ({ProductName} v{Version}, icono y splash).");
    }

    // Escudo dorado con un candado: icono de la app (fondo azul) y logo del splash (fondo transparente)
    static void WriteIcon(string path, int n, bool background)
    {
        var t = new Texture2D(n, n, TextureFormat.RGBA32, false);
        var gold = new Color(0.97f, 0.8f, 0.36f);
        var inner = new Color(0.05f, 0.14f, 0.28f);
        for (int py = 0; py < n; py++)
            for (int px = 0; px < n; px++)
            {
                float u = (px + 0.5f) / n * 2f - 1f;          // -1..1
                float v = 1f - (py + 0.5f) / n;               // 0 arriba .. 1 abajo
                Color c = background ? Color.Lerp(new Color(0.03f, 0.07f, 0.15f), new Color(0.06f, 0.2f, 0.32f), 1f - v) : Color.clear;
                float outer = Shield(u, v, 0f), core = Shield(u, v, 0.07f);
                if (outer > 0f) c = gold;
                if (core > 0f) c = Color.Lerp(inner, new Color(0.08f, 0.3f, 0.42f), 1f - v);
                // Candado
                bool body = Mathf.Abs(u) < 0.26f && v > 0.47f && v < 0.72f;
                float dr = Mathf.Sqrt(u * u + (v - 0.47f) * (v - 0.47f) * 1.0f);
                bool shackle = v < 0.47f && dr < 0.2f && dr > 0.12f;
                bool hole = Mathf.Abs(u) < 0.035f && v > 0.55f && v < 0.65f;
                if ((body || shackle) && core > 0f) c = hole ? inner : gold;
                t.SetPixel(px, py, c);
            }
        t.Apply();
        File.WriteAllBytes(path, t.EncodeToPNG());
        Object.DestroyImmediate(t);
    }

    // >0 dentro del escudo (margen m hacia dentro)
    static float Shield(float u, float v, float m)
    {
        float top = 0.1f + m, mid = 0.55f, tip = 0.93f - m * 1.3f, half = 0.74f - m;
        if (v < top || v > tip) return -1f;
        float w = v < mid ? half : half * Mathf.Pow(Mathf.Max(0f, 1f - (v - mid) / (tip - mid)), 0.85f);
        return w - Mathf.Abs(u);
    }

    // ---------------------------------------------------------- COMPILAR
    [MenuItem("Cyber Sim/4 - Compilar Windows")]
    public static void BuildWindows()
    {
        ApplyBranding();
        Build(BuildTarget.StandaloneWindows64, "Builds/Windows/CyberSim_BancoAndino.exe");
    }

    [MenuItem("Cyber Sim/5 - Compilar Android (APK)")]
    public static void BuildAndroid()
    {
        if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
        {
            EditorUtility.DisplayDialog("Android", "El módulo Android no está instalado. Instálalo desde Unity Hub (Android Build Support + SDK/NDK + OpenJDK).", "OK");
            return;
        }
        ApplyBranding();
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel25;
        EditorUserBuildSettings.buildAppBundle = false;
        Build(BuildTarget.Android, "Builds/Android/CyberSim_BancoAndino.apk");
    }

    static void Build(BuildTarget target, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { ScenePath }, locationPathName = path, target = target, options = BuildOptions.None,
        });
        var s = report.summary;
        string msg = $"Cyber Sim: compilación {target} -> {s.result} · {s.totalSize / (1024f * 1024f):0.0} MB · {s.totalTime:mm\\:ss} · {path}";
        if (s.result == BuildResult.Succeeded) Debug.Log(msg); else Debug.LogError(msg + $" ({s.totalErrors} errores)");
    }
}
#endif
