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

    [Header("Ulti — VFX")]
    [SerializeField] private GameObject _ultVFXPrefab;
    [SerializeField] private float _ultVFXDuration = 0.55f;
    [SerializeField] private float _hitstopDuration = 0.08f;

    [Header("Ulti — Absorption XP")]
    [SerializeField] private float _gemAttractRange = 14f;

    private int _currentCharges = 0;
    private int _storedUlts = 0; // 0 = rien, 1 = ULT x1 dispo, 2 = ULT x2 dispo
    private bool _overpowerActive = false;

    public int CurrentCharges => _currentCharges;
    public int MaxCharges => _maxCharges;

    private void Start()
    {
        float crystalBonus = MetaProgressionManager.Instance.GetBonusCrystalDamage();
        _ultDamage += _ultDamage * crystalBonus;
        _novaDamage += _novaDamage * crystalBonus;

        float novaBonus = MetaProgressionManager.Instance.GetBonusNovaRadius();
        _novaRadius += _novaRadius * novaBonus;

        if (MetaProgressionManager.Instance.HasCrystalMastery())
            _maxCharges = Mathf.Max(_maxCharges - 1, 2);

        GameUI.Instance.UpdateCrystalCharge(_currentCharges, _maxCharges);
        GameUI.Instance.UpdateUltStack(0);
    }

    private void Update()
    {
        if (GameManager.Instance == null || GameManager.Instance.IsGameOver) return;

        // Peut déclencher l'ult dès qu'on a au moins 1 ult stocké
        if (_storedUlts >= 1 && Input.GetKeyDown(KeyCode.F))
            TriggerUlt();
    }

    public void AbsorbProjectile()
    {
        TriggerNova();

        // Déjà au max (2 barres remplies) → nova seulement, rien d'autre
        if (_storedUlts >= 2) return;

        _currentCharges++;

        if (_currentCharges >= _maxCharges)
        {
            // Barre complétée → on stocke un ult, on remet la jauge à 0
            _storedUlts++;
            _currentCharges = 0;

            // Affiche la barre pleine/blanche
            GameUI.Instance.UpdateCrystalCharge(_maxCharges, _maxCharges);
            GameUI.Instance.SetCrystalReady(true);
            GameUI.Instance.UpdateUltStack(_storedUlts); // "ULT x1" ou "ULT x2"
        }
        else
        {
            // En train de charger la 2ème barre : retire le visuel "prêt" blanc
            if (_storedUlts == 1)
                GameUI.Instance.SetCrystalReady(false);

            GameUI.Instance.UpdateCrystalCharge(_currentCharges, _maxCharges);
        }
    }

    private void TriggerUlt()
    {
        bool isEmpowered = _storedUlts >= 2;

        if (isEmpowered)
        {
            // Ult x2 : reset complet
            _storedUlts = 0;
            _currentCharges = 0;
            GameUI.Instance.SetCrystalReady(false);
            GameUI.Instance.UpdateCrystalCharge(0, _maxCharges);
            GameUI.Instance.UpdateUltStack(0);
            TriggerEmpoweredUlt();
        }
        else
        {
            // Ult x1 : on garde la progression de la 2ème barre
            int savedCharges = _currentCharges;
            _storedUlts = 0;
            GameUI.Instance.SetCrystalReady(false);
            GameUI.Instance.UpdateUltStack(0);
            _currentCharges = savedCharges;
            GameUI.Instance.UpdateCrystalCharge(_currentCharges, _maxCharges);
            TriggerNormalUlt();
        }

        if (MetaProgressionManager.Instance.HasOverpower() && !_overpowerActive)
            StartCoroutine(OverpowerBuff());
    }

    private void TriggerNormalUlt()
    {
        DamageAllEnemies(1f);
        StartCoroutine(SlowAllEnemies());
        AttractGems(_gemAttractRange, fast: false);
        StartCoroutine(ShowUltVFX());
        StartCoroutine(HitstopRoutine());
    }

    private void TriggerEmpoweredUlt()
    {
        // Tue TOUS les ennemis de la map
        EnemyBase[] allEnemies = FindObjectsOfType<EnemyBase>();
        foreach (EnemyBase enemy in allEnemies)
        {
            if (enemy != null)
                enemy.TakeDamage(100f, DamageNumberSpawner.ColorCritical);
        }

        // Dégâts x2 sur les boss dans _ultRange
        Collider[] hits = Physics.OverlapSphere(transform.position, _ultRange);
        foreach (Collider hit in hits)
        {
            BossBase boss = hit.GetComponent<BossBase>();
            if (boss != null) boss.TakeDamage(_ultDamage * 2f);
        }

        StartCoroutine(SlowAllEnemies());
        AttractGems(float.MaxValue, fast: true); // toutes les gemmes, vitesse augmentée
        StartCoroutine(ShowUltVFX());
        StartCoroutine(HitstopRoutine());
    }

    private void DamageAllEnemies(float multiplier = 1f)
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, _ultRange);
        foreach (Collider hit in hits)
        {
            if (hit.CompareTag("Enemy"))
            {
                EnemyBase eb = hit.GetComponent<EnemyBase>();
                if (eb != null) eb.TakeDamage(_ultDamage * multiplier, DamageNumberSpawner.ColorCritical);

                BossBase boss = hit.GetComponent<BossBase>();
                if (boss != null) boss.TakeDamage(_ultDamage * multiplier);
            }
        }
    }

    private void AttractGems(float range, bool fast = false)
    {
        XPGem[] allGems = FindObjectsOfType<XPGem>();
        foreach (XPGem gem in allGems)
        {
            if (gem == null) continue;
            if (range >= float.MaxValue ||
                Vector3.Distance(transform.position, gem.transform.position) <= range)
            {
                if (fast) gem.ForceAttractFast();
                else gem.ForceAttract();
            }
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
        float elapsed = 0f;
        float duration = 0.3f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float scale = Mathf.Lerp(0f, _novaRadius * 2f, elapsed / duration);
            vfx.transform.localScale = new Vector3(scale, 0.05f, scale);
            yield return null;
        }
        Destroy(vfx);
    }

    private IEnumerator ShowUltVFX()
    {
        if (_ultVFXPrefab == null) yield break;

        GameObject vfx = Instantiate(_ultVFXPrefab, transform.position, Quaternion.identity);
        float elapsed = 0f;

        while (elapsed < _ultVFXDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float scale = Mathf.Lerp(0f, _ultRange * 2f, elapsed / _ultVFXDuration);
            vfx.transform.localScale = new Vector3(scale, 0.05f, scale);
            yield return null;
        }
        Destroy(vfx);
    }

    private IEnumerator HitstopRoutine()
    {
        Time.timeScale = 0.05f;
        yield return new WaitForSecondsRealtime(_hitstopDuration);
        Time.timeScale = 1f;
    }

    private IEnumerator SlowAllEnemies()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, _ultRange);
        foreach (Collider hit in hits)
        {
            EnemyBase eb = hit.GetComponent<EnemyBase>();
            if (eb != null) eb.SetSpeedMultiplier(_slowFactor);

            BossBase boss = hit.GetComponent<BossBase>();
            if (boss != null) boss.SetSpeedMultiplier(_slowFactor);
        }

        GameUI.Instance.ShowUltEffect(true);
        yield return new WaitForSeconds(_slowDuration);

        foreach (Collider hit in hits)
        {
            if (hit == null) continue;

            EnemyBase eb = hit.GetComponent<EnemyBase>();
            if (eb != null) eb.SetSpeedMultiplier(1f);

            BossBase boss = hit.GetComponent<BossBase>();
            if (boss != null) boss.SetSpeedMultiplier(1f);
        }

        GameUI.Instance.ShowUltEffect(false);
    }

    private IEnumerator OverpowerBuff()
    {
        _overpowerActive = true;
        WeaponBase wb = GetComponent<WeaponBase>();
        if (wb != null) wb.SetDamageMultiplier(2f);
        yield return new WaitForSeconds(5f);
        if (wb != null) wb.SetDamageMultiplier(1f);
        _overpowerActive = false;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, _ultRange);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _novaRadius);
    }
}