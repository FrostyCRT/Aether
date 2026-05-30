using UnityEngine;
using System.Collections.Generic;

public class LevelUpManager : MonoBehaviour
{
    public static LevelUpManager Instance { get; private set; }

    [Header("Références")]
    [SerializeField] private UpgradeData[] _allUpgrades;
    [SerializeField] private GameObject    _levelUpPanel;
    [SerializeField] private UpgradeUI     _upgradeUI;

    private List<UpgradeData> _currentChoices  = new List<UpgradeData>();
    private int               _pendingLevelUps  = 0;
    private bool              _waitingForChoice = false;
    private float             _delayTimer       = 0f;
    private bool              _showingDelay     = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Update()
    {
        // Délai entre deux level ups géré dans Update
        if (_showingDelay)
        {
            _delayTimer -= Time.unscaledDeltaTime; // Ignore timeScale
            if (_delayTimer <= 0f)
            {
                _showingDelay = false;
                DisplayLevelUp();
            }
            return;
        }

        if (_waitingForChoice)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1)) SelectUpgrade(0);
            if (Input.GetKeyDown(KeyCode.Alpha2)) SelectUpgrade(1);
            if (Input.GetKeyDown(KeyCode.Alpha3)) SelectUpgrade(2);
        }
    }

    public void ShowLevelUp()
    {
        _pendingLevelUps++;

        // Si déjà en train d'afficher un level up on attend
        if (_waitingForChoice || _showingDelay) return;

        DisplayLevelUp();
    }

    private void DisplayLevelUp()
    {
        if (_pendingLevelUps <= 0) return;

        _pendingLevelUps--;
        _waitingForChoice = true;
        Time.timeScale    = 0f;

        _currentChoices = GetRandomUpgrades(3);

        if (_levelUpPanel != null)
            _levelUpPanel.SetActive(true);

        _upgradeUI.DisplayUpgrades(_currentChoices);
    }

    public void SelectUpgrade(int index)
    {
        if (!_waitingForChoice) return;
        if (index < 0 || index >= _currentChoices.Count) return;

        _waitingForChoice = false;

        UpgradeData chosen = _currentChoices[index];
        chosen.Apply();

        if (_levelUpPanel != null)
            _levelUpPanel.SetActive(false);

        HealthSystem health = FindObjectOfType<HealthSystem>();
        if (health != null)
            health.SetInvincible();

        if (_pendingLevelUps > 0)
        {
            // Délai de 0.4 secondes avant le prochain level up
            _showingDelay = true;
            _delayTimer   = 0.4f;
        }
        else
        {
            Time.timeScale = 1f;
        }
    }

    private List<UpgradeData> GetRandomUpgrades(int count)
    {
        List<UpgradeData> pool = new List<UpgradeData>();
        foreach (UpgradeData upgrade in _allUpgrades)
        {
            if (upgrade.IsAvailable())
                pool.Add(upgrade);
        }

        List<UpgradeData> result = new List<UpgradeData>();
        count = Mathf.Min(count, pool.Count);

        for (int i = 0; i < count; i++)
        {
            int randomIndex = Random.Range(0, pool.Count);
            result.Add(pool[randomIndex]);
            pool.RemoveAt(randomIndex);
        }

        return result;
    }
}