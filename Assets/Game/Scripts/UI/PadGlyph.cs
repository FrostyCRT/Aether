using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// AJOUTÉ (2026-10-28, session manette) - icône d'un bouton de manette, au style de la marque branchée
// (GameInput.PadStyle) :
//   - Xbox : disque sombre, lettre de couleur (A vert, B rouge, X bleu, Y jaune) ;
//   - PlayStation : disque sombre, symbole de couleur (croix bleue, rond rouge, carré rose, triangle vert) ;
//   - Switch : disque sombre, lettre blanche ;
//   - gâchettes hautes / basses : pastille / forme de gâchette avec leur nom (LB, R1, ZL...) ;
//   - Start / Select : pastille avec le pictogramme menu (≡) / vue (deux carrés), + / − sur Switch ;
//   - clic des sticks : disque avec LS / L3.
// Tout est dessiné en code (aucune image à importer) et mis en cache. Les lettres sont du texte (Fira Sans), net à
// toutes les tailles ; formes et symboles sont des textures lissées 128 px.
public class PadGlyph : MonoBehaviour
{
    private Image _bg;
    private Image _symbol;
    private TextMeshProUGUI _text;
    private RectTransform _rt;

    private static readonly Color XboxA = new Color32(0x5D, 0xBB, 0x4E, 0xFF);
    private static readonly Color XboxB = new Color32(0xE0, 0x47, 0x3C, 0xFF);
    private static readonly Color XboxX = new Color32(0x3D, 0x8F, 0xE0, 0xFF);
    private static readonly Color XboxY = new Color32(0xF2, 0xC2, 0x30, 0xFF);
    private static readonly Color PsCross = new Color32(0x8F, 0xB4, 0xF0, 0xFF);
    private static readonly Color PsCircle = new Color32(0xF0, 0x75, 0x75, 0xFF);
    private static readonly Color PsSquare = new Color32(0xE3, 0x9A, 0xCF, 0xFF);
    private static readonly Color PsTriangle = new Color32(0x52, 0xCD, 0xB0, 0xFF);
    private static readonly Color Ink = new Color32(0xF2, 0xF2, 0xF2, 0xFF);

