using UnityEngine;
using System.Collections;

public class CrystalSystem : MonoBehaviour
{
    [Header("Jauge")]
    [SerializeField] private int _maxCharges = 15;

    [Header("Ulti")]
    [SerializeField] private float _ultDamage = 50f;
    [SerializeField] private float _ultRange = 10f;
    [SerializeField] private float _slowFactor = 0.3f;
    [SerializeField] private float _slowDuration = 3f;

    private int _currentCharges = 0;
    private bool _isReady = false;

    public int CurrentCharges => _currentCharges;
    public int MaxCharges => _maxCharges;
    public bool IsReady => _isReady;

    private void Update()
    {
        if (GameManager.Instance == null || GameManager.Instance.IsGameOver) return;

        if (_isReady && Input.GetKeyDown(KeyCode.E))
            TriggerUlt();
    }

    public void AbsorbProjectile()
    {
        if (_currentCharges >= _maxCharges) return;

        _currentCharges++;
        GameUI.Instance.UpdateCrystalCharge(_currentCharges, _maxCharges);

        if (_currentCharges >= _maxCharges)
        {
            _isReady = true;
            GameUI.Instance.SetCrystalReady(true);
        }
    }

    private void TriggerUlt()
    {
        _isReady = false;
        _currentCharges = 0;

        GameUI.Instance.SetCrystalReady(false);
        GameUI.Instance.UpdateCrystalCharge(0, _maxCharges);

        DamageAllEnemies();
        StartCoroutine(SlowAllEnemies());

        Debug.Log("ULTI DÉCLENCHÉ !");
    }

    private void DamageAllEnemies()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, _ultRange);
        int count = 0;

        foreach (Collider hit in hits)
        {
            if (hit.CompareTag("Enemy"))
            {
                EnemyBase eb = hit.GetComponent<EnemyBase>();
                if (eb != null) eb.TakeDamage(_ultDamage, DamageNumberSpawner.ColorCritical);

                BossBase boss = hit.GetComponent<BossBase>();
                if (boss != null) boss.TakeDamage(_ultDamage);

                count++;
            }
        }
        Debug.Log($"Ulti — {count} ennemis touchés dans un rayon de {_ultRange}");
    }

    private IEnumerator SlowAllEnemies()
    {
        SetEnemySpeedMultiplier(_slowFactor);
        GameUI.Instance.ShowUltEffect(true);

        yield return new WaitForSeconds(_slowDuration);

        SetEnemySpeedMultiplier(1f);
        GameUI.Instance.ShowUltEffect(false);
    }

    private void SetEnemySpeedMultiplier(float multiplier)
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, _ultRange);
        foreach (Collider hit in hits)
        {
            if (hit.CompareTag("Enemy"))
            {
                EnemyBase eb = hit.GetComponent<EnemyBase>();
                if (eb != null) eb.SetSpeedMultiplier(multiplier);
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, _ultRange);
    }
}