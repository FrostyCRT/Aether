using UnityEngine;

public class XPGem : MonoBehaviour
{
    // Types de gemmes
    public enum GemType { Small, Medium, Large }
    private GemType _gemType;

    private float _xpValue = 10f;
    private bool _attracted = false;
    private float _moveSpeed = 8f;

    private Transform _playerTransform;
    private Renderer _renderer;
    private string _poolKey;

    // Cache de l'ID des propriétés de Shader pour éviter les allocations de string
    private static readonly int ColorID = Shader.PropertyToID("_BaseColor");
    private static readonly int EmissionColorID = Shader.PropertyToID("_EmissionColor");
    private static MaterialPropertyBlock _propBlock;

    private void Awake()
    {
        _renderer = GetComponent<Renderer>();
        if (_propBlock == null) _propBlock = new MaterialPropertyBlock();

        // On mémorise proprement le nom d'origine pour l'ObjectPool
        _poolKey = name.Replace("(Clone)", "").Trim();
    }

    public void Init(GemType type, Transform player)
    {
        _gemType = type;
        _playerTransform = player;
        _attracted = false; // RESET indispensable pour le recyclage du pool

        Color targetColor = Color.blue;
        switch (type)
        {
            case GemType.Small:
                _xpValue = 10f;
                transform.localScale = Vector3.one * 0.3f;
                targetColor = new Color(0.2f, 0.6f, 1f); // Bleue
                break;

            case GemType.Medium:
                _xpValue = 20f;
                transform.localScale = Vector3.one * 0.4f;
                targetColor = new Color(0.7f, 0.2f, 1f); // Violette
                break;

            case GemType.Large:
                _xpValue = 50f;
                transform.localScale = Vector3.one * 0.6f;
                targetColor = new Color(1f, 0.8f, 0.1f); // Dorée
                break;
        }

        // OPTIMISATION CRITIQUE : Modification de la couleur sans dupliquer le Material
        if (_renderer == null) _renderer = GetComponent<Renderer>();

        _renderer.GetPropertyBlock(_propBlock);
        _propBlock.SetColor(ColorID, targetColor);
        _propBlock.SetColor(EmissionColorID, targetColor * 2f);
        _renderer.SetPropertyBlock(_propBlock);
    }

    private void Update()
    {
        // On bloque le mouvement si le jeu est en pause globale ou game over
        if (GameManager.Instance != null && (GameManager.Instance.IsPaused || GameManager.Instance.IsGameOver)) return;
        if (_playerTransform == null) return;

        // Rotation constante simple
        transform.Rotate(0f, 90f * Time.deltaTime, 0f);

        // Vecteur vers le joueur
        Vector3 offset = _playerTransform.position - transform.position;

        // OPTIMISATION : Utilisation du carré de la distance (SqrMagnitude, pas de racine carrée)
        float sqrDist = offset.sqrMagnitude;

        if (!_attracted)
        {
            float currentRadius = XPGemSpawner.Instance != null ? XPGemSpawner.Instance.AttractionRadius : 0f;
            if (sqrDist <= currentRadius * currentRadius)
            {
                _attracted = true;
            }
        }

        if (_attracted)
        {
            // Déplacement vers le joueur
            Vector3 dir = offset.normalized;
            transform.position += dir * _moveSpeed * Time.deltaTime;

            // Seuil de collecte physique (0.5f au carré = 0.25f)
            if (sqrDist <= 0.25f)
            {
                Collect();
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
            Collect();
    }

    private void Collect()
    {
        // Sécurité pour éviter un double ramassage à la même frame avant la désactivation
        if (!gameObject.activeSelf) return;

        if (XPSystem.Instance != null)
        {
            XPSystem.Instance.AddXP(_xpValue);
        }

        // CORRECTION COMPLÈTE DU POOL : On désactive ET on retourne au pool proprement
        gameObject.SetActive(false);
        if (ObjectPool.Instance != null)
        {
            ObjectPool.Instance.ReturnToPool(_poolKey, gameObject);
        }
    }
}