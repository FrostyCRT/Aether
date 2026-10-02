using UnityEngine;

// Effet visuel d'invocation du mini-boss de La Source Corrompue (boss 3). Remplace l'ancien cercle violet simple
// (_riftPortalPrefab) : tout est construit en code, aucun prefab à poser. Seule l'image du cercle runique est
// optionnelle (sinon un cercle procédural est généré).
//
// Déroulé sur `duration` secondes (le windup de l'invocation) :
//   - le cercle runique GRANDIT d'un coup (0,45 s, léger rebond), avec un halo violet qui respire en dessous ;
//   - le grand cercle et un cercle intérieur tournent en sens inverses, de plus en plus vite ;
//   - des étincelles montent depuis toute la zone, de plus en plus nombreuses ;
//   - dans le dernier tiers, un anneau SE RESSERRE vers le centre (le "ça va arriver") ;
//   - dans la 2e moitié, un pilier de lumière monte du centre ;
//   - Burst() (appelé par le boss au moment exact de l'apparition) : éclat blanc-violet, onde de choc
//     (ExpandingRingVFX), explosion d'étincelles, fondu de tout puis auto-destruction.
// Le temps utilise Time.deltaTime : tout se fige tout seul en pause.
public class SummonPortalFX : MonoBehaviour
{
    private const float GrowInDuration = 0.45f;
    private const float BurstDuration = 0.6f;
    private const int MoteCount = 28;

    private float _radius;
    private float _duration;
    private Color _main;
    private Color _bright;
    private Color _hot;

    private float _t;
    private bool _bursting;
    private float _burstT;
    private float _outerAngle;
    private float _innerAngle;

    private SpriteRenderer _glow;
    private SpriteRenderer _outer;
    private SpriteRenderer _inner;
    private SpriteRenderer _converge;
    private SpriteRenderer _flash;
    private SpriteRenderer _pillar;
    private float _pillarHeight;

    private class Mote
    {
        public SpriteRenderer sr;
        public Vector3 start;
        public float age, life, rise, size;
        public bool alive;
    }
    private Mote[] _motes;
    private float _moteSpawnAccum;
    private Transform _cam;

    // ------------------------------------------------------------------
    // Création
    // ------------------------------------------------------------------

    // position : centre de la zone (le Y est forcé au sol). runeSprite : image du cercle runique (optionnelle).
    public static SummonPortalFX Spawn(Vector3 position, float radius, float duration, Sprite runeSprite, Color color)
    {
        GameObject go = new GameObject("SummonPortalFX");
        go.transform.position = new Vector3(position.x, 0f, position.z);
        SummonPortalFX fx = go.AddComponent<SummonPortalFX>();
        fx.Init(radius, duration, runeSprite, color);
        return fx;
    }

    private void Init(float radius, float duration, Sprite runeSprite, Color color)
    {
        _radius = Mathf.Max(0.5f, radius);
        _duration = Mathf.Max(0.1f, duration);
        _main = color;
        _bright = Color.Lerp(color, Color.white, 0.5f);
        _hot = Color.Lerp(color, Color.white, 0.85f);
        _pillarHeight = _radius * 2.4f;

        Sprite ring = RingSprite();
        Sprite glowSprite = GlowSprite();

        _glow = MakeRenderer("Glow", glowSprite, 0, 0.05f);
        _outer = MakeRenderer("OuterRune", runeSprite != null ? runeSprite : ring, 2, 0.07f);
        _inner = MakeRenderer("InnerRing", ring, 3, 0.08f);
        _converge = MakeRenderer("ConvergeRing", ring, 4, 0.09f);
        _flash = MakeRenderer("Flash", glowSprite, 5, 0.10f);
        _pillar = MakeRenderer("Pillar", PillarSprite(), 6, 0f);

        _converge.enabled = false;
        _flash.enabled = false;
        _pillar.enabled = false;

        _motes = new Mote[MoteCount];
        for (int i = 0; i < MoteCount; i++)
        {
            Mote m = new Mote();
            m.sr = MakeRenderer("Mote", glowSprite, 7, 0f);
            m.sr.enabled = false;
            _motes[i] = m;
        }

        // Etat de départ : tout invisible/minuscule avant le premier rendu.
        SetFlat(_glow, 0.05f, 0f);
        SetFlat(_outer, 0.05f, 0f);
        SetFlat(_inner, 0.05f, 0f);
    }

