using UnityEngine;

public class DamageNumberSpawner : MonoBehaviour
{
    public static DamageNumberSpawner Instance { get; private set; }

    [SerializeField] private GameObject _damageNumberPrefab;

    // Couleurs selon l'arme
    public static readonly Color ColorProjectile = new Color(0f,   0.8f, 1f);   // Bleu cyan
    public static readonly Color ColorAOE        = new Color(0.4f, 0.9f, 0.2f); // Vert
    public static readonly Color ColorOrbital    = new Color(0.9f, 0.9f, 0.9f); // Blanc
    public static readonly Color ColorCritical   = new Color(1f,   0.6f, 0f);   // Orange doré
    public static readonly Color ColorPlayer     = new Color(1f,   0.2f, 0.2f); // Rouge

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void Spawn(Vector3 position, float damage, Color color, bool isCritical = false)
    {
        if (_damageNumberPrefab == null) return;

        GameObject go = Instantiate(
            _damageNumberPrefab,
            position + Vector3.up * 1.5f,
            Quaternion.identity
        );

        DamageNumber dn = go.GetComponent<DamageNumber>();
        if (dn != null) dn.Init(damage, color, isCritical);
    }
}