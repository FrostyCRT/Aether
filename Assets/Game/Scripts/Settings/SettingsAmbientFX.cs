using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// AJOUTE (2026-09-19) - ambiance vivante du fond de la page Paramètres (bibliothèque sombre) : lueurs de bougies /
// lanternes qui vacillent, halos froids des cristaux et des sphères de verre, poussières qui montent lentement.
// Même principe que TreeAmbientFX : sprites générés en code (TreeSprites), matériau ADDITIF (la lumière s'ajoute au
// décor), couleur/alpha par CanvasRenderer.SetColor (pas de reconstruction de maillage). Discret : la page est un
// écran de réglages, pas une scène. Réglable par Paramètres > Graphismes > Effets d'ambiance des menus.
//
// Positions données en pixels de l'illustration 1920 x 980 (fond étiré sur toute la page) : elles sont posées sur
// les sources de lumière visibles de SettingsBackground, hors de la zone couverte par la carte centrale.
[RequireComponent(typeof(RectTransform))]
public class SettingsAmbientFX : MonoBehaviour
{
    [SerializeField] private Material _additiveMaterial;
    [Range(0f, 2f)] [SerializeField] private float _intensity = 1f;
    [SerializeField] private int _dustCount = 46;

    private struct Glow
    {
        public RectTransform rt; public CanvasRenderer cr; public Color color; public float alpha, size, freq, phase, sway;
        public Vector2 basePos; public bool flicker;
    }

    private struct Dust
    {
        public RectTransform rt; public CanvasRenderer cr; public Color color; public float alpha, life, birth, speed, x0, y0, swayAmp, swayFreq, phase;
    }

    private readonly List<Glow> _lights = new List<Glow>();
    private readonly List<Dust> _dust = new List<Dust>();
    private RectTransform _rect;
    private float _enable;

    // (x, y) : écran 1920 x 980 depuis le coin haut-gauche de la page ; taille en pixels
    private static readonly (float x, float y, float size, float r, float g, float b, float a, bool flicker)[] Sources =
    {
        (255f, 180f, 200f, 1.00f, 0.70f, 0.32f, 0.55f, true),    // bougies, mur gauche
        (325f, 560f, 260f, 1.00f, 0.72f, 0.34f, 0.60f, true),    // lanterne gauche
        (95f, 470f, 300f, 0.42f, 0.70f, 1.00f, 0.55f, false),    // cristaux bleus, à gauche
        (1478f, 610f, 240f, 1.00f, 0.74f, 0.36f, 0.50f, true),   // lanterne droite
        (1600f, 320f, 190f, 0.55f, 0.72f, 1.00f, 0.50f, false),  // sphères de verre
        (1665f, 425f, 210f, 0.62f, 0.60f, 1.00f, 0.50f, false),
        (1745f, 440f, 190f, 0.50f, 0.72f, 1.00f, 0.45f, false),
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

    private Vector2 P(float sx, float sy)
    {
        Rect r = _rect.rect;
        return new Vector2((sx / 1920f - 0.5f) * r.width, (0.5f - sy / 980f) * r.height);
    }

    private void Build()
    {
        foreach (var s in Sources)
        {
            Glow l = new Glow
            {
                color = new Color(s.r, s.g, s.b), alpha = s.a, size = s.size, flicker = s.flicker,
                freq = Random.Range(0.8f, 1.6f), phase = Random.Range(0f, 6.28f), sway = Random.Range(3f, 8f),
                basePos = P(s.x, s.y)
            };
            l.rt = NewImage("Light", out l.cr);
            l.rt.sizeDelta = new Vector2(s.size, s.size);
            l.rt.anchoredPosition = l.basePos;
            _lights.Add(l);
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
                ? 0.72f + 0.20f * Mathf.Sin(t * l.freq * 3.1f + l.phase) + 0.08f * Mathf.Sin(t * l.freq * 7.3f + l.phase * 2f)
                : 0.82f + 0.18f * Mathf.Sin(t * l.freq * 0.9f + l.phase);
            l.rt.anchoredPosition = l.basePos + new Vector2(Mathf.Sin(t * 0.13f + l.phase) * l.sway, Mathf.Cos(t * 0.11f + l.phase) * l.sway);
            l.rt.localScale = Vector3.one * (0.95f + 0.08f * breathe);
            l.cr.SetColor(new Color(l.color.r, l.color.g, l.color.b, l.alpha * breathe * k));
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