    // Crée une icône (cachée) sous `parent`, centrée ; la hauteur est celle de la ligne qui l'accueille.
    public static PadGlyph Create(Transform parent, float height)
    {
        GameObject go = new GameObject("PadGlyph", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        PadGlyph g = go.AddComponent<PadGlyph>();
        g._rt = (RectTransform)go.transform;
        g._rt.anchorMin = g._rt.anchorMax = g._rt.pivot = new Vector2(0.5f, 0.5f);
        g._rt.sizeDelta = new Vector2(height, height);

        g._bg = go.AddComponent<Image>();
        g._bg.raycastTarget = false;
        g._bg.preserveAspect = false;

        GameObject sym = new GameObject("Symbol", typeof(RectTransform));
        sym.transform.SetParent(go.transform, false);
        RectTransform srt = (RectTransform)sym.transform;
        srt.anchorMin = srt.anchorMax = srt.pivot = new Vector2(0.5f, 0.5f);
        g._symbol = sym.AddComponent<Image>();
        g._symbol.raycastTarget = false;

        GameObject txt = new GameObject("Label", typeof(RectTransform));
        txt.transform.SetParent(go.transform, false);
        RectTransform trt = (RectTransform)txt.transform;
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = trt.offsetMax = Vector2.zero;
        g._text = txt.AddComponent<TextMeshProUGUI>();
        g._text.raycastTarget = false;
        if (GameFonts.Body != null) { g._text.font = GameFonts.Body; g._text.fontSharedMaterial = GameFonts.Body.material; }
        g._text.alignment = TextAlignmentOptions.Center;
        g._text.fontStyle = FontStyles.Bold;
        g._text.textWrappingMode = TextWrappingModes.NoWrap;
        g._text.overflowMode = TextOverflowModes.Overflow;

        go.SetActive(false);
        return g;
    }

    public RectTransform Rect => _rt;

    // Affiche le bouton `b` au style `kind` ; renvoie la largeur occupée (la hauteur ne change pas).
    public float Show(PadButton b, GameInput.PadKind kind)
    {
        float h = _rt.sizeDelta.y;
        gameObject.SetActive(b != PadButton.None);
        if (b == PadButton.None) return 0f;

        Shape shape = ShapeOf(b);
        float w = h * Aspect(shape);
        _rt.sizeDelta = new Vector2(w, h);
        _bg.sprite = Background(shape);

        // symbole (PlayStation, Start / Select) ou texte
        Sprite symbol = null;
        Color symbolColor = Ink;
        string text = "";
        Color textColor = Ink;
        float textScale = 0.62f;

        switch (b)
        {
            case PadButton.South: case PadButton.East: case PadButton.West: case PadButton.North:
                if (kind == GameInput.PadKind.PlayStation)
                {
                    symbol = Symbol(b == PadButton.South ? "cross" : b == PadButton.East ? "circle" : b == PadButton.West ? "square" : "triangle");
                    symbolColor = b == PadButton.South ? PsCross : b == PadButton.East ? PsCircle : b == PadButton.West ? PsSquare : PsTriangle;
                }
                else
                {
                    text = GameInput.PadLabel(b, kind);
                    if (kind == GameInput.PadKind.Xbox)
                        textColor = b == PadButton.South ? XboxA : b == PadButton.East ? XboxB : b == PadButton.West ? XboxX : XboxY;
                }
                break;
            case PadButton.Start:
                symbol = Symbol(kind == GameInput.PadKind.Nintendo ? "plus" : "menu");
                break;
            case PadButton.Select:
                symbol = Symbol(kind == GameInput.PadKind.Nintendo ? "minus" : "view");
                break;
            default:   // gâchettes, clic des sticks : leur nom
                text = GameInput.PadLabel(b, kind);
                textScale = text.Length > 2 ? 0.40f : 0.46f;
                break;
        }

        _symbol.gameObject.SetActive(symbol != null);
        if (symbol != null)
        {
            _symbol.sprite = symbol;
            _symbol.color = symbolColor;
            float s = h * (shape == Shape.Disc ? 0.56f : 0.5f);
            ((RectTransform)_symbol.transform).sizeDelta = new Vector2(s, s);
        }
        _text.gameObject.SetActive(text.Length > 0);
        if (text.Length > 0)
        {
            _text.text = text;
            _text.color = textColor;
            _text.fontSize = h * textScale;
            // texte des gâchettes basses un peu plus bas (la forme est plus large en haut)
            _text.rectTransform.offsetMin = new Vector2(0f, shape == Shape.Trigger ? -h * 0.04f : 0f);
            _text.rectTransform.offsetMax = new Vector2(0f, shape == Shape.Trigger ? -h * 0.04f : 0f);
        }
        return w;
    }

    // ---- formes ------------------------------------------------------------------------------------------------
    private enum Shape { Disc, Shoulder, Trigger, Small }

    private static Shape ShapeOf(PadButton b)
    {
        switch (b)
        {
            case PadButton.LeftShoulder: case PadButton.RightShoulder: return Shape.Shoulder;
            case PadButton.LeftTrigger: case PadButton.RightTrigger: return Shape.Trigger;
            case PadButton.Start: case PadButton.Select: return Shape.Small;
        }
        return Shape.Disc;
    }

    private static float Aspect(Shape s) => s == Shape.Shoulder ? 1.6f : s == Shape.Trigger ? 1.3f : s == Shape.Small ? 1.35f : 1f;

    private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

    private const int Px = 128;                                            // hauteur des textures
    private static readonly Color32 Fill = new Color32(0x22, 0x23, 0x2B, 0xF2);
    private static readonly Color32 Rim = new Color32(0x5A, 0x5C, 0x6C, 0xFF);
    private const float RimWidth = 5f;

    private static Sprite Background(Shape shape)
    {
        string key = "bg_" + shape;
        if (Cache.TryGetValue(key, out Sprite sp) && sp != null) return sp;

        int h = Px, w = Mathf.RoundToInt(Px * Aspect(shape));
        Color32[] px = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                Vector2 p = new Vector2(x + 0.5f - w * 0.5f, y + 0.5f - h * 0.5f);
                float d;   // distance signée au bord (négatif = dedans), en pixels
                switch (shape)
                {
                    case Shape.Disc: d = p.magnitude - (h * 0.5f - 2f); break;
                    case Shape.Trigger:
                        // coins du haut très arrondis, coins du bas peu : silhouette de gâchette
                        d = RoundBox(p, new Vector2(w * 0.5f - 2f, h * 0.5f - 2f), p.y > 0f ? h * 0.42f : h * 0.16f);
                        break;
                    default: d = RoundBox(p, new Vector2(w * 0.5f - 2f, h * 0.5f - 2f), h * 0.5f - 2f); break;
                }
                float inside = Mathf.Clamp01(0.5f - d);
                float rim = Mathf.Clamp01(d + RimWidth + 0.5f);          // 1 près du bord, 0 au centre
                Color c = Color.Lerp((Color)Fill, (Color)Rim, rim);
                c.a *= inside;
                px[y * w + x] = c;
            }
        sp = Bake(px, w, h);
        Cache[key] = sp;
        return sp;
    }

