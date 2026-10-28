using UnityEngine;
using System.Collections.Generic;

// AJOUTÉ (2026-10-27, refonte de la fusion Orbitaux + Foudre en Constellation Orageuse, voir WeaponFusionThunderingOrbs) -
// visuel permanent de la fusion, piloté chaque image par l'arme (Render) :
//  1. Orbes d'orage : cristaux 3D à 8 facettes (octaèdre allongé, normales plates) qui flottent au-dessus du sol en
//     tournant sur eux-mêmes, avec une traînée or -> rose (Foudre -> Orbitaux) et un halo au sol. Pendant la charge de
//     Surcharge, ils grossissent, blanchissent et tournent plus vite ; de petits arcs les relient au sol en permanence
//     (émis par l'arme via LightningBoltVFX).
//  2. Ionisation : sous chaque ennemi ionisé, un halo doré et UN ÉCLAT DE CRISTAL PAR CHARGE qui tourne autour de lui
//     (1 à 3) : le joueur lit d'un coup d'œil qui est chargé et combien. Les éclats s'affolent pendant la charge de
//     Surcharge. Tout est dessiné dans 2 maillages dynamiques (halos, éclats), pas un objet par ennemi.
//  3. Surcharge : à chaque maillon de la constellation, une étoile de cristal jaillit puis s'éteint (pool fixe).
// Grammaire visuelle du projet : aucun anneau lisse ni gradué (réservés aux télégraphes ennemis) - uniquement des
// formes de cristal et des éclairs. Or électrique de la Foudre + rose des Orbitaux : distinct de l'or/bleu nuit de la
// Tourbière Magnétique (pas de voile sombre) et du turquoise de la Couronne Prismatique.
// Racine jamais enfant du joueur : posée chaque image au SOL (hauteur absolue), rotation monde nulle.
public class StormConstellationVisual : MonoBehaviour
{
    public static readonly Color Bolt = new Color(1f, 0.95f, 0.55f);     // même jaune que LightningChainUtility
    public static readonly Color Spark = new Color(1f, 0.9f, 0.6f);
    private static readonly Color Gold = new Color(1f, 0.84f, 0.3f);
    private static readonly Color Rose = new Color(0.95f, 0.55f, 0.75f);  // DamageNumberSpawner.ColorOrbital
    private static readonly Color WhiteHot = new Color(1f, 0.99f, 0.92f);

    public const float OrbY = 0.6f;
    private const float MarkLift = 0.35f;   // au-dessus de la tête : jamais caché par le corps de l'ennemi
    private const int MaxMarks = 256;
    private const int MaxStars = 96;
    private const float StarLife = 0.42f;

    // Ennemi ionisé tel que l'arme le transmet au rendu.
    public struct Mark
    {
        public Vector3 position;
        public float height;  // au-dessus du pivot de l'ennemi
        public int stacks;
        public float fade;    // 1 = pleine charge, -> 0 dans les dernières fractions de seconde avant expiration
        public int seed;
    }

    private int _max;
    private Transform[] _orbs;
    private SpriteRenderer[] _orbGlows;
    private TrailRenderer[] _trails;
    private Material _orbMat;
    private float _flash;

    private Mesh _glowMesh, _shardMesh;
    private Vector3[] _glowVerts, _shardVerts;
    private Color[] _glowColors, _shardColors;

    private SpriteRenderer[] _stars, _starGlows;
    private float[] _starAge, _starSize;
    private int _nextStar;

    private static Mesh _orbMesh;

