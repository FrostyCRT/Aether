using UnityEngine;
using TMPro;

public class DamageNumber : MonoBehaviour
{
    [SerializeField] private float _moveSpeed = 2f;
    [SerializeField] private float _lifetime = 1f;

    private TextMeshPro _text;
    private float _timer = 0f;
    private Color _color;
    private float _totalDamage;
    private bool _isCritical;
    public float ElapsedTime => _timer;
    public Transform Target { get; private set; }

    private void Awake()
    {
        _text = GetComponent<TextMeshPro>();
    }

    public void Init(float damage, Color color, Transform target, bool isCritical = false)
    {
        _timer = 0f;
        _color = color;
        _totalDamage = damage;
        _isCritical = isCritical;
        Target = target;

        UpdateText();

        // Légère variation aléatoire sur l'axe X et Z pour espacer les textes
        transform.position += new Vector3(
            Random.Range(-0.3f, 0.3f),
            0f,
            Random.Range(-0.3f, 0.3f)
        );
    }

    // NOUVEAU — appelé quand un nouveau coup arrive sur la même cible pendant la fenêtre de fusion
    public void AddDamage(float damage, bool isCritical)
    {
        _totalDamage += damage;

        if (isCritical && !_isCritical)
        {
            _isCritical = true;
            _color = DamageNumberSpawner.ColorCritical;
        }

        _timer = 0f; // relance la durée de vie pour laisser le temps de lire le total
        UpdateText();
    }

    private void UpdateText()
    {
        _text.color = _color;
        _text.text = _isCritical ? $"{Mathf.CeilToInt(_totalDamage)}!" : $"{Mathf.CeilToInt(_totalDamage)}";
        _text.fontSize = _isCritical ? 10f : 8f;
        _text.fontStyle = FontStyles.Bold;
    }

    private void Update()
    {
        _timer += Time.deltaTime;
        transform.position += Vector3.up * _moveSpeed * Time.deltaTime;

        if (Camera.main != null)
            transform.rotation = Camera.main.transform.rotation;

        float alpha = Mathf.Clamp01(1f - (_timer / _lifetime));
        _text.color = new Color(_color.r, _color.g, _color.b, alpha);

        if (_timer >= _lifetime)
            gameObject.SetActive(false);
    }
}