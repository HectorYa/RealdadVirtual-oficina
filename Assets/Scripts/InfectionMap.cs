using System;
using System.Collections;
using TMPro;
using UnityEngine;

/// Mapa de red en la pared de la Zona TI. Al ejecutarse el malware muestra, nodo por nodo,
/// cómo la infección viaja desde el PC del usuario hasta el resto del banco, y pone en rojo
/// las pantallas reales de cada área (oficinas, cajas, cajero) en ese mismo orden.
/// TODO es FICTICIO: solo una visualización educativa.
public class InfectionMap : MonoBehaviour
{
    [Serializable] public class ScreenGroup { public Renderer[] renderers; }

    public Renderer[] nodes;          // barra de estado de cada equipo
    public Renderer[] links;          // enlace desde su padre (null en la raíz)
    public TMP_Text[] labels;
    public int[] parent;              // índice del nodo que lo infecta (-1 = origen)
    public Vector3[] points;          // posiciones locales de los nodos (para el paquete)
    public ScreenGroup[] nodeScreens; // pantallas 3D de cada área
    public Material infected, screenInfected;
    public Transform packet;          // "paquete" rojo que viaja por los enlaces
    public TMP_Text status;
    public SynthAudio audioFx;
    public float travel = 0.7f, pause = 0.55f, areaPause = 1.4f;   // las áreas del banco (nodo 4+) se muestran más tiempo

    /// Se lanza al empezar a atacar cada nodo (para mover la cámara de seguridad a esa área).
    public event Action<int> OnNodeTargeted;
    public bool Done { get; private set; }
    bool playing;
    static readonly Color Red = new Color(1f, 0.32f, 0.27f);

    void Start()
    {
        if (packet) packet.gameObject.SetActive(false);
        SetStatus(0);
    }

    public void Play()
    {
        if (playing) return;
        playing = true;
        StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        for (int i = 0; i < nodes.Length; i++)
        {
            OnNodeTargeted?.Invoke(i);
            int p = parent[i];
            if (p >= 0 && packet)
            {
                packet.gameObject.SetActive(true);
                for (float t = 0; t < travel; t += Time.deltaTime)
                {
                    packet.localPosition = Vector3.Lerp(points[p], points[i], t / travel);
                    yield return null;
                }
                packet.gameObject.SetActive(false);
                if (links[i]) links[i].sharedMaterial = infected;
            }
            else yield return new WaitForSeconds(0.3f);
            nodes[i].sharedMaterial = infected;
            if (labels[i]) labels[i].color = Red;
            if (nodeScreens != null && i < nodeScreens.Length && nodeScreens[i] != null && screenInfected)
                foreach (var r in nodeScreens[i].renderers) if (r) r.sharedMaterial = screenInfected;
            if (audioFx) audioFx.Blip();
            SetStatus(i + 1);
            yield return new WaitForSeconds(i >= 4 ? areaPause : pause);
        }
        if (status) status.text = $"RED COMPROMETIDA: {nodes.Length} de {nodes.Length} equipos infectados";
        Done = true;
    }

    /// Modo Defensa: el ataque se detuvo en el filtro; la red sigue intacta.
    public void SetContained()
    {
        if (playing || !status) return;
        status.text = $"AMENAZA CONTENIDA · 0 de {nodes.Length} equipos afectados";
        status.color = new Color(0.3f, 1f, 0.55f);
    }

    void SetStatus(int n)
    {
        if (!status) return;
        status.text = $"Equipos comprometidos: {n} / {nodes.Length}";
        if (n > 0) status.color = Red;
    }
}
