using UnityEngine;
using UnityEngine.UI;

// AJOUTÉ (2026-10-29, demande utilisateur : "restyliser la barre de chargement dans le loading screen, mettre des
// éléments comme les caractères du texte du titre, pour retrouver un peu la DA Fantasy médiéval et les couleurs actuelles
// qu'on peut retrouver souvent dans le jeu") - habillage de la barre de chargement, posé tout seul par
// LoadingScreenController sur le Slider existant (la logique de progression ne change pas : on lit slider.value).
// Reprend le vocabulaire du logo « Aether Storm Survivor » :
//  - plaque de bronze sombre aux bouts pointus (comme les empattements des lettres), liseré doré + filet intérieur ;
//  - rainure sombre où coule le remplissage ;
//  - aux deux bouts, un losange doré serti d'un cristal bleu (le losange ornemental du « O » du titre), prolongé par
//    une petite pointe dorée ;
//  - remplissage en cristal de mana : dégradé bleu profond -> cyan, facettes en biais, arête claire en haut ;
//  - une pointe de cristal lumineuse avance au bout du remplissage, et un reflet balaie le cristal.
// Couleurs du jeu : or des cartes et du HUD (1 / 0,79 / 0,30), bronze/bois sombre des cadres, cyan de la mana.
// Tout est dessiné en code (aucun asset), en double résolution avec mipmaps pour rester net quelle que soit la taille.
[RequireComponent(typeof(Slider))]
public class LoadingBarStyle : MonoBehaviour
{
    // Dimensions en pixels de référence (1920x1080), centrées sur la barre.
    private const float Width = 860f, Height = 76f;
    private const float PlateL = 372f, PlateH = 17f, PlateK = 16f;     // plaque : demi-longueur, demi-hauteur, pointe
    private const float TrackL = 352f, TrackH = 9.5f, TrackK = 9f;     // rainure
    private const float DiamondX = 377f, DiamondA = 25f, DiamondB = 30f;
    private const int Scale = 2;                                        // résolution des textures
    private const float DisplayScale = 0.94f;                           // taille affichée de toute la barre (1 = taille dessinée)
    private const float GlintSweepSeconds = 2.75f;                       // durée d'un passage du reflet (plus grand = plus lent)

    private static readonly Color Gold = new Color(1f, 0.788f, 0.302f);
    private static readonly Color GoldLight = new Color(1f, 0.93f, 0.66f);
    private static readonly Color GoldDeep = new Color(0.62f, 0.38f, 0.12f);
    private static readonly Color Copper = new Color(0.78f, 0.36f, 0.12f);   // cuivre des lettres du titre
    private static readonly Color Outline = new Color(0.09f, 0.05f, 0.03f);
    private static readonly Color BronzeTop = new Color(0.27f, 0.17f, 0.09f);
    private static readonly Color BronzeBottom = new Color(0.15f, 0.09f, 0.05f);
    private static readonly Color Groove = new Color(0.04f, 0.06f, 0.11f);
    private static readonly Color ManaDeep = new Color(0.08f, 0.27f, 0.62f);
    private static readonly Color ManaMid = new Color(0.17f, 0.74f, 0.88f);
    private static readonly Color ManaLight = new Color(0.62f, 0.96f, 1f);

    private Slider _slider;
    private Image _fill;
    private RectTransform _mask, _glint, _tip, _tipGlow;
    private Image _tipGlowImage;

