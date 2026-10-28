using UnityEngine;

// AJOUTÉ (2026-10-28, passe visuelle de la fusion Orbe rebondissant + Boue, voir WeaponFusionMudOrb) - visuel de
// Sillage Boueux, piloté chaque image par l'arme :
//  1. Orbes : cristaux 3D (bipyramide à 6 faces, normales plates) azur à cœur ambré, qui tournent sur eux-mêmes en
//     flottant, avec un halo azur au sol et une traînée azur -> ambre (Orbe -> Boue). Petit sursaut à chaque rebond.
//  2. Flaques du sillage : tache de boue sombre aux bords doux, semée de 3 éclats de cristal azur qui jaillissent à la
//     pose puis s'enfoncent et s'éteignent avec la flaque. Remplace le disque bleu-vert opaque du prefab de Boue (sa
//     zone d'effet reste celle de MudPuddleZone).
// Grammaire visuelle du projet : aucun anneau lisse ni gradué (réservés aux télégraphes ennemis), uniquement du cristal.
// Orbes et flaques en coordonnées monde sous une racine fixe à l'origine : rien n'est traîné par le joueur.
public class MudWakeVisual : MonoBehaviour
{
    public static readonly Color Azure = new Color(0.15f, 0.55f, 0.95f);   // DamageNumberSpawner.ColorBouncingOrb
    private static readonly Color Amber = new Color(0.75f, 0.58f, 0.15f);  // DamageNumberSpawner.ColorMud
    private static readonly Color Mud = new Color(0.27f, 0.2f, 0.11f);
    private static readonly Color Ice = new Color(0.7f, 0.9f, 1f);

    public const float OrbY = 0.7f;
    private const int MaxPuddles = 48;
    private const int ShardsPerPuddle = 3;
    private const float ShardPopTime = 0.25f;
    private const float FadeTime = 0.6f;

    private Transform[] _orbs;
    private SpriteRenderer[] _orbGlows;
    private TrailRenderer[] _trails;
    private float[] _bounceFlash;
    private Material _orbMat;

    private SpriteRenderer[] _blots, _blotCores;
    private SpriteRenderer[] _shards;
    private Vector3[] _puddlePos;
    private float[] _puddleRadius, _puddleAge, _puddleLife, _puddleSpin;
    private Vector3[] _shardOffset;
    private float[] _shardAngle, _shardSize;
    private int _nextPuddle;

    private static Mesh _orbMesh;

    public void Build(int orbCount)
    {
        transform.position = Vector3.zero;
        _orbs = new Transform[orbCount];
        _orbGlows = new SpriteRenderer[orbCount];
        _trails = new TrailRenderer[orbCount];
        _bounceFlash = new float[orbCount];
        _orbMat = MakeOrbMaterial();

        Material trailMat = new Material(Shader.Find("Sprites/Default"));
        for (int i = 0; i < orbCount; i++)
        {
            _orbGlows[i] = FXSprites.MakeRenderer(transform, "WakeOrbGlow" + i, FXSprites.Glow(), 10, 0.05f);

            GameObject orb = new GameObject("WakeOrb" + i);
            orb.transform.SetParent(transform, false);
            orb.AddComponent<MeshFilter>().sharedMesh = OrbMesh();
            MeshRenderer mr = orb.AddComponent<MeshRenderer>();
            mr.sharedMaterial = _orbMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            _orbs[i] = orb.transform;

            // Traînée sur un objet frère (pas enfant de l'orbe : elle ne doit pas hériter de sa rotation/échelle).
            TrailRenderer tr = new GameObject("WakeTrail" + i).AddComponent<TrailRenderer>();
            tr.transform.SetParent(transform, false);
            tr.time = 0.35f;
            tr.minVertexDistance = 0.05f;
            tr.widthMultiplier = 0.5f;
            tr.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
            tr.sharedMaterial = trailMat;
            tr.startColor = FXSprites.A(Color.Lerp(Azure, Ice, 0.3f), 0.8f);
            tr.endColor = FXSprites.A(Amber, 0f);
            tr.sortingOrder = 11;
            tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            tr.receiveShadows = false;
            tr.emitting = false;
            _trails[i] = tr;
        }

        _blots = new SpriteRenderer[MaxPuddles];
        _blotCores = new SpriteRenderer[MaxPuddles];
        _shards = new SpriteRenderer[MaxPuddles * ShardsPerPuddle];
        _puddlePos = new Vector3[MaxPuddles];
        _puddleRadius = new float[MaxPuddles];
        _puddleAge = new float[MaxPuddles];
        _puddleLife = new float[MaxPuddles];
        _puddleSpin = new float[MaxPuddles];
        _shardOffset = new Vector3[MaxPuddles * ShardsPerPuddle];
        _shardAngle = new float[MaxPuddles * ShardsPerPuddle];
        _shardSize = new float[MaxPuddles * ShardsPerPuddle];
        for (int p = 0; p < MaxPuddles; p++)
        {
            _blots[p] = FXSprites.MakeRenderer(transform, "WakeMud" + p, FXSprites.Glow(), 2, 0.03f);
            _blotCores[p] = FXSprites.MakeRenderer(transform, "WakeMudCore" + p, FXSprites.Glow(), 3, 0.04f);
            _blots[p].enabled = _blotCores[p].enabled = false;
            for (int s = 0; s < ShardsPerPuddle; s++)
            {
                int k = p * ShardsPerPuddle + s;
                _shards[k] = FXSprites.MakeRenderer(transform, "WakeShard" + k, FXSprites.CrystalFragment(k % FXSprites.CrystalFragmentVariants), 4, 0.06f);
                _shards[k].enabled = false;
            }
            _puddleLife[p] = 0f;
        }
    }

