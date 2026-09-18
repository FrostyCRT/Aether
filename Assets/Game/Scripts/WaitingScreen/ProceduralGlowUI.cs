using UnityEngine;
using UnityEngine.UI;

// MODIFIE (2026-09-17) - a l'origine dedie au LoadingScreen (halo de fond fixe,
// une seule couleur pour toute la vie de l'objet). Etendu pour servir aussi de
// "lueur magique" reutilisable sur les portraits de personnage (Selection
// Personnage) : SetColor() permet de reteindre a la volee quand le personnage
// change (meme GameObject partage, pas 3 halos dupliques), et le derive
// optionnel (_enableDrift) transforme le meme composant en petite particule
// qui flotte doucement - utile pour des etincelles ambiantes vivantes autour
// d'un perso, sans dupliquer toute la logique de pulsation/texture.
public class ProceduralGlowUI : MonoBehaviour
{
    [SerializeField] private int _textureSize = 256;
    [SerializeField] private Color _glowColor = new Color(0.6f, 0.85f, 1f); // teinte cristal bleu clair
    [SerializeField] private float _pulseSpeed = 0.5f;
    [SerializeField] private float _minAlpha = 0.15f;
    [SerializeField] private float _maxAlpha = 0.45f;
    [SerializeField] private float _minScale = 0.95f;
    [SerializeField] private float _maxScale = 1.08f;

    [Header("Dérive (optionnel - effet 'particule qui flotte')")]
    [Tooltip("Si activé, le halo dérive doucement autour de sa position de base au lieu de rester fixe (bruit de Perlin, sans à-coups).")]
    [SerializeField] private bool _enableDrift = false;
    [SerializeField] private float _driftRadius = 15f;
    [SerializeField] private float _driftSpeed = 0.3f;

    private Image _image;
    private RectTransform _rect;
    private Vector3 _baseScale;
    private Vector2 _basePosition;
    private float _driftSeedX;
    private float _driftSeedY;
    private float _pulsePhaseOffset;

    private void Awake()
    {
        _image = GetComponent<Image>();
        _rect = GetComponent<RectTransform>();
        _baseScale = _rect.localScale;
        _basePosition = _rect.anchoredPosition;

        // Décale la phase de pulsation par instance - sans ça, plusieurs halos
        // avec le même _pulseSpeed respirent exactement en même temps, ce qui
        // se voit tout de suite comme "mécanique" au lieu de vivant.
        _pulsePhaseOffset = Random.Range(0f, 100f);
        _driftSeedX = Random.Range(0f, 100f);
        _driftSeedY = Random.Range(0f, 100f);

        _image.sprite = GenerateRadialGlowSprite();
        _image.raycastTarget = false;
    }

    private Sprite GenerateRadialGlowSprite()
    {
        Texture2D tex = new Texture2D(_textureSize, _textureSize, TextureFormat.RGBA32, false);
        Vector2 center = new Vector2(_textureSize * 0.5f, _textureSize * 0.5f);
        float maxDist = _textureSize * 0.5f;

        for (int y = 0; y < _textureSize; y++)
        {
            for (int x = 0; x < _textureSize; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center) / maxDist;
                float alpha = Mathf.Clamp01(1f - dist);
                alpha = Mathf.Pow(alpha, 2.2f); // chute douce, plus concentree au centre
                tex.SetPixel(x, y, new Color(_glowColor.r, _glowColor.g, _glowColor.b, alpha));
            }
        }
        tex.Apply();

        return Sprite.Create(tex, new Rect(0, 0, _textureSize, _textureSize), new Vector2(0.5f, 0.5f));
    }

    // AJOUTE - reteinte a la volee (regenere la texture, la couleur est cuite
    // dans les pixels). Utilise par CharacterSelectUI pour partager UN seul
    // halo entre les 3 personnages plutot que d'en dupliquer un par perso.
    public void SetColor(Color color)
    {
        _glowColor = color;
        if (_image == null) _image = GetComponent<Image>();
        _image.sprite = GenerateRadialGlowSprite();
    }

    // AJOUTE - repositionne le halo (utile quand on reteint pour un autre
    // personnage dont le point d'ancrage n'est pas au même endroit). Remet
    // aussi la dérive à zéro sur la nouvelle position, plutôt que de dériver
    // depuis l'ancien point pendant la transition.
    public void SetBasePosition(Vector2 anchoredPosition)
    {
        _basePosition = anchoredPosition;
        if (_rect == null) _rect = GetComponent<RectTransform>();
        _rect.anchoredPosition = anchoredPosition;
    }

    private void Update()
    {
        float t = (Mathf.Sin((Time.time + _pulsePhaseOffset) * _pulseSpeed * Mathf.PI * 2f) + 1f) * 0.5f;

        Color c = _image.color;
        c.a = Mathf.Lerp(_minAlpha, _maxAlpha, t);
        _image.color = c;

        float scale = Mathf.Lerp(_minScale, _maxScale, t);
        _rect.localScale = _baseScale * scale;

        if (_enableDrift)
        {
            float dx = Mathf.PerlinNoise(_driftSeedX, Time.time * _driftSpeed) - 0.5f;
            float dy = Mathf.PerlinNoise(_driftSeedY, Time.time * _driftSpeed + 50f) - 0.5f;
            _rect.anchoredPosition = _basePosition + new Vector2(dx, dy) * _driftRadius * 2f;
        }
    }
}
