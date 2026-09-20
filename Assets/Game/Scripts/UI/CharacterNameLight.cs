using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// AJOUTE (2026-09-19) - "lumière" du logo-nom de la page Personnages (retour utilisateur : les nouveaux
// logos manquent de luminosité blanche, ils paraissent un peu sombres).
//
// On ne retouche PAS les images des logos (elles restent celles de l'utilisateur, remplaçables) : la lumière
// est calculée à partir du logo AFFICHÉ, donc elle suit tout changement de logo. Trois couches, toutes
// additives (Aether/UI/Additive : elles AJOUTENT de la lumière au lieu de grisailler) :
//   1. HALO      derrière le logo : sa silhouette floutée, blanc chaud -> les lettres "rayonnent" et se
//                détachent du décor ;
//   2. ÉCLAT     sur le logo : uniquement ses parties déjà claires (métal, reflets) sont éclaircies vers
//                le blanc ; les creux restent sombres, le relief est conservé (un simple éclaircissement
//                uniforme aplatirait le dessin) ;
//   3. ÉTINCELLES quelques croix de lumière qui s'allument lentement sur les points les plus brillants.
// Le tout respire très lentement. Tout est généré une fois par logo (mis en cache), sans asset ni allocation
// par image. L'écran verrouillé l'éteint (SetLit(false)).
[RequireComponent(typeof(Image))]
public class CharacterNameLight : MonoBehaviour
{
    [Header("Matériau")]
    [Tooltip("Aether/UI/Additive. Sans lui, repli sur Shader.Find (peut manquer dans un build si le shader n'est référencé nulle part).")]
    [SerializeField] private Material _additiveMaterial;

    [Header("Intensités (0 = désactivé)")]
    [Range(0f, 1.5f)] [SerializeField] private float _halo = 0.75f;
    [Range(0f, 1.5f)] [SerializeField] private float _highlights = 0.42f;
    [Range(0f, 1.5f)] [SerializeField] private float _sparkles = 0.9f;
    [Tooltip("Part de la couleur du personnage mêlée au blanc du halo (0 = blanc pur).")]
    [Range(0f, 1f)] [SerializeField] private float _accentMix = 0.22f;
    [SerializeField] private int _sparkleCount = 4;

    // définition du calcul (pixels) : le logo est réduit à 512 px de large
    private const int W = 512, PAD = 72;

    private class Baked
    {
        public Sprite haloSprite, glowSprite;
        public Vector2 padScale;                  // (W + 2 PAD) / W, (H + 2 PAD) / H
        public List<Vector2> spots = new List<Vector2>();   // points brillants, 0..1 dans le logo (origine bas-gauche)
        public Texture2D[] textures;
    }

    private static readonly Dictionary<Sprite, Baked> Cache = new Dictionary<Sprite, Baked>();

    private Image _name;
    private RectTransform _nameRt;
    private RectTransform _back, _front;
    private Image _haloImg, _glowImg;
    private CanvasRenderer _haloCr, _glowCr;
    private readonly List<RectTransform> _spark = new List<RectTransform>();
    private readonly List<CanvasRenderer> _sparkCr = new List<CanvasRenderer>();
    private readonly List<float> _sparkStart = new List<float>();
    private readonly List<float> _sparkDur = new List<float>();
    private readonly List<int> _sparkSpot = new List<int>();
    private Sprite _sparkSprite;

    private Baked _cur;
    private Sprite _curSprite;
    private Color _accent = Color.white;
    private bool _lit = true;
    private bool _built;
    private float _fade;

    // --- API -----------------------------------------------------------------

    public void SetAccent(Color accent) => _accent = accent;

    // Verrouillé : plus de lumière (comme les autres lueurs de la page).
    public void SetLit(bool lit)
    {
        _lit = lit;
        if (!lit) _fade = 0f;
        if (_back != null) _back.gameObject.SetActive(lit);
        if (_front != null) _front.gameObject.SetActive(lit);
    }

