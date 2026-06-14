using UnityEngine;

public class XPGem : MonoBehaviour
{
    [Header("Stats")]
    private float _xpValue = 10f;
    private float _attractionRadius = 4f;
    private bool _attracted = false;
    private float _moveSpeed = 8f;

    private Transform _playerTransform;
    private Rigidbody _rb;

    // Types de gemmes
    public enum GemType { Small, Medium, Large }
    private GemType _gemType;

    public void Init(GemType type, Transform player)
    {
        _gemType = type;
        _playerTransform = player;

        switch (type)
        {
            case GemType.Small:
                _xpValue = 10f;
                transform.localScale = Vector3.one * 0.3f;
                GetComponent<Renderer>().material.color = new Color(0.2f, 0.6f, 1f); // Bleue
                GetComponent<Renderer>().material.SetColor("_EmissionColor", new Color(0.2f, 0.6f, 1f) * 2f);
                break;

            case GemType.Medium:
                _xpValue = 20f;
                transform.localScale = Vector3.one * 0.4f;
                GetComponent<Renderer>().material.color = new Color(0.7f, 0.2f, 1f); // Violette
                GetComponent<Renderer>().material.SetColor("_EmissionColor", new Color(0.7f, 0.2f, 1f) * 2f);
                break;

            case GemType.Large:
                _xpValue = 50f;
                transform.localScale = Vector3.one * 0.6f;
                GetComponent<Renderer>().material.color = new Color(1f, 0.8f, 0.1f); // Dorée
                GetComponent<Renderer>().material.SetColor("_EmissionColor", new Color(1f, 0.8f, 0.1f) * 2f);
                break;
        }
    }

    public void EnableAttraction(float radius)
    {
        _attractionRadius = radius;
    }

    public float AttractionRadius { get; private set; } = 0f;

    

    private void Update()
    {
        if (_playerTransform == null) return;

        transform.Rotate(0f, 90f * Time.deltaTime, 0f);

        // Récupère le rayon actuel depuis XPGemSpawner — fonctionne même pour les vieilles gemmes
        float currentRadius = XPGemSpawner.Instance != null ? XPGemSpawner.Instance.AttractionRadius : 0f;

        float dist = Vector3.Distance(transform.position, _playerTransform.position);

        if (dist <= currentRadius)
            _attracted = true;

        if (_attracted)
        {
            Vector3 dir = (_playerTransform.position - transform.position).normalized;
            transform.position += dir * _moveSpeed * Time.deltaTime;

            if (dist <= 0.5f)
                Collect();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
            Collect();
    }

    private void Collect()
    {
        XPSystem.Instance.AddXP(_xpValue);
        Destroy(gameObject);
    }
}