    public void Bounce(int orb)
    {
        if (_bounceFlash != null && orb >= 0 && orb < _bounceFlash.Length) _bounceFlash[orb] = 1f;
    }

    public void AddPuddle(Vector3 groundPos, float radius, float life)
    {
        int p = _nextPuddle;
        _nextPuddle = (_nextPuddle + 1) % MaxPuddles;
        _puddlePos[p] = new Vector3(groundPos.x, 0f, groundPos.z);
        _puddleRadius[p] = radius;
        _puddleAge[p] = 0f;
        _puddleLife[p] = life;
        _puddleSpin[p] = Random.Range(0f, 360f);
        _blots[p].enabled = _blotCores[p].enabled = true;
        for (int s = 0; s < ShardsPerPuddle; s++)
        {
            int k = p * ShardsPerPuddle + s;
            float a = (s * 120f + Random.Range(-35f, 35f)) * Mathf.Deg2Rad;
            float r = radius * Random.Range(0.2f, 0.65f);
            _shardOffset[k] = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
            _shardAngle[k] = Random.Range(0f, 360f);
            _shardSize[k] = Random.Range(0.55f, 0.85f);
            _shards[k].enabled = true;
        }
    }

    public void Render(Vector3[] orbPositions, Vector3[] orbDirs, int count)
    {
        float time = Time.time;
        float dt = Time.deltaTime;

        for (int i = 0; i < _orbs.Length; i++)
        {
            bool on = i < count;
            _orbs[i].gameObject.SetActive(on);
            _orbGlows[i].enabled = on;
            if (!on) { _trails[i].emitting = false; continue; }

            _bounceFlash[i] = Mathf.Max(0f, _bounceFlash[i] - dt / 0.25f);
            float flash = _bounceFlash[i];
            Vector3 p = new Vector3(orbPositions[i].x, 0f, orbPositions[i].z);
            float bob = 0.08f * Mathf.Sin(time * 3.3f + i * 2.1f);

            Transform orb = _orbs[i];
            orb.position = p + Vector3.up * (OrbY + bob);
            orb.rotation = Quaternion.Euler(15f * Mathf.Sin(time * 1.4f + i), time * 160f + i * 60f, 0f);
            orb.localScale = Vector3.one * (1.3f * (1f + 0.3f * flash));

            TrailRenderer tr = _trails[i];
            tr.transform.position = p + Vector3.up * OrbY;
            if (!tr.emitting) { tr.Clear(); tr.emitting = true; }

            SpriteRenderer glow = _orbGlows[i];
            glow.transform.position = p + Vector3.up * 0.05f;
            FXSprites.SetFlat(glow, 2.4f * (1f + 0.4f * flash), 0f);
            glow.color = FXSprites.A(Color.Lerp(Azure, Ice, 0.3f * flash), 0.45f + 0.3f * flash);
        }

        for (int pIdx = 0; pIdx < MaxPuddles; pIdx++)
        {
            if (_puddleLife[pIdx] <= 0f) continue;
            _puddleAge[pIdx] += dt;
            float age = _puddleAge[pIdx], life = _puddleLife[pIdx];
            if (age >= life)
            {
                _puddleLife[pIdx] = 0f;
                _blots[pIdx].enabled = _blotCores[pIdx].enabled = false;
                for (int s = 0; s < ShardsPerPuddle; s++) _shards[pIdx * ShardsPerPuddle + s].enabled = false;
                continue;
            }

            float grow = FXSprites.EaseOutCubic(Mathf.Clamp01(age / 0.3f));   // même croissance que MudPuddleZone
            float fade = 1f - FXSprites.Smooth((age - (life - FadeTime)) / FadeTime);
            float r = _puddleRadius[pIdx];
            Vector3 c = _puddlePos[pIdx];

            SpriteRenderer blot = _blots[pIdx];
            blot.transform.position = c + Vector3.up * 0.03f;
            FXSprites.SetFlat(blot, r * 3f * grow, _puddleSpin[pIdx]);
            blot.color = FXSprites.A(Mud, 0.95f * fade);
            // Cœur plus dense : la tache se lit comme une flaque (zone de ralentissement), pas comme une ombre.
            SpriteRenderer core = _blotCores[pIdx];
            core.transform.position = c + Vector3.up * 0.04f;
            FXSprites.SetFlat(core, r * 1.9f * grow, _puddleSpin[pIdx] + 40f);
            core.color = FXSprites.A(Color.Lerp(Mud, Azure, 0.12f), 0.8f * fade);

            float pop = FXSprites.EaseOutBack(Mathf.Clamp01(age / ShardPopTime));
            for (int s = 0; s < ShardsPerPuddle; s++)
            {
                int k = pIdx * ShardsPerPuddle + s;
                SpriteRenderer sh = _shards[k];
                sh.transform.position = c + _shardOffset[k] * grow + Vector3.up * 0.06f;
                float glint = 0.75f + 0.25f * Mathf.Sin(time * 4f + k * 1.9f);
                FXSprites.SetFlat(sh, _shardSize[k] * pop * Mathf.Lerp(0.6f, 1f, fade), _shardAngle[k]);
                sh.color = FXSprites.A(Color.Lerp(Azure, Ice, 0.35f * glint), 0.9f * fade);
            }
        }
    }