    public void Build(int maxOrbs)
    {
        _max = Mathf.Max(1, maxOrbs);
        _orbs = new Transform[_max];
        _orbGlows = new SpriteRenderer[_max];
        _trails = new TrailRenderer[_max];
        _orbMat = MakeOrbMaterial();

        Material trailMat = new Material(Shader.Find("Sprites/Default"));
        for (int i = 0; i < _max; i++)
        {
            _orbGlows[i] = FXSprites.MakeRenderer(transform, "OrbGlow" + i, FXSprites.Glow(), 10, 0.05f);

            GameObject orb = new GameObject("StormOrb" + i);
            orb.transform.SetParent(transform, false);
            orb.AddComponent<MeshFilter>().sharedMesh = OrbMesh();
            MeshRenderer mr = orb.AddComponent<MeshRenderer>();
            mr.sharedMaterial = _orbMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            _orbs[i] = orb.transform;

            // Traînée sur un objet frère (pas enfant de l'orbe : elle ne doit pas hériter de sa rotation/échelle).
            TrailRenderer tr = new GameObject("StormTrail" + i).AddComponent<TrailRenderer>();
            tr.transform.SetParent(transform, false);
            tr.time = 0.22f;
            tr.minVertexDistance = 0.05f;
            tr.widthMultiplier = 0.42f;
            tr.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
            tr.sharedMaterial = trailMat;
            tr.startColor = FXSprites.A(Bolt, 0.75f);
            tr.endColor = FXSprites.A(Rose, 0f);
            tr.sortingOrder = 11;
            tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            tr.receiveShadows = false;
            tr.emitting = false;
            _trails[i] = tr;
        }

        _glowMesh = BuildQuadMesh("IonGlows", MaxMarks, FXSprites.Glow(), 8, out _glowVerts, out _glowColors);
        _shardMesh = BuildQuadMesh("IonShards", MaxMarks * 3, FXSprites.CrystalShard(), 9, out _shardVerts, out _shardColors);

        _stars = new SpriteRenderer[MaxStars];
        _starGlows = new SpriteRenderer[MaxStars];
        _starAge = new float[MaxStars];
        _starSize = new float[MaxStars];
        for (int i = 0; i < MaxStars; i++)
        {
            _starGlows[i] = FXSprites.MakeRenderer(transform, "StarGlow" + i, FXSprites.Glow(), 15, 0f);
            _stars[i] = FXSprites.MakeRenderer(transform, "Star" + i, FXSprites.CrystalShard(), 16, 0f);
            _starGlows[i].enabled = _stars[i].enabled = false;
            _starAge[i] = StarLife;
        }
    }

    // Sursaut de lumière des orbes au déclenchement de la Surcharge (s'éteint en ~0,3 s).
    public void FlashSurge() => _flash = 1f;

    // Étoile de cristal à un maillon de la constellation (position monde, au-dessus de la tête de l'ennemi). Plus grande
    // sur un maillon frappé du ciel (racine d'un groupe isolé).
    public void SpawnStar(Vector3 worldPos, bool big)
    {
        int i = _nextStar;
        _nextStar = (_nextStar + 1) % MaxStars;
        _starAge[i] = 0f;
        _starSize[i] = big ? 1.35f : 1f;
        Vector3 local = worldPos - transform.position;
        _stars[i].transform.localPosition = local + Vector3.up * 0.01f;
        _starGlows[i].transform.localPosition = local;
        _stars[i].enabled = _starGlows[i].enabled = true;
    }

    public void Render(Vector3 center, Vector3[] orbPositions, int count, float windup01, List<Mark> marks)
    {
        if (_orbs == null) return;
        count = Mathf.Clamp(count, 1, _max);
        transform.position = new Vector3(center.x, 0f, center.z);
        transform.rotation = Quaternion.identity;

        float time = Time.time;
        float dt = Time.deltaTime;
        _flash = Mathf.Max(0f, _flash - dt / 0.3f);
        float w = windup01 * windup01;
        float flicker = windup01 > 0f ? 0.5f + 0.5f * Mathf.Sin(time * Mathf.Lerp(10f, 45f, windup01)) : 0.5f;

        // Jamais blanc pur : un cristal surexposé se lit comme un carré blanc plat.
        Color orbTint = Color.Lerp(new Color(1f, 0.78f, 0.3f), WhiteHot, 0.65f * Mathf.Clamp01(w * (0.6f + 0.4f * flicker) + _flash));
        _orbMat.SetColor("_BaseColor", orbTint);
        _orbMat.color = orbTint;
        if (_orbMat.HasProperty("_EmissionColor")) _orbMat.SetColor("_EmissionColor", orbTint * (0.25f + 0.6f * w + 0.5f * _flash));

        for (int i = 0; i < _max; i++)
        {
            bool on = i < count;
            _orbs[i].gameObject.SetActive(on);
            _orbGlows[i].enabled = on;
            if (!on) { _trails[i].emitting = false; _trails[i].Clear(); continue; }

            Vector3 local = orbPositions[i] - transform.position;
            local.y = 0f;
            float bob = 0.07f * Mathf.Sin(time * 3.1f + i * 1.7f);
            float scale = 1.2f * (1f + 0.45f * w + 0.3f * _flash);

            Transform orb = _orbs[i];
            orb.localPosition = local + Vector3.up * (OrbY + bob);
            orb.localRotation = Quaternion.Euler(18f * Mathf.Sin(time * 1.3f + i), time * (140f + 260f * w) + i * 45f, 0f);
            orb.localScale = Vector3.one * scale;

            TrailRenderer tr = _trails[i];
            tr.transform.localPosition = local + Vector3.up * OrbY;
            if (!tr.emitting) { tr.Clear(); tr.emitting = true; }
            tr.widthMultiplier = 0.42f * (1f + 0.4f * w);

            SpriteRenderer glow = _orbGlows[i];
            glow.transform.localPosition = local + Vector3.up * 0.05f;
            FXSprites.SetFlat(glow, 2.2f * (1f + 0.7f * w + 0.5f * _flash) * (0.92f + 0.08f * flicker), 0f);
            glow.color = FXSprites.A(Color.Lerp(Color.Lerp(Gold, Rose, 0.35f), WhiteHot, w), 0.5f + 0.35f * w + 0.3f * _flash);
        }

        UpdateMarks(marks, windup01, time);
        UpdateStars(dt, time);
    }

