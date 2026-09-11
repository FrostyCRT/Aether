using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(Image))]
public class LoadingBarShimmerFX : MonoBehaviour
{
    [Header("Reference")]
    [SerializeField] private RectTransform _fillRect; // le Rect Transform du parent Fill, dont la largeur change avec la valeur du slider

    [Header("Reglages du balayage")]
    [SerializeField] private float _sweepDuration = 2.2f; // temps pour traverser toute la largeur actuelle
    [SerializeField] private float _shimmerWidth = 50f;

    [Header("Calibration fine (ajuste a l'oeil)")]
    // MODIFIE - separe desormais le reglage du point de depart (gauche) de
    // celui du point d'arrivee (droite, deja calibre precedemment). Avant,
    // le depart etait fixe a -_shimmerWidth*0.5f (entierement hors barre) sans
    // reglage possible, d'ou le "trop a gauche" signale.
    [SerializeField] private float _leftStartAdjustment = 25f; // positif = rapproche le point de depart du bord gauche reel
    [SerializeField] private float _rightEdgeAdjustment = 20f; // positif = rapproche le point d'arrivee du bord droit reel (inchange, deja valide)

    [Header("Texture procedurale")]
    [SerializeField] private int _textureWidth = 128;
    [SerializeField] private int _textureHeight = 64;
    [SerializeField] private float _falloffPower = 1.8f;

    private RectTransform _rect;
    private Image _image;

    private void Awake()
    {
        _rect = GetComponent<RectTransform>();
        _image = GetComponent<Image>();

        _rect.anchorMin = new Vector2(0f, 0.5f);
        _rect.anchorMax = new Vector2(0f, 0.5f);
        _rect.pivot = new Vector2(0.5f, 0.5f);

        _rect.sizeDelta = new Vector2(_shimmerWidth, _rect.sizeDelta.y);

        _image.sprite = GenerateShimmerSprite();
        _image.raycastTarget = false;
    }

    private Sprite GenerateShimmerSprite()
    {
        Texture2D tex = new Texture2D(_textureWidth, _textureHeight, TextureFormat.RGBA32, false);

        for (int y = 0; y < _textureHeight; y++)
        {
            for (int x = 0; x < _textureWidth; x++)
            {
                float dx = (x / (float)(_textureWidth - 1) - 0.5f) * 2f;
                float dy = (y / (float)(_textureHeight - 1) - 0.5f) * 2f;

                float dist = Mathf.Sqrt(dx * dx + (dy * 0.3f) * (dy * 0.3f));
                float alpha = Mathf.Clamp01(1f - dist);
                alpha = Mathf.Pow(alpha, _falloffPower);

                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        tex.Apply();

        return Sprite.Create(tex, new Rect(0, 0, _textureWidth, _textureHeight), new Vector2(0.5f, 0.5f));
    }

    private void Update()
    {
        if (_fillRect == null) return;

        float fillWidth = _fillRect.rect.width;

        // MODIFIE - startX et endX calcules independamment l'un de l'autre,
        // chacun avec son propre reglage de calibration. endX est exactement
        // la meme formule qu'avant (le reglage droit deja valide n'a pas bouge).
        float startX = -_shimmerWidth * 0.5f + _leftStartAdjustment;
        float endX = fillWidth + _shimmerWidth * 0.5f - _rightEdgeAdjustment;

        float t = Mathf.Repeat(Time.time / _sweepDuration, 1f);
        float x = Mathf.Lerp(startX, endX, t);

        _rect.anchoredPosition = new Vector2(x, _rect.anchoredPosition.y);
    }
}