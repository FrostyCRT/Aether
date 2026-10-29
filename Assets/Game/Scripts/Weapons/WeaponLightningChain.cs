using UnityEngine;

public class WeaponLightningChain : MonoBehaviour
{
    [Header("Stats")]
    [SerializeField] private float _damage = 150f; // MODIFIE - x10, cf. rescale global des degats/PV
    [SerializeField] private float _chainRange = 4f;
    [SerializeField] private int _maxChains = 2;
    [SerializeField] private float _fireRate = 1f;
    [SerializeField] private float _detectionRange = 15f;

    [Header("Limites")]
    [SerializeField] private int _maxChainUpgrades = 10;
    private int _chainUpgradeCount = 0;
    public bool IsMaxChain() => _chainUpgradeCount >= _maxChainUpgrades;
    public int ChainCount => _maxChains;   // rebonds après la 1re cible ; info-bulle du menu Pause

    public void AddChain()
    {
        if (IsMaxChain())
        {
            Debug.LogWarning("[WeaponLightningChain] AddChain() appelé alors que le plafond interne est atteint — vérifier la config UpgradeData/LevelUpManager, ce cas ne devrait jamais arriver en jeu normal.");
            return;
        }
        _maxChains++;
        _chainUpgradeCount++;
    }

    private float _cooldownTimer = 0f;

    private static readonly Collider[] _detectionBuffer = new Collider[64];

    // AJOUTE - meme trou que Fireball/Aura/Knives/Orbital : le bonus de
    // Reputation Degats n'etait jamais applique ici.
    private void Awake()
    {
        if (MetaProgressionManager.Instance != null)
        {
            float bonusDamage = MetaProgressionManager.Instance.GetReputationBonusDamage();
            _damage += _damage * bonusDamage;
        }
    }

    private void Update()
    {
        if (GameManager.Instance == null || GameManager.Instance.IsGameOver) return;
        if (GameManager.Instance.IsPaused) return;
        _cooldownTimer += Time.deltaTime;
        if (_cooldownTimer >= 1f / _fireRate)
        {
            Transform nearest = FindNearestEnemy();
            if (nearest != null)
            {
                LightningChainUtility.Trigger(this, nearest, _damage, _maxChains, _chainRange);   // cibles vérifiées entre deux sauts (voir l'utilitaire)
                _cooldownTimer = 0f;
            }
        }
    }

    private Transform FindNearestEnemy()
    {
        int count = Physics.OverlapSphereNonAlloc(transform.position, _detectionRange, _detectionBuffer, EnemyLayers.Mask);
        Transform nearest = null;
        float minDistSqr = _detectionRange * _detectionRange;
        for (int i = 0; i < count; i++)
        {
            Collider col = _detectionBuffer[i];
            if (col == null || !col.CompareTag("Enemy")) continue;
            float distSqr = (col.transform.position - transform.position).sqrMagnitude;
            if (distSqr < minDistSqr)
            {
                minDistSqr = distSqr;
                nearest = col.transform;
            }
        }
        return nearest;
    }

    public void AddDamage(float value) => _damage += _damage * value;
    public void AddFireRate(float value) => _fireRate += _fireRate * value;

    // Lu par UpgradeData.ApplyFusionResult() pour hériter des dégâts déjà accumulés au moment d'une fusion.
    public float CurrentDamage => _damage;
}