    // ------------------------------------------------------------------------------------------------------------
    // Ionisation : halos et éclats tournants (maillages dynamiques, quads posés à plat)
    // ------------------------------------------------------------------------------------------------------------

    private void UpdateMarks(List<Mark> marks, float windup01, float time)
    {
        Vector3 origin = transform.position;
        int glowCount = 0, shardCount = 0;
        float spin = Mathf.Lerp(150f, 620f, windup01);
        float boost = 1f + 0.6f * windup01;

        for (int m = 0; m < marks.Count && glowCount < MaxMarks; m++)
        {
            Mark mk = marks[m];
            Vector3 p = new Vector3(mk.position.x - origin.x, mk.position.y + mk.height + MarkLift, mk.position.z - origin.z);
            int stacks = Mathf.Clamp(mk.stacks, 1, 3);
            float fade = Mathf.Clamp01(mk.fade);
            float phase = (mk.seed & 1023) * 0.37f;

            float glowSize = (1.1f + 0.3f * stacks) * boost;
            Color glowColor = FXSprites.A(Color.Lerp(Gold, WhiteHot, 0.5f * windup01), (0.22f + 0.1f * stacks) * fade * boost);
            SetQuad(_glowVerts, _glowColors, glowCount++, p, glowSize, 0f, glowColor);

            float orbitR = 0.62f;
            float baseAngle = (time * spin + phase * 57.3f) * Mathf.Deg2Rad;
            Color shardColor = FXSprites.A(Color.Lerp(Bolt, WhiteHot, 0.25f + 0.5f * windup01), 0.95f * fade);
            for (int s = 0; s < stacks; s++)
            {
                float a = baseAngle + s * (Mathf.PI * 2f / stacks);
                Vector3 sp = p + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * orbitR + Vector3.up * 0.02f;
                SetQuad(_shardVerts, _shardColors, shardCount++, sp, 0.7f * boost, a * Mathf.Rad2Deg * 2f, shardColor);
            }
        }

        ClearQuads(_glowVerts, _glowColors, glowCount, MaxMarks);
        ClearQuads(_shardVerts, _shardColors, shardCount, MaxMarks * 3);
        Push(_glowMesh, _glowVerts, _glowColors);
        Push(_shardMesh, _shardVerts, _shardColors);
    }

    private Mesh BuildQuadMesh(string objName, int quads, Sprite sprite, int sortingOrder, out Vector3[] verts, out Color[] colors)
    {
        GameObject g = new GameObject(objName);
        g.transform.SetParent(transform, false);
        Mesh mesh = new Mesh { name = objName };
        mesh.MarkDynamic();
        verts = new Vector3[quads * 4];
        colors = new Color[quads * 4];
        Vector2[] uv = new Vector2[quads * 4];
        int[] tris = new int[quads * 6];
        for (int q = 0; q < quads; q++)
        {
            int v = q * 4, t = q * 6;
            uv[v] = new Vector2(0f, 0f); uv[v + 1] = new Vector2(0f, 1f); uv[v + 2] = new Vector2(1f, 1f); uv[v + 3] = new Vector2(1f, 0f);
            tris[t] = v; tris[t + 1] = v + 1; tris[t + 2] = v + 2;
            tris[t + 3] = v; tris[t + 4] = v + 2; tris[t + 5] = v + 3;
        }
        mesh.vertices = verts;
        mesh.colors = colors;
        mesh.uv = uv;
        mesh.triangles = tris;
        g.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer mr = g.AddComponent<MeshRenderer>();
        mr.sharedMaterial = new Material(Shader.Find("Sprites/Default")) { mainTexture = sprite.texture };
        mr.sortingOrder = sortingOrder;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        return mesh;
    }

    // Quad à plat (plan XZ), côté "size", tourné de angleDeg autour de la verticale.
    private static void SetQuad(Vector3[] verts, Color[] colors, int q, Vector3 center, float size, float angleDeg, Color color)
    {
        float h = size * 0.5f;
        float a = angleDeg * Mathf.Deg2Rad;
        float cs = Mathf.Cos(a) * h, sn = Mathf.Sin(a) * h;
        Vector3 right = new Vector3(cs, 0f, sn), fwd = new Vector3(-sn, 0f, cs);
        int v = q * 4;
        verts[v] = center - right - fwd;
        verts[v + 1] = center - right + fwd;
        verts[v + 2] = center + right + fwd;
        verts[v + 3] = center + right - fwd;
        colors[v] = colors[v + 1] = colors[v + 2] = colors[v + 3] = color;
    }

