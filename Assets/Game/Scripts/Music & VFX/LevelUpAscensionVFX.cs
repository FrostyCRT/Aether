using UnityEngine;

// AJOUTÉ (2026-10-29, retour utilisateur : "enlever la zone ronde qui apparait autour du joueur quand il y a un level up,
// ou sinon il faut que tu fasse quelque chose de graphique, comme tu as fait pour le rework des fusions") - temps fort du
// passage de niveau, qui remplace l'anneau doré d'ExpandingRingVFX et les étincelles d'ImpactSparkVFX (LevelUpManager.
// BeginPreShowFanfare). L'anneau lisse qui s'étend au sol se lisait comme une ZONE (et c'est la forme des télégraphes
// ennemis) : ici tout MONTE, rien ne s'étale au sol, l'effet dit "le héros s'élève", pas "attention, zone".
// Déroulé (temps réel : la fin se joue pendant que l'écran de choix s'ouvre, jeu en pause) :
//  1. Halo doré très doux aux pieds du héros (jamais un disque net ni un anneau), qui s'éteint vite.
//  2. Trait de lumière vertical, fin, qui jaillit du sol et s'efface en montant.
//  3. Fragments de cristal dorés (FXSprites.CrystalFragment) qui s'arrachent du sol autour du héros et montent en
//     spirale en se resserrant au-dessus de sa tête...
//  4. ...où ils se fondent en une étoile de cristal (FXSprites.CrystalShard) qui éclot avec un dépassement, tourne et
//     projette des éclats (FXSprites.Splinter) dans le plan de l'écran.
//  5. Poussières dorées qui montent en scintillant, plus lentes que le reste.
// L'effet suit le héros (il continue de bouger pendant la fanfare). Tous les sprites verticaux font face à la caméra.
// Une seule instance, réutilisée : les passages de niveau s'enchaînent en file, jamais deux fanfares à la fois.
public class LevelUpAscensionVFX : MonoBehaviour
{
    private const int ShardCount = 7;
    private const int SplinterCount = 9;
    private const int MoteCount = 12;

    private const float RiseDuration = 0.3f;    // fragments : du sol jusqu'à l'étoile (= durée de la fanfare jouable)
    private const float StarDuration = 0.75f;
    private const float SplinterDuration = 0.45f;
    private const float MoteDuration = 1.0f;
    private const float TotalDuration = 1.15f;

    private const float StarHeight = 3.2f;      // au-dessus des pieds du héros
    private const float ShardStartRadius = 1.5f;

    private static LevelUpAscensionVFX _instance;

    private static readonly Color WarmWhite = new Color(1f, 0.97f, 0.88f);

    private SpriteRenderer _halo, _beam, _starShade, _starGlow, _star;
    private SpriteRenderer[] _shards, _splinters, _motes;
    private float[] _shardAngle, _shardDelay;
    private float[] _splinterAngle, _splinterReach;
    private Vector3[] _moteStart;
    private float[] _moteRise, _motePhase, _moteDelay;

    private Transform _target, _cam;
    private Color _gold, _hot, _deep;
    private float _t = -1f, _starSpin;

    public static void Spawn(Transform target, Color gold)
    {
        if (target == null) return;
        if (_instance == null)
        {
            _instance = new GameObject("LevelUpAscensionVFX").AddComponent<LevelUpAscensionVFX>();
            _instance.Build();
        }
        _instance.Play(target, gold);
    }

    private void Build()
    {
        // Ordres de tri négatifs : les chiffres de dégâts (TextMeshPro, ordre 0) restent lisibles par-dessus.
        _halo = FXSprites.MakeRenderer(transform, "Halo", FXSprites.Glow(), -19, 0.04f);
        _beam = FXSprites.MakeRenderer(transform, "Beam", FXSprites.Pillar(), -15, 0f);
        _starShade = FXSprites.MakeRenderer(transform, "StarShade", FXSprites.Glow(), -16, 0f);
        _starGlow = FXSprites.MakeRenderer(transform, "StarGlow", FXSprites.Glow(), -14, 0f);
        _star = FXSprites.MakeRenderer(transform, "Star", FXSprites.CrystalShard(), -12, 0f);

        _shards = new SpriteRenderer[ShardCount];
        _shardAngle = new float[ShardCount];
        _shardDelay = new float[ShardCount];
        for (int i = 0; i < ShardCount; i++)
            _shards[i] = FXSprites.MakeRenderer(transform, "Shard" + i, FXSprites.CrystalFragment(i), -13, 0f);

        _splinters = new SpriteRenderer[SplinterCount];
        _splinterAngle = new float[SplinterCount];
        _splinterReach = new float[SplinterCount];
        for (int i = 0; i < SplinterCount; i++)
            _splinters[i] = FXSprites.MakeRenderer(transform, "Splinter" + i, FXSprites.Splinter(), -11, 0f);

        _motes = new SpriteRenderer[MoteCount];
        _moteStart = new Vector3[MoteCount];
        _moteRise = new float[MoteCount];
        _motePhase = new float[MoteCount];
        _moteDelay = new float[MoteCount];
        for (int i = 0; i < MoteCount; i++)
            _motes[i] = FXSprites.MakeRenderer(transform, "Mote" + i, FXSprites.Glow(), -10, 0f);
    }

