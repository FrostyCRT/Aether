using UnityEngine;
using System.Collections.Generic;

public class LevelUpManager : MonoBehaviour
{
    public static LevelUpManager Instance { get; private set; }

    [Header("Références")]
    [SerializeField] private UpgradeData[] _allUpgrades;
    [SerializeField] private GameObject _levelUpPanel;
    [SerializeField] private UpgradeUI _upgradeUI;

    private List<UpgradeData> _currentChoices = new List<UpgradeData>();
    private List<string> _chosenUpgrades = new List<string>();
    private int _pendingLevelUps = 0;
    private bool _waitingForChoice = false;
    private float _delayTimer = 0f;
    private bool _showingDelay = false;

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

        Time.timeScale = 0f;

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

        // Application de l'upgrade via la méthode Apply() de ton ScriptableObject
        chosen.Apply();

        _chosenUpgrades.Add(chosen.upgradeName);

        if (_levelUpPanel != null)
            _levelUpPanel.SetActive(false);

        // Optionnel : Récupère le joueur pour gérer l'invincibilité si tu veux garder ça ici
        GameObject playerGO = GameObject.FindWithTag("Player");
        if (playerGO != null)
        {
            HealthSystem health = playerGO.GetComponent<HealthSystem>();
            if (health != null) health.SetInvincible();
        }

        if (_pendingLevelUps > 0)
        {
            _showingDelay = true;
            _delayTimer = 0.4f;
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
            // On vérifie la disponibilité via la méthode IsAvailable() de l'UpgradeData
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

    // Le résumé reste identique
    public string GetUpgradesSummary()
    {
        if (_chosenUpgrades.Count == 0) return "";
        Dictionary<string, int> counts = new Dictionary<string, int>();
        foreach (string name in _chosenUpgrades)
        {
            if (counts.ContainsKey(name)) counts[name]++;
            else counts[name] = 1;
        }
        System.Text.StringBuilder summary = new System.Text.StringBuilder();
        foreach (var kvp in counts)
        {
            if (kvp.Value > 1) summary.AppendLine($"• {kvp.Key} x{kvp.Value}");
            else summary.AppendLine($"• {kvp.Key}");
        }
        return summary.ToString().TrimEnd();
    }
}