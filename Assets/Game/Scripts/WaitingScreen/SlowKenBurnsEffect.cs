using UnityEngine;

public class SlowKenBurnsEffect : MonoBehaviour
{
    // MODIFIE - l'amplitude oscille desormais UNIQUEMENT au-dessus de la base
    // capturee dans l'Inspector (qui doit deja etre surdimensionnee, ex. 1.15),
    // jamais en dessous. Avant : oscillait autour de 1, ce qui pouvait descendre
    // sous la taille exacte de l'ecran et reveler les bords de l'image.
    [SerializeField] private float _zoomAmplitude = 0.02f; // +/-2% autour de la base deja surdimensionnee
    [SerializeField] private float _zoomSpeed = 0.008f; // tres lent, cycle complet ~125s

    // MODIFIE - amplitude de pan reduite et exprimee en pixels absolus plutot
    // que relative, pour rester tres largement a l'interieur de la marge de
    // securite creee par le surdimensionnement de _baseScale.
    [SerializeField] private float _panAmplitude = 10f;
    [SerializeField] private float _panSpeed = 0.006f; // cycle complet ~165s

    private RectTransform _rect;
    private Vector3 _baseScale;
    private Vector2 _basePosition;

    private void Awake()
    {
        _rect = GetComponent<RectTransform>();
        // IMPORTANT - _baseScale doit deja valoir un surdimensionnement de
        // securite (ex. 1.15/1.15/1.15 regle dans l'Inspector), pas 1/1/1.
        // C'est cette valeur qui garantit que l'image ne montre jamais ses bords,
        // quelle que soit la phase du zoom/pan ci-dessous.
        _baseScale = _rect.localScale;
        _basePosition = _rect.anchoredPosition;
    }

    private void Update()
    {
        float zoom = 1f + Mathf.Sin(Time.time * _zoomSpeed * Mathf.PI * 2f) * _zoomAmplitude;
        _rect.localScale = _baseScale * zoom;

        float panX = Mathf.Sin(Time.time * _panSpeed * Mathf.PI * 2f) * _panAmplitude;
        float panY = Mathf.Cos(Time.time * _panSpeed * 0.7f * Mathf.PI * 2f) * _panAmplitude * 0.5f;
        _rect.anchoredPosition = _basePosition + new Vector2(panX, panY);
    }
}