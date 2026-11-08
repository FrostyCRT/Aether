using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// AJOUTE (2026-09-19) - ambiance vivante du fond de la page Paramètres (bibliothèque sombre) : lueurs de bougies /
// lanternes qui vacillent, halos froids des cristaux et des sphères de verre, poussières qui montent lentement.
// Même principe que TreeAmbientFX : sprites générés en code (TreeSprites), matériau ADDITIF (la lumière s'ajoute au
// décor), couleur/alpha par CanvasRenderer.SetColor (pas de reconstruction de maillage). Discret : la page est un
// écran de réglages, pas une scène. Réglable par Paramètres > Graphismes > Effets d'ambiance des menus.
//
// REFAIT (2026-11-07, retour utilisateur : "met pas de simple ronds colorés, il faut que ce soit précis sur les éléments
// du fond d'écran") - chaque lueur est maintenant une texture découpée dans l'illustration elle-même (Resources/
// SettingsGlow, générées par Tools/build_menu_glows.py settings) : la flamme, le verre de la lanterne, les arêtes des
// cristaux, le contour des sphères... avec un halo flou qui suit leur forme. Posées pixel pour pixel sur le fond
// (illustration 1920 x 1280 étirée sur la page), elles ne bougent pas : seule leur intensité vit (vacillement des
// flammes, respiration lente des cristaux et du verre).
[RequireComponent(typeof(RectTransform))]
public class SettingsAmbientFX : MonoBehaviour
{
    [SerializeField] private Material _additiveMaterial;
    [Range(0f, 2f)] [SerializeField] private float _intensity = 1f;
    [SerializeField] private int _dustCount = 46;

    private struct Glow
    {
        public CanvasRenderer cr; public float alpha, freq, phase; public bool flicker;
    }

    private struct Dust
    {
        public RectTransform rt; public CanvasRenderer cr; public Color color; public float alpha, life, birth, speed, x0, y0, swayAmp, swayFreq, phase;
    }

    private readonly List<Glow> _lights = new List<Glow>();
    private readonly List<Dust> _dust = new List<Dust>();
    private RectTransform _rect;
    private float _enable;

    // Pixels de l'illustration (1920 x 1280) : nom de la texture, coin haut-gauche, taille ; intensité ; flamme ?
    private const float ArtW = 1920f, ArtH = 1280f;
    private static readonly (string name, float x, float y, float w, float h, float a, bool flicker)[] Sources =
    {
        ("candle_l",  198f, 342f,  82f,  98f, 0.85f, true),    // chandelier mural, 3 flammes
        ("candle_m",  232f, 310f,  82f, 100f, 0.85f, true),
        ("candle_r",  264f, 346f,  82f,  98f, 0.85f, true),
        ("lantern_l", 230f, 610f, 186f, 246f, 0.80f, true),    // lanterne du bureau
        ("lantern_r", 1358f, 656f, 214f, 216f, 0.80f, true),   // lanterne de la table ronde
        ("crystals",  0f,   446f, 242f, 276f, 0.55f, false),   // cristaux bleus, à gauche
        ("amethyst",  1376f, 746f, 188f, 118f, 0.55f, false),  // améthystes au pied de la lanterne
        ("sphere_1",  1524f, 332f, 136f, 136f, 0.45f, false),  // sphères de verre suspendues
        ("sphere_2",  1600f, 332f, 136f, 136f, 0.45f, false),
        ("sphere_3",  1658f, 372f, 138f, 138f, 0.40f, false),
        ("sphere_4",  1585f, 477f, 146f, 146f, 0.45f, false),
        ("sphere_5",  1703f, 502f, 146f, 146f, 0.45f, false),
        ("globe",     798f,  462f, 128f, 130f, 0.40f, false),  // sphère armillaire près du pilier
    };

    private void Awake()
    {
        _rect = (RectTransform)transform;
        Build();
    }

    private void OnEnable() => _enable = Time.unscaledTime;

