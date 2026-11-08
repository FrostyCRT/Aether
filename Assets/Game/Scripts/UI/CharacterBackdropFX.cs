using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// AJOUTÉ (2026-11-07, retour utilisateur : "fais le travail des lueurs pour la page de Personnages, avec le gros cristal
// au milieu, et tu peux rajouter des petites lueurs, poussières mystiques un peu dans le style de la page") - vie du
// DÉCOR de la page Personnages (les effets autour du portrait restent ceux de CharacterAmbientFX) :
//   1. CRISTAL : lueur découpée dans l'illustration du héros courant (Resources/CharGlow/<héros>_crystal, générée par
//      Tools/build_menu_glows.py char_<héros>), posée pixel pour pixel sur le cristal flottant, qui respire lentement
//      avec une légère pulsation, comme une pierre vivante ;
//   2. POUSSIÈRES MYSTIQUES : petits grains lumineux qui montent lentement en ondulant (même famille que les poussières
//      des Paramètres), aux couleurs du cristal ; une partie naît sur le piédestal et monte vers le cristal, le reste
//      flotte dans les ruines ;
//   3. ÉTINCELLES : quelques points qui s'allument puis s'éteignent tout près du cristal.
// Enfant de l'image de fond (PanelBackgroundImage) : suit son cadrage « cover » quelle que soit la résolution, et reste
// DERRIÈRE le portrait et la fiche. Additif, alpha seul par CanvasRenderer.SetColor, zéro allocation par image.
// Respecte Paramètres > Graphismes > Effets d'ambiance des menus.
[RequireComponent(typeof(RectTransform))]
public class CharacterBackdropFX : MonoBehaviour
{
    // Rectangle de la lueur du cristal en pixels de l'illustration de chaque héros (Aether, Kael, Lyra).
    private static readonly (string tex, float x, float y, float w, float h, float artW, float artH)[] Crystals =
    {
        ("CharGlow/aether_crystal", 874f, 319f, 272f, 487f, 2048f, 1152f),
        ("CharGlow/kael_crystal",   834f, 284f, 452f, 542f, 2048f, 1152f),
        ("CharGlow/lyra_crystal",   774f, 264f, 572f, 582f, 2096f, 1184f),
    };

    // Couleurs des poussières (principale, secondaire) par héros : celles de son cristal.
    private static readonly Color[,] DustColors =
    {
        { new Color(1f, 0.84f, 0.50f), new Color(1f, 0.95f, 0.80f) },
        { new Color(0.50f, 1f, 0.62f), new Color(0.85f, 1f, 0.70f) },
        { new Color(0.66f, 0.56f, 1f), new Color(0.55f, 0.80f, 1f) },
    };

    // Repères en fraction de l'image (0,0 = bas-gauche) : centre du cristal, dessus du piédestal.
    private static readonly Vector2 CrystalCenter = new Vector2(0.495f, 0.52f);
    private static readonly Vector2 PedestalTop = new Vector2(0.49f, 0.30f);

    private const int PedestalMotes = 22, RuinMotes = 30, Sparks = 8;

    private struct Mote
    {
        public RectTransform rt; public CanvasRenderer cr; public bool secondary;
        public float alpha, life, birth, speed, x0, y0, swayAmp, swayFreq, phase; public bool fromPedestal;
    }

    private struct Spark { public RectTransform rt; public CanvasRenderer cr; public float birth, life, size; public bool secondary; }

    private RectTransform _rect;
    private Material _additive;
    private readonly CanvasRenderer[] _crystalGlows = new CanvasRenderer[3];
    private readonly List<Mote> _motes = new List<Mote>();
    private readonly List<Spark> _sparks = new List<Spark>();
    private int _character;
    private float _enable, _switchTime;