    private SpriteRenderer MakeRenderer(string objName, Sprite sprite, int order, float localY)
    {
        GameObject g = new GameObject(objName);
        g.transform.SetParent(transform, false);
        g.transform.localPosition = new Vector3(0f, localY, 0f);

        SpriteRenderer sr = g.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sharedMaterial = SpriteMaterial();
        sr.sortingOrder = order;
        sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        sr.receiveShadows = false;
        sr.color = Color.clear;
        return sr;
    }

    // ------------------------------------------------------------------
    // Boucle
    // ------------------------------------------------------------------

    private void Update()
    {
        float dt = Time.deltaTime;
        if (_cam == null && Camera.main != null) _cam = Camera.main.transform;

        if (_bursting)
        {
            UpdateBurst(dt);
            return;
        }

        _t += dt;
        float p = Mathf.Clamp01(_t / _duration);
        float growT = Mathf.Clamp01(_t / GrowInDuration);
        float grow = EaseOutBack(growT);
        float appear = Mathf.Clamp01(growT * 1.6f);
        float breathe = 0.5f + 0.5f * Mathf.Sin(_t * Mathf.Lerp(5f, 24f, p));   // bat de plus en plus vite
        float charge = Mathf.Lerp(0.6f, 1f, p);

        _outerAngle += Mathf.Lerp(20f, 150f, p * p) * dt;
        _innerAngle -= Mathf.Lerp(45f, 260f, p * p) * dt;

        SetFlat(_glow, _radius * 2.5f * grow, 0f);
        _glow.color = A(_main, (0.22f + 0.28f * breathe) * charge * appear);

        SetFlat(_outer, _radius * 2f * grow, _outerAngle);
        _outer.color = A(_bright, 0.95f * appear * Mathf.Lerp(0.8f, 1f, breathe));

        SetFlat(_inner, _radius * 1.3f * grow, _innerAngle);
        _inner.color = A(_hot, 0.85f * appear);

        // Anneau qui se resserre vers le centre dans le dernier tiers.
        float cp = Mathf.Clamp01((p - 0.62f) / 0.38f);
        if (cp > 0f)
        {
            _converge.enabled = true;
            SetFlat(_converge, _radius * 2f * Mathf.Lerp(1.7f, 0.15f, cp * cp), _outerAngle * 0.5f);
            _converge.color = A(_hot, Mathf.Sin(cp * Mathf.PI) * 0.9f);
        }

        // Pilier de lumière dans la 2e moitié.
        float pp = Mathf.Clamp01((p - 0.5f) / 0.5f);
        if (pp > 0f && _cam != null)
        {
            _pillar.enabled = true;
            SetPillar(_radius * (0.55f + 0.35f * pp), Mathf.Max(0.05f, _pillarHeight * pp * pp));
            _pillar.transform.rotation = _cam.rotation;
            _pillar.color = A(_bright, 0.75f * pp);
        }

        UpdateMotes(dt, p, true);
    }

    // Appelé par le boss au moment exact où le mini-boss apparaît.
    public void Burst()
    {
        if (_bursting) return;
        _bursting = true;
        _burstT = 0f;

        _flash.enabled = true;
        _converge.enabled = false;

        ExpandingRingVFX.Spawn(transform.position + Vector3.up * 0.15f, _radius * 1.7f, A(_bright, 0.8f), 0.45f);

        // Explosion d'étincelles : toutes les particules sont relancées, plus rapides et plus courtes.
        for (int i = 0; i < _motes.Length; i++)
            SpawnMote(_motes[i], 1f, true);
    }

