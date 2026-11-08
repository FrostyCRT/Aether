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
        public CanvasRenderer cr; public float alpha, freq, phase; public bool flicker;
    }

    private struct Ember
    {
        public RectTransform rt; public CanvasRenderer cr; public Color color; public float alpha, life, birth, speed, x0, y0, swayAmp, swayFreq, phase;
    }

    private readonly List<Glow> _glows = new List<Glow>();
    private readonly List<Ember> _embers = new List<Ember>();
    private RectTransform _rect;
    private float _enable;

    // REFAIT (2026-11-07, retour utilisateur : "fais le même ajustement sur les autres pages" que pour les lueurs des
    // Paramètres) - plus de ronds : chaque lueur est découpée dans l'illustration (flamme de chaque torche, brume claire
    // du fond), textures Resources/HallGlow générées par Tools/build_menu_glows.py hall. Immobiles, seule l'intensité vit.
    // Pixels de l'illustration (1888 x 1080, étirée sur la page) : texture, coin haut-gauche, taille, intensité, flamme ?
    private const float ArtW = 1888f, ArtH = 1080f;
    private static readonly (string name, float x, float y, float w, float h, float a, bool flicker)[] Sources =
    {
        ("torch_1",  444f, 482f, 149f, 166f, 0.80f, true),
        ("torch_2",  649f, 559f, 114f, 130f, 0.80f, true),
        ("torch_3",  771f, 605f,  94f, 106f, 0.75f, true),
        ("torch_4", 1259f, 609f,  90f, 102f, 0.75f, true),
        ("torch_5", 1309f, 563f, 114f, 126f, 0.80f, true),
        ("torch_6", 1417f, 473f, 130f, 164f, 0.80f, true),
        ("torch_7", 1642f, 342f, 164f, 204f, 0.85f, true),
        ("torch_8",  239f, 657f,  84f,  82f, 0.60f, true),
        ("hall_fog", 640f, 210f, 660f, 730f, 0.30f, false),   // brume lumineuse au fond de la salle
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
            CanvasRenderer cr = MenuShapedGlow.Create(_rect, "HallGlow/" + s.name, s.x, s.y, s.w, s.h, ArtW, ArtH, Additive());
            if (cr == null) continue;
            _glows.Add(new Glow { cr = cr, alpha = s.a, flicker = s.flicker, freq = Random.Range(0.8f, 1.6f), phase = Random.Range(0f, 6.28f) });
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
            g.cr.SetColor(new Color(1f, 1f, 1f, g.alpha * breathe * k));
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