    // Symboles blancs (teintés par l'Image), trait de 13 px sur 128.
    private static Sprite Symbol(string name)
    {
        string key = "sym_" + name;
        if (Cache.TryGetValue(key, out Sprite sp) && sp != null) return sp;

        const float stroke = 13f;
        int n = Px;
        Color32[] px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                Vector2 p = new Vector2(x + 0.5f - n * 0.5f, y + 0.5f - n * 0.5f);
                float d;
                float r = n * 0.5f - stroke * 0.5f - 2f;
                switch (name)
                {
                    case "cross":
                        d = Mathf.Min(Segment(p, new Vector2(-r, -r) * 0.78f, new Vector2(r, r) * 0.78f),
                                      Segment(p, new Vector2(-r, r) * 0.78f, new Vector2(r, -r) * 0.78f)) - stroke * 0.5f;
                        break;
                    case "circle": d = Mathf.Abs(p.magnitude - r * 0.86f) - stroke * 0.5f; break;
                    case "square": d = Mathf.Abs(RoundBox(p, new Vector2(r * 0.78f, r * 0.78f), 3f)) - stroke * 0.5f; break;
                    case "triangle":
                    {
                        Vector2 a = new Vector2(0f, r * 0.92f), b = new Vector2(-r * 0.92f, -r * 0.66f), c = new Vector2(r * 0.92f, -r * 0.66f);
                        d = Mathf.Min(Segment(p, a, b), Mathf.Min(Segment(p, b, c), Segment(p, c, a))) - stroke * 0.5f;
                        break;
                    }
                    case "menu":   // trois traits
                        d = Mathf.Min(Segment(p, new Vector2(-r * 0.7f, r * 0.55f), new Vector2(r * 0.7f, r * 0.55f)),
                            Mathf.Min(Segment(p, new Vector2(-r * 0.7f, 0f), new Vector2(r * 0.7f, 0f)),
                                      Segment(p, new Vector2(-r * 0.7f, -r * 0.55f), new Vector2(r * 0.7f, -r * 0.55f)))) - stroke * 0.42f;
                        break;
                    case "view":   // deux carrés décalés
                        d = Mathf.Min(Mathf.Abs(RoundBox(p - new Vector2(-r * 0.22f, r * 0.22f), new Vector2(r * 0.5f, r * 0.5f), 4f)),
                                      Mathf.Abs(RoundBox(p - new Vector2(r * 0.22f, -r * 0.22f), new Vector2(r * 0.5f, r * 0.5f), 4f))) - stroke * 0.38f;
                        break;
                    case "plus":
                        d = Mathf.Min(Segment(p, new Vector2(-r * 0.75f, 0f), new Vector2(r * 0.75f, 0f)),
                                      Segment(p, new Vector2(0f, -r * 0.75f), new Vector2(0f, r * 0.75f))) - stroke * 0.5f;
                        break;
                    default:       // minus
                        d = Segment(p, new Vector2(-r * 0.75f, 0f), new Vector2(r * 0.75f, 0f)) - stroke * 0.5f;
                        break;
                }
                float a01 = Mathf.Clamp01(0.5f - d);
                px[y * n + x] = new Color32(255, 255, 255, (byte)(a01 * 255f));
            }
        sp = Bake(px, n, n);
        Cache[key] = sp;
        return sp;
    }

    private static float RoundBox(Vector2 p, Vector2 half, float r)
    {
        r = Mathf.Min(r, Mathf.Min(half.x, half.y));
        Vector2 q = new Vector2(Mathf.Abs(p.x) - half.x + r, Mathf.Abs(p.y) - half.y + r);
        return new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - r;
    }

    private static float Segment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 pa = p - a, ba = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba));
        return (pa - ba * t).magnitude;
    }

    private static Sprite Bake(Color32[] px, int w, int h)
    {
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, true);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Trilinear;   // réduite à ~40 px dans le HUD : mipmaps, sinon crénelée
        tex.SetPixels32(px);
        tex.Apply(true, true);
        tex.hideFlags = HideFlags.HideAndDontSave;
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
    }
}
