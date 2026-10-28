using UnityEngine;

// AJOUTÉ (2026-10-27, refonte de la fusion Aura + Orbitaux en Couronne Prismatique, voir WeaponFusionManaVortex) -
// visuel permanent de la couronne, piloté chaque image par l'arme (Render) :
//  1. Cristaux : vrais cristaux 3D taillés (bipyramide à 6 faces, normales plates : chaque facette prend la lumière
//     différemment), qui tournent sur eux-mêmes et flottent juste au-dessus du sol, avec un halo au sol.
//  2. Rayons : chaque paire de cristaux voisins est reliée par un faisceau (cœur blanc + halo turquoise) qui ondule
//     légèrement : la couronne se lit comme un polygone de lumière, jamais comme un cercle.
//  3. Champ : l'intérieur du polygone est un éventail de facettes (deux teintes alternées, plus claires au centre) dont
//     l'intensité suit la DENSITÉ du champ - resserrer la couronne la fait visiblement briller plus fort.
//  4. Charge puis Réfraction : les cristaux grossissent et blanchissent, puis chacun projette un faisceau vers
//     l'extérieur avec un éclat de cristal à sa pointe.
// Grammaire visuelle du projet : aucun anneau lisse ni gradué (réservés aux télégraphes ennemis) - uniquement des
// formes de cristal et des segments droits. Turquoise prismatique (la couleur des dégâts de cette fusion) et blanc :
// distinct du vert de l'Aura, du rubis de Fragmentation, de l'or de la Tourbière et de l'orange de la Boule de feu.
// Racine jamais enfant du joueur : posée chaque image au SOL (hauteur absolue), rotation monde nulle.
public class PrismaticCrownVisual : MonoBehaviour
{
    public static readonly Color Prism = new Color(0.35f, 0.95f, 0.85f);
    public static readonly Color WhiteHot = new Color(0.92f, 1f, 0.98f);
    private static readonly Color DeepTeal = new Color(0.05f, 0.42f, 0.42f);

    private const float BeamY = 0.15f;
    private const float FillY = 0.03f;
    private const float CrystalY = 0.6f;
    private const float LaserY = 0.3f;

    private int _max;
    private Transform[] _crystals;
    private Material _crystalMat;
    private SpriteRenderer[] _crystalGlows;
    private LineRenderer[] _rimGlow, _rimCore;
    private LineRenderer[] _laserGlow, _laserCore;
    private SpriteRenderer[] _laserTips, _laserTipGlows;
    private Mesh _fillMesh;
    private Vector3[] _fillVerts;
    private Color[] _fillColors;
    private float _flash;

    private static Material _beamMat;
    private static Mesh _crystalMesh;

    public void Build(int maxCrystals)
    {
        _max = Mathf.Max(3, maxCrystals);

        _crystals = new Transform[_max];
        _crystalGlows = new SpriteRenderer[_max];
        _rimGlow = new LineRenderer[_max];
        _rimCore = new LineRenderer[_max];
        _laserGlow = new LineRenderer[_max];
        _laserCore = new LineRenderer[_max];
        _laserTips = new SpriteRenderer[_max];
        _laserTipGlows = new SpriteRenderer[_max];

        _crystalMat = MakeCrystalMaterial();
        BuildFill();

        for (int i = 0; i < _max; i++)
        {
            _crystalGlows[i] = FXSprites.MakeRenderer(transform, "CrystalGlow" + i, FXSprites.Glow(), 10, 0.06f);

            GameObject crystal = new GameObject("Crystal" + i);
            crystal.transform.SetParent(transform, false);
            crystal.AddComponent<MeshFilter>().sharedMesh = CrystalMesh();
            MeshRenderer mr = crystal.AddComponent<MeshRenderer>();
            mr.sharedMaterial = _crystalMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            _crystals[i] = crystal.transform;

            _rimGlow[i] = MakeBeam("RimGlow" + i, 11);
            _rimCore[i] = MakeBeam("RimCore" + i, 12);
            _laserGlow[i] = MakeBeam("LaserGlow" + i, 13);
            _laserCore[i] = MakeBeam("LaserCore" + i, 14);
            _laserTipGlows[i] = FXSprites.MakeRenderer(transform, "LaserTipGlow" + i, FXSprites.Glow(), 15, LaserY);
            _laserTips[i] = FXSprites.MakeRenderer(transform, "LaserTip" + i, FXSprites.CrystalShard(), 16, LaserY + 0.01f);
        }
    }