    // --- cycle de vie ------------------------------------------------------------

    private void Awake() => Build();

    private void OnEnable()
    {
        _fade = 0f;
        if (_back != null) _back.gameObject.SetActive(_lit);
        if (_front != null) _front.gameObject.SetActive(_lit);
    }

    private void OnDisable()
    {
        if (_back != null) _back.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (_back != null) Destroy(_back.gameObject);
        // le cache (statique) est libéré quand la dernière instance disparaît : ~3 petits sprites par logo
        if (FindObjectsByType<CharacterNameLight>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length <= 1)
        {
            foreach (Baked b in Cache.Values)
                if (b.textures != null) foreach (Texture2D t in b.textures) if (t != null) Destroy(t);
            Cache.Clear();
        }
        if (_sparkSprite != null) { Destroy(_sparkSprite.texture); Destroy(_sparkSprite); }
    }

    private Material Additive()
    {
        if (_additiveMaterial != null) return _additiveMaterial;
        Shader sh = Shader.Find("Aether/UI/Additive");
        if (sh != null) _additiveMaterial = new Material(sh);
        return _additiveMaterial;
    }

    private void Build()
    {
        if (_built) return;
        _built = true;

        _name = GetComponent<Image>();
        _nameRt = (RectTransform)transform;

        // couche ARRIÈRE : frère juste avant le logo (un enfant se dessinerait devant lui)
        GameObject back = new GameObject("NameLight_Back", typeof(RectTransform));
        _back = (RectTransform)back.transform;
        _back.SetParent(_nameRt.parent, false);
        _back.SetSiblingIndex(_nameRt.GetSiblingIndex());
        _haloImg = MakeImage("Halo", _back, out _haloCr);

        // couche AVANT : enfant du logo (suit ses déplacements / son échelle tout seul)
        GameObject front = new GameObject("NameLight_Front", typeof(RectTransform));
        _front = (RectTransform)front.transform;
        _front.SetParent(_nameRt, false);
        _front.anchorMin = _front.anchorMax = _front.pivot = new Vector2(0.5f, 0.5f);
        _front.anchoredPosition = Vector2.zero;
        _glowImg = MakeImage("Highlights", _front, out _glowCr);

        _sparkSprite = MakeGlintSprite();
        for (int i = 0; i < Mathf.Max(0, _sparkleCount); i++)
        {
            GameObject g = new GameObject("Sparkle" + (i + 1), typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            g.transform.SetParent(_front, false);
            RectTransform rt = (RectTransform)g.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            Image im = g.GetComponent<Image>();
            im.sprite = _sparkSprite; im.raycastTarget = false; im.material = Additive();
            CanvasRenderer cr = g.GetComponent<CanvasRenderer>();
            cr.SetColor(new Color(1, 1, 1, 0));
            _spark.Add(rt); _sparkCr.Add(cr);
            _sparkStart.Add(Time.unscaledTime + Random.Range(0.3f, 4f));
            _sparkDur.Add(Random.Range(2.2f, 3.4f));
            _sparkSpot.Add(i);
        }

        SetLit(_lit);
    }

    private Image MakeImage(string objName, Transform parent, out CanvasRenderer cr)
    {
        GameObject go = new GameObject(objName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        Image im = go.GetComponent<Image>();
        im.raycastTarget = false;
        im.material = Additive();
        cr = go.GetComponent<CanvasRenderer>();
        cr.SetColor(new Color(1, 1, 1, 0));
        return im;
    }

    // --- boucle ---------------------------------------------------------------------

    private void LateUpdate()
    {
        if (!_built || !_lit) return;

        // le logo change (changement de personnage) : on (re)calcule / récupère la lumière du nouveau
        if (_name.sprite != _curSprite)
        {
            _curSprite = _name.sprite;
            _cur = _curSprite != null ? GetBaked(_curSprite) : null;
            if (_cur != null)
            {
                _haloImg.sprite = _cur.haloSprite;
                _glowImg.sprite = _cur.glowSprite;
                for (int i = 0; i < _spark.Count; i++) _sparkSpot[i] = _cur.spots.Count > 0 ? i % _cur.spots.Count : 0;
            }
        }
        if (_cur == null) return;

        _fade = Mathf.MoveTowards(_fade, GameSettings.MenuEffectsFactor, Time.unscaledDeltaTime / 0.8f);   // Paramètres > Graphismes > Effets d'ambiance

        // rectangle réellement occupé par le logo (Image en preserveAspect : ajusté dans son cadre)
        Vector2 box = _nameRt.rect.size;
        float aspect = _curSprite.rect.width / _curSprite.rect.height;
        Vector2 shown = box.x / box.y > aspect ? new Vector2(box.y * aspect, box.y) : new Vector2(box.x, box.x / aspect);

        // ARRIÈRE : suit position / échelle du logo (qui glisse au changement de personnage)
        _back.anchorMin = _nameRt.anchorMin; _back.anchorMax = _nameRt.anchorMax; _back.pivot = _nameRt.pivot;
        _back.anchoredPosition = _nameRt.anchoredPosition;
        _back.sizeDelta = _nameRt.sizeDelta;
        _back.localScale = _nameRt.localScale;
        _back.localRotation = _nameRt.localRotation;
        RectTransform h = _haloImg.rectTransform;
        h.sizeDelta = new Vector2(shown.x * _cur.padScale.x, shown.y * _cur.padScale.y);
        h.anchoredPosition = Vector2.zero;

        // AVANT : même rectangle que le logo affiché
        _front.sizeDelta = shown;
        RectTransform g = _glowImg.rectTransform;
        g.sizeDelta = new Vector2(shown.x * _cur.padScale.x, shown.y * _cur.padScale.y);

        float t = Time.unscaledTime;
        float breathe = 0.86f + 0.14f * Mathf.Sin(t * 0.7f);
        Color warm = Color.Lerp(Color.white, _accent, _accentMix);
        _haloCr.SetColor(new Color(warm.r, warm.g, warm.b, Mathf.Clamp01(_halo * 0.85f * breathe * _fade)));
        // reflets : blanc TEINTÉ par le personnage (un blanc pur sur du cuivre le fait virer au rose)
        Color hi = Color.Lerp(Color.white, _accent, Mathf.Clamp01(_accentMix * 2.4f));
        _glowCr.SetColor(new Color(hi.r, hi.g, hi.b, Mathf.Clamp01(_highlights * (0.9f + 0.1f * Mathf.Sin(t * 0.9f + 1f)) * _fade)));

        UpdateSparkles(t, shown);
    }

    private void UpdateSparkles(float t, Vector2 shown)
    {
        for (int i = 0; i < _spark.Count; i++)
        {
            if (_cur.spots.Count == 0) { _sparkCr[i].SetColor(new Color(1, 1, 1, 0)); continue; }
            float u = (t - _sparkStart[i]) / _sparkDur[i];
            if (u > 1f)
            {
                // prochaine apparition, sur un autre point brillant
                _sparkStart[i] = t + Random.Range(1.2f, 4.5f);
                _sparkDur[i] = Random.Range(2.2f, 3.4f);
                _sparkSpot[i] = Random.Range(0, _cur.spots.Count);
                u = -1f;
            }
            if (u < 0f) { _sparkCr[i].SetColor(new Color(1, 1, 1, 0)); continue; }

            float bell = Mathf.Sin(u * Mathf.PI); bell *= bell;
            Vector2 p = _cur.spots[_sparkSpot[i] % _cur.spots.Count];
            _spark[i].anchoredPosition = new Vector2((p.x - 0.5f) * shown.x, (p.y - 0.5f) * shown.y);
            float size = shown.y * 0.34f;
            _spark[i].sizeDelta = new Vector2(size, size);
            _spark[i].localScale = Vector3.one * (0.5f + 0.6f * bell);
            _spark[i].localRotation = Quaternion.Euler(0f, 0f, u * 18f);
            _sparkCr[i].SetColor(new Color(1f, 0.97f, 0.9f, Mathf.Clamp01(0.9f * _sparkles * bell * _fade)));
        }
    }

    // --- calcul de la lumière d'un logo ------------------------------------------------------

    private Baked GetBaked(Sprite sprite)
    {
        if (Cache.TryGetValue(sprite, out Baked cached) && cached.haloSprite != null) return cached;

        int H = Mathf.Max(8, Mathf.RoundToInt(W * sprite.rect.height / sprite.rect.width));

        // 1) logo réduit -> pixels (passage par le GPU : la texture n'a pas besoin d'être lisible)
        RenderTexture rt = RenderTexture.GetTemporary(W, H, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        RenderTexture prev = RenderTexture.active;
        Graphics.Blit(sprite.texture, rt);
        RenderTexture.active = rt;
        Texture2D read = new Texture2D(W, H, TextureFormat.RGBA32, false);
        read.ReadPixels(new Rect(0, 0, W, H), 0, 0);
        read.Apply();
        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);

        // NB : sprite.texture peut contenir plusieurs sprites ; ici un logo = une texture (mode Single)
        Color32[] src = read.GetPixels32();
        Destroy(read);

        float[] alpha = new float[W * H];
        float[] bright = new float[W * H];     // partie claire du logo (reflets)
        for (int i = 0; i < src.Length; i++)
        {
            float a = src[i].a / 255f;
            float lum = (0.299f * src[i].r + 0.587f * src[i].g + 0.114f * src[i].b) / 255f;
            alpha[i] = a;
            bright[i] = a * Smooth(0.42f, 0.86f, lum);
        }

        // 2) halo : silhouette floutée à deux échelles (proche + large), posée sur un canevas plus grand
        int CW = W + 2 * PAD, CH = H + 2 * PAD;
        float[] halo = new float[CW * CH];
        // le flou "large" déborde du logo : on le calcule directement sur le canevas paddé
        float[] padded = new float[CW * CH];
        for (int y = 0; y < H; y++) for (int x = 0; x < W; x++) padded[(y + PAD) * CW + (x + PAD)] = alpha[y * W + x];
        float[] nearP = Blur(padded, CW, CH, 7, 3);
        float[] farP = Blur(padded, CW, CH, 24, 3);
        for (int i = 0; i < halo.Length; i++)
            halo[i] = Mathf.Clamp01(1.7f * nearP[i] + 1.15f * farP[i]);

        // 3) éclat : reflets du logo, légèrement adoucis, sur le même canevas paddé
        float[] glowP = new float[CW * CH];
        float[] brightSoft = Blur(bright, W, H, 1, 1);
        for (int y = 0; y < H; y++) for (int x = 0; x < W; x++) glowP[(y + PAD) * CW + (x + PAD)] = brightSoft[y * W + x];

        // 4) points brillants pour les étincelles : maxima locaux espacés
        Baked baked = new Baked { padScale = new Vector2((float)CW / W, (float)CH / H) };
        PickSpots(bright, W, H, baked.spots, 8);

        Texture2D haloTex = ToTexture(halo, CW, CH);
        Texture2D glowTex = ToTexture(glowP, CW, CH);
        baked.textures = new[] { haloTex, glowTex };
        baked.haloSprite = Sprite.Create(haloTex, new Rect(0, 0, CW, CH), new Vector2(0.5f, 0.5f), 100f);
        baked.glowSprite = Sprite.Create(glowTex, new Rect(0, 0, CW, CH), new Vector2(0.5f, 0.5f), 100f);
        Cache[sprite] = baked;
        return baked;
    }

    private static float Smooth(float e0, float e1, float x)
    {
        float t = Mathf.Clamp01((x - e0) / (e1 - e0));
        return t * t * (3f - 2f * t);
    }

    // flou boîte séparable, `passes` fois (3 passes ~ gaussienne)
    private static float[] Blur(float[] src, int w, int h, int radius, int passes)
    {
        float[] a = (float[])src.Clone();
        float[] b = new float[a.Length];
        for (int p = 0; p < passes; p++)
        {
            BoxH(a, b, w, h, radius);
            BoxV(b, a, w, h, radius);
        }
        return a;
    }

    private static void BoxH(float[] s, float[] d, int w, int h, int r)
    {
        float inv = 1f / (2 * r + 1);
        for (int y = 0; y < h; y++)
        {
            int row = y * w; float sum = 0f;
            for (int x = -r; x <= r; x++) sum += s[row + Mathf.Clamp(x, 0, w - 1)];
            for (int x = 0; x < w; x++)
            {
                d[row + x] = sum * inv;
                sum += s[row + Mathf.Min(w - 1, x + r + 1)] - s[row + Mathf.Max(0, x - r)];
            }
        }
    }

    private static void BoxV(float[] s, float[] d, int w, int h, int r)
    {
        float inv = 1f / (2 * r + 1);
        for (int x = 0; x < w; x++)
        {
            float sum = 0f;
            for (int y = -r; y <= r; y++) sum += s[Mathf.Clamp(y, 0, h - 1) * w + x];
            for (int y = 0; y < h; y++)
            {
                d[y * w + x] = sum * inv;
                sum += s[Mathf.Min(h - 1, y + r + 1) * w + x] - s[Mathf.Max(0, y - r) * w + x];
            }
        }
    }

    private static Texture2D ToTexture(float[] a, int w, int h)
    {
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        Color32[] px = new Color32[w * h];
        for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a[i]) * 255f + 0.5f));
        tex.SetPixels32(px);
        tex.Apply(false, true);
        return tex;
    }

    // k maxima de la carte "reflets", distants d'au moins 1/6 de la largeur : les étincelles se posent sur les
    // endroits déjà brillants du logo (jamais sur du vide).
    private static void PickSpots(float[] map, int w, int h, List<Vector2> outSpots, int k)
    {
        float[] soft = Blur(map, w, h, 5, 2);
        float minDist = w / 6f;
        for (int n = 0; n < k; n++)
        {
            float best = 0.12f; int bx = -1, by = -1;
            for (int y = 2; y < h - 2; y += 2)
                for (int x = 2; x < w - 2; x += 2)
                {
                    float v = soft[y * w + x];
                    if (v <= best) continue;
                    bool far = true;
                    foreach (Vector2 s in outSpots)
                    {
                        float dx = s.x * w - x, dy = s.y * h - y;
                        if (dx * dx + dy * dy < minDist * minDist) { far = false; break; }
                    }
                    if (far) { best = v; bx = x; by = y; }
                }
            if (bx < 0) break;
            outSpots.Add(new Vector2((bx + 0.5f) / w, (by + 0.5f) / h));
        }
    }

    private static Sprite MakeGlintSprite()
    {
        const int N = 64;
        Texture2D tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        Color32[] px = new Color32[N * N];
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float u = (x + 0.5f) / N * 2f - 1f, v = (y + 0.5f) / N * 2f - 1f;
                float core = Mathf.Exp(-(u * u + v * v) * 22f);
                float hh = Mathf.Exp(-v * v * 260f) * Mathf.Exp(-u * u * 5f);
                float vv = Mathf.Exp(-u * u * 260f) * Mathf.Exp(-v * v * 5f);
                float edge = 1f - Smooth(0.85f, 1f, Mathf.Max(Mathf.Abs(u), Mathf.Abs(v)));
                float a = Mathf.Clamp01((core + 0.85f * Mathf.Max(hh, vv)) * edge);
                px[y * N + x] = new Color32(255, 255, 255, (byte)(a * 255f + 0.5f));
            }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f);
    }
}
