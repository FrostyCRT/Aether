using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// AJOUTÉ (2026-11-07, retour utilisateur sur l'Onde de choc de l'Aura de Kael : "ces motifs je les vois trop souvent, je
// veux un truc fluide qui part du sol et qui s'étend, qui est lisse, d'un beau vert, pas un gros cristal vert impulsif,
// mais il faut quand même voir une impulsion") - remplace ArcaneBurstVFX (cristal brisé) pour cet effet.
//
// Déroulé (~0,9 s), tout en dégradés doux, aucune arête :
//  1. INSPIRATION : un souffle vert se resserre une fraction de seconde sous Kael (on sent la charge).
//  2. IMPULSION : un éclat clair jaillit du sol au centre et s'étale (le « coup »).
//  3. HOULE AU SOL : une vague lisse part des pieds de Kael et s'étend jusqu'au bord de l'aura - front lumineux vert
//     menthe, longue traîne émeraude qui s'efface vers l'intérieur (comme une onde dans l'eau), décélération douce.
//  4. ÉCHO : une seconde vague plus fine suit la première, plus pâle (fluidité, pas un simple cercle qui grossit).
//  5. VOILE QUI SE LÈVE : au passage du front, un rideau de lumière monte du sol (cylindre, opaque en bas, transparent
//     en haut), puis retombe - l'onde « part du sol ».
//  6. LUCIOLES : quelques points de lumière soulevés par le front, qui montent en dérivant et s'éteignent.
// Grammaire visuelle : ce n'est PAS un télégraphe ennemi (anneau fin à bord net / graduations / disque plein rouge) :
// ici, pas de trait net, un front flou à traîne, du volume (voile) et du mouvement vers le haut, en vert Kael.
// Même modèle que ArcaneBurstVFX : pool de Runner réutilisés, sprites et maillage générés une fois.
public static class AuraShockwaveVFX
{
    public const float ExpandDuration = 0.5f;   // temps mis par le front pour atteindre le bord (sert aussi à la poussée)
    private const int MaxConcurrent = 4;
    private static readonly Stack<AuraShockwaveRunner> _pool = new Stack<AuraShockwaveRunner>();
    private static int _active;

    // Rayon du front à l'instant t (0..ExpandDuration), en fraction du rayon final : décélération douce.
    public static float FrontFraction(float t) => FXSprites.EaseOutCubic(t / ExpandDuration);

    // Délai avant que le front atteigne une distance donnée (inverse de FrontFraction).
    public static float DelayToReach(float distance, float radius)
    {
        float f = Mathf.Clamp01(distance / Mathf.Max(0.01f, radius));
        return ExpandDuration * (1f - Mathf.Pow(1f - f, 1f / 3f));
    }

    // follow + offset : l'onde reste centrée sur le personnage pendant toute l'animation, même s'il se déplace
    // (retour utilisateur 2026-11-07). Sans follow, elle reste à la position de départ.
    public static void Spawn(Vector3 center, float radius, Transform follow = null, Vector3 offset = default)
    {
        if (_active >= MaxConcurrent) return;
        AuraShockwaveRunner r = _pool.Count > 0 ? _pool.Pop() : null;
        if (r == null) r = new GameObject("AuraShockwaveVFX").AddComponent<AuraShockwaveRunner>();
        else r.gameObject.SetActive(true);
        _active++;
        r.Play(center, radius, Done, follow, offset);
    }

    private static void Done(AuraShockwaveRunner r)
    {
        _active = Mathf.Max(0, _active - 1);
        r.gameObject.SetActive(false);
        _pool.Push(r);
    }

    // ---- sprites / maillage générés une fois ----------------------------------------------------------------------
    private static Sprite _swell, _echo, _bloom;
    private static Mesh _veilMesh;
    private static Texture2D _veilTex;

    // Houle : front doux à 92 % du rayon du sprite + longue traîne vers l'intérieur, centre vide.
    public static Sprite Swell() => _swell != null ? _swell : (_swell = MakeRadial(256, r =>
    {
        float front = Mathf.Exp(-Mathf.Pow((r - 0.9f) / 0.055f, 2f));                 // crête lumineuse, sans bord net
        float tail = r < 0.9f ? Mathf.Exp(-(0.9f - r) / 0.22f) * 0.55f : 0f;           // traîne qui s'efface vers le centre
        float outer = r > 0.9f ? Mathf.Exp(-Mathf.Pow((r - 0.9f) / 0.05f, 2f)) : 1f;  // avant de la vague très court
        return Mathf.Clamp01(Mathf.Max(front, tail) * outer);
    }));