    // Petit sursaut de lumière au déclenchement de la Réfraction (s'éteint en ~0,3 s).
    public void FlashRefraction() => _flash = 1f;

    public void Render(Vector3 center, float radius, float angleDeg, int count, float density01, float charge01, float laserLength, float laserAlpha)
    {
        if (_crystals == null) return;
        count = Mathf.Clamp(count, 3, _max);
        transform.position = new Vector3(center.x, 0f, center.z);
        transform.rotation = Quaternion.identity;

        float time = Time.time;
        _flash = Mathf.Max(0f, _flash - Time.deltaTime / 0.3f);
        float step = 360f / count;
        float chargeEase = charge01 * charge01;

        // Teinte des cristaux : turquoise -> blanc pendant la charge, avec un scintillement qui accélère.
        float flicker = charge01 > 0f ? 0.5f + 0.5f * Mathf.Sin(time * Mathf.Lerp(8f, 40f, charge01)) : 0.5f;
        Color crystalTint = Color.Lerp(new Color(0.5f, 1f, 0.92f), WhiteHot, Mathf.Clamp01(chargeEase * (0.6f + 0.4f * flicker) + _flash));
        _crystalMat.SetColor("_BaseColor", crystalTint);
        _crystalMat.color = crystalTint;

        for (int i = 0; i < _max; i++)
        {
            bool on = i < count;
            _crystals[i].gameObject.SetActive(on);
            _crystalGlows[i].enabled = on;
            _rimGlow[i].enabled = on;
            _rimCore[i].enabled = on;
            bool laserOn = on && laserAlpha > 0f && laserLength > 0.05f;
            _laserGlow[i].enabled = laserOn;
            _laserCore[i].enabled = laserOn;
            _laserTips[i].enabled = laserOn;
            _laserTipGlows[i].enabled = laserOn;
            if (!on) continue;

            float a = (angleDeg + step * i) * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            Vector3 local = dir * radius;
            float nextA = (angleDeg + step * ((i + 1) % count)) * Mathf.Deg2Rad;
            Vector3 nextLocal = new Vector3(Mathf.Cos(nextA), 0f, Mathf.Sin(nextA)) * radius;

            // Cristal : flotte, tourne sur lui-même, grossit pendant la charge.
            float bob = 0.08f * Mathf.Sin(time * 2.4f + i * 1.3f);
            float scale = 1f + 0.45f * chargeEase + 0.25f * _flash;
            Transform cr = _crystals[i];
            cr.localPosition = local + Vector3.up * (CrystalY + bob);
            cr.localRotation = Quaternion.Euler(0f, time * 90f + i * 37f, 0f);
            cr.localScale = Vector3.one * scale;

            SpriteRenderer glow = _crystalGlows[i];
            glow.transform.localPosition = local + Vector3.up * 0.06f;
            FXSprites.SetFlat(glow, 2.6f * (1f + 0.8f * chargeEase + 0.6f * _flash), 0f);
            glow.color = FXSprites.A(Color.Lerp(Prism, WhiteHot, chargeEase), 0.5f + 0.35f * chargeEase + 0.3f * _flash);

            // Rayon vers le cristal suivant : ondulation de largeur décalée par arête, blanchit pendant la charge.
            float shimmer = 1f + 0.12f * Mathf.Sin(time * 7f + i * 2.1f);
            Vector3 p0 = local + Vector3.up * BeamY;
            Vector3 p1 = nextLocal + Vector3.up * BeamY;
            SetBeam(_rimGlow[i], p0, p1, 0.75f * shimmer * (1f + 0.3f * chargeEase), FXSprites.A(Prism, 0.42f + 0.25f * density01));
            SetBeam(_rimCore[i], p0, p1, 0.16f * shimmer, FXSprites.A(Color.Lerp(new Color(0.75f, 1f, 0.97f), WhiteHot, chargeEase), 0.95f));

            if (!laserOn) continue;

            // Faisceau de Réfraction, du cristal vers l'extérieur.
            float laserFlicker = 1f + 0.15f * Mathf.Sin(time * 31f + i * 1.7f);
            Vector3 l0 = local + Vector3.up * LaserY;
            Vector3 l1 = local + dir * laserLength + Vector3.up * LaserY;
            SetBeam(_laserGlow[i], l0, l1, 1.15f * laserFlicker, FXSprites.A(Prism, 0.55f * laserAlpha));
            SetBeam(_laserCore[i], l0, l1, 0.28f * laserFlicker, FXSprites.A(WhiteHot, laserAlpha));

            Vector3 tip = l1 + Vector3.up * 0.01f;
            _laserTips[i].transform.localPosition = tip;
            FXSprites.SetFlat(_laserTips[i], 1.3f * laserFlicker, time * 260f + i * 40f);
            _laserTips[i].color = FXSprites.A(Color.Lerp(Prism, WhiteHot, 0.6f), laserAlpha);
            _laserTipGlows[i].transform.localPosition = tip - Vector3.up * 0.01f;
            FXSprites.SetFlat(_laserTipGlows[i], 2.4f, 0f);
            _laserTipGlows[i].color = FXSprites.A(Prism, 0.5f * laserAlpha);
        }

        UpdateFill(radius, angleDeg, count, density01, chargeEase);
    }