    private void UpdateBurst(float dt)
    {
        _burstT += dt;
        float q = Mathf.Clamp01(_burstT / BurstDuration);
        float inv = 1f - q;

        SetFlat(_flash, _radius * (1.6f + 3f * q), 0f);
        _flash.color = A(_hot, inv * inv);

        SetFlat(_outer, _radius * 2f * (1f + 0.35f * q), _outerAngle);
        _outer.color = A(_bright, 0.95f * inv);

        SetFlat(_inner, _radius * 1.3f * (1f + 0.5f * q), _innerAngle);
        _inner.color = A(_hot, 0.85f * inv);

        SetFlat(_glow, _radius * 2.5f * (1f + 0.4f * q), 0f);
        _glow.color = A(_main, 0.5f * inv);

        if (_cam != null && _pillar.enabled)
        {
            SetPillar(_radius * (0.9f * inv + 0.1f), _pillarHeight * (1f + 0.5f * q));
            _pillar.transform.rotation = _cam.rotation;
            _pillar.color = A(_hot, inv);
        }

        UpdateMotes(dt, 1f, false);

        if (q >= 1f) Destroy(gameObject);
    }

    // ------------------------------------------------------------------
    // Étincelles
    // ------------------------------------------------------------------

    private void UpdateMotes(float dt, float p, bool spawning)
    {
        if (spawning)
        {
            _moteSpawnAccum += Mathf.Lerp(8f, 45f, p) * dt;
            while (_moteSpawnAccum >= 1f)
            {
                _moteSpawnAccum -= 1f;
                for (int i = 0; i < _motes.Length; i++)
                {
                    if (_motes[i].alive) continue;
                    SpawnMote(_motes[i], p, false);
                    break;
                }
            }
        }

        for (int i = 0; i < _motes.Length; i++)
        {
            Mote m = _motes[i];
            if (!m.alive) continue;

            m.age += dt;
            float q = m.age / m.life;
            if (q >= 1f)
            {
                m.alive = false;
                m.sr.enabled = false;
                continue;
            }

            m.sr.transform.localPosition = m.start + Vector3.up * (m.rise * m.age);
            float size = m.size * (1f - 0.5f * q);
            float moteScale = size / Mathf.Max(0.01f, m.sr.sprite.bounds.size.x);
            m.sr.transform.localScale = new Vector3(moteScale, moteScale, 1f);
            if (_cam != null) m.sr.transform.rotation = _cam.rotation;
            m.sr.color = A(_hot, Mathf.Sin(q * Mathf.PI) * 0.9f);
        }
    }

    private void SpawnMote(Mote m, float p, bool burst)
    {
        Vector2 r = Random.insideUnitCircle * _radius * 0.9f;
        m.start = new Vector3(r.x, 0.1f, r.y);
        m.age = 0f;
        m.life = burst ? Random.Range(0.35f, 0.6f) : Random.Range(0.7f, 1.3f);
        m.rise = burst ? Random.Range(4f, 8f) : Random.Range(2f, 4.5f) * (0.7f + 0.6f * p);
        m.size = Random.Range(0.12f, 0.3f) * (0.8f + _radius * 0.1f);
        m.alive = true;
        m.sr.enabled = true;
    }

    // ------------------------------------------------------------------
    // Utilitaires de mise en forme
    // ------------------------------------------------------------------

    // Pose le sprite À PLAT sur le sol, à un diamètre monde donné, tourné de angleDeg autour de la verticale.
    private static void SetFlat(SpriteRenderer sr, float diameter, float angleDeg)
    {
        float s = diameter / Mathf.Max(0.01f, sr.sprite.bounds.size.x);
        sr.transform.localScale = new Vector3(s, s, 1f);
        sr.transform.localRotation = Quaternion.Euler(90f, 0f, 0f) * Quaternion.Euler(0f, 0f, angleDeg);
    }

    // Pilier : sprite debout (pivot en bas), largeur/hauteur monde.
    private void SetPillar(float width, float height)
    {
        Vector3 size = _pillar.sprite.bounds.size;
        _pillar.transform.localScale = new Vector3(width / Mathf.Max(0.01f, size.x), height / Mathf.Max(0.01f, size.y), 1f);
    }

    private static Color A(Color c, float alpha)
    {
        c.a = Mathf.Clamp01(alpha);
        return c;
    }

    private static float Smooth(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }

