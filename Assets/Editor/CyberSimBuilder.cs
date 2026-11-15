#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using URP = UnityEngine.Rendering.Universal;

/// Menú: Cyber Sim > 1 - Construir escena  /  2 - Preparar para Android
/// Recorrido: 1) Hall de ventanillas (Z 0-18)  2) Pasillo de oficinas (Z 18-32)  3) Zona TI (Z 32-40)
public static class CyberSimBuilder
{
    const string ScenePath = "Assets/Scenes/CyberSim_Lab.unity";
    const float RoomW = 12f, HallL = 18f, CorrEnd = 32f, TIEnd = 40f, Ceil = 3.4f, CorrHalf = 1.2f;
    const float StationZ = 34.85f;     // centro del escritorio de la estación TI-01

    static Material mFloor, mFloorCorr, mWall, mMetal, mDark, mGlass, mWhite, mCyan, mGreen, mRed, mOrange, mScreen, mScreenAlert, mChair;
    static Material mCeil, mNavy, mGold, mSignGold, mWood, mCounter, mPlant, mPlantD, mPot, mFabric, mBoard, mDoor;
    static Material mCarpet, mRaised, mGrid, mFrosted, mPaper, mRedPaint, mWhiteboard, mLeather, mTeal, mBinder, mPostIt;
    static Material mCabinet, mVault, mTIDoor, mMapBg, mNodeBg, mNodeOk, mNodeBad, mLinkIdle, mScreenTV, mCold, mExit;
    static readonly Color SignTextColor = new Color(0.97f, 0.83f, 0.38f);
    static readonly Color Ink = new Color(0.1f, 0.12f, 0.16f);
    static readonly List<Renderer> strips = new List<Renderer>(), leds = new List<Renderer>(), screens = new List<Renderer>();
    static readonly List<Light> lights = new List<Light>();
    static readonly List<Transform> beacons = new List<Transform>();
    static TextMeshPro wallStatus;
    static System.Random rng;

    // ------------------------------------------------------------------ BUILD
    [MenuItem("Cyber Sim/1 - Construir escena")]
    public static void Build()
    {
        if (TMP_Settings.instance == null)
        {
            EditorUtility.DisplayDialog("TextMeshPro", "Importa primero: Window > TextMeshPro > Import TMP Essential Resources y vuelve a ejecutar.", "OK");
            return;
        }
        if (GraphicsSettings.currentRenderPipeline == null)
        {
            EditorUtility.DisplayDialog("URP", "Asigna un URP Asset en Project Settings > Graphics (y Quality) y vuelve a ejecutar.", "OK");
            return;
        }
        foreach (var d in new[] { "Materials", "Textures", "Scenes", "Settings" }) Directory.CreateDirectory("Assets/" + d);
        AssetDatabase.Refresh();
        strips.Clear(); leds.Clear(); screens.Clear(); lights.Clear(); beacons.Clear();
        rng = new System.Random(7);

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.36f, 0.37f, 0.40f);
        MakeMaterials();

        var env = new GameObject("BancoAndino").transform;
        BuildShell(env);

        var hall = Group(env, "1_HallVentanillas", Vector3.zero);
        BuildHallDecor(hall);
        BuildTellers(hall);
        BuildReception(hall);
        BuildPlatform(hall);
        BuildWaitingArea(hall);
        BuildLounge(hall);
        BuildAtm(hall);
        BuildVault(hall);
        BuildQueue(hall);
        BuildPlants(hall);

        var corr = Group(env, "2_PasilloOficinas", Vector3.zero);
        BuildCorridor(corr);
        BuildOffices(corr);

        var ti = Group(env, "3_ZonaTI", Vector3.zero);
        var door = BuildTIRoom(ti);
        BuildMainStation(ti, out var marker, out var seatPoint, out var screenAnchor);
        var map = BuildInfectionMap(ti);

