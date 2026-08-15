using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class WeaponLightningChain : MonoBehaviour
{
    [Header("Stats")]
    [SerializeField] private float _damage = 15f;
    [SerializeField] private float _chainRange = 4f;  // Distance de propagation
    // MODIFIÉ — 3 → 2 : point de départ au déblocage (1er pick de la carte Lightning).
    // Les 3 paliers d'amélioration suivants ajoutent +1 rebond chacun via AddChain() → max 5.
    [SerializeField] private int _maxChains = 2;
    [SerializeField] private float _fireRate = 1f;
    [SerializeField] private float _detectionRange = 15f;

    [Header("Limites")]
    // MODIFIÉ — commentaire : ce plafond correspond exactement aux 3 paliers d'amélioration
    // gérés par UpgradeData (2 au déblocage + 3 = 5 max). Remonté par sécurité à 10 pour éviter
    // tout blocage silencieux si le design évolue, la vraie limite reste UpgradeData/LevelUpManager.
    [SerializeField] private int _maxChainUpgrades = 10;
    private int _chainUpgradeCount = 0;
    public bool IsMaxChain() => _chainUpgradeCount >= _maxChainUpgrades;

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

    // AJOUTÉ — buffer réutilisable pour aligner cette arme sur le pattern non-alloc des autres
    // armes (WeaponFireball/WeaponShurikenBarrage), au lieu de FindGameObjectsWithTag qui alloue
    // un nouveau tableau à chaque appel. Sur un run de 15 min avec beaucoup d'ennemis à l'écran
    // en fin de partie, ça évite des pics de garbage collection.
    private static readonly Collider[] _detectionBuffer = new Collider[64];

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
        List<GameObject> hit = new List<GameObject>();
        Transform current = firstTarget;
        for (int i = 0; i <= _maxChains; i++)
        {
            if (current == null) break;
            // Dégâts dégressifs — chaque rebond fait moins de dégâts
            float damage = _damage * Mathf.Pow(0.7f, i);
            EnemyBase eb = current.GetComponent<EnemyBase>();
            if (eb != null) eb.TakeDamage(damage, DamageNumberSpawner.ColorCritical);
            BossBase boss = current.GetComponent<BossBase>();
            if (boss != null) boss.TakeDamage(damage, DamageNumberSpawner.ColorCritical);
            hit.Add(current.gameObject);
            // VFX éclair simple — ligne jaune en Gizmo pour l'instant
            Debug.DrawLine(
                i == 0 ? transform.position : hit[i - 1].transform.position,
                current.position,
                Color.yellow, 0.1f
            );
            // Cherche le prochain ennemi proche non encore touché
            current = FindNextChainTarget(current.position, hit);
            yield return new WaitForSeconds(0.05f); // Délai visuel entre chaque rebond
        }
    }

    // MODIFIÉ — remplace FindGameObjectsWithTag("Enemy") par OverlapSphereNonAlloc + buffer partagé
    private Transform FindNearestEnemy()
    {
        int count = Physics.OverlapSphereNonAlloc(transform.position, _detectionRange, _detectionBuffer);
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

    // MODIFIÉ — même remplacement, en excluant les ennemis déjà touchés dans cette chaîne
    private Transform FindNextChainTarget(Vector3 from, List<GameObject> alreadyHit)
    {
        int count = Physics.OverlapSphereNonAlloc(from, _chainRange, _detectionBuffer);
        Transform nearest = null;
        float minDistSqr = _chainRange * _chainRange;
        for (int i = 0; i < count; i++)
        {
            Collider col = _detectionBuffer[i];
            if (col == null || !col.CompareTag("Enemy")) continue;
            if (alreadyHit.Contains(col.gameObject)) continue;
            float distSqr = (col.transform.position - from).sqrMagnitude;
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
}