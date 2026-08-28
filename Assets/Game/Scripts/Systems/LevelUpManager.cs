using UnityEngine;
using System.Collections.Generic;

public class LevelUpManager : MonoBehaviour
{
    public static LevelUpManager Instance { get; private set; }

    [Header("References")]
    [SerializeField] private UpgradeData[] _allUpgrades;
    [SerializeField] private GameObject _levelUpPanel;
    [SerializeField] private UpgradeUI _upgradeUI;

    private List<UpgradeData> _currentChoices = new List<UpgradeData>();
    private List<string> _chosenUpgrades = new List<string>();
    private int _pendingLevelUps = 0;
    private bool _waitingForChoice = false;
    private float _delayTimer = 0f;
    private bool _showingDelay = false;

    // Niveau courant de chaque upgrade pour la run en cours.
    // Vit ici (pas dans le ScriptableObject UpgradeData) car UpgradeData est un asset
    // partage entre toutes les runs, pas un etat de run individuelle.
    private readonly Dictionary<UpgradeData, int> _upgradeLevels = new Dictionary<UpgradeData, int>();

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

    // Lecture du niveau actuel d'une upgrade (0 si jamais piochee)
    public int GetLevel(UpgradeData upgrade)
    {
        return _upgradeLevels.TryGetValue(upgrade, out int level) ? level : 0;
    }

    // Incremente et retourne le nouveau niveau, appele par UpgradeData.Apply()
    public int IncrementLevel(UpgradeData upgrade)
    {
        int newLevel = GetLevel(upgrade) + 1;
        _upgradeLevels[upgrade] = newLevel;
        return newLevel;
    }

    // Securite si jamais LevelUpManager doit etre reutilise sans recharger la scene
    // (ex: bouton "Rejouer" qui ne recharge pas la scene de jeu)
    public void ResetLevels()
    {
        _upgradeLevels.Clear();
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

        // AJOUTE - le script UpgradeUI vit sur un GameObject distinct de _levelUpPanel
        // dans la Hierarchy actuelle (confirme via debug : les deux objets s'appellent
        // tous les deux "LevelUpPanel" mais ne sont PAS le meme objet). Ce second objet
        // doit lui aussi etre actif, sinon StartCoroutine() echoue silencieusement dans
        // UpgradeUI (Unity refuse de demarrer une coroutine sur un GameObject inactif),
        // ce qui empechait tout le systeme de delai/animation au clic de fonctionner,
        // meme si l'affichage des cartes et les clics eux-memes marchaient normalement
        // (un appel de methode direct, contrairement a StartCoroutine, fonctionne sur
        // un GameObject inactif).
        if (_upgradeUI != null)
            _upgradeUI.gameObject.SetActive(true);

        _upgradeUI.DisplayUpgrades(_currentChoices);
    }

    public void SelectUpgrade(int index)
    {
        if (!_waitingForChoice) return;
        if (index < 0 || index >= _currentChoices.Count) return;

        _waitingForChoice = false;
        UpgradeData chosen = _currentChoices[index];

        // Application de l'upgrade via la methode Apply() de ton ScriptableObject
        chosen.Apply();

        _chosenUpgrades.Add(chosen.upgradeName);

        if (_levelUpPanel != null)
            _levelUpPanel.SetActive(false);

        // AJOUTE - desactive l'objet UpgradeUI en meme temps que le panel visuel, en
        // symetrie avec l'activation ajoutee dans DisplayLevelUp() ci-dessus. Sans
        // danger pour l'animation en cours : SelectUpgrade() est appele en DERNIERE
        // ligne de la coroutine AnimatePickThenConfirm() dans UpgradeUI, donc la
        // coroutine a deja fini de s'executer au moment ou cette desactivation a lieu.
        if (_upgradeUI != null)
            _upgradeUI.gameObject.SetActive(false);

        // Optionnel : Recupere le joueur pour gerer l'invincibilite si tu veux garder ca ici
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
            // On verifie la disponibilite via la methode IsAvailable() de l'UpgradeData
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

    // Le resume reste identique
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
            if (kvp.Value > 1) summary.AppendLine($"\u2022 {kvp.Key} x{kvp.Value}");
            else summary.AppendLine($"\u2022 {kvp.Key}");
        }
        return summary.ToString().TrimEnd();

    }
    public List<string> GetUpgradesList()
    {
        if (_chosenUpgrades.Count == 0) return new List<string>();

        Dictionary<string, int> counts = new Dictionary<string, int>();
        foreach (string name in _chosenUpgrades)
        {
            if (counts.ContainsKey(name)) counts[name]++;
            else counts[name] = 1;
        }

        List<string> lines = new List<string>();
        foreach (var kvp in counts)
        {
            lines.Add(kvp.Value > 1 ? $"\u2022 {kvp.Key} x{kvp.Value}" : $"\u2022 {kvp.Key}");
        }

        return lines;
    }
}