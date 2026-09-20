using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// AJOUTE (2026-09-20) - ambiance vivante de l'onglet Réputation (grand hall de pierre) : flammes de torches qui
// vacillent, lueur chaude au fond de la salle, braises et poussières qui montent lentement. Même principe que
// TreeAmbientFX / SettingsAmbientFX : sprites générés en code (TreeSprites), matériau ADDITIF, couleur/alpha par
// CanvasRenderer.SetColor (pas de reconstruction de maillage). Réglable par Paramètres > Graphismes > Effets
// d'ambiance des menus (GameSettings.MenuEffectsFactor).
//
// Positions données en pixels de l'illustration 1920 x 1080 (fond étiré sur tout l'écran), posées sur les
// torches visibles de ghibli_stone_hall.
[RequireComponent(typeof(RectTransform))]
public class ReputationHallFX : MonoBehaviour
{
    [SerializeField] private Material _additiveMaterial;
    [Range(0f, 2f)] [SerializeField] private float _intensity = 1f;
    [SerializeField] private int _emberCount = 44;

    private struct Glow
    {
        public RectTransform rt; public CanvasRenderer cr; public Color color; public float alpha, freq, phase, sway, size;
        public Vector2 basePos; public bool flicker;
    }

    private struct Ember
    {
        public RectTransform rt; public CanvasRenderer cr; public Color color; public float alpha, life, birth, speed, x0, y0, swayAmp, swayFreq, phase;
    }

    private readonly List<Glow> _glows = new List<Glow>();
    private readonly List<Ember> _embers = new List<Ember>();
    private RectTransform _rect;
    private float _enable;

    // (x, y) : écran 1920 x 1080 depuis le coin haut-gauche ; taille en pixels
    private static readonly (float x, float y, float size, float r, float g, float b, float a, bool flicker)[] Sources =
    {
        (715f, 612f, 230f, 1.00f, 0.62f, 0.26f, 0.60f, true),      // torche centre-gauche
        (528f, 596f, 210f, 1.00f, 0.60f, 0.24f, 0.55f, true),
        (1388f, 642f, 250f, 1.00f, 0.62f, 0.26f, 0.60f, true),     // torches centre-droit
        (1330f, 692f, 200f, 1.00f, 0.58f, 0.22f, 0.50f, true),
        (288f, 684f, 220f, 1.00f, 0.60f, 0.24f, 0.50f, true),      // extrême gauche
        (1796f, 516f, 250f, 1.00f, 0.62f, 0.26f, 0.55f, true),     // extrême droite
        (960f, 760f, 1100f, 1.00f, 0.72f, 0.42f, 0.10f, false),    // lueur diffuse au fond de la salle
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
        return new Vector2((sx / 1920f - 0.5f) * r.width, (0.5f - sy / 1080f) * r.height);
    }

    private void Build()
    {
        foreach (var s in Sources)
        {
            Glow g = new Glow
            {
                color = new Color(s.r, s.g, s.b), alpha = s.a, size = s.size, flicker = s.flicker,
                freq = Random.Range(0.8f, 1.6f), phase = Random.Range(0f, 6.28f), sway = s.flicker ? Random.Range(3f, 7f) : 0f,
                basePos = P(s.x, s.y)
            };
            g.rt = NewImage("Glow", out g.cr);
            g.rt.sizeDelta = new Vector2(s.size, s.size);
            g.rt.anchoredPosition = g.basePos;
            _glows.Add(g);
        }

        for (int i = 0; i < _emberCount; i++)
        {
            Ember e = new Ember();
            e.rt = NewImage("Ember", out e.cr);
            float size = Random.Range(5f, 16f);
            e.rt.sizeDelta = new Vector2(size, size);
            e.color = Random.value < 0.75f ? new Color(1f, 0.72f, 0.36f) : new Color(1f, 0.9f, 0.7f);
            e.alpha = Random.Range(0.3f, 0.75f);
            Respawn(ref e, true);
            _embers.Add(e);
        }
    }

    private void Respawn(ref Ember e, bool randomAge)
    {
        Rect r = _rect.rect;
        e.life = Random.Range(9f, 20f);
        e.birth = Time.unscaledTime - (randomAge ? Random.Range(0f, e.life) : 0f);
        e.speed = Random.Range(10f, 26f);
        e.x0 = Random.Range(-r.width * 0.5f, r.width * 0.5f);
        e.y0 = Random.Range(-r.height * 0.55f, r.height * 0.05f);
        e.swayAmp = Random.Range(12f, 36f);
        e.swayFreq = Random.Range(0.18f, 0.5f);
        e.phase = Random.Range(0f, 6.28f);
    }

    private void Update()
    {
        float t = Time.unscaledTime;
        float fade = Mathf.SmoothStep(0f, 1f, (t - _enable) / 1.4f);
        float k = _intensity * fade * GameSettings.MenuEffectsFactor;

        for (int i = 0; i < _glows.Count; i++)
        {
            Glow g = _glows[i];
            float breathe = g.flicker
                ? 0.72f + 0.2f * Mathf.Sin(t * g.freq * 3.4f + g.phase) + 0.08f * Mathf.Sin(t * g.freq * 8.1f + g.phase * 2f)
                : 0.85f + 0.15f * Mathf.Sin(t * g.freq * 0.5f + g.phase);
            g.rt.anchoredPosition = g.basePos + new Vector2(Mathf.Sin(t * 0.13f + g.phase) * g.sway, Mathf.Cos(t * 0.11f + g.phase) * g.sway);
            g.rt.localScale = Vector3.one * (0.95f + 0.08f * breathe);
            g.cr.SetColor(new Color(g.color.r, g.color.g, g.color.b, g.alpha * breathe * k));
        }

        for (int i = 0; i < _embers.Count; i++)
        {
            Ember e = _embers[i];
            float age = t - e.birth;
            float u = age / e.life;
            if (u >= 1f) { Respawn(ref e, false); _embers[i] = e; age = 0f; u = 0f; }
            float env = Mathf.SmoothStep(0f, 1f, u / 0.2f) * (1f - Mathf.SmoothStep(0f, 1f, (u - 0.7f) / 0.3f));
            float twinkle = 0.65f + 0.35f * Mathf.Sin(age * 1.4f + e.phase);
            e.rt.anchoredPosition = new Vector2(e.x0 + Mathf.Sin(age * e.swayFreq + e.phase) * e.swayAmp, e.y0 + e.speed * age);
            e.cr.SetColor(new Color(e.color.r, e.color.g, e.color.b, e.alpha * env * twinkle * k));
        }
    }
}