    private void Awake()
    {
        _slider = GetComponent<Slider>();

        // L'habillage d'origine (cadre, fond, remplissage) est masqué ; le Slider continue de tourner dessous.
        foreach (Transform child in transform) child.gameObject.SetActive(false);

        RectTransform root = (RectTransform)transform;
        root.sizeDelta = new Vector2(Width, Height);
        root.localScale = Vector3.one * DisplayScale;

        MakeImage("Frame", MakeFrameBase(), Vector2.zero, new Vector2(Width, Height));

        // Remplissage : image « Filled » pleine largeur de la rainure, rognée par fillAmount (la texture ne s'étire pas).
        Vector2 trackSize = new Vector2(TrackL * 2f, TrackH * 2f + 2f);
        _fill = MakeImage("ManaFill", MakeFill(trackSize), Vector2.zero, trackSize);
        _fill.type = Image.Type.Filled;
        _fill.fillMethod = Image.FillMethod.Horizontal;
        _fill.fillOrigin = (int)Image.OriginHorizontal.Left;

        // Reflet qui balaie le cristal, limité à la partie remplie.
        GameObject maskGo = new GameObject("GlintMask", typeof(RectTransform), typeof(RectMask2D));
        maskGo.transform.SetParent(transform, false);
        _mask = (RectTransform)maskGo.transform;
        _mask.anchorMin = _mask.anchorMax = new Vector2(0.5f, 0.5f);
        _mask.pivot = new Vector2(0f, 0.5f);
        _mask.anchoredPosition = new Vector2(-TrackL, 0f);
        _glint = MakeImage("Glint", MakeGlint(), Vector2.zero, new Vector2(70f, trackSize.y), _mask).rectTransform;
        _glint.anchorMin = _glint.anchorMax = new Vector2(0f, 0.5f);

        // Losanges sertis + pointes, par-dessus le bout du remplissage.
        MakeImage("Ornaments", MakeFrameTop(), Vector2.zero, new Vector2(Width, Height));

        // Pointe de cristal au bout du remplissage.
        _tipGlowImage = MakeImage("TipGlow", MakeGlow(), Vector2.zero, new Vector2(64f, 64f));
        _tipGlow = _tipGlowImage.rectTransform;
        _tip = MakeImage("TipCrystal", MakeTipCrystal(), Vector2.zero, new Vector2(18f, 38f)).rectTransform;

        Refresh();
    }

    private void Update() => Refresh();

    private void Refresh()
    {
        float v = Mathf.InverseLerp(_slider.minValue, _slider.maxValue, _slider.value);
        _fill.fillAmount = v;
        float w = TrackL * 2f * v;
        _mask.sizeDelta = new Vector2(w, TrackH * 2f + 2f);

        float t = Time.unscaledTime;
        float sweep = Mathf.Repeat(t / GlintSweepSeconds, 1f);
        _glint.anchoredPosition = new Vector2(Mathf.Lerp(-40f, TrackL * 2f + 40f, sweep), 0f);

        bool showTip = v > 0.005f && v < 0.999f;
        _tip.gameObject.SetActive(showTip);
        _tipGlow.gameObject.SetActive(showTip);
        if (!showTip) return;
        float x = -TrackL + w;
        float bob = Mathf.Sin(t * 3.1f) * 0.6f;
        _tip.anchoredPosition = new Vector2(x, bob);
        _tipGlow.anchoredPosition = new Vector2(x, 0f);
        float pulse = 0.55f + 0.2f * Mathf.Sin(t * 4.2f);
        _tipGlowImage.color = new Color(ManaLight.r, ManaLight.g, ManaLight.b, pulse);
    }

