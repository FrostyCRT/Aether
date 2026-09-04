using UnityEngine;
using System.Collections;

public class CrystalSystem : MonoBehaviour
{
    [Header("Jauge")]
    [SerializeField] private int _maxCharges = 6;

    [Header("Ulti")]
    [SerializeField] private float _ultDamage = 500f; // MODIFIE - x10, cf. rescale global des degats/PV
    [SerializeField] private float _ultRange = 10f;
    [SerializeField] private float _slowFactor = 0.3f;
    [SerializeField] private float _slowDuration = 3f;

    // AJOUTE - l'ultime devenait mecaniquement de moins en moins efficace au fil
    // d'une run, puisque les degats etaient fixes alors que les PV ennemis
    // scalent jusqu'a x5. Scaling lineaire x1 -> x4 sur 15 minutes (duree de run
    // de reference), plafonne au-dela pour eviter une inflation infinie sur une
    // run anormalement longue. x4 plutot que x5 pile pour rester legerement sous
    // le scaling des PV ennemis - l'ultime reste un vrai temps fort, pas un
    // bouton qui trivialise tout.
    [Header("Scaling de l'ultime dans le temps")]
    [SerializeField] private float _ultScaleRampDuration = 900f; // 15 minutes
    [SerializeField] private float _ultScaleMaxMultiplier = 4f;

    [Header("Nova")]
    [SerializeField] private float _novaDamage = 100f; // MODIFIE - x10, cf. rescale global des degats/PV
    [SerializeField] private float _novaRadius = 3f;
    [SerializeField] private GameObject _novaVFXPrefab;

    [Header("Ulti — VFX")]
    [SerializeField] private GameObject _ultVFXPrefab;
    [SerializeField] private float _ultVFXDuration = 0.55f;
    [SerializeField] private float _hitstopDuration = 0.08f;

    [Header("Ulti — Absorption XP")]
    [SerializeField] private float _gemAttractRange = 14f;

    private int _currentCharges = 0;
    private int _storedUlts = 0;
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

    // AJOUTE - multiplicateur de degats de l'ultime selon le temps de survie
    // ecoule cette run. 1f au debut, monte lineairement jusqu'a
    // _ultScaleMaxMultiplier a _ultScaleRampDuration secondes, plafonne ensuite.
    private float GetUltDamageScale()
    {
        if (GameManager.Instance == null || _ultScaleRampDuration <= 0f) return 1f;

        float t = Mathf.Clamp01(GameManager.Instance.RunTimer / _ultScaleRampDuration);
        return Mathf.Lerp(1f, _ultScaleMaxMultiplier, t);
    }

    private void Update()
    {
        if (GameManager.Instance == null || GameManager.Instance.IsGameOver) return;

        if (_storedUlts >= 1 && Input.GetKeyDown(KeyCode.F))
            TriggerUlt();
    }

    public void AbsorbProjectile()
    {
        TriggerNova();

        if (_storedUlts >= 2) return;

        _currentCharges++;

        if (_currentCharges >= _maxCharges)
        {
            _storedUlts++;
            _currentCharges = 0;

            GameUI.Instance.UpdateCrystalCharge(_maxCharges, _maxCharges);
            GameUI.Instance.SetCrystalReady(true);
            GameUI.Instance.UpdateUltStack(_storedUlts);
        }
        else
        {
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
            _storedUlts = 0;
            _currentCharges = 0;
            GameUI.Instance.SetCrystalReady(false);
            GameUI.Instance.UpdateCrystalCharge(0, _maxCharges);
            GameUI.Instance.UpdateUltStack(0);
            TriggerEmpoweredUlt();
        }
        else
        {
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
        // MODIFIE - passe le multiplicateur de scaling temporel a DamageAllEnemies,
        // au lieu du 1f fixe d'avant (qui ne faisait donc jamais rien).
        DamageAllEnemies(GetUltDamageScale());
        StartCoroutine(SlowAllEnemies());
        AttractGems(_gemAttractRange, fast: false);
        StartCoroutine(ShowUltVFX());
        StartCoroutine(HitstopRoutine());
    }

    private void TriggerEmpoweredUlt()
    {
        // MODIFIE - remplace le 100f fixe, completement deconnecte de _ultDamage
        // et du scaling temporel, par une vraie formule basee sur _ultDamage :
        // x5 de base (un vrai "wipe" garanti tot dans la run) multiplie par le
        // meme scaling temporel que le reste de l'ultime, pour rester efficace
        // contre des ennemis a PV scales en fin de run.
        float scale = GetUltDamageScale();
        float empoweredDamage = _ultDamage * 5f * scale;

        EnemyBase[] allEnemies = FindObjectsOfType<EnemyBase>();
        foreach (EnemyBase enemy in allEnemies)
        {
            if (enemy != null)
                enemy.TakeDamage(empoweredDamage, DamageNumberSpawner.ColorCritical);
        }

        // MODIFIE - x2 sur les boss, desormais lui aussi multiplie par le scaling
        // temporel (avant : x2 fixe, ne bougeait jamais avec la progression de la run).
        Collider[] hits = Physics.OverlapSphere(transform.position, _ultRange);
        foreach (Collider hit in hits)
        {
            BossBase boss = hit.GetComponent<BossBase>();
            if (boss != null) boss.TakeDamage(_ultDamage * 2f * scale);
        }

        StartCoroutine(SlowAllEnemies());
        AttractGems(float.MaxValue, fast: true);
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