using UnityEngine;

// AJOUTÉ (2026-10-28, refonte de la fusion Couteaux + Orbe rebondissant en Valse des Lames, voir
// WeaponFusionRicochetBlades) - visuel de la fusion, piloté chaque image par l'arme :
//  1. Lames : cristaux 3D en losange allongé (normales plates) qui tournent à plat comme un boomerang, avec une traînée
//     magenta -> azur (Couteaux -> Orbe). Chaque rebond les agrandit et les éclaircit vers l'azur ; au retour elles
//     deviennent presque blanches et leur traînée s'allonge : le joueur voit lesquelles reviennent vers lui.
//  2. Charge du Kaléidoscope : 5 éclats de cristal posés au sol autour du joueur, qui s'allument un par un à mesure que
//     les lames sont rattrapées, puis pulsent ensemble quand la gerbe est prête.
//  3. Kaléidoscope : une étoile de cristal jaillit sous le joueur au départ de la gerbe.
// Grammaire visuelle du projet : aucun anneau lisse ni gradué (réservés aux télégraphes ennemis), uniquement du cristal.
// Racine jamais enfant du joueur, rotation monde nulle.
public class BladeWaltzVisual : MonoBehaviour
{
    public static readonly Color Magenta = new Color(0.82f, 0.22f, 0.9f);   // DamageNumberSpawner.ColorKnives
    public static readonly Color Azure = new Color(0.15f, 0.55f, 0.95f);    // DamageNumberSpawner.ColorBouncingOrb
    public static readonly Color Spark = new Color(0.8f, 0.7f, 1f);
    private static readonly Color WhiteHot = new Color(0.95f, 0.97f, 1f);

    public const float BladeY = 0.7f;
    private const int Stages = 4;   // 0, 1, 2 rebonds, puis retour
    private const float BurstLife = 0.5f;
    private const float ShardRadius = 1.9f;

    private Transform _bladeRoot;   // fixe à l'origine : lames et traînées en coordonnées monde, jamais traînées par le joueur
    private Transform[] _blades;
    private MeshRenderer[] _bladeRenderers;
    private TrailRenderer[] _trails;
    private int[] _stage;
    private Material[] _stageMats;
    private SpriteRenderer[] _shards, _shardGlows;
    private SpriteRenderer _burst, _burstGlow;
    private float _burstAge = BurstLife;
    private float _shardSpin;
    private readonly float[] _shardLight = new float[WeaponFusionRicochetBlades.ChargeShards];

    private static Mesh _bladeMesh;

    public void Build(int maxBlades)
    {
        _blades = new Transform[maxBlades];
        _bladeRenderers = new MeshRenderer[maxBlades];
        _trails = new TrailRenderer[maxBlades];
        _stage = new int[maxBlades];

        _stageMats = new Material[Stages];
        for (int s = 0; s < Stages; s++) _stageMats[s] = MakeBladeMaterial(StageColor(s), s == Stages - 1 ? 0.55f : 0.3f + 0.08f * s);

        _bladeRoot = new GameObject("BladeWaltzBlades").transform;
        Material trailMat = new Material(Shader.Find("Sprites/Default"));
        for (int i = 0; i < maxBlades; i++)
        {
            GameObject g = new GameObject("WaltzBlade" + i);
            g.transform.SetParent(_bladeRoot, false);
            g.AddComponent<MeshFilter>().sharedMesh = BladeMesh();
            MeshRenderer mr = g.AddComponent<MeshRenderer>();
            mr.sharedMaterial = _stageMats[0];
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            g.SetActive(false);
            _blades[i] = g.transform;
            _bladeRenderers[i] = mr;

            // Traînée sur un objet frère (pas enfant de la lame : elle ne doit pas hériter de sa rotation/échelle).
            TrailRenderer tr = new GameObject("WaltzTrail" + i).AddComponent<TrailRenderer>();
            tr.transform.SetParent(_bladeRoot, false);
            tr.minVertexDistance = 0.05f;
            tr.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
            tr.sharedMaterial = trailMat;
            tr.sortingOrder = 11;
            tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            tr.receiveShadows = false;
            tr.emitting = false;
            _trails[i] = tr;
        }

        int n = WeaponFusionRicochetBlades.ChargeShards;
        _shards = new SpriteRenderer[n];
        _shardGlows = new SpriteRenderer[n];
        for (int i = 0; i < n; i++)
        {
            _shardGlows[i] = FXSprites.MakeRenderer(transform, "ChargeGlow" + i, FXSprites.Glow(), 12, 0.15f);
            _shards[i] = FXSprites.MakeRenderer(transform, "ChargeShard" + i, FXSprites.CrystalShard(), 13, 0.18f);
        }

        _burstGlow = FXSprites.MakeRenderer(transform, "KaleidoscopeGlow", FXSprites.Glow(), 14, 0.08f);
        _burst = FXSprites.MakeRenderer(transform, "KaleidoscopeStar", FXSprites.CrystalShard(), 15, 0.1f);
        _burst.enabled = _burstGlow.enabled = false;
    }