    // Écho : crête plus fine, traîne courte.
    public static Sprite Echo() => _echo != null ? _echo : (_echo = MakeRadial(256, r =>
    {
        float front = Mathf.Exp(-Mathf.Pow((r - 0.92f) / 0.035f, 2f));
        float tail = r < 0.92f ? Mathf.Exp(-(0.92f - r) / 0.1f) * 0.35f : 0f;
        float outer = r > 0.92f ? Mathf.Exp(-Mathf.Pow((r - 0.92f) / 0.03f, 2f)) : 1f;
        return Mathf.Clamp01(Mathf.Max(front, tail) * outer);
    }));

    // Éclat central : disque très doux (gaussienne).
    public static Sprite Bloom() => _bloom != null ? _bloom : (_bloom = MakeRadial(128, r => Mathf.Exp(-r * r * 4.5f)));

    private static Sprite MakeRadial(int n, System.Func<float, float> alpha)
    {
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n * 2f - 1f, v = (y + 0.5f) / n * 2f - 1f;
                float r = Mathf.Sqrt(u * u + v * v);
                float a = r >= 1f ? 0f : alpha(r) * FXSprites.Smooth((1f - r) / 0.04f);
                px[y * n + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f));
            }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n / 2f, 0, SpriteMeshType.FullRect);   // 1 unité = rayon
    }

    // Voile : cylindre ouvert de rayon 1 et hauteur 1, UV v = hauteur.
    public static Mesh VeilMesh()
    {
        if (_veilMesh != null) return _veilMesh;
        const int seg = 64;
        var verts = new Vector3[(seg + 1) * 2];
        var uvs = new Vector2[verts.Length];
        var tris = new int[seg * 6];
        for (int i = 0; i <= seg; i++)
        {
            float a = i / (float)seg * Mathf.PI * 2f;
            var p = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            verts[i * 2] = p; verts[i * 2 + 1] = p + Vector3.up;
            uvs[i * 2] = new Vector2(i / (float)seg, 0f); uvs[i * 2 + 1] = new Vector2(i / (float)seg, 1f);
            if (i < seg)
            {
                int b = i * 2, t = i * 6;
                tris[t] = b; tris[t + 1] = b + 1; tris[t + 2] = b + 2;
                tris[t + 3] = b + 2; tris[t + 4] = b + 1; tris[t + 5] = b + 3;
            }
        }
        _veilMesh = new Mesh { name = "AuraShockwaveVeil", vertices = verts, uv = uvs, triangles = tris };
        _veilMesh.RecalculateBounds();
        return _veilMesh;
    }

    // Dégradé vertical du voile : plein au ras du sol, s'efface vers le haut, avec un léger liseré au pied.
    public static Texture2D VeilTexture()
    {
        if (_veilTex != null) return _veilTex;
        const int h = 128;
        _veilTex = new Texture2D(4, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color32[4 * h];
        for (int y = 0; y < h; y++)
        {
            float v = y / (h - 1f);
            float a = Mathf.Pow(1f - v, 2.2f) * FXSprites.Smooth(v / 0.06f);
            for (int x = 0; x < 4; x++) px[y * 4 + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
        }
        _veilTex.SetPixels32(px);
        _veilTex.Apply();
        return _veilTex;
    }
}

public class AuraShockwaveRunner : MonoBehaviour
{
    // Palette : vert Kael, du menthe lumineux (crête) à l'émeraude profond (traîne).
    private static readonly Color Mint = new Color(0.72f, 1f, 0.78f);
    private static readonly Color Emerald = new Color(0.22f, 0.86f, 0.46f);
    private static readonly Color Deep = new Color(0.10f, 0.55f, 0.30f);
    private const float GroundY = 0.04f;
    private const int MoteCount = 18;

    private SpriteRenderer _gather, _bloom, _swell, _swellBody, _echo;
    private MeshRenderer _veil;
    private Material _veilMat;
    private SpriteRenderer[] _motes;
    private Vector3[] _moteDir;
    private float[] _moteAt, _moteSpeed, _moteSize;
    private float _radius;
    private System.Action<AuraShockwaveRunner> _done;
    private Coroutine _co;
    private Transform _follow;
    private Vector3 _offset;

    public void Play(Vector3 center, float radius, System.Action<AuraShockwaveRunner> done, Transform follow = null, Vector3 offset = default)
    {
        _follow = follow;
        _offset = offset;
        transform.position = new Vector3(center.x, GroundY, center.z);
        _radius = Mathf.Max(1f, radius);
        _done = done;
        Build();
        if (_co != null) StopCoroutine(_co);
        _co = StartCoroutine(Animate());
    }

    private void Build()
    {
        if (_swell != null) return;
        // ordres négatifs : les chiffres de dégâts restent lisibles par-dessus
        _swellBody = FXSprites.MakeRenderer(transform, "SwellBody", AuraShockwaveVFX.Swell(), -19, 0f);
        _swell = FXSprites.MakeRenderer(transform, "Swell", AuraShockwaveVFX.Swell(), -18, 0.01f);
        _echo = FXSprites.MakeRenderer(transform, "Echo", AuraShockwaveVFX.Echo(), -17, 0.02f);
        _gather = FXSprites.MakeRenderer(transform, "Gather", AuraShockwaveVFX.Bloom(), -16, 0.03f);
        _bloom = FXSprites.MakeRenderer(transform, "Bloom", AuraShockwaveVFX.Bloom(), -15, 0.04f);

        var veilGO = new GameObject("Veil");
        veilGO.transform.SetParent(transform, false);
        veilGO.AddComponent<MeshFilter>().sharedMesh = AuraShockwaveVFX.VeilMesh();
        _veil = veilGO.AddComponent<MeshRenderer>();
        _veilMat = new Material(Shader.Find("Sprites/Default")) { mainTexture = AuraShockwaveVFX.VeilTexture() };
        _veil.sharedMaterial = _veilMat;
        _veil.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _veil.receiveShadows = false;
        _veil.sortingOrder = -14;

        _motes = new SpriteRenderer[MoteCount];
        _moteDir = new Vector3[MoteCount];
        _moteAt = new float[MoteCount];
        _moteSpeed = new float[MoteCount];
        _moteSize = new float[MoteCount];
        for (int i = 0; i < MoteCount; i++) _motes[i] = FXSprites.MakeRenderer(transform, "Mote" + i, FXSprites.Glow(), -13, 0.2f);
    }

    private static float Bump(float t, float a, float b, float c)   // 0 -> 1 entre a et b, 1 -> 0 entre b et c
        => t < b ? FXSprites.Smooth((t - a) / Mathf.Max(0.001f, b - a)) : 1f - FXSprites.Smooth((t - b) / Mathf.Max(0.001f, c - b));

    private IEnumerator Animate()
    {
        float r = _radius;
        float T = AuraShockwaveVFX.ExpandDuration;
        const float total = 0.95f;
        for (int i = 0; i < MoteCount; i++)
        {
            float a = (i + Random.Range(-0.3f, 0.3f)) / MoteCount * Mathf.PI * 2f;
            _moteDir[i] = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            _moteAt[i] = Random.Range(0.35f, 0.95f);       // fraction du rayon où le front le soulève
            _moteSpeed[i] = Random.Range(1.4f, 2.6f);
            _moteSize[i] = Random.Range(0.18f, 0.34f);
        }
        Camera cam = Camera.main;
        Quaternion face = cam != null ? cam.transform.rotation : Quaternion.Euler(65f, 0f, 0f);

        for (float t = 0f; t < total; t += Time.deltaTime)
        {
            if (_follow != null)
            {
                Vector3 c = _follow.position + _offset;
                transform.position = new Vector3(c.x, GroundY, c.z);
            }

            // 1. inspiration (avant le coup) : souffle qui se resserre sous Kael
            float g = Bump(t, 0f, 0.05f, 0.14f);
            FXSprites.SetFlat(_gather, Mathf.Lerp(r * 0.9f, r * 0.35f, FXSprites.Smooth(t / 0.14f)), 0f);
            _gather.color = FXSprites.A(Emerald, 0.35f * g);

            // 2. impulsion : éclat clair au centre qui s'étale
            float bt = t - 0.08f;
            float b = bt < 0f ? 0f : Bump(bt, 0f, 0.05f, 0.4f);
            FXSprites.SetFlat(_bloom, Mathf.Lerp(r * 0.35f, r * 1.1f, FXSprites.EaseOutCubic(Mathf.Max(0f, bt) / 0.4f)), 0f);
            _bloom.color = FXSprites.A(Color.Lerp(Mint, Color.white, 0.35f), 0.75f * b);

            // 3. houle : front qui part des pieds et s'étend jusqu'au bord de l'aura
            float wt = t - 0.08f;
            float f = wt <= 0f ? 0f : AuraShockwaveVFX.FrontFraction(wt);
            float swellD = Mathf.Max(0.01f, 2f * r * Mathf.Lerp(0.12f, 1.02f, f) / 0.9f);   // la crête (0,9 du sprite) tombe au front
            float life = wt <= 0f ? 0f : Mathf.Clamp01(wt / 0.06f) * (1f - FXSprites.Smooth((wt - T * 0.7f) / (total - 0.08f - T * 0.7f)));
            FXSprites.SetFlat(_swell, swellD, 0f);
            _swell.color = FXSprites.A(Mint, 0.95f * life);
            FXSprites.SetFlat(_swellBody, swellD * 1.015f, 0f);
            _swellBody.color = FXSprites.A(Color.Lerp(Emerald, Deep, f * 0.6f), 0.38f * life * (1f - 0.5f * f));   // la traîne s'efface en s'étalant : jamais une zone pleine

            // 4. écho : seconde vague, plus fine et plus pâle
            float et = t - 0.2f;
            float ef = et <= 0f ? 0f : AuraShockwaveVFX.FrontFraction(et * 1.1f);
            float elife = et <= 0f ? 0f : Mathf.Clamp01(et / 0.08f) * (1f - FXSprites.Smooth((et - 0.25f) / 0.45f));
            FXSprites.SetFlat(_echo, Mathf.Max(0.01f, 2f * r * Mathf.Lerp(0.1f, 0.94f, ef) / 0.92f), 0f);
            _echo.color = FXSprites.A(Mint, 0.55f * elife);

            // 5. voile : rideau de lumière qui monte du sol au passage du front, puis retombe
            float vr = r * Mathf.Lerp(0.12f, 1.0f, f);
            float rise = wt <= 0f ? 0f : Bump(wt, 0f, T * 0.45f, total - 0.25f);
            _veil.transform.localScale = new Vector3(vr, Mathf.Lerp(0.05f, 1.5f, rise), vr);
            _veilMat.color = FXSprites.A(Color.Lerp(Mint, Emerald, 0.35f), 0.34f * rise);

            // 6. lucioles soulevées par le front
            for (int i = 0; i < MoteCount; i++)
            {
                float since = wt - AuraShockwaveVFX.DelayToReach(_moteAt[i] * r, r);
                SpriteRenderer m = _motes[i];
                if (since <= 0f) { m.color = Color.clear; continue; }
                float u = since / 0.6f;
                if (u >= 1f) { m.color = Color.clear; continue; }
                Vector3 p = _moteDir[i] * (_moteAt[i] * r + since * 1.2f) + Vector3.up * (0.15f + since * _moteSpeed[i]);
                m.transform.localPosition = p;
                m.transform.rotation = face;
                float s = _moteSize[i] * (1f - u * 0.4f);
                m.transform.localScale = new Vector3(s, s, 1f) / Mathf.Max(0.01f, m.sprite.bounds.size.x);
                m.color = FXSprites.A(Color.Lerp(Color.white, Mint, 0.5f), 0.9f * Mathf.Sin(u * Mathf.PI));
            }
            yield return null;
        }

        _gather.color = _bloom.color = _swell.color = _swellBody.color = _echo.color = Color.clear;
        _veilMat.color = Color.clear;
        for (int i = 0; i < MoteCount; i++) _motes[i].color = Color.clear;
        _co = null;
        _done?.Invoke(this);
    }
}
