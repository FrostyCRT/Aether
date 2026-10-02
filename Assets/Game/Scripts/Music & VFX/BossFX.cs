using UnityEngine;

// Effets d'ARRIVÉE et de MORT des boss, entièrement construits en code (aucun prefab à poser).
//
// Mort (PlayDeath) : sur `duration` secondes, le boss reste en place mais inoffensif.
//   - t = 0 : coup d'éclat (flash, onde de choc, gerbe d'étincelles) et court RALENTI global (désactivable) ;
//   - jusqu'à 72 % : il se débat (tremblement de plus en plus fort), son corps s'embrase de blanc, des braises montent ;
//   - après : il rétrécit jusqu'à disparaître en s'embrasant ;
//   - t = duration : GRAND FINAL (éclat, pilier de lumière, double onde de choc, grosse gerbe d'étincelles), le boss est
//     détruit à cet instant, et l'effet termine ses particules tout seul.
// Arrivée (PlayArrival) : éclat, onde de choc, courte colonne de lumière et gerbe d'étincelles à l'apparition d'un boss.
//
// Le temps utilise Time.deltaTime : tout se fige tout seul en pause. Le ralenti global ne touche JAMAIS à
// Time.timeScale si le jeu est en pause / sur un écran de choix, et ne le restaure que si personne d'autre ne l'a modifié.
public class BossFX : MonoBehaviour
{
    private enum Mode { Arrival, Death }

    private const int SparkCount = 140;
    private const float SlowScale = 0.25f;
    private const float SlowHold = 0.18f;
    private const float SlowRecover = 0.22f;

    private class Spark
    {
        public SpriteRenderer sr;
        public Vector3 pos, vel;
        public float age, life, size, gravity;
        public Color color;
        public bool alive;
    }

    private Mode _mode;
    private Spark[] _sparks;
    private SpriteRenderer _flash;
    private SpriteRenderer _pillar;
    private Transform _cam;

    private float _radius;
    private Color _main, _bright, _hot;
    private float _t;
    private float _lifeTotal;

    private float _flashT = -1f, _flashDur, _flashPeak;
    private Vector3 _flashCenter;

    private float _pillarT = -1f, _pillarDur, _pillarHeight, _pillarWidth;
    private Vector3 _pillarBase;

    // Mort
    private Transform _target;
    private Vector3 _basePos;
    private Vector3 _baseScale;
    private float _dur;
    private bool _finaleDone;
    private float _sparkAccum;
    private Renderer _glowRenderer;
    private string _glowProp;
    private Color _glowColor;
    private MaterialPropertyBlock _pb;

    // Ralenti
    private bool _slowEnabled;
    private bool _slowActive;
    private float _slowTimer;
    private float _slowAppliedScale = -1f;

    // ------------------------------------------------------------------
    // Création
    // ------------------------------------------------------------------

    public static void PlayArrival(Vector3 position, float radius, Color color)
    {
        GameObject go = new GameObject("BossArrivalFX");
        BossFX fx = go.AddComponent<BossFX>();
        fx.InitArrival(new Vector3(position.x, 0f, position.z), radius, color);
    }

    // target : le boss mourant (détruit par cet effet au moment du final). glowRenderer/glowProperty : corps du boss et
    // propriété d'émission pour l'embrasement (peuvent être nuls : alors pas d'embrasement).
    public static void PlayDeath(Transform target, float radius, Color color, float duration, bool slowMotion,
        Renderer glowRenderer, string glowProperty, Color glowColor)
    {
        GameObject go = new GameObject("BossDeathFX");
        BossFX fx = go.AddComponent<BossFX>();
        fx.InitDeath(target, radius, color, duration, slowMotion, glowRenderer, glowProperty, glowColor);
    }

    private void BuildCommon(float radius, Color color)
    {
        _radius = Mathf.Max(1f, radius);
        _main = color;
        _bright = Color.Lerp(color, Color.white, 0.5f);
        _hot = Color.Lerp(color, Color.white, 0.85f);

        _pillar = FXSprites.MakeRenderer(transform, "Pillar", FXSprites.Pillar(), 19, 0f);
        _pillar.enabled = false;
        _flash = FXSprites.MakeRenderer(transform, "Flash", FXSprites.Glow(), 20, 0f);
        _flash.enabled = false;

        _sparks = new Spark[SparkCount];
        for (int i = 0; i < SparkCount; i++)
        {
            Spark s = new Spark();
            s.sr = FXSprites.MakeRenderer(transform, "Spark", FXSprites.Glow(), 21, 0f);
            s.sr.enabled = false;
            _sparks[i] = s;
        }
    }