    private static Color StageColor(int stage)
    {
        switch (stage)
        {
            case 0: return Color.Lerp(Magenta, Azure, 0.25f);
            case 1: return Color.Lerp(Magenta, Azure, 0.6f);
            case 2: return Color.Lerp(Azure, WhiteHot, 0.25f);
            default: return Color.Lerp(Azure, WhiteHot, 0.45f);
        }
    }

    public void Activate(int slot, Vector3 groundPos)
    {
        Vector3 p = groundPos + Vector3.up * BladeY;
        _blades[slot].position = p;
        _blades[slot].gameObject.SetActive(true);
        _stage[slot] = -1;
        TrailRenderer tr = _trails[slot];
        tr.transform.position = p;
        tr.Clear();
        tr.emitting = true;
    }

    // La traînée continue de s'estomper seule : seule la lame disparaît.
    public void Deactivate(int slot)
    {
        _blades[slot].gameObject.SetActive(false);
        _trails[slot].emitting = false;
    }

    public void SetBlade(int slot, Vector3 groundPos, float size, int stage, float spinDeg)
    {
        Vector3 p = groundPos + Vector3.up * BladeY;
        Transform t = _blades[slot];
        t.position = p;
        t.rotation = Quaternion.Euler(0f, spinDeg, 0f);
        t.localScale = Vector3.one * (1.25f * size);
        _trails[slot].transform.position = p;

        stage = Mathf.Clamp(stage, 0, Stages - 1);
        if (_stage[slot] == stage) return;
        _stage[slot] = stage;
        _bladeRenderers[slot].sharedMaterial = _stageMats[stage];

        TrailRenderer tr = _trails[slot];
        bool back = stage == Stages - 1;
        tr.time = back ? 0.26f : 0.18f;
        tr.widthMultiplier = 0.36f * size;
        tr.startColor = FXSprites.A(back ? Color.Lerp(Azure, WhiteHot, 0.5f) : Color.Lerp(Magenta, Azure, stage * 0.35f), back ? 0.7f : 0.8f);
        tr.endColor = FXSprites.A(back ? Azure : Magenta, 0f);
    }

    public void Burst(Vector3 center)
    {
        _burstAge = 0f;
        transform.position = new Vector3(center.x, 0f, center.z);
        _burst.enabled = _burstGlow.enabled = true;
    }

