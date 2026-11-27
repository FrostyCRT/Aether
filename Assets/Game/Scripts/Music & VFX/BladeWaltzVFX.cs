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
//
// MODIFIÉ (2026-11-25, « Valse de lune », retours lot 1 groupe 3 : lueur des lames « trop simple », charge « violet
// éclaté moche ») - identité de Lyra (croissant de lune, bleu nuit, blanc de lune) :
//  1. Lames : Lame croissant à lame de cristal de lune (SM_LyraDaggerCrystal, Resources/FX/WaltzDagger).
//  2. Traînée accrochée à la POINTE de la lame (plus au centre) : avec la rotation, elle trace des arcs en croissant ;
//     blanc lunaire -> bleu glacier -> violet, plus longue et plus blanche au retour.
//  3. Charge : 5 croissants de lune en cristal 3D (Resources/FX/WaltzMoonShard) qui flottent à hauteur de taille,
//     bleu nuit translucides éteints, blanc lunaire allumés (« phases de lune »). Au Kaléidoscope, ils filent vers
//     l'extérieur avec un bref éclat blanc au sol (plus de halo violet).
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
    private Transform[] _moons;
    private MeshRenderer[] _moonRenderers;
    private MaterialPropertyBlock _mpb;
    private SpriteRenderer _burst;
    private float _burstAge = BurstLife;
    private float _shardSpin;
    private readonly float[] _shardLight = new float[WeaponFusionRicochetBlades.ChargeShards];

    // Croissants de la charge : éteints (bleu nuit translucide) -> allumés (blanc lunaire)
    private static readonly Color MoonIce = new Color(0.62f, 0.82f, 1f);
    private static readonly Color MoonViolet = new Color(0.55f, 0.42f, 1f);
    private static readonly Color OffA = new Color(0.18f, 0.22f, 0.48f), OffB = new Color(0.26f, 0.2f, 0.48f), OffDeep = new Color(0.06f, 0.07f, 0.18f);
    private static readonly Color OnA = new Color(0.9f, 0.95f, 1f), OnB = new Color(0.8f, 0.83f, 1f), OnDeep = new Color(0.45f, 0.55f, 0.85f);
    private const float MoonHeight = 0.9f;
    private const float MoonScale = 1.6f;
    private static readonly int IdColorA = Shader.PropertyToID("_ColorA"), IdColorB = Shader.PropertyToID("_ColorB"),
        IdDeep = Shader.PropertyToID("_DeepColor"), IdGlow = Shader.PropertyToID("_Glow"), IdAlpha = Shader.PropertyToID("_Alpha"),
        IdEdge = Shader.PropertyToID("_EdgeOpacity");
    // Pointe de la lame dans le repère de WaltzDagger (enfant « Dagger » à z = 0,16, pointe du maillage à z = -0,62)
    private static readonly Vector3 TipLocal = new Vector3(0f, 0f, -0.46f);

    private static Mesh _bladeMesh;
    // AJOUTÉ (2026-11-23, retour utilisateur : « mettre le modèle 3D des couteaux sur cette fusion, en le modifiant un peu
    // pour avoir le style des orbes rebondissants ») - si Resources/FX/WaltzDagger existe : la dague de Lyra à lame de
    // cristal (matériau de l'Orbe rebondissant), garde et poignée en métal peint. Le palier de rebond se lit alors à la
    // taille et à la traînée (les matériaux de la dague ne changent pas). Repli : l'ancien losange de cristal procédural.
    private bool _daggerMode;
    private const float DaggerScale = 2.16f;   // même taille qu'un Couteau de Lyra de base (taille 1)

    public void Build(int maxBlades)
    {
        _blades = new Transform[maxBlades];
        _bladeRenderers = new MeshRenderer[maxBlades];
        _trails = new TrailRenderer[maxBlades];
        _stage = new int[maxBlades];

        _stageMats = new Material[Stages];
        for (int s = 0; s < Stages; s++) _stageMats[s] = MakeBladeMaterial(StageColor(s), s == Stages - 1 ? 0.55f : 0.3f + 0.08f * s);

        _bladeRoot = new GameObject("BladeWaltzBlades").transform;
        Material trailMat = Resources.Load<Material>("FX/M_FX_SoftTrail");
        if (trailMat == null) trailMat = new Material(Shader.Find("Sprites/Default"));
        GameObject daggerPrefab = Resources.Load<GameObject>("FX/WaltzDagger");
        _daggerMode = daggerPrefab != null;
        for (int i = 0; i < maxBlades; i++)
        {
            GameObject g;
            MeshRenderer mr;
            if (_daggerMode)
            {
                g = Instantiate(daggerPrefab, _bladeRoot, false);
                g.name = "WaltzBlade" + i;
                mr = g.GetComponentInChildren<MeshRenderer>(true);
            }
            else
            {
                g = new GameObject("WaltzBlade" + i);
                g.transform.SetParent(_bladeRoot, false);
                g.AddComponent<MeshFilter>().sharedMesh = BladeMesh();
                mr = g.AddComponent<MeshRenderer>();
                mr.sharedMaterial = _stageMats[0];
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
            }
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
        _moons = new Transform[n];
        _moonRenderers = new MeshRenderer[n];
        _mpb = new MaterialPropertyBlock();
        GameObject moonPrefab = Resources.Load<GameObject>("FX/WaltzMoonShard");
        for (int i = 0; i < n; i++)
        {
            GameObject m = moonPrefab != null ? Instantiate(moonPrefab, transform, false) : new GameObject();
            m.name = "ChargeMoon" + i;
            _moons[i] = m.transform;
            _moonRenderers[i] = m.GetComponent<MeshRenderer>();
        }
        if (moonPrefab == null) Debug.LogWarning("BladeWaltzVisual : Resources/FX/WaltzMoonShard introuvable, charge invisible");

        _burst = FXSprites.MakeRenderer(transform, "KaleidoscopeStar", FXSprites.CrystalShard(), 15, 0.1f);
        _burst.enabled = false;
    }

    private static Gradient TrailGradient(bool back)
    {
        var g = new Gradient();
        // (2026-11-25, retour : « on voit beaucoup les lueurs de la traînée blanche ») - moins opaque ; les bords du ruban
        // sont maintenant fondus par la texture de M_FX_SoftTrail (T_TrailSoftEdge).
        g.SetKeys(new[] { new GradientColorKey(WhiteHot, 0f), new GradientColorKey(back ? Color.Lerp(MoonIce, WhiteHot, 0.4f) : MoonIce, 0.35f), new GradientColorKey(MoonViolet, 1f) },
                  new[] { new GradientAlphaKey(back ? 0.65f : 0.55f, 0f), new GradientAlphaKey(0.32f, 0.4f), new GradientAlphaKey(0f, 1f) });
        return g;
    }

    private Vector3 TrailPoint(Transform blade, Vector3 center) =>
        _daggerMode ? center + blade.rotation * (TipLocal * blade.localScale.x) : center;

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
        tr.emitting = false;   // allumée au 1er SetBlade, une fois placée à la pointe (sinon trait centre -> pointe)
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
        t.localScale = Vector3.one * ((_daggerMode ? DaggerScale : 1.25f) * size);
        _trails[slot].transform.position = TrailPoint(t, p);
        if (_stage[slot] < 0) { _trails[slot].Clear(); _trails[slot].emitting = true; }

        stage = Mathf.Clamp(stage, 0, Stages - 1);
        if (_stage[slot] == stage) return;
        _stage[slot] = stage;
        if (!_daggerMode) _bladeRenderers[slot].sharedMaterial = _stageMats[stage];

        TrailRenderer tr = _trails[slot];
        bool back = stage == Stages - 1;
        tr.time = back ? 0.22f : 0.15f;
        tr.widthMultiplier = 0.26f * size;   // bords fondus : un peu plus large pour garder la même présence au cœur
        tr.colorGradient = TrailGradient(back);
    }

    public void Burst(Vector3 center)
    {
        _burstAge = 0f;
        transform.position = new Vector3(center.x, 0f, center.z);
        _burst.enabled = true;
    }

    public void Render(Vector3 center, int litShards, bool ready)
    {
        transform.position = new Vector3(center.x, 0f, center.z);
        transform.rotation = Quaternion.identity;
        float time = Time.time;
        float dt = Time.deltaTime;

        // Kaléidoscope en cours : les croissants filent vers l'extérieur, allumés, puis reviennent éteints
        bool flying = _burstAge < BurstLife;
        float bt = flying ? _burstAge / BurstLife : 1f;

        _shardSpin = Mathf.Repeat(_shardSpin + (ready ? 120f : 35f) * dt, 360f);
        float pulse = ready ? 0.5f + 0.5f * Mathf.Sin(time * 6f) : 0f;
        int n = _moons.Length;
        for (int i = 0; i < n; i++)
        {
            bool on = i < litShards || ready;
            _shardLight[i] = Mathf.MoveTowards(_shardLight[i], on ? 1f : 0f, dt / 0.15f);
            float l = flying ? 1f - bt : _shardLight[i];

            float a = (_shardSpin + i * 360f / n) * Mathf.Deg2Rad;
            Vector3 outward = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            float radius = ShardRadius + (flying ? 5f * FXSprites.Smooth(bt) : 0f);
            float bob = Mathf.Sin(time * 2.2f + i * 1.3f) * 0.06f;
            _moons[i].localPosition = outward * radius + Vector3.up * (MoonHeight + bob);
            // cornes tournées vers l'extérieur (pointe des cornes = -Z du maillage)
            _moons[i].localRotation = Quaternion.LookRotation(-outward, Vector3.up);
            float scale = MoonScale * (flying ? 1f - 0.6f * bt : 0.8f + 0.2f * l + 0.08f * pulse);
            _moons[i].localScale = Vector3.one * scale;

            MeshRenderer r = _moonRenderers[i];
            if (r == null) continue;
            r.GetPropertyBlock(_mpb);
            _mpb.SetColor(IdColorA, Color.Lerp(OffA, OnA, l));
            _mpb.SetColor(IdColorB, Color.Lerp(OffB, OnB, l));
            _mpb.SetColor(IdDeep, Color.Lerp(OffDeep, OnDeep, l));
            _mpb.SetFloat(IdGlow, Mathf.Lerp(0.8f, 1.25f, l) + 0.2f * pulse);
            _mpb.SetFloat(IdAlpha, Mathf.Lerp(0.35f, 0.8f, l));
            _mpb.SetFloat(IdEdge, Mathf.Lerp(0.35f, 0.9f, l));
            r.SetPropertyBlock(_mpb);
        }

        if (flying)
        {
            _burstAge += dt;
            float t = _burstAge / BurstLife;
            if (t >= 1f) _burst.enabled = false;
            else
            {
                // bref éclat blanc lunaire au sol
                float grow = FXSprites.EaseOutBack(Mathf.Clamp01(t / 0.3f));
                float alpha = 1f - FXSprites.Smooth((t - 0.2f) / 0.8f);
                FXSprites.SetFlat(_burst, 3.2f * grow, time * 160f);
                _burst.color = FXSprites.A(Color.Lerp(WhiteHot, MoonIce, t), 0.85f * alpha);
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