    private Image MakeImage(string name, Sprite sprite, Vector2 pos, Vector2 size, Transform parent = null)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent != null ? parent : transform, false);
        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        Image img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.raycastTarget = false;
        return img;
    }

    // ---------------------------------------------------------------------------------------------------------------
    // Formes (distances signées en pixels de référence : négatif = dedans)
    // ---------------------------------------------------------------------------------------------------------------

    // Barre aux bouts pointus : rectangle de demi-longueur L, demi-hauteur h, dont les bouts se terminent en pointe de
    // longueur k (comme un hexagone allongé).
    private static float PointedBar(float x, float y, float L, float h, float k)
    {
        float ax = Mathf.Abs(x), ay = Mathf.Abs(y);
        float n = Mathf.Sqrt(h * h + k * k);
        float edge = ((ax - L) * h + ay * k) / n;
        return Mathf.Max(ay - h, edge);
    }

    private static float Diamond(float x, float y, float a, float b)
    {
        return (Mathf.Abs(x) * b + Mathf.Abs(y) * a - a * b) / Mathf.Sqrt(a * a + b * b);
    }

    private static float Cover(float d, float px) => Mathf.Clamp01(0.5f - d * px);

    private static Color Over(Color dst, Color src, float a)
    {
        a = Mathf.Clamp01(a * src.a);
        float outA = a + dst.a * (1f - a);
        if (outA <= 0f) return new Color(0f, 0f, 0f, 0f);
        Color c = (src * a + dst * dst.a * (1f - a)) / outA;
        c.a = outA;
        return c;
    }

    private delegate Color Painter(float x, float y, float px);

    // Rend une texture centrée de taille w x h (pixels de référence) à Scale fois la résolution, avec mipmaps.
    private static Sprite Render(float w, float h, Painter paint)
    {
        int tw = Mathf.CeilToInt(w * Scale), th = Mathf.CeilToInt(h * Scale);
        Texture2D tex = new Texture2D(tw, th, TextureFormat.RGBA32, true);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Trilinear;
        Color[] px = new Color[tw * th];
        float inv = 1f / Scale;
        for (int j = 0; j < th; j++)
            for (int i = 0; i < tw; i++)
            {
                float x = (i + 0.5f) * inv - w * 0.5f;
                float y = (j + 0.5f) * inv - h * 0.5f;
                px[j * tw + i] = paint(x, y, Scale);
            }
        tex.SetPixels(px);
        tex.Apply(true, true);
        tex.hideFlags = HideFlags.HideAndDontSave;
        return Sprite.Create(tex, new Rect(0, 0, tw, th), new Vector2(0.5f, 0.5f), 100f * Scale);
    }

    // Plaque de bronze, liseré doré, filet intérieur, rainure.
    private static Sprite MakeFrameBase()
    {
        return Render(Width, Height, (x, y, s) =>
        {
            Color c = new Color(0f, 0f, 0f, 0f);
            float plate = PointedBar(x, y, PlateL, PlateH, PlateK);

            // ombre portée douce (lisibilité sur l'illustration claire)
            float shadow = PointedBar(x, y + 3f, PlateL, PlateH, PlateK);
            c = Over(c, new Color(0f, 0f, 0f, 1f), 0.5f * Mathf.Clamp01(1f - Mathf.Max(0f, shadow) / 9f));

            // contour sombre, liseré doré (plus clair en haut), puis bronze
            c = Over(c, Outline, Cover(plate - 1.4f, s));
            float k = Mathf.InverseLerp(-PlateH, PlateH, y);
            Color rim = k < 0.5f ? Color.Lerp(Copper, Gold, k * 2f) : Color.Lerp(Gold, GoldLight, (k - 0.5f) * 2f);
            c = Over(c, rim, Cover(plate, s));
            Color bronze = Color.Lerp(BronzeBottom, BronzeTop, k);
            c = Over(c, bronze, Cover(plate + 2.6f, s));

            // filet doré intérieur
            float inner = Mathf.Abs(plate + 4.6f) - 0.55f;
            c = Over(c, Gold, 0.55f * Cover(inner, s));

            // rainure, plus sombre en haut (creusée)
            float track = PointedBar(x, y, TrackL, TrackH, TrackK);
            c = Over(c, Outline, Cover(track - 1f, s));
            float g = Mathf.InverseLerp(TrackH, -TrackH, y);
            c = Over(c, Color.Lerp(Groove * 0.6f, Groove * 1.6f, g), Cover(track, s));
            return c;
        });
    }

    // Losanges dorés sertis d'un cristal, et pointes dorées aux deux bouts.
    private static Sprite MakeFrameTop()
    {
        return Render(Width, Height, (x, y, s) =>
        {
            Color c = new Color(0f, 0f, 0f, 0f);
            float side = Mathf.Sign(x);
            float lx = x - side * DiamondX;   // repère local du losange

            // petits losanges dorés sur le liseré (haut et bas), comme les ornements des lettres du titre
            for (int i = -1; i <= 1; i++)
            {
                float ox = x - i * PlateL * 0.5f;
                float oy = Mathf.Abs(y) - PlateH;
                float stud = Diamond(ox, oy, 7f, 5f);
                if (stud > 2f) continue;
                c = Over(c, Outline, Cover(stud - 1.2f, s));
                c = Over(c, y > 0f ? Color.Lerp(Gold, GoldLight, 0.5f) : Gold, Cover(stud, s));
                c = Over(c, ManaMid, Cover(Diamond(ox, oy, 2.6f, 2f), s));
            }

            // pointe au-delà du losange
            float spike = Diamond(lx - side * 30f, y, 16f, 3.2f);
            c = Over(c, Outline, Cover(spike - 1.2f, s));
            c = Over(c, Color.Lerp(GoldDeep, GoldLight, Mathf.InverseLerp(-3f, 3f, y)), Cover(spike, s));

            float d = Diamond(lx, y, DiamondA, DiamondB);
            float shadow = Diamond(lx, y + 3f, DiamondA, DiamondB);
            c = Over(c, new Color(0f, 0f, 0f, 1f), 0.45f * Mathf.Clamp01(1f - Mathf.Max(0f, shadow) / 7f));
            c = Over(c, Outline, Cover(d - 1.4f, s));

            // or biseauté : la moitié haute-gauche plus claire (lumière du logo, venue du haut)
            bool lit = (y - lx * 0.35f) > 0f;
            Color gold = lit ? Color.Lerp(Gold, GoldLight, 0.45f) : Color.Lerp(Gold, GoldDeep, 0.45f);
            c = Over(c, gold, Cover(d, s));

            // gorge sombre du serti
            float bezel = Diamond(lx, y, DiamondA - 7f, DiamondB - 8f);
            c = Over(c, Outline, Cover(bezel, s));

            // cristal : 4 facettes de luminosité différente, arête claire
            float gem = Diamond(lx, y, DiamondA - 9.5f, DiamondB - 10.5f);
            Color facet = lx < 0f
                ? (y > 0f ? ManaLight : ManaMid)
                : (y > 0f ? Color.Lerp(ManaMid, ManaLight, 0.4f) : ManaDeep);
            c = Over(c, facet, Cover(gem, s));
            float ridge = Mathf.Min(Mathf.Abs(lx) - 0.5f, Mathf.Abs(y) - 0.5f);
            c = Over(c, Color.white, 0.35f * Cover(Mathf.Max(ridge, gem), s));
            float spark = Diamond(lx + 4f, y - 6f, 2.6f, 3.4f);
            c = Over(c, Color.white, 0.9f * Cover(spark, s));
            return c;
        });
    }

    // Cristal de mana du remplissage : dégradé vertical, facettes en biais, arête claire en haut.
    private static Sprite MakeFill(Vector2 size)
    {
        return Render(size.x, size.y, (x, y, s) =>
        {
            float shape = PointedBar(x, y, TrackL - 1.2f, TrackH - 1.2f, TrackK - 1.1f);
            float a = Cover(shape, s);
            if (a <= 0f) return new Color(0f, 0f, 0f, 0f);

            float k = Mathf.InverseLerp(-TrackH, TrackH, y);
            Color c = k < 0.55f ? Color.Lerp(ManaDeep, ManaMid, k / 0.55f) : Color.Lerp(ManaMid, ManaLight, (k - 0.55f) / 0.45f);

            // facettes : bandes en biais de largeurs et de luminosités inégales (cristal taillé, pas des rayures)
            float u = (x + y * 1.6f) / 38f;
            float cell = Mathf.Floor(u);
            float h1 = Hash(cell), h2 = Hash(cell + 17.3f);
            float cut = 0.35f + 0.3f * h1;                 // la bande est coupée en deux facettes, à un endroit variable
            float local = u - cell;
            float facet = local < cut ? 0.92f + 0.12f * h2 : 1.0f + 0.1f * h1;
            float edgeLine = Mathf.Clamp01(1f - Mathf.Abs(local - cut) * 38f / 1.1f);
            c *= facet;
            c = Color.Lerp(c, ManaLight, 0.22f * edgeLine);

            // arête claire en haut, ombre en bas
            c = Color.Lerp(c, Color.white, 0.45f * Mathf.Clamp01(1f - Mathf.Abs(y - (TrackH - 3.2f)) / 1.3f));
            c = Color.Lerp(c, ManaDeep * 0.6f, 0.35f * Mathf.Clamp01((-TrackH + 3f - y) / 3f));
            c.a = a;
            return c;
        });
    }

    private static float Hash(float n) => Mathf.Repeat(Mathf.Sin(n * 127.1f) * 43758.5453f, 1f);

    private static Sprite MakeGlint()
    {
        return Render(70f, TrackH * 2f + 2f, (x, y, s) =>
        {
            float d = Mathf.Abs(x + y * 1.6f) / 18f;
            float a = Mathf.Clamp01(1f - d);
            return new Color(1f, 1f, 1f, 0.5f * a * a);
        });
    }

    private static Sprite MakeGlow()
    {
        return Render(64f, 64f, (x, y, s) =>
        {
            float r = Mathf.Sqrt(x * x + y * y) / 32f;
            float a = Mathf.Clamp01(1f - r);
            return new Color(1f, 1f, 1f, a * a * a);
        });
    }

    // Pointe de cristal verticale, deux facettes, contour sombre.
    private static Sprite MakeTipCrystal()
    {
        return Render(18f, 38f, (x, y, s) =>
        {
            Color c = new Color(0f, 0f, 0f, 0f);
            float d = Diamond(x, y, 7f, 17f);
            c = Over(c, Outline, Cover(d - 1.3f, s));
            Color f = x < 0f ? Color.Lerp(ManaLight, Color.white, 0.6f) : Color.Lerp(ManaMid, ManaLight, 0.5f);
            if (y < 0f) f = Color.Lerp(f, ManaMid, 0.35f);
            c = Over(c, f, Cover(d, s));
            c = Over(c, Color.white, 0.8f * Cover(Mathf.Max(Mathf.Abs(x) - 0.5f, d + 2f), s));
            return c;
        });
    }
}
