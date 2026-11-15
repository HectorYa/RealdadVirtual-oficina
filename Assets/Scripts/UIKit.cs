using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// Utilidades para construir interfaz por código (origen arriba-izquierda, píxeles).
public static class UIKit
{
    static Sprite circle;

    public static Sprite Circle()
    {
        if (circle) return circle;
        int n = 64; var t = new Texture2D(n, n, TextureFormat.RGBA32, false);
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
                t.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(n / 2f - Vector2.Distance(new Vector2(x, y), new Vector2(n / 2f, n / 2f)))));
        t.Apply();
        circle = Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
        return circle;
    }

    public static RectTransform Panel(Transform p, string name, float x, float y, float w, float h, Color c, bool raycast = true)
    {
        var g = new GameObject(name, typeof(RectTransform), typeof(Image));
        g.transform.SetParent(p, false);
        var rt = (RectTransform)g.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = new Vector2(x, -y); rt.sizeDelta = new Vector2(w, h);
        var im = g.GetComponent<Image>(); im.color = c; im.raycastTarget = raycast;
        return rt;
    }

    public static TextMeshProUGUI Label(Transform p, string s, float x, float y, float w, float h, float size, Color c,
        TextAlignmentOptions a = TextAlignmentOptions.TopLeft, bool bold = false)
    {
        var g = new GameObject("Text", typeof(RectTransform));
        g.transform.SetParent(p, false);
        var rt = (RectTransform)g.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = new Vector2(x, -y); rt.sizeDelta = new Vector2(w, h);
        var t = g.AddComponent<TextMeshProUGUI>();
        t.text = s; t.fontSize = size; t.color = c; t.alignment = a; t.raycastTarget = false;
        t.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
        return t;
    }

    public static Button Btn(Transform p, string label, float x, float y, float w, float h, Color bg, Color fg, float size, UnityAction onClick)
    {
        var rt = Panel(p, "Button", x, y, w, h, bg);
        var b = rt.gameObject.AddComponent<Button>();
        b.targetGraphic = rt.GetComponent<Image>();
        var cb = b.colors;
        cb.highlightedColor = new Color(0.9f, 0.9f, 0.9f, 1f);
        cb.pressedColor = new Color(0.72f, 0.72f, 0.72f, 1f);
        b.colors = cb;
        Label(rt, label, 0, 0, w, h, size, fg, TextAlignmentOptions.Center, true);
        if (onClick != null) b.onClick.AddListener(onClick);
        return b;
    }
}