    // Rebond léger : dépasse un peu la valeur finale puis revient.
    private static float EaseOutBack(float t)
    {
        const float c1 = 1.5f;
        const float c3 = c1 + 1f;
        float u = t - 1f;
        return 1f + c3 * u * u * u + c1 * u * u;
    }

    // ------------------------------------------------------------------
    // Matériau + sprites procéduraux (générés une seule fois, aucun asset à importer)
    // ------------------------------------------------------------------

    private static Material _spriteMat;
    private static Material SpriteMaterial()
    {
        if (_spriteMat == null) _spriteMat = new Material(Shader.Find("Sprites/Default"));
        return _spriteMat;
    }

    // Anneaux fins + graduations (cercle de repli si aucune image de cercle runique n'est fournie ; sert aussi de
    // cercle intérieur et d'anneau qui se resserre). Blanc : la couleur vient du SpriteRenderer.
    private static Sprite _ringSprite;
    private static Sprite RingSprite()
    {
        if (_ringSprite != null) return _ringSprite;

        const int n = 256;
        Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                float px = (x + 0.5f) / n * 2f - 1f;
                float py = (y + 0.5f) / n * 2f - 1f;
                float r = Mathf.Sqrt(px * px + py * py);
                float ang = Mathf.Atan2(py, px);

                float a = 0f;
                a = Mathf.Max(a, Band(r, 0.93f, 0.030f));   // anneau extérieur épais
                a = Mathf.Max(a, Band(r, 0.84f, 0.010f));   // anneau fin
                a = Mathf.Max(a, Band(r, 0.62f, 0.012f));   // anneau intérieur

                // 24 graduations entre les deux anneaux extérieurs
                float seg = (ang + Mathf.PI) / (2f * Mathf.PI) * 24f;
                float tick = Smooth(1f - Mathf.Abs(seg - Mathf.Round(seg)) / 0.07f);
                a = Mathf.Max(a, tick * RangeMask(r, 0.855f, 0.915f));

                // 12 graduations plus courtes à l'intérieur
                float seg2 = (ang + Mathf.PI) / (2f * Mathf.PI) * 12f;
                float tick2 = Smooth(1f - Mathf.Abs(seg2 - Mathf.Round(seg2)) / 0.06f);
                a = Mathf.Max(a, tick2 * RangeMask(r, 0.64f, 0.74f));

                // voile très léger dans le disque central
                if (r < 0.6f) a = Mathf.Max(a, 0.06f);

                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();
        _ringSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        return _ringSprite;
    }

    private static float Band(float r, float center, float halfWidth)
    {
        return Smooth(1f - Mathf.Abs(r - center) / halfWidth);
    }

    private static float RangeMask(float r, float lo, float hi)
    {
        return Smooth((r - lo) / 0.01f) * Smooth((hi - r) / 0.01f);
    }

    // Disque doux (halo, éclat, étincelles).
    private static Sprite _glowSprite;
    private static Sprite GlowSprite()
    {
        if (_glowSprite != null) return _glowSprite;

        const int n = 128;
        Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                float px = (x + 0.5f) / n * 2f - 1f;
                float py = (y + 0.5f) / n * 2f - 1f;
                float r = Mathf.Sqrt(px * px + py * py);
                float a = Smooth(1f - r);
                a *= a;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();
        _glowSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        return _glowSprite;
    }

    // Pilier de lumière : bords doux, s'estompe vers le haut, pivot en bas.
    private static Sprite _pillarSprite;
    private static Sprite PillarSprite()
    {
        if (_pillarSprite != null) return _pillarSprite;

        const int w = 64, h = 256;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        for (int y = 0; y < h; y++)
        {
            float v = y / (h - 1f);   // 0 = bas, 1 = haut
            float vertical = Mathf.Pow(1f - v, 0.7f) * Smooth(v / 0.04f);
            for (int x = 0; x < w; x++)
            {
                float u = (x + 0.5f) / w * 2f - 1f;
                float side = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(u)), 1.6f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, side * vertical));
            }
        }
        tex.Apply();
        _pillarSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), 100f, 0, SpriteMeshType.FullRect);
        return _pillarSprite;
    }
}