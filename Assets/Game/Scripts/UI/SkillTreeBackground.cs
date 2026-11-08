using UnityEngine;
using UnityEngine.UI;

// AJOUTÉ (2026-11-08, retour utilisateur : "le background de la page compétence fait très Ghibli [...] l'arbre sur l'écran
// d'accueil est censé représenter cet arbre de compétence") - fond de la page Compétences selon la difficulté, comme l'accueil :
// Modéré = jour, Violent = crépuscule, Apocalyptique = nuit (3 images au même cadrage, 2048 x 1152).
//
// L'illustration est en 16:9 et la page fait toute la largeur sous la barre d'onglets : on retire la bande du haut que la
// barre occuperait, au lieu d'écraser l'image. L'arbre garde ainsi ses proportions à 1920 x 1080, et reste au même endroit
// relatif à tout autre format (TreeAmbientFX place ses effets en fraction de la page).
[RequireComponent(typeof(RectTransform))]
public class SkillTreeBackground : MonoBehaviour
{
    private const float TopCrop = 100f / 1080f;

    [SerializeField] private Texture2D _day, _dusk, _night;

    // Par difficulté (jour, crépuscule, nuit) : voile sur l'image, pour que les médaillons ressortent, et force des lueurs
    // additives, très visibles sur une nuit et noyées dans le blanc en plein jour.
    private static readonly float[] Brightness = { 0.84f, 0.90f, 1f };
    private static readonly float[] FxIntensity = { 0.7f, 0.9f, 1.15f };
    private static readonly Color[] Mist =
        { new Color(1f, 0.97f, 0.9f), new Color(1f, 0.74f, 0.55f), new Color(0.42f, 0.56f, 0.86f) };

    // Compensation des halos des nœuds (voir HaloFactor) : luminosité du décor (0-255) sous laquelle le halo est au plus bas,
    // au-dessus de laquelle il est intact, et distance du centre du nœud où mesurer (la bordure visible du halo).
    private const float DarkDecor = 20f, ClearDecor = 70f, DarkHalo = 0.75f, HaloRingRadius = 105f;
    private const int MapW = 240, MapH = 135, RingSamples = 24;

    private RawImage _image;
    private TreeAmbientFX _fx;
    private RectTransform _rect;
    private readonly byte[][] _maps = new byte[3][];   // luminosité réduite de chaque image, calculée une seule fois
    private byte[] _map;
    private float _brightness = 1f;

    private void Awake()
    {
        _rect = (RectTransform)transform;

        // « PanelBackground » : nom reconnu par le mode contemplation et l'animation d'ouverture des pages (le fond reste en place)
        var go = new GameObject("PanelBackground", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        var rt = (RectTransform)go.transform;
        rt.SetParent(transform, false);
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        rt.SetAsFirstSibling();
        _image = go.GetComponent<RawImage>();
        _image.raycastTarget = false;
        _image.uvRect = new Rect(0f, 0f, 1f, 1f - TopCrop);
        _fx = GetComponentInChildren<TreeAmbientFX>(true);
    }

    // La difficulté se choisit sur l'accueil : cette page est forcément fermée à ce moment-là.
    private void OnEnable()
    {
        int tier = Mathf.Clamp((int)DifficultyTiers.Current, 0, 2);
        _image.texture = tier == 0 ? _day : tier == 1 ? _dusk : _night;
        float b = Brightness[tier];
        _brightness = b;
        _image.color = new Color(b, b, b, 1f);
        if (_fx != null) { _fx.SetIntensity(FxIntensity[tier]); _fx.SetMistColor(Mist[tier]); }
        if (_maps[tier] == null && _image.texture != null) _maps[tier] = BuildLuminanceMap(_image.texture);
        _map = _maps[tier];
    }

    // AJOUTÉ (2026-11-08, retour utilisateur : "le halo du noeud de gauche me parait toujours aussi gros") - un halo additif de
    // même force paraît plus gros et plus brillant sur des rochers presque noirs que sur un décor clair. Renvoie le facteur à
    // appliquer à l'intensité d'un halo centré sur worldPos, selon le décor qui entoure sa bordure.
    public float HaloFactor(Vector3 worldPos)
    {
        if (_map == null) return 1f;
        Vector2 center = _rect.InverseTransformPoint(worldPos);
        Rect r = _rect.rect;
        float sum = 0f;
        for (int i = 0; i < RingSamples; i++)
        {
            float angle = i * Mathf.PI * 2f / RingSamples;
            Vector2 p = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * HaloRingRadius;
            Vector2 n = Rect.PointToNormalized(r, p);
            int x = Mathf.RoundToInt(n.x * (MapW - 1));
            int y = Mathf.RoundToInt(n.y * (1f - TopCrop) * (MapH - 1));
            sum += _map[y * MapW + x];
        }
        float decor = sum / RingSamples * _brightness;
        return Mathf.Lerp(DarkHalo, 1f, Mathf.InverseLerp(DarkDecor, ClearDecor, decor));
    }

    private static byte[] BuildLuminanceMap(Texture source)
    {
        RenderTexture rt = RenderTexture.GetTemporary(MapW, MapH, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        Graphics.Blit(source, rt);
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = rt;
        var read = new Texture2D(MapW, MapH, TextureFormat.RGBA32, false, false);
        read.ReadPixels(new Rect(0, 0, MapW, MapH), 0, 0, false);
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(rt);

        Color32[] pixels = read.GetPixels32();
        Destroy(read);
        var map = new byte[pixels.Length];
        for (int i = 0; i < pixels.Length; i++)
            map[i] = (byte)((pixels[i].r + pixels[i].g + pixels[i].b) / 3);
        return map;
    }
}
