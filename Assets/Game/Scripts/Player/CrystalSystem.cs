using UnityEngine;
using System.Collections;

public class CrystalSystem : MonoBehaviour
{
    [Header("Jauge")]
    [SerializeField] private int   _maxCharges    = 15;
    [SerializeField] private float _ultDamage     = 50f;
    [SerializeField] private float _slowFactor    = 0.3f; // 30% de la vitesse normale
    [SerializeField] private float _slowDuration  = 3f;

    private int  _currentCharges = 0;
    private bool _isReady        = false;

    public int  CurrentCharges => _currentCharges;
    public int  MaxCharges     => _maxCharges;
    public bool IsReady        => _isReady;

    private void Update()
    {
        if (GameManager.Instance == null || GameManager.Instance.IsGameOver) return;

        if (_isReady)
        {
            Debug.Log($"Ulti prêt — appuie sur E | GetKeyDown E : {Input.GetKeyDown(KeyCode.E)}");
            if (Input.GetKeyDown(KeyCode.E))
                TriggerUlt();
        }
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
        _isReady        = false;
        _currentCharges = 0;

        // D'abord reset les cristaux en gris
        GameUI.Instance.SetCrystalReady(false);
        // Puis met à jour le compteur
        GameUI.Instance.UpdateCrystalCharge(0, _maxCharges);

        DamageAllEnemies();
        StartCoroutine(SlowAllEnemies());

        Debug.Log("ULTI DÉCLENCHÉ !");
    }

    private void DamageAllEnemies()
    {
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
        foreach (GameObject enemy in enemies)
        {
            EnemyBase eb = enemy.GetComponent<EnemyBase>();
            if (eb != null) eb.TakeDamage(_ultDamage);

            BossBase boss = enemy.GetComponent<BossBase>();
            if (boss != null) boss.TakeDamage(_ultDamage);
        }
    }

    private IEnumerator SlowAllEnemies()
    {
        // Applique le ralentissement
        SetEnemySpeedMultiplier(_slowFactor);
        GameUI.Instance.ShowUltEffect(true);

        yield return new WaitForSeconds(_slowDuration);

        // Retire le ralentissement
        SetEnemySpeedMultiplier(1f);
        GameUI.Instance.ShowUltEffect(false);
    }

    private void SetEnemySpeedMultiplier(float multiplier)
    {
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
        foreach (GameObject enemy in enemies)
        {
            EnemyBase eb = enemy.GetComponent<EnemyBase>();
            if (eb != null) eb.SetSpeedMultiplier(multiplier);
        }
    }
}