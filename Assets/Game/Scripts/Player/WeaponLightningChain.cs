using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class WeaponLightningChain : MonoBehaviour
{
    [Header("Stats")]
    [SerializeField] private float _damage = 20f;
    [SerializeField] private float _chainRange = 4f;  // Distance de propagation
    [SerializeField] private int _maxChains = 3;   // Nombre de rebonds max
    [SerializeField] private float _fireRate = 1f;
    [SerializeField] private float _detectionRange = 15f;

    [Header("Limites")]
    [SerializeField] private int _maxChainUpgrades = 3; // Max 3 upgrades de chaîne
    private int _chainUpgradeCount = 0;

    public bool IsMaxChain() => _chainUpgradeCount >= _maxChainUpgrades;

    public void AddChain()
    {
        if (IsMaxChain()) return;
        _maxChains++;
        _chainUpgradeCount++;
    }

    private float _cooldownTimer = 0f;

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

    private Transform FindNearestEnemy()
    {
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
        Transform nearest = null;
        float minDist = _detectionRange;

        foreach (GameObject enemy in enemies)
        {
            float dist = Vector3.Distance(transform.position, enemy.transform.position);
            if (dist < minDist) { minDist = dist; nearest = enemy.transform; }
        }
        return nearest;
    }

    private Transform FindNextChainTarget(Vector3 from, List<GameObject> alreadyHit)
    {
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
        Transform nearest = null;
        float minDist = _chainRange;

        foreach (GameObject enemy in enemies)
        {
            if (alreadyHit.Contains(enemy)) continue;
            float dist = Vector3.Distance(from, enemy.transform.position);
            if (dist < minDist) { minDist = dist; nearest = enemy.transform; }
        }
        return nearest;
    }

    public void AddDamage(float value) => _damage += _damage * value;
    public void AddFireRate(float value) => _fireRate += _fireRate * value;
}