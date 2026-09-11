using UnityEngine;
using UnityEngine.UI;

public class ProceduralGlowUI : MonoBehaviour
{
    [SerializeField] private int _textureSize = 256;
    [SerializeField] private Color _glowColor = new Color(0.6f, 0.85f, 1f); // teinte cristal bleu clair
    [SerializeField] private float _pulseSpeed = 0.5f;
    [SerializeField] private float _minAlpha = 0.15f;
    [SerializeField] private float _maxAlpha = 0.45f;
    [SerializeField] private float _minScale = 0.95f;
    [SerializeField] private float _maxScale = 1.08f;

    private Image _image;
    private RectTransform _rect;
    private Vector3 _baseScale;

    private void Awake()
    {
        _image = GetComponent<Image>();
        _rect = GetComponent<RectTransform>();
        _baseScale = _rect.localScale;
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

    private void Update()
    {
        float t = (Mathf.Sin(Time.time * _pulseSpeed * Mathf.PI * 2f) + 1f) * 0.5f;

        Color c = _image.color;
        c.a = Mathf.Lerp(_minAlpha, _maxAlpha, t);
        _image.color = c;

        float scale = Mathf.Lerp(_minScale, _maxScale, t);
        _rect.localScale = _baseScale * scale;
    }
}