    public void Render(Vector3 center, int litShards, bool ready)
    {
        transform.position = new Vector3(center.x, 0f, center.z);
        transform.rotation = Quaternion.identity;
        float time = Time.time;
        float dt = Time.deltaTime;

        _shardSpin = Mathf.Repeat(_shardSpin + (ready ? 160f : 40f) * dt, 360f);
        float pulse = ready ? 0.5f + 0.5f * Mathf.Sin(time * 9f) : 0f;
        int n = _shards.Length;
        for (int i = 0; i < n; i++)
        {
            bool on = i < litShards || ready;
            _shardLight[i] = Mathf.MoveTowards(_shardLight[i], on ? 1f : 0f, dt / 0.15f);
            float l = _shardLight[i];

            float a = (_shardSpin + i * 360f / n) * Mathf.Deg2Rad;
            Vector3 local = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * ShardRadius;
            _shards[i].transform.localPosition = local + Vector3.up * 0.18f;
            _shardGlows[i].transform.localPosition = local + Vector3.up * 0.15f;

            float size = 1.3f + 0.5f * l + 0.3f * pulse;
            FXSprites.SetFlat(_shards[i], size, -a * Mathf.Rad2Deg + 90f);
            _shards[i].color = FXSprites.A(Color.Lerp(Color.Lerp(Magenta, Azure, i / (float)(n - 1)), WhiteHot, 0.3f * l + 0.4f * pulse), 0.3f + 0.7f * l);
            FXSprites.SetFlat(_shardGlows[i], 2.2f + 0.6f * pulse, 0f);
            _shardGlows[i].color = FXSprites.A(Color.Lerp(Magenta, Azure, 0.5f), (0.5f + 0.3f * pulse) * l);
        }

        if (_burstAge < BurstLife)
        {
            _burstAge += dt;
            float t = _burstAge / BurstLife;
            if (t >= 1f) _burst.enabled = _burstGlow.enabled = false;
            else
            {
                float grow = FXSprites.EaseOutBack(Mathf.Clamp01(t / 0.35f));
                float alpha = 1f - FXSprites.Smooth((t - 0.35f) / 0.65f);
                FXSprites.SetFlat(_burst, 4.2f * grow, time * 220f);
                _burst.color = FXSprites.A(Color.Lerp(WhiteHot, Azure, t), alpha);
                FXSprites.SetFlat(_burstGlow, 6.5f * grow, 0f);
                _burstGlow.color = FXSprites.A(Color.Lerp(Magenta, Azure, 0.5f), 0.7f * alpha);
            }
        }
    }

    // ------------------------------------------------------------------------------------------------------------
    // Lame 3D : losange allongé (pointe avant longue, talon court, arête haute et basse), normales plates par facette
    // ------------------------------------------------------------------------------------------------------------

    private static Mesh BladeMesh()
    {
        if (_bladeMesh != null) return _bladeMesh;
        Vector3 tip = new Vector3(0f, 0f, 0.62f), heel = new Vector3(0f, 0f, -0.62f);
        Vector3 left = new Vector3(-0.17f, 0f, 0.08f), right = new Vector3(0.17f, 0f, 0.08f);
        Vector3 top = new Vector3(0f, 0.09f, 0.08f), bottom = new Vector3(0f, -0.09f, 0.08f);
        Vector3[] ring = { tip, right, heel, left };   // contour du losange, vu de dessus

        var verts = new System.Collections.Generic.List<Vector3>();
        var tris = new System.Collections.Generic.List<int>();
        for (int i = 0; i < 4; i++)
        {
            Vector3 r0 = ring[i], r1 = ring[(i + 1) % 4];
            AddTri(verts, tris, top, r0, r1);
            AddTri(verts, tris, bottom, r1, r0);
        }
        Mesh m = new Mesh { name = "WaltzBlade" };
        m.SetVertices(verts);
        m.SetTriangles(tris, 0);
        m.RecalculateNormals();   // sommets non partagés : une normale par facette
        m.RecalculateBounds();
        _bladeMesh = m;
        return m;
    }

    private static void AddTri(System.Collections.Generic.List<Vector3> verts, System.Collections.Generic.List<int> tris, Vector3 a, Vector3 b, Vector3 c)
    {
        int s = verts.Count;
        verts.Add(a); verts.Add(b); verts.Add(c);
        tris.Add(s); tris.Add(s + 1); tris.Add(s + 2);
    }

    // Même shader (URP Lit) que les orbes des Orbitaux, avec émission : la lame brille par elle-même. Copie de leur
    // matériau (jamais modifié lui-même), repli sur Sprites/Default si le prefab est introuvable.
    private static Material MakeBladeMaterial(Color tint, float emission)
    {
        GameObject orb = Resources.Load<GameObject>("OrbitalProjectile");
        MeshRenderer src = orb != null ? orb.GetComponent<MeshRenderer>() : null;
        Material m = src != null && src.sharedMaterial != null ? new Material(src.sharedMaterial) : new Material(Shader.Find("Sprites/Default"));
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.9f);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0.2f);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", tint);
        m.color = tint;
        if (m.HasProperty("_EmissionColor"))
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", tint * emission);
        }
        return m;
    }

    private void OnDestroy()
    {
        if (_bladeRoot != null) Destroy(_bladeRoot.gameObject);
        if (_stageMats != null)
            for (int i = 0; i < _stageMats.Length; i++) if (_stageMats[i] != null) Destroy(_stageMats[i]);
    }
}