    // ------------------------------------------------------------------------------------------------------------
    // Orbe 3D : bipyramide hexagonale (6 côtés, pointes haute et basse), normales plates par facette
    // ------------------------------------------------------------------------------------------------------------

    private static Mesh OrbMesh()
    {
        if (_orbMesh != null) return _orbMesh;
        const int sides = 6;
        const float radius = 0.42f, top = 0.5f, bottom = -0.5f;
        Vector3 up = new Vector3(0f, top, 0f), down = new Vector3(0f, bottom, 0f);
        Vector3[] ring = new Vector3[sides];
        for (int i = 0; i < sides; i++)
        {
            float a = i * Mathf.PI * 2f / sides;
            ring[i] = new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
        }

        var verts = new System.Collections.Generic.List<Vector3>();
        var tris = new System.Collections.Generic.List<int>();
        for (int i = 0; i < sides; i++)
        {
            Vector3 r0 = ring[i], r1 = ring[(i + 1) % sides];
            AddTri(verts, tris, up, r1, r0);
            AddTri(verts, tris, down, r0, r1);
        }
        Mesh m = new Mesh { name = "WakeOrb" };
        m.SetVertices(verts);
        m.SetTriangles(tris, 0);
        m.RecalculateNormals();   // sommets non partagés : une normale par facette
        m.RecalculateBounds();
        _orbMesh = m;
        return m;
    }

    private static void AddTri(System.Collections.Generic.List<Vector3> verts, System.Collections.Generic.List<int> tris, Vector3 a, Vector3 b, Vector3 c)
    {
        int s = verts.Count;
        verts.Add(a); verts.Add(b); verts.Add(c);
        tris.Add(s); tris.Add(s + 1); tris.Add(s + 2);
    }

    // Même shader (URP Lit) que les orbes des Orbitaux : cristal azur, émission ambrée (la boue qu'il entraîne).
    private static Material MakeOrbMaterial()
    {
        GameObject orb = Resources.Load<GameObject>("OrbitalProjectile");
        MeshRenderer src = orb != null ? orb.GetComponent<MeshRenderer>() : null;
        Material m = src != null && src.sharedMaterial != null ? new Material(src.sharedMaterial) : new Material(Shader.Find("Sprites/Default"));
        Color tint = Color.Lerp(Azure, Ice, 0.2f);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.9f);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0.2f);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", tint);
        m.color = tint;
        if (m.HasProperty("_EmissionColor"))
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", Color.Lerp(Azure, Amber, 0.45f) * 0.45f);
        }
        return m;
    }

    private void OnDestroy()
    {
        if (_orbMat != null) Destroy(_orbMat);
    }
}