    public static CharacterBackdropFX Ensure(RectTransform backgroundImage)
    {
        Transform existing = backgroundImage.Find("BackdropFX");
        if (existing != null) return existing.GetComponent<CharacterBackdropFX>();
        var go = new GameObject("BackdropFX", typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(backgroundImage, false);
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        return go.AddComponent<CharacterBackdropFX>();
    }

    public void SetCharacter(int index)
    {
        index = Mathf.Clamp(index, 0, 2);
        if (index == _character && _switchTime > 0f) return;
        _character = index;
        _switchTime = Time.unscaledTime;
        for (int i = 0; i < _crystalGlows.Length; i++)
            if (_crystalGlows[i] != null) _crystalGlows[i].gameObject.SetActive(i == index);
    }

    private void Awake()
    {
        _rect = (RectTransform)transform;
        Shader sh = Shader.Find("Aether/UI/Additive");
        if (sh != null) _additive = new Material(sh);

        for (int i = 0; i < Crystals.Length; i++)
        {
            var c = Crystals[i];
            _crystalGlows[i] = MenuShapedGlow.Create(_rect, c.tex, c.x, c.y, c.w, c.h, c.artW, c.artH, _additive);
            if (_crystalGlows[i] != null) _crystalGlows[i].gameObject.SetActive(false);
        }

        for (int i = 0; i < PedestalMotes + RuinMotes; i++)
        {
            Mote m = new Mote { fromPedestal = i < PedestalMotes, secondary = Random.value < 0.35f };
            m.rt = NewDot("Mote", out m.cr);
            float size = m.fromPedestal ? Random.Range(14f, 26f) : Random.Range(12f, 26f);   // fond clair : grains assez gros pour se voir
            m.rt.sizeDelta = new Vector2(size, size);
            m.alpha = Random.Range(0.65f, 1f);
            Respawn(ref m, true);
            _motes.Add(m);
        }

        for (int i = 0; i < Sparks; i++)
        {
            Spark s = new Spark { secondary = Random.value < 0.5f };
            s.rt = NewDot("Spark", out s.cr);
            RespawnSpark(ref s, true);
            _sparks.Add(s);
        }
    }

    private void OnEnable() => _enable = Time.unscaledTime;

    private void OnDestroy() { if (_additive != null) Destroy(_additive); }

    private RectTransform NewDot(string name, out CanvasRenderer cr)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(_rect, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        var im = go.GetComponent<Image>();
        im.sprite = TreeSprites.Glow;
        im.material = _additive;
        im.raycastTarget = false;
        cr = go.GetComponent<CanvasRenderer>();
        cr.SetColor(new Color(1f, 1f, 1f, 0f));
        return rt;
    }

    // fraction de l'image (0..1, origine bas-gauche) -> position locale (centre du rect)
    private Vector2 F(float fx, float fy)
    {
        Rect r = _rect.rect;
        return new Vector2((fx - 0.5f) * r.width, (fy - 0.5f) * r.height);
    }

    private void Respawn(ref Mote m, bool randomAge)
    {
        Vector2 p;
        if (m.fromPedestal)
        {
            // naît sur le disque du piédestal, monte vers le cristal et au-delà
            float a = Random.Range(0f, Mathf.PI * 2f);
            p = F(PedestalTop.x + Mathf.Cos(a) * Random.Range(0f, 0.09f), PedestalTop.y + Mathf.Sin(a) * Random.Range(0f, 0.025f));
            m.life = Random.Range(7f, 13f);
            m.speed = Random.Range(18f, 34f);
            m.swayAmp = Random.Range(8f, 26f);
        }
        else
        {
            // dans les ruines, en évitant le centre (le cristal a ses propres grains)
            float fx = Random.value < 0.5f ? Random.Range(0.02f, 0.38f) : Random.Range(0.6f, 0.98f);
            p = F(fx, Random.Range(0.08f, 0.75f));
            m.life = Random.Range(11f, 22f);
            m.speed = Random.Range(6f, 15f);
            m.swayAmp = Random.Range(10f, 34f);
        }
        m.x0 = p.x; m.y0 = p.y;
        m.birth = Time.unscaledTime - (randomAge ? Random.Range(0f, m.life) : 0f);
        m.swayFreq = Random.Range(0.15f, 0.45f);
        m.phase = Random.Range(0f, 6.28f);
    }

    private void RespawnSpark(ref Spark s, bool randomAge)
    {
        float a = Random.Range(0f, Mathf.PI * 2f), d = Random.Range(0.04f, 0.10f);
        s.rt.anchoredPosition = F(CrystalCenter.x + Mathf.Cos(a) * d * 0.62f, CrystalCenter.y + Mathf.Sin(a) * d * 1.3f);
        s.life = Random.Range(1.2f, 2.6f);
        s.birth = Time.unscaledTime + (randomAge ? -Random.Range(0f, s.life) : Random.Range(0.2f, 2.5f));
        s.size = Random.Range(22f, 36f);
        s.rt.sizeDelta = new Vector2(s.size, s.size);
    }

    private void Update()
    {
        float t = Time.unscaledTime;
        float k = Mathf.SmoothStep(0f, 1f, (t - _enable) / 1.2f) * GameSettings.MenuEffectsFactor;
        float swap = Mathf.SmoothStep(0f, 1f, (t - _switchTime) / 0.6f);   // fondu au changement de héros
        Color main = DustColors[_character, 0], second = DustColors[_character, 1];

        CanvasRenderer glow = _crystalGlows[_character];
        if (glow != null)
        {
            // respiration lente + petite pulsation, comme une pierre vivante
            float breathe = 0.70f + 0.20f * Mathf.Sin(t * 0.9f) + 0.10f * Mathf.Sin(t * 2.3f + 1.3f);
            glow.SetColor(new Color(1f, 1f, 1f, 0.75f * breathe * k * swap));
        }

        for (int i = 0; i < _motes.Count; i++)
        {
            Mote m = _motes[i];
            float age = t - m.birth;
            float u = age / m.life;
            if (u >= 1f) { Respawn(ref m, false); _motes[i] = m; age = 0f; u = 0f; }
            float env = Mathf.SmoothStep(0f, 1f, u / 0.2f) * (1f - Mathf.SmoothStep(0f, 1f, (u - 0.65f) / 0.35f));
            float twinkle = 0.65f + 0.35f * Mathf.Sin(age * 1.3f + m.phase);
            m.rt.anchoredPosition = new Vector2(m.x0 + Mathf.Sin(age * m.swayFreq + m.phase) * m.swayAmp, m.y0 + m.speed * age);
            Color c = m.secondary ? second : main;
            m.cr.SetColor(new Color(c.r, c.g, c.b, m.alpha * env * twinkle * k));
        }

        for (int i = 0; i < _sparks.Count; i++)
        {
            Spark s = _sparks[i];
            float u = (t - s.birth) / s.life;
            if (u >= 1f) { RespawnSpark(ref s, false); _sparks[i] = s; continue; }
            float a = u <= 0f ? 0f : Mathf.Sin(u * Mathf.PI);
            Color c = s.secondary ? second : main;
            s.cr.SetColor(new Color(c.r, c.g, c.b, 0.85f * a * a * k));
        }
    }
}
