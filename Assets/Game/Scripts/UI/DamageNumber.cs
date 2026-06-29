using UnityEngine;
using TMPro;

public class DamageNumber : MonoBehaviour
{
    [SerializeField] private float _moveSpeed = 2f;
    [SerializeField] private float _lifetime = 1f;

    private TextMeshPro _text;
    private float _timer = 0f;
    private Color _color;

    private void Awake()
    {
        _text = GetComponent<TextMeshPro>();
    }

    public void Init(float damage, Color color, bool isCritical = false)
    {
        // CORRECTION : Reset impératif du timer pour le recyclage du pool
        _timer = 0f;

        _color = color;
        _text.color = color;
        _text.text = isCritical ? $"{Mathf.CeilToInt(damage)}!" : $"{Mathf.CeilToInt(damage)}";
        _text.fontSize = isCritical ? 10f : 8f;
        _text.fontStyle = FontStyles.Bold;

        // Légère variation aléatoire sur l'axe X et Z pour espacer les textes
        transform.position += new Vector3(
            Random.Range(-0.3f, 0.3f),
            0f,
            Random.Range(-0.3f, 0.3f)
        );
    }

    private void Update()
    {
        _timer += Time.deltaTime;

        // Monte vers le haut
        transform.position += Vector3.up * _moveSpeed * Time.deltaTime;

        // CORRECTION : Le texte fait toujours face à la caméra pour éviter l'effet écrasé/étiré
        if (Camera.main != null)
        {
            transform.rotation = Camera.main.transform.rotation;
        }

        // Calcul du fondu (Fade out) progressif
        float alpha = Mathf.Clamp01(1f - (_timer / _lifetime));
        _text.color = new Color(_color.r, _color.g, _color.b, alpha);

        if (_timer >= _lifetime)
        {
            gameObject.SetActive(false);
        }
    }
}