    private Material Additive()
    {
        if (_additiveMaterial != null) return _additiveMaterial;
        Shader sh = Shader.Find("Aether/UI/Additive");
        if (sh != null) _additiveMaterial = new Material(sh);
        return _additiveMaterial;
    }

    private RectTransform NewImage(string objName, out CanvasRenderer cr)
    {
        GameObject go = new GameObject(objName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(_rect, false);
        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        Image im = go.GetComponent<Image>();
        im.sprite = TreeSprites.Glow;
        im.raycastTarget = false;
        im.material = Additive();
        cr = go.GetComponent<CanvasRenderer>();
        cr.SetColor(new Color(1, 1, 1, 0));
        return rt;
    }

    private void Build()
    {
        foreach (var s in Sources)
        {
            CanvasRenderer cr = MenuShapedGlow.Create(_rect, "SettingsGlow/" + s.name, s.x, s.y, s.w, s.h, ArtW, ArtH, Additive());
            if (cr == null) continue;
            _lights.Add(new Glow { cr = cr, alpha = s.a, flicker = s.flicker, freq = Random.Range(0.8f, 1.6f), phase = Random.Range(0f, 6.28f) });
        }

        for (int i = 0; i < _dustCount; i++)
        {
            Dust d = new Dust();
            d.rt = NewImage("Dust", out d.cr);
            float size = Random.Range(6f, 18f);
            d.rt.sizeDelta = new Vector2(size, size);
            d.color = Random.value < 0.7f ? new Color(1f, 0.86f, 0.55f) : new Color(0.65f, 0.8f, 1f);
            d.alpha = Random.Range(0.25f, 0.6f);
            Respawn(ref d, true);
            _dust.Add(d);
        }
    }

    private void Respawn(ref Dust d, bool randomAge)
    {
        Rect r = _rect.rect;
        d.life = Random.Range(12f, 26f);
        d.birth = Time.unscaledTime - (randomAge ? Random.Range(0f, d.life) : 0f);
        d.speed = Random.Range(6f, 16f);
        d.x0 = Random.Range(-r.width * 0.5f, r.width * 0.5f);
        d.y0 = Random.Range(-r.height * 0.55f, r.height * 0.1f);
        d.swayAmp = Random.Range(10f, 34f);
        d.swayFreq = Random.Range(0.15f, 0.45f);
        d.phase = Random.Range(0f, 6.28f);
    }

    private void Update()
    {
        float t = Time.unscaledTime;
        float fade = Mathf.SmoothStep(0f, 1f, (t - _enable) / 1.4f);
        float k = _intensity * fade * GameSettings.MenuEffectsFactor;

        for (int i = 0; i < _lights.Count; i++)
        {
            Glow l = _lights[i];
            float breathe = l.flicker
                ? 0.74f + 0.16f * Mathf.Sin(t * l.freq * 3.1f + l.phase) + 0.10f * Mathf.Sin(t * l.freq * 7.9f + l.phase * 2f)
                : 0.70f + 0.30f * Mathf.Sin(t * l.freq * 0.7f + l.phase);
            l.cr.SetColor(new Color(1f, 1f, 1f, l.alpha * breathe * k));
        }

        for (int i = 0; i < _dust.Count; i++)
        {
            Dust d = _dust[i];
            float age = t - d.birth;
            float u = age / d.life;
            if (u >= 1f) { Respawn(ref d, false); _dust[i] = d; age = 0f; u = 0f; }
            float env = Mathf.SmoothStep(0f, 1f, u / 0.2f) * (1f - Mathf.SmoothStep(0f, 1f, (u - 0.7f) / 0.3f));
            float twinkle = 0.7f + 0.3f * Mathf.Sin(age * 0.9f + d.phase);
            d.rt.anchoredPosition = new Vector2(d.x0 + Mathf.Sin(age * d.swayFreq + d.phase) * d.swayAmp, d.y0 + d.speed * age);
            d.cr.SetColor(new Color(d.color.r, d.color.g, d.color.b, d.alpha * env * twinkle * k));
        }
    }
}
