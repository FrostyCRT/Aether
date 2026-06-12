using UnityEngine;
using System.Collections;

public class CrystalSystem : MonoBehaviour
{
    [Header("Jauge")]
    [SerializeField] private int _maxCharges = 6;

    [Header("Ulti")]
    [SerializeField] private float _ultDamage   = 50f;
    [SerializeField] private float _ultRange    = 10f;
    [SerializeField] private float _slowFactor  = 0.3f;
    [SerializeField] private float _slowDuration = 3f;

    [Header("Nova")]
    [SerializeField] private float _novaDamage  = 10f;
    [SerializeField] private float _novaRadius  = 3f;
    [SerializeField] private GameObject _novaVFXPrefab; // Prefab visuel de la nova

    private int  _currentCharges = 0;
    private bool _isReady        = false;

    public int  CurrentCharges => _currentCharges;
    public int  MaxCharges     => _maxCharges;
    public bool IsReady        => _isReady;

    private void Update()
    {
        if (GameManager.Instance == null || GameManager.Instance.IsGameOver) return;
        if (_isReady && Input.GetKeyDown(KeyCode.F))
            TriggerUlt();
    }

    public void AbsorbProjectile()
    {
        if (_currentCharges >= _maxCharges) return;
        _currentCharges++;
        GameUI.Instance.UpdateCrystalCharge(_currentCharges, _maxCharges);

        // Nova à chaque absorption
        TriggerNova();

        if (_currentCharges >= _maxCharges)
        {
            _isReady = true;
            GameUI.Instance.SetCrystalReady(true);
        }
    }

    private void TriggerNova()
    {
        // Dégâts
        Collider[] hits = Physics.OverlapSphere(transform.position, _novaRadius);
        foreach (Collider hit in hits)
        {
            if (hit.CompareTag("Enemy"))
            {
                EnemyBase eb = hit.GetComponent<EnemyBase>();
                if (eb != null) eb.TakeDamage(_novaDamage, DamageNumberSpawner.ColorCritical);

                BossBase boss = hit.GetComponent<BossBase>();
                if (boss != null) boss.TakeDamage(_novaDamage);
            }
        }

        // Visuel
        if (_novaVFXPrefab != null)
            StartCoroutine(ShowNovaVFX());
    }

    private IEnumerator ShowNovaVFX()
    {
        GameObject vfx = Instantiate(_novaVFXPrefab, transform.position, Quaternion.identity);
        
        // Animation d'expansion puis disparition
        float duration = 0.3f;
        float elapsed  = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float scale = Mathf.Lerp(0f, _novaRadius * 2f, elapsed / duration);
            vfx.transform.localScale = new Vector3(scale, 0.05f, scale);
            yield return null;
        }

        Destroy(vfx);
    }

    private void TriggerUlt()
    {
        _isReady        = false;
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
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _novaRadius);
    }
}