    private void InitArrival(Vector3 ground, float radius, Color color)
    {
        _mode = Mode.Arrival;
        BuildCommon(radius, color);
        _lifeTotal = 1.4f;

        Vector3 mid = ground + Vector3.up * 1.0f;
        StartFlash(mid, _radius * 3f, 0.35f);
        StartPillar(ground, _radius * 2.2f, _radius * 0.8f, 0.55f);
        ExpandingRingVFX.Spawn(ground + Vector3.up * 0.15f, _radius * 2.4f, FXSprites.A(_bright, 0.8f), 0.5f);
        ExpandingRingVFX.Spawn(ground + Vector3.up * 0.15f, _radius * 1.3f, FXSprites.A(_hot, 0.7f), 0.3f);
        Burst(ground + Vector3.up * 0.2f, 34, 3f, 8f, 1.3f, 0.5f, 1.0f, 0.12f, 0.28f, 12f);
    }

    private void InitDeath(Transform target, float radius, Color color, float duration, bool slowMotion,
        Renderer glowRenderer, string glowProperty, Color glowColor)
    {
        _mode = Mode.Death;
        BuildCommon(radius, color);

        _target = target;
        _basePos = target != null ? target.position : transform.position;
        _baseScale = target != null ? target.localScale : Vector3.one;
        _dur = Mathf.Max(0.3f, duration);
        _lifeTotal = _dur + 1.4f;

        _glowRenderer = glowRenderer;
        _glowProp = string.IsNullOrEmpty(glowProperty) ? "_EmissionColor" : glowProperty;
        _glowColor = glowColor;
        _pb = new MaterialPropertyBlock();

        // Coup d'éclat initial.
        Vector3 ground = new Vector3(_basePos.x, 0f, _basePos.z);
        Vector3 mid = ground + Vector3.up * 1.2f;
        StartFlash(mid, _radius * 2.5f, 0.3f);
        ExpandingRingVFX.Spawn(ground + Vector3.up * 0.15f, _radius * 2.2f, FXSprites.A(_bright, 0.85f), 0.5f);
        Burst(mid, 40, 4f, 11f, 0.9f, 0.5f, 1.0f, 0.12f, 0.3f, 12f);

        _slowEnabled = slowMotion;
        BeginSlowMo();
    }

    // ------------------------------------------------------------------
    // Boucle
    // ------------------------------------------------------------------

    private void Update()
    {
        float dt = Time.deltaTime;
        if (_cam == null && Camera.main != null) _cam = Camera.main.transform;

        UpdateSlowMo();

        if (_mode == Mode.Death) UpdateDeath(dt);
        else _t += dt;

        UpdateFlash(dt);
        UpdatePillar(dt);
        UpdateSparks(dt);

        if (_t >= _lifeTotal) Destroy(gameObject);
    }

