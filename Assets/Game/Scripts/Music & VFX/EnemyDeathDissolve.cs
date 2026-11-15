using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// AJOUTÉ (2026-11-14, retour utilisateur : « quand les ennemis meurent, il faut que ce soit un minimum graphique, parce que là
// ils dépopent juste. On peut faire une fondue rapide de leur disparition ? ») - à la mort, EnemyBase rend l'ennemi au pool
// tout de suite, comme avant : le gameplay ne voit jamais un ennemi « mourant » (armes, apparitions, dégâts de contact,
// grille de séparation inchangés). Ce script laisse à sa place une COPIE purement visuelle, figée dans sa dernière pose, qui
// s'efface en Duration secondes :
//  - ennemis VAT (Loup, Gobelin, Golem, Araignée, Corbeau) : vrai fondu en transparence, _Dissolve 0 -> 1 lu par
//    VATLit.shader (alpha, léger éclaircissement au début). MODIFIÉ (même jour, 2e retour : la dissolution en blocs de
//    pixels « fait jeu rétro, pas dans la DA ») - la copie prend une variante TRANSPARENTE de son matériau, créée une seule
//    fois par matériau source (_fadeMaterials), jamais par mort. Coût : seules les copies en cours de fondu (une poignée à
//    la fois, 0,35 s chacune) sont dessinées en transparent ; les ennemis vivants restent opaques et regroupés ;
//  - autre mesh (Bulbe provisoire) : la copie rétrécit jusqu'à disparaître, faute de shader qui sache s'effacer.
// Copies réutilisées (réserve _pool) : même règle que EnemyDeathFX, appelé en rafale.
public class EnemyDeathDissolve : MonoBehaviour
{
    private const float Duration = 0.35f;
    private const float Sink = 0.12f;   // s'enfonce à peine pendant le fondu (unités monde)
    private static readonly int DissolveID = Shader.PropertyToID("_Dissolve");

    private static readonly Stack<EnemyDeathDissolve> _pool = new Stack<EnemyDeathDissolve>();
    private static readonly Dictionary<Material, Material> _fadeMaterials = new Dictionary<Material, Material>();

    // Variante transparente d'un matériau VATLit : mêmes textures et réglages, seulement le mélange et la file de rendu.
    private static Material FadeMaterial(Material source)
    {
        if (source == null) return null;
        if (_fadeMaterials.TryGetValue(source, out Material m) && m != null) return m;
        m = new Material(source) { name = source.name + " (fondu)" };
        m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 1f);   // garde la profondeur : pas de membres qui se voient au travers du corps
        m.SetOverrideTag("RenderType", "Transparent");
        m.renderQueue = (int)RenderQueue.Transparent;
        _fadeMaterials[source] = m;
        return m;
    }

    private Material[] _materialsBuffer = new Material[0];
    private Vector3 _basePosition;

    private MeshFilter _filter;
    private MeshRenderer _renderer;
    private MaterialPropertyBlock _props;
    private bool _dissolve;
    private Vector3 _baseScale;
    private float _t;

    public static void Play(Renderer source)
    {
        if (source == null || !(source is MeshRenderer) || !source.TryGetComponent(out MeshFilter sourceFilter)
            || sourceFilter.sharedMesh == null)
            return;

        EnemyDeathDissolve ghost = null;
        while (ghost == null && _pool.Count > 0) ghost = _pool.Pop();
        if (ghost == null)
        {
            GameObject go = new GameObject("EnemyDeathDissolve");
            ghost = go.AddComponent<EnemyDeathDissolve>();
            ghost._filter = go.AddComponent<MeshFilter>();
            ghost._renderer = go.AddComponent<MeshRenderer>();
            ghost._props = new MaterialPropertyBlock();
        }
        ghost.Init(source, sourceFilter);
    }

    private void Init(Renderer source, MeshFilter sourceFilter)
    {
        Transform src = source.transform;
        transform.SetPositionAndRotation(src.position, src.rotation);
        _basePosition = src.position;
        _baseScale = src.lossyScale;
        transform.localScale = _baseScale;
        gameObject.layer = source.gameObject.layer;

        _filter.sharedMesh = sourceFilter.sharedMesh;
        _dissolve = source.GetComponent<VATAnimatedRenderer>() != null;
        Material[] mats = source.sharedMaterials;
        if (_materialsBuffer.Length != mats.Length) _materialsBuffer = new Material[mats.Length];
        for (int i = 0; i < mats.Length; i++) _materialsBuffer[i] = _dissolve ? FadeMaterial(mats[i]) : mats[i];
        _renderer.sharedMaterials = _materialsBuffer;
        _renderer.shadowCastingMode = source.shadowCastingMode;
        _renderer.receiveShadows = source.receiveShadows;

        // reprend la pose figée (_VatFrame/_VatFrameNext) et la teinte du moment (flash de dégâts compris)
        source.GetPropertyBlock(_props);
        if (_dissolve) _props.SetFloat(DissolveID, 0.001f);   // > 0 : l'éclat de départ est visible dès la 1re image
        _renderer.SetPropertyBlock(_props);

        _t = 0f;
        gameObject.SetActive(true);
    }

    private void Update()
    {
        _t += Time.deltaTime;
        float p = Mathf.Clamp01(_t / Duration);
        if (_dissolve)
        {
            float fade = 1f - (1f - p) * (1f - p);   // part vite puis s'adoucit
            _props.SetFloat(DissolveID, Mathf.Max(0.001f, fade));
            _renderer.SetPropertyBlock(_props);
            transform.position = _basePosition + Vector3.down * (Sink * fade);
        }
        else
        {
            transform.localScale = _baseScale * (1f - p * p);
        }

        if (p >= 1f)
        {
            gameObject.SetActive(false);
            _pool.Push(this);
        }
    }
}