    private static void ClearQuads(Vector3[] verts, Color[] colors, int from, int to)
    {
        for (int q = from; q < to; q++)
        {
            int v = q * 4;
            if (verts[v] == Vector3.zero && verts[v + 2] == Vector3.zero) break;   // déjà vidé lors d'une image précédente
            verts[v] = verts[v + 1] = verts[v + 2] = verts[v + 3] = Vector3.zero;
            colors[v] = colors[v + 1] = colors[v + 2] = colors[v + 3] = Color.clear;
        }
    }

    private static void Push(Mesh mesh, Vector3[] verts, Color[] colors)
    {
        mesh.vertices = verts;
        mesh.colors = colors;
        mesh.bounds = new Bounds(Vector3.zero, new Vector3(200f, 10f, 200f));   // marques sur tout l'écran : jamais coupées
    }

    // ------------------------------------------------------------------------------------------------------------
    // Étoiles de Surcharge
    // ------------------------------------------------------------------------------------------------------------

    private void UpdateStars(float dt, float time)
    {
        for (int i = 0; i < MaxStars; i++)
        {
            if (_starAge[i] >= StarLife) continue;
            _starAge[i] += dt;
            float t = _starAge[i] / StarLife;
            if (t >= 1f) { _stars[i].enabled = _starGlows[i].enabled = false; continue; }

            float grow = FXSprites.EaseOutBack(Mathf.Clamp01(t / 0.35f));
            float alpha = 1f - FXSprites.Smooth((t - 0.35f) / 0.65f);
            float size = _starSize[i];
            FXSprites.SetFlat(_stars[i], 2.4f * size * grow, time * 200f + i * 23f);
            _stars[i].color = FXSprites.A(Color.Lerp(WhiteHot, Bolt, t * 0.8f), alpha);
            FXSprites.SetFlat(_starGlows[i], 3.6f * size * grow, 0f);
            _starGlows[i].color = FXSprites.A(Gold, 0.7f * alpha);
        }
    }

    // ------------------------------------------------------------------------------------------------------------
    // Orbe 3D : octaèdre allongé (4 côtés, pointes haute et basse), normales plates par facette
    // ------------------------------------------------------------------------------------------------------------

    private static Mesh OrbMesh()
    {
        if (_orbMesh != null) return _orbMesh;
        const int sides = 4;
        const float radius = 0.36f, top = 0.62f, bottom = -0.62f;
        Vector3 up = new Vector3(0f, top, 0f), down = new Vector3(0f, bottom, 0f);
        Vector3[] ring = new Vector3[sides];
        for (int i = 0; i < sides; i++)
        {
            float a = i * Mathf.PI * 2f / sides;
            ring[i] = new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
        }

        var verts = new List<Vector3>();
        var tris = new List<int>();
        for (int i = 0; i < sides; i++)
        {
            Vector3 r0 = ring[i], r1 = ring[(i + 1) % sides];
            AddTri(verts, tris, up, r1, r0);
            AddTri(verts, tris, down, r0, r1);
        }
        Mesh m = new Mesh { name = "StormOrb" };
        m.SetVertices(verts);
        m.SetTriangles(tris, 0);
        m.RecalculateNormals();   // sommets non partagés : une normale par facette
        m.RecalculateBounds();
        _orbMesh = m;
        return m;
    }

    private static void AddTri(List<Vector3> verts, List<int> tris, Vector3 a, Vector3 b, Vector3 c)
    {
        int s = verts.Count;
        verts.Add(a); verts.Add(b); verts.Add(c);
        tris.Add(s); tris.Add(s + 1); tris.Add(s + 2);
    }

    // Même shader (URP Lit) que les orbes des Orbitaux, avec émission : l'orbe d'orage brille par lui-même. Copie de leur
    // matériau (jamais modifié lui-même), repli sur Sprites/Default si le prefab est introuvable.
    private static Material MakeOrbMaterial()
    {
        GameObject orb = Resources.Load<GameObject>("OrbitalProjectile");
        MeshRenderer src = orb != null ? orb.GetComponent<MeshRenderer>() : null;
        Material m = src != null && src.sharedMaterial != null ? new Material(src.sharedMaterial) : new Material(Shader.Find("Sprites/Default"));
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.9f);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0.15f);
        if (m.HasProperty("_EmissionColor")) m.EnableKeyword("_EMISSION");
        return m;
    }

    private void OnDestroy()
    {
        if (_orbMat != null) Destroy(_orbMat);
        if (_glowMesh != null) Destroy(_glowMesh);
        if (_shardMesh != null) Destroy(_shardMesh);
    }
}