    private void UpdateDeath(float dt)
    {
        _t += dt;

        if (!_finaleDone)
        {
            if (_target == null)
            {
                Finale();   // le boss a disparu autrement (rechargement de scène...) : on joue quand même le final
            }
            else
            {
                float k = Mathf.Clamp01(_t / _dur);
                const float jitterEnd = 0.72f;

                // Tremblement de plus en plus fort.
                float amp = Mathf.Lerp(0.04f, 0.22f, k);
                Vector3 jitter = new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)) * amp;
                _target.position = _basePos + jitter;

                // Rétrécissement final (ease-in cubique).
                float sk = Mathf.Clamp01((k - jitterEnd) / (1f - jitterEnd));
                float scale = Mathf.Max(0.001f, 1f - sk * sk * sk);
                _target.localScale = _baseScale * scale;

                // Embrasement du corps.
                SetGlow(k);

                // Braises qui montent, de plus en plus nombreuses.
                _sparkAccum += Mathf.Lerp(20f, 70f, k) * dt;
                while (_sparkAccum >= 1f)
                {
                    _sparkAccum -= 1f;
                    Vector2 p = Random.insideUnitCircle * _radius * 0.6f;
                    Vector3 origin = new Vector3(_basePos.x + p.x, 0.3f, _basePos.z + p.y);
                    Color c = Color.Lerp(_bright, _hot, Random.value);
                    Emit(origin, new Vector3(Random.Range(-0.6f, 0.6f), Random.Range(2.5f, 5.5f), Random.Range(-0.6f, 0.6f)),
                        Random.Range(0.6f, 1.1f), Random.Range(0.1f, 0.24f), 0f, c);
                }

                if (_t >= _dur) Finale();
            }
        }
    }

    private void Finale()
    {
        _finaleDone = true;

        Vector3 ground = new Vector3(_basePos.x, 0f, _basePos.z);
        Vector3 mid = ground + Vector3.up * 1.4f;

        StartFlash(mid, _radius * 5.5f, 0.5f);
        StartPillar(ground, _radius * 3.5f, _radius * 1.1f, 0.8f);
        ExpandingRingVFX.Spawn(ground + Vector3.up * 0.15f, _radius * 3.6f, FXSprites.A(_bright, 0.9f), 0.6f);
        ExpandingRingVFX.Spawn(ground + Vector3.up * 0.15f, _radius * 2.0f, FXSprites.A(_hot, 0.8f), 0.4f);
        Burst(mid, 90, 5f, 14f, 0.9f, 0.7f, 1.3f, 0.14f, 0.34f, 12f);

        if (_target != null) Destroy(_target.gameObject);
        _target = null;
    }

    private void SetGlow(float k)
    {
        if (_glowRenderer == null) return;
        _glowRenderer.GetPropertyBlock(_pb);
        _pb.SetColor(_glowProp, _glowColor * Mathf.Lerp(0f, 5f, k));
        _glowRenderer.SetPropertyBlock(_pb);
    }

    // ------------------------------------------------------------------
    // Éclat, pilier, étincelles
    // ------------------------------------------------------------------

    private void StartFlash(Vector3 center, float peakDiameter, float duration)
    {
        _flashCenter = center;
        _flashPeak = peakDiameter;
        _flashDur = Mathf.Max(0.05f, duration);
        _flashT = 0f;
        _flash.enabled = true;
    }

    private void UpdateFlash(float dt)
    {
        if (_flashT < 0f) return;

        _flashT += dt;
        float q = _flashT / _flashDur;
        if (q >= 1f)
        {
            _flash.enabled = false;
            _flashT = -1f;
            return;
        }

        float size = _flashPeak * (0.5f + 0.5f * FXSprites.EaseOutCubic(q));
        float s = size / Mathf.Max(0.01f, _flash.sprite.bounds.size.x);
        _flash.transform.position = _flashCenter;
        _flash.transform.localScale = new Vector3(s, s, 1f);
        if (_cam != null) _flash.transform.rotation = _cam.rotation;
        float inv = 1f - q;
        _flash.color = FXSprites.A(_hot, inv * inv);
    }

    private void StartPillar(Vector3 baseGround, float height, float width, float duration)
    {
        _pillarBase = baseGround;
        _pillarHeight = height;
        _pillarWidth = width;
        _pillarDur = Mathf.Max(0.1f, duration);
        _pillarT = 0f;
        _pillar.enabled = true;
    }

    private void UpdatePillar(float dt)
    {
        if (_pillarT < 0f) return;

        _pillarT += dt;
        float q = _pillarT / _pillarDur;
        if (q >= 1f)
        {
            _pillar.enabled = false;
            _pillarT = -1f;
            return;
        }

        float grow = FXSprites.EaseOutCubic(Mathf.Clamp01(q / 0.2f));
        float fade = q < 0.35f ? 1f : 1f - (q - 0.35f) / 0.65f;
        Vector3 size = _pillar.sprite.bounds.size;
        _pillar.transform.position = _pillarBase;
        _pillar.transform.localScale = new Vector3(_pillarWidth / Mathf.Max(0.01f, size.x), Mathf.Max(0.05f, _pillarHeight * grow) / Mathf.Max(0.01f, size.y), 1f);
        if (_cam != null) _pillar.transform.rotation = _cam.rotation;
        _pillar.color = FXSprites.A(_bright, fade * 0.9f);
    }

    private void Emit(Vector3 pos, Vector3 vel, float life, float size, float gravity, Color color)
    {
        for (int i = 0; i < _sparks.Length; i++)
        {
            Spark s = _sparks[i];
            if (s.alive) continue;

            s.pos = pos;
            s.vel = vel;
            s.age = 0f;
            s.life = life;
            s.size = size;
            s.gravity = gravity;
            s.color = color;
            s.alive = true;
            s.sr.enabled = true;
            return;
        }
    }

    private void Burst(Vector3 origin, int count, float speedMin, float speedMax, float upBias,
        float lifeMin, float lifeMax, float sizeMin, float sizeMax, float gravity)
    {
        for (int i = 0; i < count; i++)
        {
            Vector2 d = Random.insideUnitCircle.normalized;
            float speed = Random.Range(speedMin, speedMax);
            Vector3 vel = new Vector3(d.x * speed, upBias * speed * Random.Range(0.4f, 1.2f), d.y * speed);

            Color c = Color.Lerp(_bright, _hot, Random.value);
            if (Random.value < 0.25f) c = _main;

            Emit(origin + Random.insideUnitSphere * 0.3f, vel, Random.Range(lifeMin, lifeMax),
                Random.Range(sizeMin, sizeMax), gravity, c);
        }
    }

    private void UpdateSparks(float dt)
    {
        for (int i = 0; i < _sparks.Length; i++)
        {
            Spark s = _sparks[i];
            if (!s.alive) continue;

            s.age += dt;
            float q = s.age / s.life;
            if (q >= 1f)
            {
                s.alive = false;
                s.sr.enabled = false;
                continue;
            }

            s.vel.y -= s.gravity * dt;
            s.pos += s.vel * dt;
            if (s.pos.y < 0.05f)
            {
                s.pos.y = 0.05f;
                s.vel.y *= -0.3f;
                s.vel.x *= 0.7f;
                s.vel.z *= 0.7f;
            }

            float size = s.size * (1f - 0.6f * q);
            float sc = size / Mathf.Max(0.01f, s.sr.sprite.bounds.size.x);
            s.sr.transform.position = s.pos;
            s.sr.transform.localScale = new Vector3(sc, sc, 1f);
            if (_cam != null) s.sr.transform.rotation = _cam.rotation;
            s.sr.color = FXSprites.A(s.color, Mathf.Pow(1f - q, 1.2f));
        }
    }

    // ------------------------------------------------------------------
    // Ralenti (touche à Time.timeScale avec prudence)
    // ------------------------------------------------------------------

    private void BeginSlowMo()
    {
        if (!_slowEnabled) return;

        // Jeu en pause / écran de choix (timeScale à 0) ou déjà ralenti par autre chose : on n'y touche pas.
        if (!Mathf.Approximately(Time.timeScale, 1f)) return;

        _slowActive = true;
        _slowTimer = 0f;
        _slowAppliedScale = SlowScale;
        Time.timeScale = SlowScale;
    }

    private void UpdateSlowMo()
    {
        if (!_slowActive) return;

        // Quelqu'un d'autre a pris la main sur le temps (pause, choix de niveau...) : on s'efface, sans rien restaurer.
        if (!Mathf.Approximately(Time.timeScale, _slowAppliedScale))
        {
            _slowActive = false;
            return;
        }

        _slowTimer += Time.unscaledDeltaTime;

        float target;
        if (_slowTimer < SlowHold)
        {
            target = SlowScale;
        }
        else
        {
            float r = (_slowTimer - SlowHold) / SlowRecover;
            if (r >= 1f)
            {
                Time.timeScale = 1f;
                _slowActive = false;
                return;
            }
            target = Mathf.Lerp(SlowScale, 1f, r * r);
        }

        Time.timeScale = target;
        _slowAppliedScale = target;
    }

    private void OnDestroy()
    {
        if (_slowActive && Mathf.Approximately(Time.timeScale, _slowAppliedScale))
            Time.timeScale = 1f;
    }
}