    // ------------------------------------------------------------------------------------------------------------
    // Champ : éventail de facettes (un triangle par arête, sommets séparés pour des couleurs franches par facette)
    // ------------------------------------------------------------------------------------------------------------

    private void BuildFill()
    {
        GameObject fill = new GameObject("CrownField");
        fill.transform.SetParent(transform, false);
        _fillMesh = new Mesh { name = "CrownField" };
        _fillMesh.MarkDynamic();
        _fillVerts = new Vector3[_max * 3];
        _fillColors = new Color[_max * 3];
        int[] tris = new int[_max * 3];
        for (int i = 0; i < tris.Length; i++) tris[i] = i;
        _fillMesh.vertices = _fillVerts;
        _fillMesh.colors = _fillColors;
        _fillMesh.triangles = tris;
        fill.AddComponent<MeshFilter>().sharedMesh = _fillMesh;
        MeshRenderer mr = fill.AddComponent<MeshRenderer>();
        mr.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
        mr.sortingOrder = 9;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
    }

    private void UpdateFill(float radius, float angleDeg, int count, float density01, float chargeEase)
    {
        float step = 360f / count;
        float strength = (0.7f + 0.6f * density01) * (1f + 0.5f * chargeEase + 0.6f * _flash);
        float centerAlpha = 0.26f * strength;
        float edgeAlpha = 0.1f * strength;
        Vector3 c = Vector3.up * FillY;

        for (int i = 0; i < _max; i++)
        {
            int v = i * 3;
            if (i >= count)
            {
                _fillVerts[v] = _fillVerts[v + 1] = _fillVerts[v + 2] = c;
                _fillColors[v] = _fillColors[v + 1] = _fillColors[v + 2] = Color.clear;
                continue;
            }
            float a0 = (angleDeg + step * i) * Mathf.Deg2Rad;
            float a1 = (angleDeg + step * (i + 1)) * Mathf.Deg2Rad;
            // Ordre (centre, a1, a0) : face tournée vers le haut (le shader ne coupe aucune face, mais autant être juste).
            _fillVerts[v] = c;
            _fillVerts[v + 1] = c + new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * radius;
            _fillVerts[v + 2] = c + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * radius;

            // Facettes alternées : une claire (prisme), une sombre (sarcelle) - lecture "pierre taillée vue de dessus".
            Color facet = (i % 2 == 0) ? Prism : Color.Lerp(Prism, DeepTeal, 0.7f);
            _fillColors[v] = FXSprites.A(Color.Lerp(facet, WhiteHot, 0.35f), centerAlpha);
            _fillColors[v + 1] = FXSprites.A(facet, edgeAlpha);
            _fillColors[v + 2] = FXSprites.A(facet, edgeAlpha);
        }
        _fillMesh.vertices = _fillVerts;
        _fillMesh.colors = _fillColors;
        _fillMesh.RecalculateBounds();
    }