        BuildBeacons(env);
        BuildPostFX();
        BuildPlayerAndManagers(seatPoint, screenAnchor, marker, door, map);

        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        Debug.Log("Cyber Sim: escena lista. Pulsa Play. Teclado: WASD, clic derecho para mirar, E para abrir la puerta / sentarte.");
    }

    [MenuItem("Cyber Sim/2 - Preparar para Android (móvil)")]
    public static void SetupAndroid()
    {
        PlayerSettings.productName = "CyberSim Banco";
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.demo.cybersim");
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel25;
        if (File.Exists(ScenePath)) EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        EditorUtility.DisplayDialog("Android",
            "Listo: orientación horizontal, IL2CPP, ARM64, API 25.\n\nAhora: File > Build Profiles (Build Settings) > Android > Switch Platform > Build And Run.\n(Necesitas el módulo Android instalado desde Unity Hub y la depuración USB activada en el móvil.)", "OK");
    }

    // -------------------------------------------------------------- MATERIALS
    static Material Mat(string n, System.Action<Material> setup)
    {
        string p = $"Assets/Materials/{n}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(p);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, p); }
        setup(m); EditorUtility.SetDirty(m); return m;
    }

    static Material Lit(string n, Color c, float metal, float smooth, Color emis = default, bool glass = false)
    {
        return Mat("Sim_" + n, m =>
        {
            m.SetColor("_BaseColor", c); m.SetFloat("_Metallic", metal); m.SetFloat("_Smoothness", smooth);
            if (emis != default)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emis);
                // Sin esto Unity borra el keyword _EMISSION al guardar el asset y la emisión no se renderiza
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                // Respaldo: color base = color de emisión (recortado). Si el keyword vuelve a perderse,
                // la superficie igual se ve iluminada en vez de negra.
                m.SetColor("_BaseColor", new Color(Mathf.Clamp01(emis.r), Mathf.Clamp01(emis.g), Mathf.Clamp01(emis.b), 1f));
            }
            if (glass)
            {
                m.SetFloat("_Surface", 1);
                m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_ZWrite", 0); m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.renderQueue = 3000; m.SetOverrideTag("RenderType", "Transparent");
            }
        });
    }

    static Texture2D Tex(string name, int n, System.Func<int, int, Color> f)
    {
        var t = new Texture2D(n, n);
        for (int y = 0; y < n; y++) for (int x = 0; x < n; x++) t.SetPixel(x, y, f(x, y));
        string path = $"Assets/Textures/{name}.png";
        File.WriteAllBytes(path, t.EncodeToPNG());
        Object.DestroyImmediate(t);
        AssetDatabase.ImportAsset(path);
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static void MakeMaterials()
    {
        // Suelo tipo mármol cálido del vestíbulo del banco
        var tile = Tex("TechTile", 128, (x, y) => (x < 3 || y < 3) ? new Color(0.66f, 0.65f, 0.62f) : new Color(0.90f, 0.89f, 0.86f));
        // Alfombra modular de oficina (gris azulado con ligera textura)
        var nr = new System.Random(3);
        var carpet = Tex("Carpet", 64, (x, y) =>
        {
            float k = 0.86f + 0.28f * (float)nr.NextDouble();
            if (x == 0 || y == 0) k *= 0.8f;
            return new Color(0.22f * k, 0.26f * k, 0.33f * k);
        });
        // Piso técnico elevado de la sala TI (placas de 60 cm)
        var raised = Tex("RaisedFloor", 64, (x, y) =>
        {
            if (x < 2 || y < 2) return new Color(0.32f, 0.33f, 0.35f);
            bool dot = (x % 8 == 4) && (y % 8 == 4);
            return dot ? new Color(0.6f, 0.61f, 0.63f) : new Color(0.74f, 0.75f, 0.77f);
        });

        mFloor = Lit("Floor", Color.white, 0.05f, 0.7f);
        mFloor.SetTexture("_BaseMap", tile); mFloor.SetTextureScale("_BaseMap", new Vector2(RoomW / 1.2f, 6f / 1.2f));
        mFloorCorr = Lit("FloorCorridor", Color.white, 0.05f, 0.7f);
        mFloorCorr.SetTexture("_BaseMap", tile); mFloorCorr.SetTextureScale("_BaseMap", new Vector2(2f, 14f / 1.2f));
        mCarpet = Lit("Carpet", Color.white, 0f, 0.08f);
        mCarpet.SetTexture("_BaseMap", carpet); mCarpet.SetTextureScale("_BaseMap", new Vector2(4.8f / 0.5f, 7f / 0.5f));
        mRaised = Lit("RaisedFloor", Color.white, 0.2f, 0.45f);
        mRaised.SetTexture("_BaseMap", raised); mRaised.SetTextureScale("_BaseMap", new Vector2(RoomW / 0.6f, 4f / 0.6f));

        mWall = Lit("Wall", new Color(0.90f, 0.88f, 0.84f), 0f, 0.25f);
        mCeil = Lit("Ceiling", new Color(0.94f, 0.94f, 0.95f), 0f, 0.04f);
        mGrid = Lit("CeilGrid", new Color(0.70f, 0.71f, 0.73f), 0.5f, 0.3f);
        mMetal = Lit("Metal", new Color(0.6f, 0.63f, 0.67f), 1f, 0.55f);
        mDark = Lit("Dark", new Color(0.05f, 0.06f, 0.07f), 0.6f, 0.5f);
        mGlass = Lit("Glass", new Color(0.6f, 0.8f, 0.9f, 0.12f), 0f, 0.95f, default, true);
        mFrosted = Lit("Frosted", new Color(0.95f, 0.97f, 1f, 0.6f), 0f, 0.6f, default, true);
        mWhite = Lit("EmitWhite", Color.black, 0, 0, new Color(1f, 0.95f, 0.86f) * 2.5f);
        mCold = Lit("EmitCold", Color.black, 0, 0, new Color(0.8f, 0.9f, 1f) * 2.5f);
        mCyan = Lit("EmitCyan", Color.black, 0, 0, new Color(0f, 0.9f, 1f) * 2.5f);
        mGreen = Lit("EmitGreen", Color.black, 0, 0, new Color(0f, 1f, 0.2f) * 3f);
        mRed = Lit("EmitRed", Color.black, 0, 0, new Color(1f, 0.1f, 0.1f) * 3f);
        mOrange = Lit("EmitOrange", Color.black, 0, 0, new Color(1f, 0.5f, 0.1f) * 2.5f);
        mExit = Lit("EmitExit", Color.black, 0, 0, new Color(0.1f, 0.9f, 0.35f) * 1.6f);
        mScreen = Lit("ScreenIdle", Color.black, 0, 0.8f, new Color(0.22f, 0.42f, 0.72f));
        mScreenTV = Lit("ScreenTV", Color.black, 0, 0.8f, new Color(0.06f, 0.16f, 0.36f));
        mScreenAlert = Lit("ScreenAlert", Color.black, 0, 0.8f, new Color(1f, 0.05f, 0.05f));
        mChair = Lit("Chair", new Color(0.1f, 0.1f, 0.12f), 0f, 0.2f);

        // Paleta Banco Andino
        mNavy = Lit("BankNavy", new Color(0.055f, 0.11f, 0.26f), 0.15f, 0.55f);
        mGold = Lit("BankGold", new Color(0.82f, 0.66f, 0.26f), 1f, 0.7f);
        mSignGold = Lit("SignGold", new Color(0.95f, 0.80f, 0.34f), 0.3f, 0.6f, new Color(0.5f, 0.38f, 0.10f));
        mWood = Lit("DeskWood", new Color(0.42f, 0.27f, 0.16f), 0f, 0.5f);
        mCounter = Lit("CounterTop", new Color(0.93f, 0.93f, 0.94f), 0f, 0.7f);
        mPlant = Lit("Plant", new Color(0.14f, 0.44f, 0.19f), 0f, 0.35f);
        mPlantD = Lit("PlantDark", new Color(0.09f, 0.32f, 0.14f), 0f, 0.35f);
        mPot = Lit("Pot", new Color(0.72f, 0.70f, 0.66f), 0f, 0.45f);
        mFabric = Lit("ChairFabric", new Color(0.15f, 0.21f, 0.34f), 0f, 0.3f);
        mBoard = Lit("InfoBoard", new Color(0.04f, 0.08f, 0.18f), 0.2f, 0.6f, new Color(0.03f, 0.07f, 0.16f));
        mDoor = Lit("Door", new Color(0.10f, 0.13f, 0.20f), 0.3f, 0.6f);

        // Oficinas, utilería y Zona TI
        mPaper = Lit("Paper", new Color(0.95f, 0.95f, 0.93f), 0f, 0.15f);
        mRedPaint = Lit("RedPaint", new Color(0.72f, 0.07f, 0.06f), 0.1f, 0.6f);
        mWhiteboard = Lit("Whiteboard", new Color(0.97f, 0.97f, 0.98f), 0f, 0.85f);
        mLeather = Lit("Leather", new Color(0.07f, 0.05f, 0.04f), 0f, 0.5f);
        mTeal = Lit("Teal", new Color(0.0f, 0.42f, 0.48f), 0f, 0.4f);
        mBinder = Lit("Binder", new Color(0.15f, 0.3f, 0.62f), 0f, 0.35f);
        mPostIt = Lit("PostIt", new Color(1f, 0.88f, 0.3f), 0f, 0.2f);
        mCabinet = Lit("Cabinet", new Color(0.52f, 0.54f, 0.57f), 0.4f, 0.4f);
        mVault = Lit("Vault", new Color(0.62f, 0.64f, 0.68f), 1f, 0.8f);
        mTIDoor = Lit("TIDoor", new Color(0.36f, 0.40f, 0.45f), 0.5f, 0.5f);
        mMapBg = Lit("MapBg", Color.black, 0f, 0.05f, new Color(0.01f, 0.03f, 0.06f));
        mNodeBg = Lit("NodeBg", Color.black, 0f, 0.05f, new Color(0.04f, 0.08f, 0.15f));
        mNodeOk = Lit("NodeOk", Color.black, 0f, 0.5f, new Color(0.1f, 1f, 0.4f) * 1.4f);
        mNodeBad = Lit("NodeBad", Color.black, 0f, 0.5f, new Color(1f, 0.08f, 0.05f) * 2.2f);
        mLinkIdle = Lit("LinkIdle", Color.black, 0f, 0.5f, new Color(0.1f, 0.55f, 0.8f) * 0.9f);
    }

    // ---------------------------------------------------------------- HELPERS
    static GameObject Prim(string n, Transform p, Vector3 pos, Vector3 sc, Material m,
        PrimitiveType t = PrimitiveType.Cube, Vector3 rot = default, bool col = false)
    {
        var g = GameObject.CreatePrimitive(t); g.name = n;
        g.transform.SetParent(p, false);
        g.transform.localPosition = pos; g.transform.localEulerAngles = rot; g.transform.localScale = sc;
        g.GetComponent<Renderer>().sharedMaterial = m;
        if (!col) Object.DestroyImmediate(g.GetComponent<Collider>());
        return g;
    }

    static Transform Group(Transform p, string n, Vector3 pos, float yaw = 0f)
    {
        var g = new GameObject(n).transform; g.SetParent(p, false);
        g.localPosition = pos; g.localEulerAngles = new Vector3(0f, yaw, 0f);
        return g;
    }

    static TextMeshPro T3(Transform p, string n, Vector3 pos, string s, float h, Color c, Vector2 box)
    {
        var g = new GameObject(n); g.transform.SetParent(p, false); g.transform.localPosition = pos;
        var t = g.AddComponent<TextMeshPro>();
        t.text = s; t.fontSize = h * 10f; t.color = c; t.alignment = TextAlignmentOptions.Center;
        t.rectTransform.sizeDelta = box;
        return t;
    }

    static void AutoSize(TextMeshPro t, float min)
    {
        t.enableAutoSizing = true; t.fontSizeMax = t.fontSize; t.fontSizeMin = min;
    }

    static Light LightAt(Transform p, Vector3 pos, Color c, float intensity, float range)
    {
        var g = new GameObject("Light"); g.transform.SetParent(p, false); g.transform.localPosition = pos;
        var l = g.AddComponent<Light>(); l.type = LightType.Point; l.color = c; l.intensity = intensity; l.range = range;
        l.shadows = LightShadows.None;
        return l;
    }

    // Muro macizo a lo largo de Z (en la X dada) o a lo largo de X (en la Z dada)
    static GameObject WallZ(Transform p, string n, float x, float z0, float z1, float y0, float y1, Material m, float t = 0.1f)
        => Prim(n, p, new Vector3(x, (y0 + y1) * 0.5f, (z0 + z1) * 0.5f), new Vector3(t, y1 - y0, z1 - z0), m, col: true);
    static GameObject WallX(Transform p, string n, float z, float x0, float x1, float y0, float y1, Material m, float t = 0.1f)
        => Prim(n, p, new Vector3((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, z), new Vector3(x1 - x0, y1 - y0, t), m, col: true);

    // Suelo + techo + retícula de falso techo. Se divide por zonas para que cada pieza reciba sus propias luces.
    static void FloorCeil(Transform p, string n, float x0, float x1, float z0, float z1, Material floor)
    {
        float cx = (x0 + x1) * 0.5f, cz = (z0 + z1) * 0.5f, w = x1 - x0, l = z1 - z0;
        Prim(n + "_Floor", p, new Vector3(cx, -0.05f, cz), new Vector3(w, 0.1f, l), floor, col: true);
        Prim(n + "_Ceil", p, new Vector3(cx, Ceil + 0.05f, cz), new Vector3(w, 0.1f, l), mCeil);
        for (float x = x0 + 1.2f; x < x1 - 0.1f; x += 1.2f)
            Prim("Grid", p, new Vector3(x, Ceil - 0.008f, cz), new Vector3(0.025f, 0.016f, l), mGrid);
        for (float z = z0 + 1.2f; z < z1 - 0.1f; z += 1.2f)
            Prim("Grid", p, new Vector3(cx, Ceil - 0.008f, z), new Vector3(w, 0.016f, 0.025f), mGrid);
    }

    // Letrero de zona: placa azul marino con borde dorado. El texto se lee desde el lado local -Z.
    // w <= 0 calcula el ancho según el título. yaw = 0 pared trasera / 180 pared frontal /
    // -90 cara +X (izquierda) / 90 cara -X (derecha).
    // hang = cuelga del techo con dos varillas doradas y lleva texto en ambas caras.
    static TextMeshPro Sign(Transform p, string n, Vector3 pos, float yaw, string title, float h, float w = 0f, bool hang = false)
    {
        if (w <= 0f) w = title.Length * 0.6f * h + 0.5f;
        float ph = Mathf.Max(0.40f, 0.75f * h + 0.24f);
        var g = new GameObject(n); g.transform.SetParent(p, false);
        g.transform.localPosition = pos; g.transform.localEulerAngles = new Vector3(0f, yaw, 0f);
        Prim("Plate", g.transform, Vector3.zero, new Vector3(w, ph, 0.06f), mNavy);
        Prim("TrimTop", g.transform, new Vector3(0f, ph * 0.5f, 0f), new Vector3(w, 0.05f, 0.07f), mGold);
        Prim("TrimBot", g.transform, new Vector3(0f, -ph * 0.5f, 0f), new Vector3(w, 0.05f, 0.07f), mGold);
        var front = T3(g.transform, "Title", new Vector3(0f, 0f, -0.045f), title, h, SignTextColor, new Vector2(w, ph));
        if (hang)
        {
            var back = T3(g.transform, "TitleB", new Vector3(0f, 0f, 0.045f), title, h, SignTextColor, new Vector2(w, ph));
            back.transform.localEulerAngles = new Vector3(0f, 180f, 0f);
            // Varillas hasta el techo (el letrero siempre cuelga de un padre con y = 0)
            float rodLen = Ceil - (pos.y + ph * 0.5f) - 0.02f;
            if (rodLen > 0.08f)
            {
                float yRod = ph * 0.5f + rodLen * 0.5f;
                float xr = Mathf.Max(0.12f, w * 0.5f - 0.2f);
                Prim("RodL", g.transform, new Vector3(-xr, yRod, 0f), new Vector3(0.03f, rodLen, 0.03f), mGold);
                Prim("RodR", g.transform, new Vector3(xr, yRod, 0f), new Vector3(0.03f, rodLen, 0.03f), mGold);
            }
        }
        return front;
    }

    // Afiche enmarcado con banda de color, título y texto. Se lee desde el lado local -Z.
    static Transform Poster(Transform p, string n, Vector3 pos, float yaw, string title, string body, float w, float h, Material accent)
    {
        var g = Group(p, n, pos, yaw);
        Prim("Frame", g, new Vector3(0f, 0f, 0.01f), new Vector3(w + 0.05f, h + 0.05f, 0.02f), mDark);
        Prim("Paper", g, new Vector3(0f, 0f, -0.002f), new Vector3(w, h, 0.01f), mPaper);
        const float bh = 0.2f;
        Prim("Band", g, new Vector3(0f, h * 0.5f - bh * 0.5f, -0.009f), new Vector3(w, bh, 0.006f), accent);
        var tt = T3(g, "Title", new Vector3(0f, h * 0.5f - bh * 0.5f, -0.014f), title, 0.055f, Color.white, new Vector2(w - 0.06f, bh - 0.02f));
        AutoSize(tt, 0.2f); tt.fontStyle = FontStyles.Bold;
        if (!string.IsNullOrEmpty(body))
        {
            var b = T3(g, "Body", new Vector3(0f, -bh * 0.5f, -0.012f), body, 0.042f, Ink, new Vector2(w - 0.1f, h - bh - 0.08f));
            b.alignment = TextAlignmentOptions.TopLeft; AutoSize(b, 0.16f);
        }
        return g;
    }

    static Transform Whiteboard(Transform p, string n, Vector3 pos, float yaw, float w, float h, string text)
    {
        var g = Group(p, n, pos, yaw);
        Prim("Frame", g, new Vector3(0f, 0f, 0.01f), new Vector3(w + 0.06f, h + 0.06f, 0.02f), mMetal);
        Prim("Board", g, new Vector3(0f, 0f, -0.002f), new Vector3(w, h, 0.01f), mWhiteboard);
        Prim("Tray", g, new Vector3(0f, -h * 0.5f - 0.04f, -0.03f), new Vector3(w * 0.6f, 0.03f, 0.06f), mMetal);
        Prim("MarkerA", g, new Vector3(-0.2f, -h * 0.5f - 0.015f, -0.04f), new Vector3(0.02f, 0.06f, 0.02f), mBinder, PrimitiveType.Cylinder, new Vector3(0, 0, 90));
        Prim("MarkerB", g, new Vector3(0.05f, -h * 0.5f - 0.015f, -0.04f), new Vector3(0.02f, 0.06f, 0.02f), mRedPaint, PrimitiveType.Cylinder, new Vector3(0, 0, 90));
        var t = T3(g, "Text", new Vector3(0f, 0f, -0.009f), text, 0.05f, new Color(0.1f, 0.2f, 0.55f), new Vector2(w - 0.15f, h - 0.12f));
        t.alignment = TextAlignmentOptions.TopLeft; AutoSize(t, 0.18f);
        return g;
    }

    // Televisor / pantalla de pared con texto. Frente hacia local -Z.
    static void Tv(Transform p, string n, Vector3 pos, float yaw, float w, float h, string text, Color tc)
    {
        var g = Group(p, n, pos, yaw);
        Prim("Body", g, Vector3.zero, new Vector3(w + 0.05f, h + 0.05f, 0.05f), mDark);
        screens.Add(Prim("Screen", g, new Vector3(0f, 0f, -0.027f), new Vector3(w, h, 0.005f), mScreenTV).GetComponent<Renderer>());
        if (text != null)
        {
            var t = T3(g, "Text", new Vector3(0f, 0f, -0.032f), text, h * 0.16f, tc, new Vector2(w * 0.92f, h * 0.9f));
            AutoSize(t, 0.2f);
        }
    }

    // Monitor de escritorio. El usuario está del lado local -Z.
    static void Monitor(Transform p, Vector3 basePos, float yaw, float w = 0.54f, float h = 0.32f)
    {
        var m = Group(p, "Monitor", basePos, yaw);
        Prim("Base", m, new Vector3(0f, 0.01f, 0.03f), new Vector3(0.22f, 0.02f, 0.16f), mDark);
        Prim("Neck", m, new Vector3(0f, 0.12f, 0.05f), new Vector3(0.05f, 0.22f, 0.03f), mDark);
        Prim("Body", m, new Vector3(0f, 0.12f + h * 0.5f, 0.02f), new Vector3(w, h, 0.03f), mDark);
        screens.Add(Prim("Screen", m, new Vector3(0f, 0.12f + h * 0.5f, 0.004f), new Vector3(w - 0.03f, h - 0.03f, 0.004f), mScreen).GetComponent<Renderer>());
    }

    // Escritorio de oficina completo. El usuario se sienta del lado local -Z mirando a +Z.
    static Transform OfficeDesk(Transform p, Vector3 pos, float yaw, Material top, int monitors = 1, Material chairM = null, float w = 1.4f)
    {
        var d = Group(p, "Escritorio", pos, yaw);
        Prim("Top", d, new Vector3(0f, 0.75f, 0f), new Vector3(w, 0.05f, 0.7f), top, col: true);
        Prim("SideL", d, new Vector3(-w * 0.5f + 0.03f, 0.37f, 0f), new Vector3(0.04f, 0.74f, 0.66f), mNavy);
        Prim("SideR", d, new Vector3(w * 0.5f - 0.03f, 0.37f, 0f), new Vector3(0.04f, 0.74f, 0.66f), mNavy);
        Prim("Modesty", d, new Vector3(0f, 0.45f, 0.32f), new Vector3(w - 0.1f, 0.5f, 0.03f), mNavy);
        Prim("Drawers", d, new Vector3(w * 0.5f - 0.3f, 0.4f, 0.02f), new Vector3(0.4f, 0.62f, 0.6f), mCabinet);
        if (monitors <= 1) Monitor(d, new Vector3(0f, 0.775f, 0.16f), 0f);
        else { Monitor(d, new Vector3(-0.3f, 0.775f, 0.14f), -15f); Monitor(d, new Vector3(0.3f, 0.775f, 0.14f), 15f); }
        Prim("Keyboard", d, new Vector3(0f, 0.785f, -0.12f), new Vector3(0.42f, 0.02f, 0.14f), mDark);
        Prim("Mouse", d, new Vector3(0.32f, 0.785f, -0.12f), new Vector3(0.06f, 0.02f, 0.1f), mDark);
        Prim("Papers", d, new Vector3(-w * 0.5f + 0.25f, 0.783f, -0.05f), new Vector3(0.21f, 0.015f, 0.29f), mPaper, rot: new Vector3(0f, 8f, 0f));
        Prim("Folder", d, new Vector3(-w * 0.5f + 0.27f, 0.795f, 0.0f), new Vector3(0.23f, 0.01f, 0.31f), mBinder, rot: new Vector3(0f, -5f, 0f));
        Prim("Mug", d, new Vector3(w * 0.5f - 0.15f, 0.82f, 0.12f), new Vector3(0.08f, 0.05f, 0.08f), mPaper, PrimitiveType.Cylinder);
        Chair(d, new Vector3(0f, 0f, -0.7f), 0f, chairM);
        return d;
    }

    static void Chair(Transform p, Vector3 pos, float yaw, Material m = null)
    {
        if (m == null) m = mChair;
        var c = new GameObject("Chair").transform; c.SetParent(p, false); c.localPosition = pos; c.localEulerAngles = new Vector3(0, yaw, 0);
        Prim("Seat", c, new Vector3(0, 0.48f, 0), new Vector3(0.45f, 0.06f, 0.45f), m);
        Prim("Back", c, new Vector3(0, 0.8f, -0.22f), new Vector3(0.45f, 0.55f, 0.06f), m);
        Prim("ArmL", c, new Vector3(-0.24f, 0.62f, -0.02f), new Vector3(0.04f, 0.04f, 0.32f), mDark);
        Prim("ArmR", c, new Vector3(0.24f, 0.62f, -0.02f), new Vector3(0.04f, 0.04f, 0.32f), mDark);
        Prim("Post", c, new Vector3(0, 0.25f, 0), new Vector3(0.05f, 0.23f, 0.05f), mDark, PrimitiveType.Cylinder);
        Prim("Base", c, new Vector3(0, 0.03f, 0), new Vector3(0.5f, 0.02f, 0.5f), mDark, PrimitiveType.Cylinder);
    }

    // Portátil abierto sobre un escritorio. El usuario se sienta del lado local +Z (yaw = 0).
    static void Laptop(Transform p, Vector3 basePos, float yaw)
    {
        var l = new GameObject("Laptop"); l.transform.SetParent(p, false);
        l.transform.localPosition = basePos; l.transform.localEulerAngles = new Vector3(0f, yaw, 0f);
        Prim("Base", l.transform, Vector3.zero, new Vector3(0.34f, 0.02f, 0.26f), mDark);
        Prim("Lid", l.transform, new Vector3(0f, 0.10f, -0.12f), new Vector3(0.34f, 0.20f, 0.02f), mDark, rot: new Vector3(-12f, 0f, 0f));
        Prim("Screen", l.transform, new Vector3(0f, 0.10f, -0.104f), new Vector3(0.30f, 0.16f, 0.01f), mScreen, rot: new Vector3(-12f, 0f, 0f));
    }

    static void FilingCabinet(Transform p, Vector3 pos, float yaw, int drawers = 4)
    {
        var c = Group(p, "Archivador", pos, yaw); float h = drawers * 0.32f;
        Prim("Body", c, new Vector3(0f, h * 0.5f, 0f), new Vector3(0.45f, h, 0.6f), mCabinet, col: true);
        for (int i = 0; i < drawers; i++)
        {
            float y = 0.16f + i * 0.32f;
            Prim("Gap", c, new Vector3(0f, y + 0.155f, -0.301f), new Vector3(0.43f, 0.01f, 0.004f), mDark);
            Prim("Handle", c, new Vector3(0f, y + 0.04f, -0.31f), new Vector3(0.14f, 0.025f, 0.02f), mMetal);
            Prim("Label", c, new Vector3(0f, y + 0.1f, -0.302f), new Vector3(0.1f, 0.05f, 0.003f), mPaper);
        }
    }

    static void Bookshelf(Transform p, Vector3 pos, float yaw, float w)
    {
        var b = Group(p, "Librero", pos, yaw); const float h = 1.9f, dp = 0.35f;
        var binderMats = new[] { mBinder, mRedPaint, mPostIt, mNavy, mTeal, mPaper };
        Prim("Back", b, new Vector3(0f, h * 0.5f, dp * 0.5f - 0.01f), new Vector3(w, h, 0.02f), mWood, col: true);
        Prim("SideL", b, new Vector3(-w * 0.5f, h * 0.5f, 0f), new Vector3(0.03f, h, dp), mWood);
        Prim("SideR", b, new Vector3(w * 0.5f, h * 0.5f, 0f), new Vector3(0.03f, h, dp), mWood);
        for (int i = 0; i < 5; i++)
            Prim("Shelf", b, new Vector3(0f, 0.02f + i * 0.465f, 0f), new Vector3(w, 0.03f, dp), mWood);
        for (int r = 0; r < 3; r++)
        {
            float y = 0.035f + (r + 1) * 0.465f;
            float x = -w * 0.5f + 0.05f;
            while (x < w * 0.5f - 0.12f)
            {
                if (rng.Next(0, 9) == 0) { x += 0.15f; continue; }
                float bw = 0.06f + (float)rng.NextDouble() * 0.03f, bh = 0.27f + (float)rng.NextDouble() * 0.08f;
                Prim("Binder", b, new Vector3(x + bw * 0.5f, y + bh * 0.5f, 0.02f), new Vector3(bw, bh, 0.26f), binderMats[rng.Next(binderMats.Length)]);
                x += bw + 0.006f;
            }
        }
        // Repisa inferior: cajas archivadoras
        for (float x = -w * 0.5f + 0.25f; x < w * 0.5f - 0.2f; x += 0.42f)
            Prim("Box", b, new Vector3(x, 0.19f, 0.01f), new Vector3(0.36f, 0.3f, 0.3f), mPaper);
    }

    static void Printer(Transform p, Vector3 pos, float yaw)
    {
        var g = Group(p, "Impresora", pos, yaw);
        Prim("Stand", g, new Vector3(0f, 0.35f, 0f), new Vector3(0.6f, 0.7f, 0.5f), mCabinet, col: true);
        Prim("Body", g, new Vector3(0f, 0.88f, 0f), new Vector3(0.5f, 0.36f, 0.42f), mPaper);
        Prim("Top", g, new Vector3(0f, 1.065f, 0.02f), new Vector3(0.5f, 0.02f, 0.38f), mDark);
        Prim("Tray", g, new Vector3(0f, 0.9f, -0.24f), new Vector3(0.3f, 0.02f, 0.1f), mDark, rot: new Vector3(-10f, 0f, 0f));
        Prim("Sheet", g, new Vector3(0f, 0.915f, -0.24f), new Vector3(0.21f, 0.005f, 0.09f), mPaper, rot: new Vector3(-10f, 0f, 0f));
        Prim("Panel", g, new Vector3(0.16f, 1.0f, -0.211f), new Vector3(0.12f, 0.06f, 0.005f), mScreen);
    }

    static void Plant(Transform p, Vector3 pos, float s = 1f)
    {
        var g = Group(p, "Planta", pos);
        Prim("Pot", g, new Vector3(0f, 0.17f * s, 0f), new Vector3(0.44f, 0.34f, 0.44f) * s, mPot, PrimitiveType.Cylinder, col: true);
        Prim("Trunk", g, new Vector3(0f, 0.55f * s, 0f), new Vector3(0.07f, 0.5f, 0.07f) * s, mWood, PrimitiveType.Cylinder);
        Prim("LeafA", g, new Vector3(0f, 1.0f * s, 0f), new Vector3(0.7f, 0.75f, 0.7f) * s, mPlant, PrimitiveType.Sphere);
        Prim("LeafB", g, new Vector3(0.22f, 1.32f, 0.1f) * s, new Vector3(0.45f, 0.5f, 0.45f) * s, mPlantD, PrimitiveType.Sphere);
        Prim("LeafC", g, new Vector3(-0.18f, 1.24f, -0.12f) * s, new Vector3(0.4f, 0.44f, 0.4f) * s, mPlant, PrimitiveType.Sphere);
    }

    // Reloj de pared. Frente hacia local -Z.
    static void Clock(Transform p, Vector3 pos, float yaw)
    {
        var g = Group(p, "Reloj", pos, yaw);
        Prim("Rim", g, new Vector3(0f, 0f, 0.0f), new Vector3(0.44f, 0.02f, 0.44f), mDark, PrimitiveType.Cylinder, new Vector3(90f, 0f, 0f));
        Prim("Face", g, new Vector3(0f, 0f, -0.012f), new Vector3(0.4f, 0.01f, 0.4f), mPaper, PrimitiveType.Cylinder, new Vector3(90f, 0f, 0f));
        for (int i = 0; i < 12; i++)
        {
            float a = i * 30f * Mathf.Deg2Rad;
            Prim("Tick", g, new Vector3(Mathf.Sin(a) * 0.17f, Mathf.Cos(a) * 0.17f, -0.024f), new Vector3(0.012f, i % 3 == 0 ? 0.04f : 0.02f, 0.004f), mDark, rot: new Vector3(0f, 0f, -i * 30f));
        }
        void Hand(float deg, float len, float wd)
        {
            float a = deg * Mathf.Deg2Rad;
            Prim("Hand", g, new Vector3(Mathf.Sin(a) * len * 0.5f, Mathf.Cos(a) * len * 0.5f, -0.028f), new Vector3(wd, len, 0.004f), mDark, rot: new Vector3(0f, 0f, -deg));
        }
        Hand(280f, 0.1f, 0.018f); Hand(85f, 0.15f, 0.012f);
    }

    // Cámara de seguridad de techo. Mira hacia local +Z.
    static void SecurityCam(Transform p, Vector3 pos, float yaw)
    {
        var g = Group(p, "CamaraSeguridad", pos, yaw);
        Prim("Mount", g, new Vector3(0f, -0.05f, 0f), new Vector3(0.05f, 0.1f, 0.05f), mDark);
        var b = Group(g, "Body", new Vector3(0f, -0.12f, 0.05f)); b.localEulerAngles = new Vector3(28f, 0f, 0f);
        Prim("Housing", b, Vector3.zero, new Vector3(0.11f, 0.1f, 0.24f), mPaper);
        Prim("Lens", b, new Vector3(0f, 0f, 0.121f), new Vector3(0.07f, 0.07f, 0.01f), mDark);
        Prim("LED", b, new Vector3(0.035f, 0.035f, 0.124f), new Vector3(0.012f, 0.012f, 0.005f), mRed);
    }

    static void Extinguisher(Transform p, Vector3 pos, float yaw)
    {
        var g = Group(p, "Extintor", pos, yaw);
        // pos = 5 cm delante de la pared; la pared queda en local +Z
        Prim("Bracket", g, new Vector3(0f, 0.85f, 0.03f), new Vector3(0.12f, 0.06f, 0.04f), mDark);
        Prim("Tank", g, new Vector3(0f, 0.55f, -0.06f), new Vector3(0.17f, 0.25f, 0.17f), mRedPaint, PrimitiveType.Cylinder);
        Prim("Valve", g, new Vector3(0f, 0.84f, -0.06f), new Vector3(0.06f, 0.05f, 0.06f), mDark, PrimitiveType.Cylinder);
        Prim("Hose", g, new Vector3(0.07f, 0.6f, -0.13f), new Vector3(0.025f, 0.22f, 0.025f), mDark, PrimitiveType.Cylinder);
        Prim("SignPlate", g, new Vector3(0f, 1.35f, 0.04f), new Vector3(0.3f, 0.3f, 0.01f), mRedPaint);
        T3(g, "SignText", new Vector3(0f, 1.35f, 0.033f), "EXTINTOR", 0.045f, Color.white, new Vector2(0.28f, 0.28f));
    }

    static void ExitSign(Transform p, Vector3 pos, float yaw)
    {
        var g = Group(p, "Salida", pos, yaw);
        Prim("Box", g, Vector3.zero, new Vector3(0.55f, 0.2f, 0.06f), mExit);
        var t = T3(g, "Text", new Vector3(0f, 0f, -0.035f), "SALIDA", 0.09f, Color.white, new Vector2(0.5f, 0.18f));
        t.fontStyle = FontStyles.Bold;
    }

    static void TrashBin(Transform p, Vector3 pos)
    {
        var g = Group(p, "Papelera", pos);
        Prim("Bin", g, new Vector3(0f, 0.2f, 0f), new Vector3(0.3f, 0.2f, 0.3f), mDark, PrimitiveType.Cylinder);
        Prim("Rim", g, new Vector3(0f, 0.405f, 0f), new Vector3(0.32f, 0.01f, 0.32f), mMetal, PrimitiveType.Cylinder);
    }

    static void WaterDispenser(Transform p, Vector3 pos, float yaw)
    {
        var g = Group(p, "Dispensador", pos, yaw);
        Prim("Body", g, new Vector3(0f, 0.5f, 0f), new Vector3(0.32f, 1.0f, 0.32f), mPaper, col: true);
        Prim("Bottle", g, new Vector3(0f, 1.22f, 0f), new Vector3(0.27f, 0.22f, 0.27f), mGlass, PrimitiveType.Cylinder);
        Prim("Water", g, new Vector3(0f, 1.18f, 0f), new Vector3(0.24f, 0.17f, 0.24f), mTeal, PrimitiveType.Cylinder);
        Prim("TapR", g, new Vector3(-0.06f, 0.78f, -0.17f), new Vector3(0.03f, 0.05f, 0.03f), mRedPaint);
        Prim("TapB", g, new Vector3(0.06f, 0.78f, -0.17f), new Vector3(0.03f, 0.05f, 0.03f), mBinder);
        Prim("Tray", g, new Vector3(0f, 0.6f, -0.17f), new Vector3(0.22f, 0.02f, 0.06f), mDark);
    }

    static void Sofa(Transform p, Vector3 pos, float yaw)
    {
        var g = Group(p, "Sofa", pos, yaw);
        Prim("Base", g, new Vector3(0f, 0.22f, 0f), new Vector3(1.9f, 0.3f, 0.8f), mFabric, col: true);
        Prim("Back", g, new Vector3(0f, 0.6f, 0.32f), new Vector3(1.9f, 0.5f, 0.18f), mFabric);
        Prim("ArmL", g, new Vector3(-0.88f, 0.48f, 0f), new Vector3(0.16f, 0.26f, 0.8f), mFabric);
        Prim("ArmR", g, new Vector3(0.88f, 0.48f, 0f), new Vector3(0.16f, 0.26f, 0.8f), mFabric);
        Prim("CushionL", g, new Vector3(-0.4f, 0.41f, -0.04f), new Vector3(0.78f, 0.1f, 0.62f), mNavy);
        Prim("CushionR", g, new Vector3(0.4f, 0.41f, -0.04f), new Vector3(0.78f, 0.1f, 0.62f), mNavy);
        foreach (float x in new[] { -0.85f, 0.85f })
            foreach (float z in new[] { -0.32f, 0.32f })
                Prim("Foot", g, new Vector3(x, 0.035f, z), new Vector3(0.06f, 0.07f, 0.06f), mDark);
    }

    // Rack de servidores. dir = +1 el frente mira a +X, -1 mira a -X.
    static void Rack(Transform p, float frontX, float z, int dir)
    {
        var r = new GameObject("Rack").transform; r.SetParent(p, false);
        Prim("Body", r, new Vector3(frontX - dir * 0.45f, 1.1f, z), new Vector3(0.9f, 2.2f, 0.6f), mDark, col: true);
        Prim("Top", r, new Vector3(frontX - dir * 0.45f, 2.21f, z), new Vector3(0.92f, 0.02f, 0.62f), mMetal);
        for (int i = 0; i < 10; i++)
        {
            float y = 0.2f + i * 0.19f;
            Prim("Unit", r, new Vector3(frontX + dir * 0.01f, y, z), new Vector3(0.03f, 0.15f, 0.52f), mMetal);
            leds.Add(Prim("LED", r, new Vector3(frontX + dir * 0.03f, y, z + 0.2f), new Vector3(0.015f, 0.03f, 0.05f),
                rng.Next(0, 7) == 0 ? mOrange : mGreen).GetComponent<Renderer>());
            if (rng.Next(0, 3) == 0)
                leds.Add(Prim("LED2", r, new Vector3(frontX + dir * 0.03f, y, z + 0.12f), new Vector3(0.015f, 0.03f, 0.03f), mCyan).GetComponent<Renderer>());
        }
        Prim("GlassDoor", r, new Vector3(frontX + dir * 0.06f, 1.1f, z), new Vector3(0.012f, 2.1f, 0.58f), mGlass);
    }

    // ------------------------------------------------------------- ESTRUCTURA
    static void BuildShell(Transform env)
    {
        var s = Group(env, "Estructura", Vector3.zero);
        // Suelos/techos por zona
        FloorCeil(s, "Hall0", -6f, 6f, 0f, 6f, mFloor);
        FloorCeil(s, "Hall1", -6f, 6f, 6f, 12f, mFloor);
        FloorCeil(s, "Hall2", -6f, 6f, 12f, HallL, mFloor);
        FloorCeil(s, "Pasillo", -CorrHalf, CorrHalf, HallL, CorrEnd, mFloorCorr);
        FloorCeil(s, "OfContabilidad", -6f, -CorrHalf, HallL, 25f, mCarpet);
        FloorCeil(s, "OfGerencia", -6f, -CorrHalf, 25f, CorrEnd, mCarpet);
        FloorCeil(s, "OfCreditos", CorrHalf, 6f, HallL, 25f, mCarpet);
        FloorCeil(s, "OfRRHH", CorrHalf, 6f, 25f, CorrEnd, mCarpet);
        FloorCeil(s, "TI0", -6f, 6f, CorrEnd, 36f, mRaised);
        FloorCeil(s, "TI1", -6f, 6f, 36f, TIEnd, mRaised);

        // Muros exteriores
        WallZ(s, "WallL", -RoomW / 2 - 0.05f, 0f, TIEnd, 0f, Ceil, mWall);
        WallZ(s, "WallR", RoomW / 2 + 0.05f, 0f, TIEnd, 0f, Ceil, mWall);
        WallX(s, "WallFront", -0.05f, -6f, 6f, 0f, Ceil, mWall);
        WallX(s, "WallBack", TIEnd + 0.05f, -6f, 6f, 0f, Ceil, mWall);
        // Fondo del hall con la entrada al pasillo
        WallX(s, "HallBackL", HallL, -6f, -CorrHalf, 0f, Ceil, mWall);
        WallX(s, "HallBackR", HallL, CorrHalf, 6f, 0f, Ceil, mWall);
        WallX(s, "HallBackTop", HallL, -CorrHalf, CorrHalf, 2.4f, Ceil, mWall);
        // Separación entre oficinas
        WallX(s, "DivOfL", 25f, -6f, -CorrHalf, 0f, Ceil, mWall);
        WallX(s, "DivOfR", 25f, CorrHalf, 6f, 0f, Ceil, mWall);
        // Muro de la Zona TI con el vano de la puerta
        WallX(s, "TIFrontL", CorrEnd, -6f, -0.6f, 0f, Ceil, mWall);
        WallX(s, "TIFrontR", CorrEnd, 0.6f, 6f, 0f, Ceil, mWall);
        WallX(s, "TIFrontTop", CorrEnd, -0.6f, 0.6f, 2.15f, Ceil, mWall);

        // Zócalo de marca y carril dorado en el hall
        Prim("BaseL", s, new Vector3(-RoomW / 2 + 0.03f, 0.08f, HallL / 2), new Vector3(0.06f, 0.16f, HallL), mNavy);
        Prim("BaseR", s, new Vector3(RoomW / 2 - 0.03f, 0.08f, HallL / 2), new Vector3(0.06f, 0.16f, HallL), mNavy);
        Prim("BaseF", s, new Vector3(0, 0.08f, 0.03f), new Vector3(RoomW, 0.16f, 0.06f), mNavy);
        Prim("BaseBL", s, new Vector3(-3.6f, 0.08f, HallL - 0.08f), new Vector3(4.8f, 0.16f, 0.06f), mNavy);
        Prim("BaseBR", s, new Vector3(3.6f, 0.08f, HallL - 0.08f), new Vector3(4.8f, 0.16f, 0.06f), mNavy);
        Prim("RailL", s, new Vector3(-RoomW / 2 + 0.06f, 1.02f, HallL / 2), new Vector3(0.05f, 0.05f, HallL - 0.4f), mGold);
        Prim("RailR", s, new Vector3(RoomW / 2 - 0.06f, 1.02f, HallL / 2), new Vector3(0.05f, 0.05f, HallL - 0.4f), mGold);
        // Zócalo oscuro en la Zona TI
        Prim("TIBaseL", s, new Vector3(-RoomW / 2 + 0.03f, 0.06f, 36f), new Vector3(0.06f, 0.12f, 8f), mDark);
        Prim("TIBaseR", s, new Vector3(RoomW / 2 - 0.03f, 0.06f, 36f), new Vector3(0.06f, 0.12f, 8f), mDark);
        Prim("TIBaseB", s, new Vector3(0f, 0.06f, TIEnd - 0.03f), new Vector3(RoomW, 0.12f, 0.06f), mDark);
    }

    // ------------------------------------------------------------- 1. HALL
    static void BuildHallDecor(Transform hall)
    {
        foreach (float x in new[] { -3f, 0f, 3f })
            strips.Add(Prim("LED_Strip", hall, new Vector3(x, Ceil - 0.02f, HallL / 2), new Vector3(0.18f, 0.04f, HallL - 1.5f), mWhite).GetComponent<Renderer>());
        foreach (float z in new[] { 1.5f, 4.5f, 7.5f, 10.5f, 13.5f, 16.5f })
            lights.Add(LightAt(hall, new Vector3(0, 2.3f, z), new Color(1f, 0.95f, 0.86f), 1.3f, 12f));

        // Zaguán: puertas de cristal tintado + cabecera de marca
        Prim("DoorBack", hall, new Vector3(0, 1.05f, 0.03f), new Vector3(3.2f, 2.14f, 0.02f), mDoor);
        foreach (float dx in new[] { -0.8f, 0.8f })
        {
            Prim("DoorGlass", hall, new Vector3(dx, 1.05f, 0.10f), new Vector3(1.5f, 2.1f, 0.04f), mGlass);
            Prim("DoorFrame", hall, new Vector3(dx + (dx < 0 ? -0.77f : 0.77f), 1.05f, 0.10f), new Vector3(0.06f, 2.14f, 0.07f), mDoor);
            Prim("DoorHandle", hall, new Vector3(dx + (dx < 0 ? 0.28f : -0.28f), 1.0f, 0.14f), new Vector3(0.04f, 0.5f, 0.04f), mGold);
        }
        Prim("DoorTop", hall, new Vector3(0, 2.2f, 0.10f), new Vector3(3.3f, 0.14f, 0.08f), mDoor);
        Prim("DoorSill", hall, new Vector3(0, 0.03f, 0.10f), new Vector3(3.3f, 0.06f, 0.12f), mMetal);

        var ent = Sign(hall, "Entrance", new Vector3(0f, 2.95f, 0.10f), 180f, "BANCO ANDINO", 0.62f, 6.4f);
        ent.transform.localPosition = new Vector3(0f, 0.08f, -0.045f);
        T3(ent.transform.parent, "EntranceSub", new Vector3(0f, -0.22f, -0.045f), "SUCURSAL CENTRAL", 0.14f,
            new Color(0.88f, 0.90f, 0.96f), new Vector2(6.4f, 0.28f));
        ExitSign(hall, new Vector3(0f, 2.45f, 0.14f), 180f);

        Sign(hall, "ServA", new Vector3(-4.6f, 1.55f, 0.10f), 180f, "AHORRO · CRÉDITOS", 0.16f);
        Sign(hall, "ServB", new Vector3(4.5f, 1.55f, 0.10f), 180f, "INVERSIONES", 0.16f);
        Prim("Mat", hall, new Vector3(0, 0.012f, 1.0f), new Vector3(3.4f, 0.02f, 1.5f), mNavy);

        // Tablón de estado de red (a la izquierda de la entrada al pasillo)
        const float bx = -3.6f, bw = 4.0f;
        Prim("WallDisplay", hall, new Vector3(bx, 2.45f, HallL - 0.08f), new Vector3(bw, 1.3f, 0.06f), mBoard);
        Prim("FrameT", hall, new Vector3(bx, 3.13f, HallL - 0.10f), new Vector3(bw + 0.12f, 0.07f, 0.06f), mGold);
        Prim("FrameB", hall, new Vector3(bx, 1.77f, HallL - 0.10f), new Vector3(bw + 0.12f, 0.07f, 0.06f), mGold);
        Prim("FrameL", hall, new Vector3(bx - bw / 2 - 0.05f, 2.45f, HallL - 0.10f), new Vector3(0.07f, 1.43f, 0.06f), mGold);
        Prim("FrameR", hall, new Vector3(bx + bw / 2 + 0.05f, 2.45f, HallL - 0.10f), new Vector3(0.07f, 1.43f, 0.06f), mGold);
        T3(hall, "OpsTitle", new Vector3(bx, 2.85f, HallL - 0.13f), "BANCO ANDINO", 0.36f, SignTextColor, new Vector2(bw, 0.6f));
        T3(hall, "BoardSub", new Vector3(bx, 2.44f, HallL - 0.13f), "SUCURSAL CENTRAL · ATENCIÓN AL CLIENTE", 0.15f, new Color(0.85f, 0.9f, 1f), new Vector2(bw, 0.35f));
        wallStatus = T3(hall, "NetStatus", new Vector3(bx, 2.02f, HallL - 0.13f), "ESTADO DE RED: NORMAL", 0.2f, new Color(0.2f, 1f, 0.45f), new Vector2(bw, 0.5f));

        // Publicidad a la derecha de la entrada al pasillo
        Tv(hall, "TvPromo", new Vector3(3.6f, 2.35f, HallL - 0.08f), 0f, 2.2f, 0.95f,
            "CRÉDITO HIPOTECARIO\n<size=60%>Tasa preferencial · Consulta en Plataforma</size>", new Color(0.95f, 0.85f, 0.45f));

        // Marco de la entrada al pasillo + letrero
        foreach (float x in new[] { -CorrHalf, CorrHalf })
            Prim("PortalPost", hall, new Vector3(x, 1.2f, HallL - 0.07f), new Vector3(0.08f, 2.4f, 0.06f), mGold);
        Prim("PortalTop", hall, new Vector3(0f, 2.4f, HallL - 0.07f), new Vector3(2.48f, 0.08f, 0.06f), mGold);
        Sign(hall, "SignOficinas", new Vector3(0f, 2.9f, HallL - 0.09f), 0f, "OFICINAS  ·  ZONA TI", 0.13f, 2.3f);

        // Detalles de ambientación
        Clock(hall, new Vector3(-5.94f, 2.62f, 7.8f), -90f);
        SecurityCam(hall, new Vector3(-5.7f, Ceil, 0.4f), 45f);
        SecurityCam(hall, new Vector3(5.7f, Ceil, HallL - 0.4f), -135f);
        Extinguisher(hall, new Vector3(5.95f, 0f, 12.2f), 90f);
        TrashBin(hall, new Vector3(-1.75f, 0f, HallL - 0.4f));
        TrashBin(hall, new Vector3(1.9f, 0f, 0.5f));

        // Dispensador de turnos junto a la entrada
        var k = Group(hall, "Turnero", new Vector3(-1.6f, 0f, 2.4f));
        Prim("Pedestal", k, new Vector3(0f, 0.55f, 0f), new Vector3(0.38f, 1.1f, 0.3f), mNavy, col: true);
        Prim("Trim", k, new Vector3(0f, 1.11f, 0f), new Vector3(0.4f, 0.03f, 0.32f), mGold);
        var head = Group(k, "Head", new Vector3(0f, 1.3f, 0f)); head.localEulerAngles = new Vector3(20f, 0f, 0f);
        Prim("Body", head, Vector3.zero, new Vector3(0.42f, 0.34f, 0.06f), mDark);
        screens.Add(Prim("Screen", head, new Vector3(0f, 0f, -0.032f), new Vector3(0.36f, 0.28f, 0.005f), mScreen).GetComponent<Renderer>());
        T3(head, "Text", new Vector3(0f, 0f, -0.037f), "TOME SU\nTURNO", 0.06f, Color.white, new Vector2(0.34f, 0.26f));
        Prim("Ticket", k, new Vector3(0f, 0.95f, -0.16f), new Vector3(0.06f, 0.08f, 0.005f), mPaper);
    }

    static void BuildTellers(Transform hall)
    {
        int n = 0;
        foreach (float z in new[] { 2.5f, 4.5f, 6.5f, 8.5f, 10.5f })
        {
            n++;
            var d = Group(hall, "Caja_" + n.ToString("00"), Vector3.zero);
            Prim("Top", d, new Vector3(3.6f, 0.75f, z), new Vector3(0.75f, 0.05f, 1.5f), mCounter, col: true);
            Prim("LegA", d, new Vector3(3.6f, 0.37f, z + 0.7f), new Vector3(0.7f, 0.74f, 0.04f), mNavy);
            Prim("LegB", d, new Vector3(3.6f, 0.37f, z - 0.7f), new Vector3(0.7f, 0.74f, 0.04f), mNavy);
            for (int i = -1; i <= 1; i += 2)
            {
                Prim("Monitor", d, new Vector3(3.5f, 1.02f, z + i * 0.33f), new Vector3(0.04f, 0.36f, 0.56f), mDark, rot: new Vector3(0, -i * 10f, 0));
                screens.Add(Prim("Screen", d, new Vector3(3.525f, 1.02f, z + i * 0.33f), new Vector3(0.01f, 0.31f, 0.51f), mScreen, rot: new Vector3(0, -i * 10f, 0)).GetComponent<Renderer>());
            }
            Prim("Keyboard", d, new Vector3(3.75f, 0.78f, z), new Vector3(0.14f, 0.02f, 0.45f), mDark);
            // Contadora de billetes, impresora de comprobantes y sello
            Prim("CashCounter", d, new Vector3(3.85f, 0.85f, z + 0.55f), new Vector3(0.25f, 0.15f, 0.22f), mDark);
            Prim("CashSlot", d, new Vector3(3.85f, 0.93f, z + 0.55f), new Vector3(0.12f, 0.02f, 0.16f), mGold);
            Prim("CashLCD", d, new Vector3(3.72f, 0.88f, z + 0.55f), new Vector3(0.005f, 0.04f, 0.1f), mGreen);
            Prim("Printer", d, new Vector3(3.85f, 0.83f, z - 0.55f), new Vector3(0.16f, 0.11f, 0.16f), mPaper);
            Prim("Receipt", d, new Vector3(3.76f, 0.9f, z - 0.55f), new Vector3(0.005f, 0.06f, 0.06f), mPaper);
            Prim("Stamp", d, new Vector3(3.95f, 0.81f, z - 0.25f), new Vector3(0.05f, 0.06f, 0.05f), mRedPaint, PrimitiveType.Cylinder);
            Chair(d, new Vector3(4.4f, 0, z), -90f);

            // Ventanilla hacia el pasillo: frente del mostrador + mampara de seguridad con ranura
            Prim("CounterFront", d, new Vector3(3.18f, 0.5f, z), new Vector3(0.08f, 1.0f, 1.6f), mNavy, col: true);
            Prim("CounterTrim", d, new Vector3(3.18f, 1.02f, z), new Vector3(0.10f, 0.05f, 1.62f), mGold);
            Prim("Ventanilla", d, new Vector3(3.18f, 1.47f, z), new Vector3(0.03f, 0.72f, 1.6f), mGlass);
            Prim("VentFrame", d, new Vector3(3.18f, 1.85f, z), new Vector3(0.05f, 0.04f, 1.62f), mMetal);
            Prim("MoneyTray", d, new Vector3(3.14f, 1.06f, z), new Vector3(0.22f, 0.02f, 0.34f), mMetal);
            Prim("Speaker", d, new Vector3(3.165f, 1.5f, z + 0.5f), new Vector3(0.01f, 0.08f, 0.08f), mMetal, PrimitiveType.Cylinder, new Vector3(0, 0, 90));
            Sign(d, "SignCaja", new Vector3(3.6f, 2.0f, z), 90f, "CAJA " + n.ToString("00"), 0.22f, hang: true);
        }
        Sign(hall, "SignModulos", new Vector3(3.6f, 3.05f, 9.5f), 90f, "VENTANILLAS · CAJAS", 0.28f, hang: true);

        // Mueble de caudales detrás de los cajeros
        Prim("Credenza", hall, new Vector3(5.62f, 0.45f, 6.5f), new Vector3(0.55f, 0.9f, 9.4f), mNavy, col: true);
        Prim("CredenzaTop", hall, new Vector3(5.62f, 0.915f, 6.5f), new Vector3(0.58f, 0.03f, 9.44f), mCounter);
        for (float z = 2.2f; z < 11f; z += 0.8f)
            Prim("CredHandle", hall, new Vector3(5.335f, 0.7f, z), new Vector3(0.02f, 0.03f, 0.2f), mGold);

        // Pantalla de turnos sobre las ventanillas
        Tv(hall, "Turnos", new Vector3(5.94f, 2.62f, 6.5f), 90f, 1.7f, 0.55f,
            "TURNO  <color=#FFD24A>A-023</color>   >   CAJA 03", Color.white);
    }

    static void BuildReception(Transform hall)
    {
        var r = Group(hall, "Recepcion", Vector3.zero);
        Prim("Top", r, new Vector3(-3.6f, 0.75f, 2.6f), new Vector3(1.1f, 0.06f, 1.4f), mCounter, col: true);
        Prim("Base", r, new Vector3(-3.6f, 0.37f, 2.6f), new Vector3(1.0f, 0.74f, 1.3f), mNavy, col: true);
        Prim("Ledge", r, new Vector3(-3.6f, 0.97f, 1.95f), new Vector3(1.16f, 0.4f, 0.06f), mNavy);
        Prim("LedgeTrim", r, new Vector3(-3.6f, 1.19f, 1.95f), new Vector3(1.16f, 0.05f, 0.07f), mGold);
        Laptop(r, new Vector3(-3.6f, 0.80f, 3.0f), 0f);
        Prim("Phone", r, new Vector3(-3.25f, 0.81f, 2.75f), new Vector3(0.18f, 0.06f, 0.2f), mDark);
        Prim("Brochures", r, new Vector3(-3.95f, 0.82f, 2.2f), new Vector3(0.22f, 0.08f, 0.3f), mBinder);
        Chair(r, new Vector3(-3.6f, 0f, 3.75f), 180f);
        Sign(r, "SignRecep", new Vector3(-3.6f, 2.15f, 2.6f), 0f, "RECEPCIÓN", 0.30f, hang: true);
    }

    // Plataforma de atención: asesores para consultas, aperturas y reclamos
    static void BuildPlatform(Transform hall)
    {
        int n = 0;
        foreach (float z in new[] { 6.2f, 9.4f })
        {
            n++;
            var d = Group(hall, "Plataforma_" + n, Vector3.zero);
            OfficeDesk(d, new Vector3(-4.6f, 0f, z), 90f, mWood, 1, mFabric);
            Chair(d, new Vector3(-3.7f, 0f, z - 0.38f), -90f, mFabric);
            Chair(d, new Vector3(-3.7f, 0f, z + 0.38f), -90f, mFabric);
            Prim("NamePlate", d, new Vector3(-4.3f, 0.81f, z), new Vector3(0.04f, 0.07f, 0.26f), mGold, rot: new Vector3(0f, 0f, -15f));
            Sign(d, "SignPlat", new Vector3(-4.6f, 2.15f, z), -90f, "ASESOR " + n.ToString("00"), 0.18f, hang: true);
        }
        Sign(hall, "SignPlataforma", new Vector3(-4.4f, 3.05f, 7.8f), -90f, "PLATAFORMA · CONSULTAS", 0.24f, hang: true);

        // Mesa de formularios
        var f = Group(hall, "MesaFormularios", new Vector3(-4.9f, 0f, 12.4f));
        Prim("Top", f, new Vector3(0f, 1.02f, 0f), new Vector3(0.6f, 0.04f, 1.5f), mCounter, col: true);
        Prim("Body", f, new Vector3(0f, 0.5f, 0f), new Vector3(0.4f, 1.0f, 1.2f), mNavy, col: true);
        Prim("Trim", f, new Vector3(0f, 0.99f, 0f), new Vector3(0.62f, 0.03f, 1.52f), mGold);
        for (int i = 0; i < 3; i++)
            Prim("Forms", f, new Vector3(0.05f, 1.05f, -0.45f + i * 0.45f), new Vector3(0.21f, 0.01f, 0.29f), mPaper, rot: new Vector3(0, i * 7f, 0));
        Prim("Pen", f, new Vector3(0.12f, 1.05f, 0.25f), new Vector3(0.01f, 0.07f, 0.01f), mDark, PrimitiveType.Cylinder, new Vector3(90, 30, 0));
        Prim("Holder", f, new Vector3(-0.22f, 1.15f, 0f), new Vector3(0.12f, 0.22f, 1.2f), mDark);
        T3(f, "Label", new Vector3(0.24f, 1.25f, 0f), "FORMULARIOS", 0.07f, SignTextColor, new Vector2(1.1f, 0.15f)).transform.localEulerAngles = new Vector3(0, -90f, 0);
    }

    static void BuildWaitingArea(Transform hall)
    {
        var w = Group(hall, "SalaEspera", Vector3.zero);
        foreach (float z in new[] { 3.2f, 4.3f, 5.4f })
            Chair(w, new Vector3(2.3f, 0f, z), 90f, mFabric);
        Prim("SideTop", w, new Vector3(2.3f, 0.33f, 2.2f), new Vector3(0.5f, 0.06f, 0.5f), mWood, col: true);
        Prim("SideLeg", w, new Vector3(2.3f, 0.16f, 2.2f), new Vector3(0.1f, 0.3f, 0.1f), mDark);
        Prim("Magazines", w, new Vector3(2.3f, 0.37f, 2.2f), new Vector3(0.22f, 0.02f, 0.3f), mRedPaint, rot: new Vector3(0, 12, 0));
        Sign(w, "SignEspera", new Vector3(2.3f, 2.95f, 4.3f), 90f, "SALA DE ESPERA", 0.16f, hang: true);
    }

    static void BuildLounge(Transform hall)
    {
        var l = Group(hall, "Lounge", Vector3.zero);
        Sofa(l, new Vector3(-3.4f, 0f, 15.2f), 90f);
        Prim("TableTop", l, new Vector3(-4.6f, 0.4f, 15.2f), new Vector3(0.6f, 0.04f, 1.0f), mWood, col: true);
        Prim("TableBase", l, new Vector3(-4.6f, 0.2f, 15.2f), new Vector3(0.4f, 0.38f, 0.8f), mDark);
        Prim("Brochure", l, new Vector3(-4.6f, 0.43f, 15.0f), new Vector3(0.21f, 0.01f, 0.29f), mBinder, rot: new Vector3(0, 20, 0));
        Tv(l, "TvLounge", new Vector3(-5.94f, 1.75f, 15.2f), -90f, 1.6f, 0.9f,
            "BANCO ANDINO\n<size=60%>Ahorra hoy, invierte mañana</size>", new Color(0.95f, 0.85f, 0.45f));
        WaterDispenser(l, new Vector3(-5.6f, 0f, HallL - 0.5f), -90f);
    }

    static void BuildAtm(Transform hall)
    {
        var a = Group(hall, "CajeroAutomatico", Vector3.zero);
        Prim("Body", a, new Vector3(5.72f, 0.95f, 0.9f), new Vector3(0.55f, 1.9f, 0.9f), mNavy, col: true);
        Prim("Screen", a, new Vector3(5.42f, 1.45f, 0.9f), new Vector3(0.03f, 0.42f, 0.6f), mScreen);
        Prim("Keypad", a, new Vector3(5.43f, 1.05f, 0.9f), new Vector3(0.10f, 0.04f, 0.3f), mMetal, rot: new Vector3(0f, 0f, -12f));
        Prim("Slot", a, new Vector3(5.42f, 0.76f, 0.9f), new Vector3(0.04f, 0.06f, 0.34f), mGold);
        Prim("Top", a, new Vector3(5.72f, 1.94f, 0.9f), new Vector3(0.6f, 0.06f, 0.95f), mGold);
        Sign(a, "SignAtm", new Vector3(5.72f, 2.45f, 1.1f), 90f, "CAJERO 24H", 0.24f, hang: true);
    }

    static void BuildVault(Transform hall)
    {
        var v = Group(hall, "Boveda", new Vector3(5.95f, 0f, 14.6f), 90f);     // frente hacia -X (al hall)
        Prim("Frame", v, new Vector3(0f, 1.3f, 0f), new Vector3(2.3f, 2.3f, 0.06f), mDark);
        Prim("Ring", v, new Vector3(0f, 1.3f, -0.05f), new Vector3(2.0f, 0.04f, 2.0f), mMetal, PrimitiveType.Cylinder, new Vector3(90f, 0f, 0f));
        Prim("Door", v, new Vector3(0f, 1.3f, -0.1f), new Vector3(1.8f, 0.05f, 1.8f), mVault, PrimitiveType.Cylinder, new Vector3(90f, 0f, 0f));
        Prim("Hub", v, new Vector3(0f, 1.3f, -0.17f), new Vector3(0.3f, 0.04f, 0.3f), mGold, PrimitiveType.Cylinder, new Vector3(90f, 0f, 0f));
        foreach (float a in new[] { 0f, 60f, 120f })
            Prim("Spoke", v, new Vector3(0f, 1.3f, -0.2f), new Vector3(0.9f, 0.04f, 0.04f), mGold, rot: new Vector3(0f, 0f, a));
        for (int i = 0; i < 12; i++)
        {
            float a = i * 30f * Mathf.Deg2Rad;
            Prim("Bolt", v, new Vector3(Mathf.Cos(a) * 0.78f, 1.3f + Mathf.Sin(a) * 0.78f, -0.14f), new Vector3(0.07f, 0.02f, 0.07f), mGold, PrimitiveType.Cylinder, new Vector3(90f, 0f, 0f));
        }
        Prim("Hinge", v, new Vector3(0.95f, 1.3f, -0.1f), new Vector3(0.15f, 0.9f, 0.15f), mMetal);
        Sign(v, "SignBoveda", new Vector3(0f, 2.75f, -0.04f), 0f, "BÓVEDA", 0.2f);
        // Cordón de seguridad
        foreach (float x in new[] { -1.0f, 1.0f })
        {
            Prim("PostBase", v, new Vector3(x, 0.03f, -0.9f), new Vector3(0.3f, 0.06f, 0.3f), mMetal, PrimitiveType.Cylinder, col: true);
            Prim("Post", v, new Vector3(x, 0.5f, -0.9f), new Vector3(0.05f, 0.9f, 0.05f), mGold, PrimitiveType.Cylinder);
            Prim("Knob", v, new Vector3(x, 0.97f, -0.9f), new Vector3(0.11f, 0.07f, 0.11f), mGold, PrimitiveType.Sphere);
        }
        Prim("Rope", v, new Vector3(0f, 0.9f, -0.9f), new Vector3(2.0f, 0.05f, 0.03f), mRedPaint);
    }

    static void BuildQueue(Transform hall)
    {
        var q = Group(hall, "Cola", Vector3.zero);
        var pts = new[] { new Vector3(1.7f, 0f, 5.2f), new Vector3(1.7f, 0f, 6.6f), new Vector3(1.7f, 0f, 8.0f), new Vector3(2.6f, 0f, 8.7f) };
        foreach (var p in pts)
        {
            Prim("PostBase", q, p + new Vector3(0f, 0.03f, 0f), new Vector3(0.30f, 0.06f, 0.30f), mMetal, PrimitiveType.Cylinder, col: true);
            Prim("Post", q, p + new Vector3(0f, 0.5f, 0f), new Vector3(0.05f, 0.9f, 0.05f), mGold, PrimitiveType.Cylinder);
            Prim("Knob", q, p + new Vector3(0f, 0.97f, 0f), new Vector3(0.11f, 0.07f, 0.11f), mGold, PrimitiveType.Sphere);
        }
        for (int i = 0; i < pts.Length - 1; i++)
        {
            Vector3 a = pts[i], b = pts[i + 1];
            Vector3 d = b - a;
            float ang = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            Prim("Rope", q, (a + b) * 0.5f + new Vector3(0f, 0.9f, 0f), new Vector3(0.03f, 0.05f, d.magnitude), mNavy, rot: new Vector3(0f, ang, 0f));
        }
        Sign(q, "SignCola", new Vector3(1.7f, 1.95f, 6.6f), 90f, "FORME COLA AQUÍ", 0.20f, hang: true);
    }

    static void BuildPlants(Transform hall)
    {
        foreach (var p in new[] { new Vector3(-4.15f, 0f, 0.85f), new Vector3(4.7f, 0f, 0.6f), new Vector3(-2.95f, 0f, 16.8f), new Vector3(3.0f, 0f, 16.8f) })
            Plant(hall, p);
    }

    // ------------------------------------------------------ 2. PASILLO DE OFICINAS
    static readonly (float zs, float ze, string name)[] OfficesL = { (HallL + 0.05f, 25f, "CONTABILIDAD"), (25f, CorrEnd - 0.05f, "GERENCIA") };
    static readonly (float zs, float ze, string name)[] OfficesR = { (HallL + 0.05f, 25f, "CRÉDITOS Y PRÉSTAMOS"), (25f, CorrEnd - 0.05f, "RECURSOS HUMANOS") };

    static void BuildCorridor(Transform p)
    {
        Partition(p, -1, OfficesL);
        Partition(p, 1, OfficesR);

        strips.Add(Prim("LED_Strip", p, new Vector3(0f, Ceil - 0.02f, 25f), new Vector3(0.18f, 0.04f, 13f), mWhite).GetComponent<Renderer>());
        foreach (float z in new[] { 20f, 24.5f, 29f })
            lights.Add(LightAt(p, new Vector3(0f, 2.5f, z), new Color(1f, 0.95f, 0.88f), 1.0f, 7f));
        Prim("Runner", p, new Vector3(0f, 0.012f, 25f), new Vector3(1.3f, 0.02f, 13.6f), mNavy);
        Prim("RunnerTrimL", p, new Vector3(-0.66f, 0.013f, 25f), new Vector3(0.03f, 0.02f, 13.6f), mGold);
        Prim("RunnerTrimR", p, new Vector3(0.66f, 0.013f, 25f), new Vector3(0.03f, 0.02f, 13.6f), mGold);

        Sign(p, "SignPasillo", new Vector3(0f, 2.8f, 19.2f), 0f, "OFICINAS ADMINISTRATIVAS", 0.12f, 2.2f, hang: true);
        Sign(p, "SignHaciaTI", new Vector3(0f, 2.8f, 27.6f), 0f, "ZONA TI  AL FONDO", 0.13f, 2.1f, hang: true);
        SecurityCam(p, new Vector3(0.9f, Ceil, CorrEnd - 0.35f), 180f);
        TrashBin(p, new Vector3(-0.95f, 0f, CorrEnd - 0.35f));
    }

    // Tabique de oficinas a lo largo del pasillo: pilar, puerta abierta, tramo macizo (afiche) y vidrio esmerilado.
    static void Partition(Transform p, int side, (float zs, float ze, string name)[] offices)
    {
        float x = side * CorrHalf;
        string[][] posters =
        {
            new[] { "¿CÓMO DETECTAR UN PHISHING?", "- Remitente con dominio extraño\n- Urgencia o amenazas\n- Adjuntos .exe, .zip o con doble extensión\n- Enlaces que no coinciden\n- Te piden claves o datos\n\nSi dudas: NO abras, REPORTA." },
            new[] { "RANSOMWARE", "Malware que CIFRA los archivos del banco y pide un rescate para devolverlos.\n\nSe propaga por la red interna en minutos.\n\nDefensa: no abrir adjuntos sospechosos y mantener copias de seguridad." },
            new[] { "¿QUÉ ES UN VIRUS?", "Un virus (malware) es un programa malicioso escondido en archivos o adjuntos.\n\nAl abrirlo se ejecuta, se copia a otros equipos de la red y roba o daña la información." },
            new[] { "CONTRASEÑAS SEGURAS", "- 12 caracteres o más\n- Letras, números y símbolos\n- Una distinta por sistema\n- Nunca la anotes en un papel\n- TI jamás te la pedirá por correo" },
        };
        var accents = new[] { mRedPaint, mNavy, mTeal, mNavy };
        int pi = side < 0 ? 0 : 2;
        float yaw = side > 0 ? 90f : -90f;

        foreach (var o in offices)
        {
            float d0 = o.zs + 0.4f, d1 = o.zs + 1.4f, s1 = o.zs + 2.6f, g0 = s1, g1 = o.ze - 0.1f;
            WallZ(p, "Pier", x, o.zs, d0, 0f, Ceil, mWall);
            WallZ(p, "Lintel", x, d0, d1, 2.1f, Ceil, mWall);
            Prim("JambA", p, new Vector3(x, 1.05f, d0 + 0.025f), new Vector3(0.14f, 2.1f, 0.05f), mDoor);
            Prim("JambB", p, new Vector3(x, 1.05f, d1 - 0.025f), new Vector3(0.14f, 2.1f, 0.05f), mDoor);
            Prim("Head", p, new Vector3(x, 2.1f, (d0 + d1) * 0.5f), new Vector3(0.14f, 0.06f, 1.0f), mDoor);
            // Puerta de madera abierta hacia la oficina
            Prim("DoorLeaf", p, new Vector3(x + side * 0.52f, 1.03f, d0 + 0.07f), new Vector3(0.92f, 2.04f, 0.04f), mWood, col: true);
            Prim("DoorKnob", p, new Vector3(x + side * 0.9f, 1.0f, d0 + 0.12f), new Vector3(0.04f, 0.04f, 0.06f), mGold);
            WallZ(p, "Solid", x, d1, s1, 0f, Ceil, mWall);
            // Vidrio esmerilado con montantes
            WallZ(p, "Glass", x, g0, g1, 0.05f, 2.6f, mGlass, 0.03f);
            Prim("Frosted", p, new Vector3(x, 1.3f, (g0 + g1) * 0.5f), new Vector3(0.035f, 0.4f, g1 - g0), mFrosted);
            Prim("Sill", p, new Vector3(x, 0.03f, (g0 + g1) * 0.5f), new Vector3(0.07f, 0.06f, g1 - g0), mMetal);
            Prim("TopRail", p, new Vector3(x, 2.6f, (g0 + g1) * 0.5f), new Vector3(0.07f, 0.05f, g1 - g0), mMetal);
            for (int i = 0; i <= 3; i++)
                Prim("Mullion", p, new Vector3(x, 1.3f, g0 + (g1 - g0) * i / 3f), new Vector3(0.06f, 2.6f, 0.05f), mMetal);
            WallZ(p, "Header", x, g0, o.ze, 2.6f, Ceil, mWall);
            WallZ(p, "PierEnd", x, g1, o.ze, 0f, 2.6f, mWall);

            Sign(p, "Sign_" + o.name, new Vector3(side * (CorrHalf - 0.085f), 2.5f, (d0 + d1) * 0.5f), yaw, o.name, 0.13f, 1.75f);
            Poster(p, "Afiche", new Vector3(side * (CorrHalf - 0.075f), 1.5f, (d1 + s1) * 0.5f), yaw,
                posters[pi][0], posters[pi][1], 0.95f, 1.25f, accents[pi]);
            pi++;
        }
    }

    static void OfficeLight(Transform o)
    {
        strips.Add(Prim("Panel", o, new Vector3(0.3f, Ceil - 0.02f, 0f), new Vector3(1.2f, 0.04f, 0.6f), mWhite).GetComponent<Renderer>());
        lights.Add(LightAt(o, new Vector3(0.3f, 2.6f, 0f), new Color(1f, 0.95f, 0.88f), 1.7f, 8f));
    }

    // Marco local de cada oficina: origen en su centro, -X hacia el pasillo, +X hacia el muro exterior.
    static void BuildOffices(Transform p)
    {
        float czA = (HallL + 0.05f + 25f) * 0.5f, czB = (25f + CorrEnd - 0.05f) * 0.5f;

        // CONTABILIDAD
        var c = Group(p, "Of_Contabilidad", new Vector3(-3.6f, 0f, czA), 180f);
        OfficeLight(c);
        OfficeDesk(c, new Vector3(0.9f, 0f, -1.5f), -90f, mWood, 2);
        OfficeDesk(c, new Vector3(0.9f, 0f, 1.0f), -90f, mWood, 1);
        foreach (float x in new[] { -0.4f, 0.1f, 0.6f }) FilingCabinet(c, new Vector3(x, 0f, 3.1f), 0f);
        Printer(c, new Vector3(1.9f, 0f, 2.9f), 90f);
        var chart = Poster(c, "Balance", new Vector3(2.37f, 1.75f, -0.3f), 90f, "BALANCE MENSUAL", "", 1.4f, 0.9f, mNavy);
        float[] bars = { 0.25f, 0.38f, 0.31f, 0.46f, 0.42f, 0.55f };
        for (int i = 0; i < bars.Length; i++)
            Prim("Bar", chart, new Vector3(-0.5f + i * 0.2f, -0.38f + bars[i] * 0.5f, -0.012f), new Vector3(0.12f, bars[i], 0.004f), i == bars.Length - 1 ? mGold : mTeal);
        Plant(c, new Vector3(1.9f, 0f, -3.0f));
        Clock(c, new Vector3(0.5f, 2.4f, -3.40f), 180f);

        // CRÉDITOS Y PRÉSTAMOS
        var k = Group(p, "Of_Creditos", new Vector3(3.6f, 0f, czA), 0f);
        OfficeLight(k);
        OfficeDesk(k, new Vector3(1.0f, 0f, 0.2f), -90f, mWood, 1);
        Prim("NamePlate", k, new Vector3(0.68f, 0.81f, 0.2f), new Vector3(0.04f, 0.07f, 0.26f), mGold, rot: new Vector3(0f, 0f, 15f));
        Chair(k, new Vector3(0.05f, 0f, -0.2f), 90f, mFabric);
        Chair(k, new Vector3(0.05f, 0f, 0.6f), 90f, mFabric);
        Bookshelf(k, new Vector3(0.8f, 0f, -3.25f), 180f, 1.6f);
        FilingCabinet(k, new Vector3(2.1f, 0f, -2.4f), 90f, 3);
        Poster(k, "Creditos", new Vector3(2.37f, 1.7f, 1.9f), 90f, "CRÉDITOS A TU MEDIDA", "Hipotecario\nVehicular\nPersonal\nPyME\n\nConsulta con tu asesor.", 0.9f, 1.2f, mGold);
        var stand = Group(k, "Folletero", new Vector3(-1.2f, 0f, 2.9f));
        Prim("Post", stand, new Vector3(0f, 0.6f, 0f), new Vector3(0.05f, 1.2f, 0.05f), mMetal);
        Prim("Base", stand, new Vector3(0f, 0.02f, 0f), new Vector3(0.4f, 0.04f, 0.3f), mMetal);
        for (int i = 0; i < 3; i++)
            Prim("Brochure", stand, new Vector3(0f, 0.7f + i * 0.25f, -0.05f), new Vector3(0.3f, 0.2f, 0.02f), i == 1 ? mGold : mBinder, rot: new Vector3(-12f, 0f, 0f));
        Plant(k, new Vector3(2.0f, 0f, 3.0f));

        // GERENCIA
        var g = Group(p, "Of_Gerencia", new Vector3(-3.6f, 0f, czB), 180f);
        OfficeLight(g);
        OfficeDesk(g, new Vector3(1.1f, 0f, -1.4f), -90f, mWood, 1, mLeather, 1.8f);
        Chair(g, new Vector3(0.15f, 0f, -1.8f), 90f, mFabric);
        Chair(g, new Vector3(0.15f, 0f, -1.0f), 90f, mFabric);
        var mt = Group(g, "MesaReuniones", new Vector3(0f, 0f, 1.7f));
        Prim("Top", mt, new Vector3(0f, 0.74f, 0f), new Vector3(1.2f, 0.03f, 1.2f), mWood, PrimitiveType.Cylinder, col: true);
        Prim("Stem", mt, new Vector3(0f, 0.37f, 0f), new Vector3(0.1f, 0.36f, 0.1f), mDark, PrimitiveType.Cylinder);
        Prim("Foot", mt, new Vector3(0f, 0.02f, 0f), new Vector3(0.6f, 0.02f, 0.6f), mDark, PrimitiveType.Cylinder);
        for (int i = 0; i < 4; i++)
        {
            float a = i * 90f + 45f, r = a * Mathf.Deg2Rad;
            Chair(mt, new Vector3(Mathf.Sin(r) * 0.95f, 0f, Mathf.Cos(r) * 0.95f), a + 180f, mFabric);
        }
        Bookshelf(g, new Vector3(0.8f, 0f, -3.25f), 180f, 1.8f);
        Poster(g, "Mision", new Vector3(2.37f, 1.7f, 0.4f), 90f, "BANCO ANDINO", "Misión: brindar servicios financieros seguros y confiables a cada cliente.\n\nVisión: ser el banco más confiable del país.", 1.1f, 0.9f, mGold);
        Plant(g, new Vector3(2.0f, 0f, 3.1f));

        // RECURSOS HUMANOS
        var h = Group(p, "Of_RRHH", new Vector3(3.6f, 0f, czB), 0f);
        OfficeLight(h);
        OfficeDesk(h, new Vector3(0.9f, 0f, -1.6f), -90f, mWood, 1);
        OfficeDesk(h, new Vector3(0.9f, 0f, 1.2f), -90f, mWood, 1);
        Whiteboard(h, "Pizarra", new Vector3(2.37f, 1.65f, -0.2f), 90f, 1.6f, 1.0f,
            "PLAN DE CAPACITACIÓN 2026\n\n- Ene: Inducción\n- Mar: Atención al cliente\n- Jun: Seguridad de la información\n- Sep: Prevención de fraudes");
        foreach (float x in new[] { -0.3f, 0.2f }) FilingCabinet(h, new Vector3(x, 0f, 3.1f), 0f);
        Poster(h, "Capacitacion", new Vector3(2.37f, 1.6f, 2.5f), 90f, "CAPACITACIÓN", "Seguridad de la información\n\nJueves 10:00 · Sala 2\nAsistencia obligatoria", 0.8f, 1.0f, mRedPaint);
        Plant(h, new Vector3(-1.3f, 0f, 3.0f));
    }

    // ------------------------------------------------------------- 3. ZONA TI
    static DoorInteraction BuildTIRoom(Transform p)
    {
        // Puerta con bisagra a la izquierda; se abre hacia dentro de la sala (+Z)
        var hinge = Group(p, "PuertaZonaTI", new Vector3(-0.55f, 0f, CorrEnd));
        Prim("Leaf", hinge, new Vector3(0.55f, 1.05f, 0f), new Vector3(1.08f, 2.08f, 0.05f), mTIDoor, col: true);
        Prim("Visor", hinge, new Vector3(0.55f, 1.6f, 0f), new Vector3(0.28f, 0.45f, 0.056f), mGlass);
        Prim("HandleOut", hinge, new Vector3(0.95f, 1.0f, -0.05f), new Vector3(0.14f, 0.03f, 0.03f), mMetal);
        Prim("HandleIn", hinge, new Vector3(0.95f, 1.0f, 0.05f), new Vector3(0.14f, 0.03f, 0.03f), mMetal);
        Prim("WarnPlate", hinge, new Vector3(0.55f, 1.18f, -0.03f), new Vector3(0.5f, 0.16f, 0.008f), mPostIt);
        var warn = T3(hinge, "WarnText", new Vector3(0.55f, 1.18f, -0.036f), "ACCESO RESTRINGIDO", 0.045f, Color.black, new Vector2(0.48f, 0.14f));
        warn.fontStyle = FontStyles.Bold; AutoSize(warn, 0.2f);
        Prim("KickPlate", hinge, new Vector3(0.55f, 0.12f, 0f), new Vector3(1.08f, 0.2f, 0.054f), mMetal);
        // Marco
        foreach (float x in new[] { -0.6f, 0.6f })
            Prim("Jamb", p, new Vector3(x, 1.08f, CorrEnd), new Vector3(0.06f, 2.16f, 0.14f), mMetal);
        Prim("JambTop", p, new Vector3(0f, 2.16f, CorrEnd), new Vector3(1.26f, 0.06f, 0.14f), mMetal);
        // Lector de tarjetas
        Prim("CardReader", p, new Vector3(0.85f, 1.2f, CorrEnd - 0.065f), new Vector3(0.09f, 0.13f, 0.03f), mDark);
        var led = Prim("ReaderLED", p, new Vector3(0.85f, 1.24f, CorrEnd - 0.082f), new Vector3(0.03f, 0.015f, 0.006f), mRed).GetComponent<Renderer>();
        Prim("ReaderPad", p, new Vector3(0.85f, 1.18f, CorrEnd - 0.082f), new Vector3(0.06f, 0.05f, 0.004f), mMetal);
        // Letrero ZONA TI
        Sign(p, "SignZonaTI", new Vector3(0f, 2.78f, CorrEnd - 0.09f), 0f, "ZONA TI", 0.34f, 2.6f);
        T3(p, "SignZonaTISub", new Vector3(0f, 2.37f, CorrEnd - 0.06f), "TECNOLOGÍA DE LA INFORMACIÓN · SOLO PERSONAL AUTORIZADO", 0.06f,
            new Color(0.25f, 0.3f, 0.4f), new Vector2(2.9f, 0.16f));

        var door = hinge.gameObject.AddComponent<DoorInteraction>();
        door.led = led; door.ledOpen = mGreen; door.width = 1.1f;

        // Iluminación fría de sala técnica
        foreach (float x in new[] { -2.5f, 2.5f })
            strips.Add(Prim("LED_Strip", p, new Vector3(x, Ceil - 0.02f, 36f), new Vector3(0.18f, 0.04f, 6.8f), mCold).GetComponent<Renderer>());
        var cold = new Color(0.85f, 0.92f, 1f);
        lights.Add(LightAt(p, new Vector3(0f, 2.6f, 34f), cold, 1.2f, 8f));
        lights.Add(LightAt(p, new Vector3(0f, 2.6f, 38.3f), cold, 1.2f, 8f));
        lights.Add(LightAt(p, new Vector3(-3.8f, 2.4f, 36.3f), new Color(0.7f, 0.85f, 1f), 0.9f, 6f));

        // Sala de servidores
        for (float z = 33.3f; z < 39.6f; z += 1.2f) Rack(p, -4.95f, z, 1);
        foreach (float z in new[] { 38.1f, 39.3f }) Rack(p, 4.95f, z, -1);
        Prim("CableTray", p, new Vector3(-5.4f, 2.95f, 36.3f), new Vector3(0.45f, 0.04f, 7f), mMetal);
        Prim("CablesA", p, new Vector3(-5.5f, 2.99f, 36.3f), new Vector3(0.1f, 0.04f, 7f), mBinder);
        Prim("CablesB", p, new Vector3(-5.3f, 2.99f, 36.3f), new Vector3(0.08f, 0.04f, 7f), mPostIt);
        for (float z = 33.3f; z < 39.6f; z += 1.2f)
            Prim("Drop", p, new Vector3(-5.4f, 2.6f, z), new Vector3(0.06f, 0.7f, 0.06f), mBinder);
        Sign(p, "SignServidores", new Vector3(-4.3f, 2.85f, 36.3f), -90f, "SALA DE SERVIDORES", 0.2f, hang: true);

        // UPS, gabinete de red y aire acondicionado de precisión
        Prim("UPS", p, new Vector3(5.5f, 0.5f, 36.9f), new Vector3(0.8f, 1.0f, 0.7f), mDark, col: true);
        Prim("UPSLcd", p, new Vector3(5.09f, 0.8f, 36.9f), new Vector3(0.01f, 0.08f, 0.16f), mGreen);
        T3(p, "UPSLabel", new Vector3(5.09f, 0.6f, 36.9f), "UPS 10 kVA", 0.04f, Color.white, new Vector2(0.4f, 0.1f)).transform.localEulerAngles = new Vector3(0, -90, 0);
        Prim("NetCabinet", p, new Vector3(5.78f, 1.6f, 35.5f), new Vector3(0.4f, 0.6f, 0.6f), mDark);
        for (int i = 0; i < 4; i++)
            leds.Add(Prim("NetLED", p, new Vector3(5.575f, 1.5f + i * 0.08f, 35.5f - 0.2f + i * 0.1f), new Vector3(0.01f, 0.02f, 0.03f), mGreen).GetComponent<Renderer>());
        Prim("PatchPanel", p, new Vector3(5.575f, 1.75f, 35.5f), new Vector3(0.01f, 0.06f, 0.5f), mMetal);
        Prim("AC", p, new Vector3(5.86f, 2.75f, 34.3f), new Vector3(0.25f, 0.32f, 1.0f), mPaper);
        Prim("ACVent", p, new Vector3(5.73f, 2.65f, 34.3f), new Vector3(0.01f, 0.06f, 0.9f), mDark);
        T3(p, "ACTemp", new Vector3(5.725f, 2.8f, 34.6f), "18°C", 0.04f, new Color(0.2f, 0.8f, 1f), new Vector2(0.2f, 0.08f)).transform.localEulerAngles = new Vector3(0, -90, 0);

        // Pared de la puerta (lado interior): pizarra con la topología + afiches
        Whiteboard(p, "PizarraRed", new Vector3(-3.2f, 1.6f, CorrEnd + 0.08f), 180f, 2.6f, 1.2f,
            "RED DEL BANCO\n\nINTERNET\n   |\nFIREWALL  ->  DMZ (correo, web)\n   |\nRED INTERNA  (VLAN 10)\n   |-- Contabilidad / Créditos / Gerencia / RR.HH.\n   |-- Cajas y cajeros ATM\n   |-- Servidor central + copias de seguridad");
        ExitSign(p, new Vector3(0f, 2.45f, CorrEnd + 0.09f), 180f);
        Extinguisher(p, new Vector3(1.15f, 0f, CorrEnd + 0.1f), 180f);
        Poster(p, "CadenaAtaque", new Vector3(2.55f, 1.55f, CorrEnd + 0.08f), 180f, "CADENA DE UN ATAQUE",
            "1. Llega un correo de phishing\n2. El usuario abre el adjunto\n3. Se ejecuta el malware\n4. Roba credenciales\n5. Se propaga por la red\n6. Cifra los datos (ransomware)", 1.2f, 1.45f, mRedPaint);
        Poster(p, "Contrasenas", new Vector3(4.1f, 1.55f, CorrEnd + 0.08f), 180f, "BLOQUEA TU PC",
            "Al levantarte pulsa\nWindows + L\n\nUn equipo desbloqueado es una puerta abierta.", 0.9f, 1.1f, mNavy);

        // Pared del fondo: afiches a los lados del mapa de red
        Poster(p, "Reporta", new Vector3(-3.4f, 1.55f, TIEnd - 0.08f), 0f, "SI DUDAS, REPORTA",
            "Correo sospechoso:\nreenvíalo a\nseguridad@bancoandino.com\n\nAnexo SOC: 4040\n(24 horas)", 1.0f, 1.25f, mTeal);
        Poster(p, "TiposMalware", new Vector3(3.5f, 1.55f, TIEnd - 0.08f), 0f, "MALWARE MÁS COMÚN",
            "- Virus: infecta archivos\n- Gusano: se copia solo por la red\n- Troyano: se disfraza de archivo útil\n- Ransomware: cifra y pide rescate\n- Spyware: roba datos y claves", 1.1f, 1.25f, mRedPaint);

        Plant(p, new Vector3(5.45f, 0f, CorrEnd + 0.6f), 0.9f);
        TrashBin(p, new Vector3(1.4f, 0f, StationZ + 0.2f));
        SecurityCam(p, new Vector3(-5.5f, Ceil, TIEnd - 0.4f), 135f);
        return door;
    }

    // Estación TI-01: el usuario se sienta mirando hacia la puerta (-Z) y al levantarse queda frente al mapa de red.
    // Se construye en un marco local girado 180° con las mismas medidas de la estación original.
    static void BuildMainStation(Transform ti, out GameObject marker, out Transform seatPoint, out GameObject screenAnchor)
    {
        var s = Group(ti, "EstacionTI01", new Vector3(0f, 0f, StationZ), 180f);
        Prim("DeskTop", s, new Vector3(0, 0.75f, 0f), new Vector3(2.2f, 0.06f, 0.9f), mMetal, col: true);
        Prim("LegL", s, new Vector3(-1.0f, 0.37f, 0f), new Vector3(0.05f, 0.74f, 0.8f), mDark);
        Prim("LegR", s, new Vector3(1.0f, 0.37f, 0f), new Vector3(0.05f, 0.74f, 0.8f), mDark);
        Prim("Modesty", s, new Vector3(0f, 0.45f, 0.42f), new Vector3(2.0f, 0.55f, 0.03f), mDark);
        Prim("MonitorBody", s, new Vector3(0, 1.1f, -0.12f), new Vector3(0.84f, 0.49f, 0.04f), mDark);
        Prim("MonitorNeck", s, new Vector3(0, 0.9f, -0.08f), new Vector3(0.06f, 0.3f, 0.04f), mDark);
        Prim("MonitorBase", s, new Vector3(0, 0.79f, -0.12f), new Vector3(0.3f, 0.02f, 0.2f), mDark);
        Prim("Keyboard", s, new Vector3(0, 0.79f, -0.4f), new Vector3(0.45f, 0.02f, 0.15f), mDark);
        Prim("Mouse", s, new Vector3(0.35f, 0.79f, -0.4f), new Vector3(0.06f, 0.02f, 0.1f), mDark);
        Prim("PC", s, new Vector3(-0.9f, 0.45f, 0.05f), new Vector3(0.2f, 0.4f, 0.4f), mDark);
        Prim("PCLed", s, new Vector3(-0.9f, 0.6f, -0.151f), new Vector3(0.02f, 0.02f, 0.005f), mCyan);
        Prim("IPPhone", s, new Vector3(-0.72f, 0.81f, -0.25f), new Vector3(0.2f, 0.06f, 0.2f), mDark, rot: new Vector3(-10f, 20f, 0f));
        Prim("Mug", s, new Vector3(0.62f, 0.82f, -0.3f), new Vector3(0.08f, 0.05f, 0.08f), mPaper, PrimitiveType.Cylinder);
        Prim("Badge", s, new Vector3(0.55f, 0.785f, -0.15f), new Vector3(0.09f, 0.005f, 0.06f), mPaper);

        // Post-it con la contraseña pegado en el escritorio (mala práctica a propósito)
        Prim("PostIt", s, new Vector3(-0.42f, 0.783f, -0.33f), new Vector3(0.09f, 0.004f, 0.09f), mPostIt, rot: new Vector3(0f, 12f, 0f));
        var pi = T3(s, "PostItText", new Vector3(-0.42f, 0.787f, -0.33f), "Clave correo:\nAdmin2026*", 0.011f, new Color(0.1f, 0.15f, 0.5f), new Vector2(0.085f, 0.085f));
        pi.transform.localEulerAngles = new Vector3(90f, 12f, 0f);

        foreach (int i in new[] { -1, 1 })
        {
            Prim("SideMonitor", s, new Vector3(i * 0.85f, 1.02f, 0f), new Vector3(0.5f, 0.3f, 0.03f), mDark, rot: new Vector3(0, i * -20f, 0));
            screens.Add(Prim("SideScreen", s, new Vector3(i * 0.85f, 1.02f, -0.02f), new Vector3(0.46f, 0.26f, 0.01f), mScreen, rot: new Vector3(0, i * -20f, 0)).GetComponent<Renderer>());
        }

        Chair(s, new Vector3(0, 0, -1.3f), 0f);

        screenAnchor = new GameObject("MainScreen");
        screenAnchor.transform.SetParent(s, false);
        screenAnchor.transform.localPosition = new Vector3(0, 1.1f, -0.145f);

        var sp = new GameObject("SeatPoint"); sp.transform.SetParent(s, false); sp.transform.localPosition = new Vector3(0, 0, -1.25f);
        seatPoint = sp.transform;

        // Marcador luminoso detrás de la silla para identificar la estación
        var m = new GameObject("SeatMarker"); m.transform.SetParent(s, false); m.transform.localPosition = new Vector3(0, 0, -2.1f);
        Prim("Ring", m.transform, new Vector3(0, 0.02f, 0), new Vector3(1.3f, 0.01f, 1.3f), mCyan, PrimitiveType.Cylinder);
        Sign(m.transform, "SignEstacion", new Vector3(0, 2.3f, 1.2f), 0f, "ESTACIÓN TI-01", 0.22f, hang: true);
        LightAt(m.transform, new Vector3(0, 1.5f, 0.6f), new Color(0.3f, 1f, 1f), 2f, 5f);
        marker = m;
    }

    // Mapa de red en la pared del fondo de la Zona TI (frente al usuario cuando se levanta).
    static InfectionMap BuildInfectionMap(Transform ti)
    {
        var g = Group(ti, "MapaDeRed", new Vector3(0f, 2.05f, TIEnd - 0.08f), 0f);
        Prim("Frame", g, new Vector3(0f, 0f, 0.02f), new Vector3(4.4f, 2.0f, 0.03f), mMetal);
        Prim("Panel", g, new Vector3(0f, 0f, 0f), new Vector3(4.3f, 1.9f, 0.03f), mMapBg);
        var title = T3(g, "Title", new Vector3(0f, 0.8f, -0.02f), "MAPA DE RED · BANCO ANDINO", 0.11f, new Color(0.6f, 0.9f, 1f), new Vector2(4f, 0.2f));
        title.fontStyle = FontStyles.Bold;
        T3(g, "Sub", new Vector3(0f, 0.64f, -0.02f), "Así se propaga un virus (ransomware) desde un solo equipo", 0.06f, new Color(0.75f, 0.82f, 0.9f), new Vector2(4f, 0.14f));

        var defs = new (string label, float x, float y, int parent)[]
        {
            ("TU PC\nTI-01", -1.7f, 0.05f, -1),
            ("SERVIDOR\nDE CORREO", -0.95f, 0.35f, 0),
            ("SERVIDOR\nCENTRAL", -0.2f, 0.05f, 1),
            ("COPIAS DE\nSEGURIDAD", -0.95f, -0.4f, 2),
            ("CONTABILIDAD", 0.6f, 0.42f, 2),
            ("CRÉDITOS", 0.6f, 0.14f, 2),
            ("GERENCIA", 0.6f, -0.14f, 2),
            ("RR.HH.", 0.6f, -0.42f, 2),
            ("CAJAS\n01-05", 1.5f, 0.28f, 5),
            ("CAJEROS\nATM", 1.5f, -0.28f, 8),
        };
        int n = defs.Length;
        var nodes = new Renderer[n]; var links = new Renderer[n]; var labels = new TMP_Text[n];
        var parent = new int[n]; var points = new Vector3[n];
        for (int i = 0; i < n; i++)
        {
            var d = defs[i];
            parent[i] = d.parent;
            points[i] = new Vector3(d.x, d.y, -0.045f);
            var node = Group(g, "Nodo_" + i, new Vector3(d.x, d.y, 0f));
            Prim("Bg", node, new Vector3(0f, 0f, -0.025f), new Vector3(0.56f, 0.21f, 0.02f), mNodeBg);
            nodes[i] = Prim("Status", node, new Vector3(0f, -0.09f, -0.037f), new Vector3(0.56f, 0.03f, 0.004f), mNodeOk).GetComponent<Renderer>();
            var lb = T3(node, "Label", new Vector3(0f, 0.012f, -0.038f), d.label, 0.05f, Color.white, new Vector2(0.52f, 0.16f));
            AutoSize(lb, 0.22f);
            labels[i] = lb;
            if (d.parent >= 0)
            {
                var a = new Vector2(defs[d.parent].x, defs[d.parent].y); var b = new Vector2(d.x, d.y);
                Vector2 dv = b - a; float ang = Mathf.Atan2(dv.y, dv.x) * Mathf.Rad2Deg;
                links[i] = Prim("Link_" + i, g, new Vector3((a.x + b.x) * 0.5f, (a.y + b.y) * 0.5f, -0.018f),
                    new Vector3(dv.magnitude, 0.018f, 0.004f), mLinkIdle, rot: new Vector3(0f, 0f, ang)).GetComponent<Renderer>();
            }
        }
        var packet = Prim("Paquete", g, points[0], Vector3.one * 0.07f, mNodeBad, PrimitiveType.Sphere).transform;

        // Leyenda y contador
        Prim("LegendOk", g, new Vector3(-1.95f, -0.78f, -0.02f), new Vector3(0.08f, 0.04f, 0.004f), mNodeOk);
        T3(g, "LegendOkT", new Vector3(-1.62f, -0.78f, -0.02f), "Seguro", 0.05f, Color.white, new Vector2(0.5f, 0.1f)).alignment = TextAlignmentOptions.Left;
        Prim("LegendBad", g, new Vector3(-1.2f, -0.78f, -0.02f), new Vector3(0.08f, 0.04f, 0.004f), mNodeBad);
        T3(g, "LegendBadT", new Vector3(-0.87f, -0.78f, -0.02f), "Infectado", 0.05f, Color.white, new Vector2(0.5f, 0.1f)).alignment = TextAlignmentOptions.Left;
        var status = T3(g, "Status", new Vector3(0.95f, -0.78f, -0.02f), "", 0.065f, new Color(0.3f, 1f, 0.55f), new Vector2(2.2f, 0.14f));
        status.alignment = TextAlignmentOptions.Right; status.fontStyle = FontStyles.Bold;

        var map = g.gameObject.AddComponent<InfectionMap>();
        map.nodes = nodes; map.links = links; map.labels = labels; map.parent = parent; map.points = points;
        map.infected = mNodeBad; map.packet = packet; map.status = status;
        return map;
    }

    static void BuildBeacons(Transform env)
    {
        foreach (var pos in new[]
        {
            new Vector3(-5.2f, 3.2f, 2f), new Vector3(5.2f, 3.2f, 2f), new Vector3(-5.2f, 3.2f, 15.5f), new Vector3(5.2f, 3.2f, 15.5f),
            new Vector3(0f, 3.2f, 23f), new Vector3(-3f, 3.2f, 33f), new Vector3(3f, 3.2f, 39.3f)
        })
        {
            var b = new GameObject("Beacon").transform; b.SetParent(env, false); b.position = pos;
            Prim("Lamp", b, Vector3.zero, new Vector3(0.25f, 0.12f, 0.25f), mRed, PrimitiveType.Cylinder);
            var s = new GameObject("Spot"); s.transform.SetParent(b, false); s.transform.localEulerAngles = new Vector3(25, 0, 0);
            var l = s.AddComponent<Light>(); l.type = LightType.Spot; l.color = new Color(1f, 0.05f, 0.05f); l.intensity = 14f; l.range = 14f; l.spotAngle = 60f;
            l.shadows = LightShadows.None;
            beacons.Add(b);
            b.gameObject.SetActive(false);   // se encienden solo con la alerta
        }
    }

    // ---------------------------------------------------------- POST-PROCESADO
    static void BuildPostFX()
    {
        const string path = "Assets/Settings/CyberSim_PostFX.asset";
        var prof = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
        if (prof == null) { prof = ScriptableObject.CreateInstance<VolumeProfile>(); AssetDatabase.CreateAsset(prof, path); }

        T Get<T>() where T : VolumeComponent
        {
            if (!prof.TryGet<T>(out var c))
            {
                c = prof.Add<T>(true);
                c.name = typeof(T).Name;
                AssetDatabase.AddObjectToAsset(c, prof);
            }
            return c;
        }

        var bloom = Get<URP.Bloom>();
        bloom.threshold.Override(1.0f); bloom.intensity.Override(0.55f); bloom.scatter.Override(0.6f);
        var tone = Get<URP.Tonemapping>();
        tone.mode.Override(URP.TonemappingMode.Neutral);
        var col = Get<URP.ColorAdjustments>();
        col.postExposure.Override(0.15f); col.contrast.Override(10f); col.saturation.Override(6f);
        var vig = Get<URP.Vignette>();
        vig.intensity.Override(0.22f); vig.smoothness.Override(0.45f);
        EditorUtility.SetDirty(prof);

        var v = new GameObject("PostFX").AddComponent<Volume>();
        v.isGlobal = true; v.sharedProfile = prof;
    }

    // ----------------------------------------------------- PLAYER + MANAGERS
    static void BuildPlayerAndManagers(Transform seatPoint, GameObject screenAnchor, GameObject marker, DoorInteraction door, InfectionMap map)
    {
        var player = new GameObject("Player"); player.transform.position = new Vector3(0, 0, 1.5f);
        var cc = player.AddComponent<CharacterController>(); cc.height = 1.8f; cc.radius = 0.3f; cc.center = new Vector3(0, 0.9f, 0);
        var cg = new GameObject("Main Camera") { tag = "MainCamera" }; cg.transform.SetParent(player.transform, false);
        cg.transform.localPosition = new Vector3(0, 1.65f, 0);
        var cam = cg.AddComponent<Camera>(); cam.nearClipPlane = 0.05f; cam.farClipPlane = 60f; cam.fieldOfView = 65f;
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.45f, 0.47f, 0.50f);
        var camData = cg.GetComponent<URP.UniversalAdditionalCameraData>();
        if (camData == null) camData = cg.AddComponent<URP.UniversalAdditionalCameraData>();
        camData.renderPostProcessing = true;
        camData.antialiasing = URP.AntialiasingMode.FastApproximateAntialiasing;
        cg.AddComponent<AudioListener>();
        var pc = player.AddComponent<PlayerController>(); pc.cam = cg.transform;

        var monitor = screenAnchor.AddComponent<MonitorUI>();

        var sim = new GameObject("Simulation");
        var seat = sim.AddComponent<SeatInteraction>(); seat.seatPoint = seatPoint; seat.player = pc;
        seat.sitRadius = 1.2f;   // solo desde el lado de la silla, no a través del escritorio
        seat.seatedCamLocal = new Vector3(0f, 1.10f, 0.69f);   // el monitor ocupa casi toda la pantalla al sentarse
        var audio = sim.AddComponent<SynthAudio>();
        var lighting = sim.AddComponent<LightingController>();
        lighting.strips = strips.ToArray(); lighting.screens = screens.ToArray(); lighting.leds = leds.ToArray();
        lighting.stripAlert = mRed; lighting.screenAlert = mScreenAlert; lighting.ledAlert = mRed;
        lighting.lights = lights.ToArray(); lighting.beacons = beacons.ToArray(); lighting.wallStatus = wallStatus;
        var overlay = sim.AddComponent<OverlayUI>(); overlay.seat = seat; overlay.door = door;
        door.player = player.transform;
        map.audioFx = audio;
        var mgr = sim.AddComponent<SimulationManager>();
        mgr.player = pc; mgr.seat = seat; mgr.monitor = monitor; mgr.lighting = lighting; mgr.overlay = overlay; mgr.audioFx = audio;
        mgr.seatMarker = marker; mgr.door = door; mgr.infectionMap = map;
        var menu = sim.AddComponent<GameMenu>(); menu.player = pc; menu.overlay = overlay;
    }
}
#endif
