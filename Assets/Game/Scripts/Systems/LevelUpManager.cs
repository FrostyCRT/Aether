using UnityEngine;
using System.Collections.Generic;

public class LevelUpManager : MonoBehaviour
{
    public static LevelUpManager Instance { get; private set; }

    [Header("Références")]
    [SerializeField] private UpgradeData[] _allUpgrades;
    [SerializeField] private GameObject    _levelUpPanel;
    [SerializeField] private UpgradeUI     _upgradeUI;

    private List<UpgradeData> _currentChoices   = new List<UpgradeData>();
    private List<string>      _chosenUpgrades   = new List<string>(); // ← historique
    private int               _pendingLevelUps  = 0;
    private bool              _waitingForChoice = false;
    private float             _delayTimer       = 0f;
    private bool              _showingDelay     = false;
    public bool IsWaitingForChoice => _waitingForChoice;

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
        if (_showingDelay)
        {
            _delayTimer -= Time.unscaledDeltaTime;
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
        if (_waitingForChoice || _showingDelay) return;
        DisplayLevelUp();
    }

    private void DisplayLevelUp()
    {
        if (_pendingLevelUps <= 0) return;
        _pendingLevelUps--;
        _waitingForChoice = true;
        Time.timeScale    = 0f;
        _currentChoices   = GetRandomUpgrades(3);

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

        // On enregistre l'upgrade choisie
        _chosenUpgrades.Add(chosen.upgradeName);

        if (_levelUpPanel != null)
            _levelUpPanel.SetActive(false);

        HealthSystem health = FindObjectOfType<HealthSystem>();
        if (health != null)
            health.SetInvincible();

        if (_pendingLevelUps > 0)
        {
            _showingDelay = true;
            _delayTimer   = 0.4f;
        }
        else
        {
            Time.timeScale = 1f;
        }
    }

    // Retourne un résumé lisible des upgrades prises
    public string GetUpgradesSummary()
    {
        if (_chosenUpgrades.Count == 0)
            return "Aucune upgrade pour l'instant.";

        // Compte les doublons (ex: Dégâts x3)
        Dictionary<string, int> counts = new Dictionary<string, int>();
        foreach (string name in _chosenUpgrades)
        {
            if (counts.ContainsKey(name))
                counts[name]++;
            else
                counts[name] = 1;
        }

        string summary = "";
        foreach (var kvp in counts)
            summary += kvp.Value > 1 ? $"• {kvp.Key} x{kvp.Value}\n" : $"• {kvp.Key}\n";

        return summary.TrimEnd();
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