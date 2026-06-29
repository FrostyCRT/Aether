using UnityEngine;
using System.Collections;

public class CrystalSystem : MonoBehaviour
{
    [Header("Jauge")]
    [SerializeField] private int _maxCharges = 6;

    [Header("Ulti")]
    [SerializeField] private float _ultDamage = 50f;
    [SerializeField] private float _ultRange = 10f;
    [SerializeField] private float _slowFactor = 0.3f;
    [SerializeField] private float _slowDuration = 3f;

    [Header("Nova")]
    [SerializeField] private float _novaDamage = 10f;
    [SerializeField] private float _novaRadius = 3f;
    [SerializeField] private GameObject _novaVFXPrefab;

    private int _currentCharges = 0;
    private bool _isReady = false;
    private bool _overpowerActive = false; // Empêche les stacks infinis

    public int CurrentCharges => _currentCharges;
    public int MaxCharges => _maxCharges;
    public bool IsReady => _isReady;

    private void Start()
    {
        float crystalBonus = MetaProgressionManager.Instance.GetBonusCrystalDamage();
        _ultDamage += _ultDamage * crystalBonus;
        _novaDamage += _novaDamage * crystalBonus;

        float novaBonus = MetaProgressionManager.Instance.GetBonusNovaRadius();
        _novaRadius += _novaRadius * novaBonus;

        if (MetaProgressionManager.Instance.HasCrystalMastery())
            _maxCharges = Mathf.Max(_maxCharges - 1, 2);
    }

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

        TriggerNova();

        if (_currentCharges >= _maxCharges)
        {
            _isReady = true;
            GameUI.Instance.SetCrystalReady(true);
        }
    }

    private void TriggerNova()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, _novaRadius);
        foreach (Collider hit in hits)
        {
            if (hit.CompareTag("Enemy"))
            {
                EnemyBase eb = hit.GetComponent<EnemyBase>();
                if (eb != null) eb.TakeDamage(_novaDamage, DamageNumberSpawner.ColorCritical, true);

                BossBase boss = hit.GetComponent<BossBase>();
                if (boss != null) boss.TakeDamage(_novaDamage);
            }
        }

        if (_novaVFXPrefab != null)
            StartCoroutine(ShowNovaVFX());
    }

    private IEnumerator ShowNovaVFX()
    {
        GameObject vfx = Instantiate(_novaVFXPrefab, transform.position, Quaternion.identity);

        float duration = 0.3f;
        float elapsed = 0f;

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
        _isReady = false;
        _currentCharges = 0;
        GameUI.Instance.SetCrystalReady(false);
        GameUI.Instance.UpdateCrystalCharge(0, _maxCharges);

        DamageAllEnemies();
        StartCoroutine(SlowAllEnemies());

        if (MetaProgressionManager.Instance.HasOverpower() && !_overpowerActive)
            StartCoroutine(OverpowerBuff());
    }

    private IEnumerator OverpowerBuff()
    {
        _overpowerActive = true;

        WeaponBase wb = GetComponent<WeaponBase>();
        if (wb != null) wb.SetDamageMultiplier(2f); // x2 dégâts pendant 5s

        yield return new WaitForSeconds(5f);

        if (wb != null) wb.SetDamageMultiplier(1f); // Retour normal
        _overpowerActive = false;
    }

    private void DamageAllEnemies()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, _ultRange);
        foreach (Collider hit in hits)
        {
            if (hit.CompareTag("Enemy"))
            {
                EnemyBase eb = hit.GetComponent<EnemyBase>();
                if (eb != null) eb.TakeDamage(_ultDamage, DamageNumberSpawner.ColorCritical);

                BossBase boss = hit.GetComponent<BossBase>();
                if (boss != null) boss.TakeDamage(_ultDamage);
            }
        }
    }

    private IEnumerator SlowAllEnemies()
    {
        // Récupère exactement les ennemis touchés au moment du cast
        Collider[] hits = Physics.OverlapSphere(transform.position, _ultRange);
        foreach (Collider hit in hits)
        {
            if (hit.CompareTag("Enemy"))
            {
                EnemyBase eb = hit.GetComponent<EnemyBase>();
                if (eb != null) eb.SetSpeedMultiplier(_slowFactor);
            }
        }

        GameUI.Instance.ShowUltEffect(true);
        yield return new WaitForSeconds(_slowDuration);

        // Remet uniquement les ennemis qui avaient été ralentis
        foreach (Collider hit in hits)
        {
            if (hit == null) continue; // L'ennemi est peut-être mort entre-temps
            if (hit.CompareTag("Enemy"))
            {
                EnemyBase eb = hit.GetComponent<EnemyBase>();
                if (eb != null) eb.SetSpeedMultiplier(1f);
            }
        }

        GameUI.Instance.ShowUltEffect(false);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, _ultRange);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _novaRadius);
    }
}