    private void Play(Transform target, Color gold)
    {
        _target = target;
        _gold = new Color(gold.r, gold.g, gold.b, 1f);
        _hot = Color.Lerp(_gold, WarmWhite, 0.65f);
        _deep = Color.Lerp(_gold, new Color(0.55f, 0.25f, 0.02f), 0.45f);
        _starSpin = Random.Range(0f, 360f);

        float baseAngle = Random.Range(0f, 360f);
        for (int i = 0; i < ShardCount; i++)
        {
            _shardAngle[i] = baseAngle + (360f / ShardCount) * i + Random.Range(-10f, 10f);
            _shardDelay[i] = Random.Range(0f, 0.05f);
        }
        for (int i = 0; i < SplinterCount; i++)
        {
            _splinterAngle[i] = (360f / SplinterCount) * i + Random.Range(-14f, 14f);
            _splinterReach[i] = Random.Range(0.8f, 1.25f);
        }
        for (int i = 0; i < MoteCount; i++)
        {
            float rad = Random.Range(0f, Mathf.PI * 2f);
            _moteStart[i] = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad)) * Random.Range(0.3f, 1.5f);
            _moteRise[i] = Random.Range(1.4f, 2.8f);
            _motePhase[i] = Random.Range(0f, Mathf.PI * 2f);
            _moteDelay[i] = Random.Range(0f, 0.25f);
        }

        gameObject.SetActive(true);
        _t = 0f;
        Tick(0f);
    }

    private void LateUpdate()
    {
        if (_t < 0f) return;
        _t += Time.unscaledDeltaTime;
        if (_t >= TotalDuration || _target == null)
        {
            _t = -1f;
            gameObject.SetActive(false);
            return;
        }
        Tick(_t);
    }

    private void Tick(float t)
    {
        transform.position = _target.position;
        if (_cam == null && Camera.main != null) _cam = Camera.main.transform;
        Quaternion face = _cam != null ? _cam.rotation : Quaternion.identity;
        Vector3 right = face * Vector3.right, up = face * Vector3.up;

        UpdateHalo(t);
        UpdateBeam(t, face);
        UpdateShards(t, face);
        Vector3 starPos = transform.position + Vector3.up * StarHeight;
        UpdateStar(t, face, starPos);
        UpdateSplinters(t, face, right, up, starPos);
        UpdateMotes(t, face);
    }

    private void UpdateHalo(float t)
    {
        float p = Mathf.Clamp01(t / 0.55f);
        FXSprites.SetFlat(_halo, Mathf.Lerp(1.6f, 3.2f, FXSprites.EaseOutCubic(p)), 0f);
        _halo.color = FXSprites.A(_gold, 0.6f * FXSprites.Smooth(Mathf.Clamp01(t / 0.05f)) * (1f - FXSprites.Smooth(p)));
    }

    private void UpdateBeam(float t, Quaternion face)
    {
        float p = Mathf.Clamp01(t / 0.6f);
        float grow = FXSprites.EaseOutCubic(Mathf.Clamp01(t / 0.2f));
        Billboard(_beam, transform.position, face, 0f,
            Mathf.Lerp(1.3f, 0.35f, FXSprites.Smooth(p)), Mathf.Max(0.05f, StarHeight * 1.35f * grow));
        _beam.color = FXSprites.A(_hot, 0.9f * (1f - FXSprites.Smooth(p)));
    }

    // Fragments : arrachés du sol, montent en spirale en se resserrant, se fondent dans l'étoile à l'arrivée.
    private void UpdateShards(float t, Quaternion face)
    {
        for (int i = 0; i < ShardCount; i++)
        {
            SpriteRenderer sr = _shards[i];
            float local = (t - _shardDelay[i]) / RiseDuration;
            if (local < 0f || local > 1.15f) { sr.color = Color.clear; continue; }

            float p = Mathf.Clamp01(local);
            float e = FXSprites.EaseOutCubic(p);
            float ang = (_shardAngle[i] + 150f * e) * Mathf.Deg2Rad;
            float radius = Mathf.Lerp(ShardStartRadius, 0.12f, e);
            Vector3 pos = transform.position + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * radius
                + Vector3.up * Mathf.Lerp(0.15f, StarHeight, FXSprites.Smooth(p));

            float pop = FXSprites.EaseOutBack(Mathf.Clamp01(local / 0.25f), 1.8f);
            float merge = Mathf.Clamp01((local - 0.85f) / 0.3f);   // absorbé par l'étoile
            Billboard(sr, pos, face, _shardAngle[i] + 300f * p, 0.85f * pop * (1f - 0.6f * merge));
            sr.color = FXSprites.A(Color.Lerp(_hot, _gold, FXSprites.Smooth(p * 2f)), 1f - FXSprites.Smooth(merge));
        }
    }

    private void UpdateStar(float t, Quaternion face, Vector3 pos)
    {
        float local = t - RiseDuration * 0.85f;
        if (local < 0f) { _star.color = Color.clear; _starGlow.color = Color.clear; _starShade.color = Color.clear; return; }

        float p = Mathf.Clamp01(local / StarDuration);
        float grow = FXSprites.EaseOutBack(Mathf.Clamp01(local / 0.18f), 1.6f);
        float fade = 1f - FXSprites.Smooth(Mathf.Clamp01((p - 0.4f) / 0.6f));
        Billboard(_star, pos, face, _starSpin + 70f * FXSprites.EaseOutCubic(p), 3.2f * grow * Mathf.Lerp(1f, 0.8f, p));
        _star.color = FXSprites.A(Color.Lerp(_hot, _gold, FXSprites.Smooth(p * 3f)), fade);

        // Ombre ambrée sous l'étoile : la détache du sol clair (sans elle, l'or se fond dans l'herbe).
        Billboard(_starShade, pos, face, 0f, 3.8f * grow);
        _starShade.color = FXSprites.A(_deep * 0.45f, 0.55f * fade);

        // Éclat derrière l'étoile : flash bref à l'éclosion, puis lueur douce qui s'éteint avec elle.
        float flash = 1f - FXSprites.Smooth(Mathf.Clamp01(local / 0.2f));
        Billboard(_starGlow, pos, face, 0f, Mathf.Lerp(3f, 4.8f, flash) * grow);
        _starGlow.color = FXSprites.A(Color.Lerp(_gold, WarmWhite, flash), (0.3f + 0.5f * flash) * fade);
    }

    // Éclats projetés depuis l'étoile dans le plan de l'écran (toujours lisibles, quel que soit l'angle de caméra).
    private void UpdateSplinters(float t, Quaternion face, Vector3 right, Vector3 up, Vector3 starPos)
    {
        float local = t - RiseDuration * 0.9f;
        float p = Mathf.Clamp01(local / SplinterDuration);
        for (int i = 0; i < SplinterCount; i++)
        {
            SpriteRenderer sr = _splinters[i];
            if (local < 0f || p >= 1f) { sr.color = Color.clear; continue; }

            float e = FXSprites.EaseOutCubic(p);
            float rad = _splinterAngle[i] * Mathf.Deg2Rad;
            Vector3 dir = right * Mathf.Cos(rad) + up * Mathf.Sin(rad);
            Billboard(sr, starPos + dir * (0.6f + 3.2f * _splinterReach[i] * e), face, _splinterAngle[i],
                Mathf.Lerp(2.4f, 0.9f, e));
            Color c = p < 0.35f ? Color.Lerp(WarmWhite, _hot, p / 0.35f) : Color.Lerp(_hot, _deep, (p - 0.35f) / 0.65f);
            sr.color = FXSprites.A(c, p < 0.06f ? p / 0.06f : 1f - FXSprites.Smooth((p - 0.06f) / 0.94f));
        }
    }

    private void UpdateMotes(float t, Quaternion face)
    {
        for (int i = 0; i < MoteCount; i++)
        {
            SpriteRenderer sr = _motes[i];
            float p = Mathf.Clamp01((t - _moteDelay[i]) / MoteDuration);
            if (p <= 0f || p >= 1f) { sr.color = Color.clear; continue; }

            Vector3 pos = transform.position + _moteStart[i] * (1f - 0.3f * p) + Vector3.up * (0.2f + _moteRise[i] * FXSprites.EaseOutQuad(p));
            Billboard(sr, pos, face, 0f, Mathf.Lerp(0.45f, 0.14f, p));
            float twinkle = 0.6f + 0.4f * Mathf.Sin(t * 26f + _motePhase[i]);
            float alpha = FXSprites.Smooth(p / 0.1f) * (1f - FXSprites.Smooth((p - 0.4f) / 0.6f));
            sr.color = FXSprites.A(Color.Lerp(_hot, _gold, p), alpha * twinkle);
        }
    }

    // Pose un sprite face à la caméra, à une taille monde donnée (height < 0 : carré), tourné de angleDeg dans le plan
    // de l'écran (Splinter pointe vers +X : angle = direction de vol).
    private static void Billboard(SpriteRenderer sr, Vector3 pos, Quaternion face, float angleDeg, float width, float height = -1f)
    {
        Vector3 size = sr.sprite.bounds.size;
        float sx = width / Mathf.Max(0.01f, size.x);
        float sy = height < 0f ? sx : height / Mathf.Max(0.01f, size.y);
        Transform tr = sr.transform;
        tr.position = pos;
        tr.rotation = face * Quaternion.Euler(0f, 0f, angleDeg);
        tr.localScale = new Vector3(sx, sy, 1f);
    }
}