    // ------------------------------------------------------------------------------------------------------------
    // Faisceaux (LineRenderer + texture au profil doux dans l'épaisseur)
    // ------------------------------------------------------------------------------------------------------------

    private LineRenderer MakeBeam(string objName, int sortingOrder)
    {
        GameObject g = new GameObject(objName);
        g.transform.SetParent(transform, false);
        LineRenderer lr = g.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.positionCount = 2;
        lr.sharedMaterial = BeamMaterial();
        lr.textureMode = LineTextureMode.Stretch;
        lr.numCapVertices = 2;
        lr.sortingOrder = sortingOrder;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        lr.enabled = false;
        return lr;
    }

    private static void SetBeam(LineRenderer lr, Vector3 a, Vector3 b, float width, Color color)
    {
        lr.SetPosition(0, a);
        lr.SetPosition(1, b);
        lr.widthMultiplier = width;
        lr.startColor = color;
        lr.endColor = color;
    }

    private static Material BeamMaterial()
    {
        if (_beamMat != null) return _beamMat;
        const int h = 64;
        Texture2D tex = new Texture2D(4, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        for (int y = 0; y < h; y++)
        {
            float v = (y + 0.5f) / h * 2f - 1f;              // -1 .. 1 dans l'épaisseur
            float alpha = Mathf.Exp(-v * v * 5f);             // profil gaussien : bords doux
            float core = Mathf.Exp(-v * v * 40f);             // filet plus clair au centre
            Color col = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha + core * 0.3f));
            for (int x = 0; x < 4; x++) tex.SetPixel(x, y, col);
        }
        tex.Apply();
        _beamMat = new Material(Shader.Find("Sprites/Default")) { mainTexture = tex };
        return _beamMat;
    }

    // ------------------------------------------------------------------------------------------------------------
    // Cristal 3D : bipyramide hexagonale allongée (pointe haute plus longue), normales plates par facette.
    // ------------------------------------------------------------------------------------------------------------

    private static Mesh CrystalMesh()
    {
        if (_crystalMesh != null) return _crystalMesh;
        const int sides = 6;
        const float radius = 0.4f, top = 0.9f, bottom = -0.5f;
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
        Mesh m = new Mesh { name = "PrismCrystal" };
        m.SetVertices(verts);
        m.SetTriangles(tris, 0);
        m.RecalculateNormals();   // sommets non partagés : une normale par facette
        m.RecalculateBounds();
        _crystalMesh = m;
        return m;
    }

    private static void AddTri(System.Collections.Generic.List<Vector3> verts, System.Collections.Generic.List<int> tris, Vector3 a, Vector3 b, Vector3 c)
    {
        int s = verts.Count;
        verts.Add(a); verts.Add(b); verts.Add(c);
        tris.Add(s); tris.Add(s + 1); tris.Add(s + 2);
    }

    // Même shader (URP Lit) que les orbes des Orbitaux : les facettes prennent la lumière de la scène. Copie de leur
    // matériau (jamais modifié lui-même), repli sur Sprites/Default si le prefab est introuvable.
    private static Material MakeCrystalMaterial()
    {
        GameObject orb = Resources.Load<GameObject>("OrbitalProjectile");
        MeshRenderer src = orb != null ? orb.GetComponent<MeshRenderer>() : null;
        Material m = src != null && src.sharedMaterial != null ? new Material(src.sharedMaterial) : new Material(Shader.Find("Sprites/Default"));
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.85f);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0.1f);
        return m;
    }

    private void OnDestroy()
    {
        if (_crystalMat != null) Destroy(_crystalMat);
        if (_fillMesh != null) Destroy(_fillMesh);
    }
}
