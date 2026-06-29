using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class WeaponLightningChain : MonoBehaviour
{
    [Header("Références & Prefabs")]
    [SerializeField] private LayerMask _enemyLayer; // À assigner sur le calque "Enemy"
    private GameObject _lightningVFXPrefab; // Assigné dynamiquement via Init()

    [Header("Stats de Base")]
    [SerializeField] private float _baseDamage = 20f;
    [SerializeField] private float _chainRange = 4f;
    [SerializeField] private int _maxChains = 3;
    [SerializeField] private float _baseFireRate = 1f;
    [SerializeField] private float _detectionRange = 15f;

    [Header("Limites")]
    [SerializeField] private int _maxChainUpgrades = 3;
    private int _chainUpgradeCount = 0;

    // Stats réelles calculées
    private float _damage;
    private float _fireRate;
    private float _cooldownTimer = 0f;

    // Buffer d'optimisation mémoire pour les OverlapSphere
    private Collider[] _detectionBuffer = new Collider[100];

    public bool IsMaxChain() => _chainUpgradeCount >= _maxChainUpgrades;

    // CORRECTION : Implémentation de la méthode Init demandée par PlayerUpgrades
    public void Init(GameObject prefab)
    {
        _lightningVFXPrefab = prefab;
    }

    private void Start()
    {
        _damage = _baseDamage;
        _fireRate = _baseFireRate;
    }

    public void AddChain()
    {
        if (IsMaxChain()) return;
        _maxChains++;
        _chainUpgradeCount++;
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
                StartCoroutine(ChainLightning(nearest));
                _cooldownTimer = 0f;
            }
        }
    }

    private IEnumerator ChainLightning(Transform firstTarget)
    {
        // On stocke les instances de transform valides pour l'historique des positions
        List<Transform> hitHistory = new List<Transform>();
        Transform current = firstTarget;

        Vector3 lastValidPosition = transform.position;

        for (int i = 0; i <= _maxChains; i++)
        {
            // SÉCURITÉ : Si la cible actuelle a été détruite entre-temps
            if (current == null) break;

            Vector3 currentTargetPos = current.position;
            float currentDamage = _damage * Mathf.Pow(0.7f, i);

            // Application des dégâts
            EnemyBase eb = current.GetComponent<EnemyBase>();
            if (eb != null) eb.TakeDamage(currentDamage, DamageNumberSpawner.ColorCritical);

            BossBase boss = current.GetComponent<BossBase>();
            if (boss != null) boss.TakeDamage(currentDamage, DamageNumberSpawner.ColorCritical);

            hitHistory.Add(current);

            // Rendu visuel temporaire (Debug) ou instanciation de ton VFX
            Vector3 originPos = (i == 0) ? transform.position : lastValidPosition;
            Debug.DrawLine(originPos, currentTargetPos, Color.yellow, 0.1f);

            // Sauvegarde de la dernière position valide connue avant le délai
            lastValidPosition = currentTargetPos;

            // Recherche sécurisée de la cible suivante
            current = FindNextChainTarget(currentTargetPos, hitHistory);

            yield return new WaitForSeconds(0.05f);
        }
    }

    // CORRECTION PERFORMANCE : Utilisation d'OverlapSphereNonAlloc
    private Transform FindNearestEnemy()
    {
        int numColliders = Physics.OverlapSphereNonAlloc(transform.position, _detectionRange, _detectionBuffer, _enemyLayer);
        Transform nearest = null;
        float minDist = _detectionRange;

        for (int i = 0; i < numColliders; i++)
        {
            if (_detectionBuffer[i] == null) continue;
            float dist = Vector3.Distance(transform.position, _detectionBuffer[i].transform.position);
            if (dist < minDist)
            {
                minDist = dist;
                nearest = _detectionBuffer[i].transform;
            }
        }
        return nearest;
    }

    // CORRECTION PERFORMANCE : Plus aucun FindGameObjectsWithTag dans les rebonds !
    private Transform FindNextChainTarget(Vector3 from, List<Transform> alreadyHit)
    {
        int numColliders = Physics.OverlapSphereNonAlloc(from, _chainRange, _detectionBuffer, _enemyLayer);
        Transform nearest = null;
        float minDist = _chainRange;

        for (int i = 0; i < numColliders; i++)
        {
            Transform t = _detectionBuffer[i].transform;
            if (t == null || alreadyHit.Contains(t)) continue;

            float dist = Vector3.Distance(from, t.position);
            if (dist < minDist)
            {
                minDist = dist;
                nearest = t;
            }
        }
        return nearest;
    }

    public void AddDamage(float value)
    {
        _baseDamage += _baseDamage * value;
        _damage = _baseDamage;
    }

    public void AddFireRate(float value)
    {
        _baseFireRate += _baseFireRate * value;
        _fireRate = _baseFireRate;
    }
}