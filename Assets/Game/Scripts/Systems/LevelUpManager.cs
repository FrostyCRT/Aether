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

    private readonly Dictionary<UpgradeData, int> _upgradeLevels = new Dictionary<UpgradeData, int>();

    private readonly List<UpgradeData> _obtainedOrder = new List<UpgradeData>();

    // AJOUTE - garantit que la carte de deblocage de l'arme exclusive du
    // personnage (Fireball/AuraUpgrade/Knives) apparait parmi les 3 choix du TOUT
    // PREMIER level-up ou elle est proposable, UNE SEULE FOIS par run - que le
    // joueur la prenne ou non ensuite, elle repasse dans le pool aleatoire normal.
    // Objectif : le joueur decouvre son identite de personnage tot dans la run,
    // sans dependre de la chance, tout en gardant un vrai choix (elle n'est que
    // 1 des 3 cartes, pas imposee).
    private bool _hasOfferedCharacterUnlock = false;

    public bool IsWaitingForChoice => _waitingForChoice;

    public UpgradeData[] AllUpgrades => _allUpgrades;

    public IReadOnlyList<UpgradeData> ObtainedOrder => _obtainedOrder;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public int GetLevel(UpgradeData upgrade)
    {
        return _upgradeLevels.TryGetValue(upgrade, out int level) ? level : 0;
    }

    public int IncrementLevel(UpgradeData upgrade)
    {
        int previousLevel = GetLevel(upgrade);
        int newLevel = previousLevel + 1;
        _upgradeLevels[upgrade] = newLevel;

        if (previousLevel == 0)
            _obtainedOrder.Add(upgrade);

        return newLevel;
    }

    public void ResetLevels()
    {
        _upgradeLevels.Clear();
        _obtainedOrder.Clear();
        _hasOfferedCharacterUnlock = false;
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
            // MODIFIE - les raccourcis clavier passent maintenant par
            // UpgradeUI.SelectCardByIndex() plutot que d'appeler SelectUpgrade()
            // directement. Avant ce correctif, le clavier confirmait le pick
            // instantanement, en ignorant completement le delai et l'animation
            // qui fonctionnaient pourtant normalement au clic souris - meme
            // symptome que l'ancien listener persistant sur les boutons.
            if (_upgradeUI != null)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1)) _upgradeUI.SelectCardByIndex(0);
                if (Input.GetKeyDown(KeyCode.Alpha2)) _upgradeUI.SelectCardByIndex(1);
                if (Input.GetKeyDown(KeyCode.Alpha3)) _upgradeUI.SelectCardByIndex(2);
            }
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

        // AJOUTE - synchronise GameManager.IsPaused avec le gel du level-up.
        // Avant ce correctif, seul Time.timeScale passait a 0 ici ; or PlayerController
        // et CrystalSystem se basent sur GameManager.Instance.IsPaused (pas sur
        // Time.timeScale directement) pour savoir s'ils doivent s'arreter. Resultat :
        // ces scripts continuaient de traiter leur logique par frame pendant un
        // level-up, meme si le temps etait par ailleurs bien fige. On utilise
        // SetPausedFlag() plutot que TogglePause()/ResumePause() pour ne PAS
        // declencher en plus l'ouverture du menu pause manuel ou le HUD.
        if (GameManager.Instance != null)
            GameManager.Instance.SetPausedFlag(true);

        _currentChoices = GetRandomUpgrades(3);

        if (_levelUpPanel != null)
            _levelUpPanel.SetActive(true);

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

        chosen.Apply();

        _chosenUpgrades.Add(chosen.upgradeName);

        if (_levelUpPanel != null)
            _levelUpPanel.SetActive(false);

        if (_upgradeUI != null)
            _upgradeUI.gameObject.SetActive(false);

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

            // AJOUTE - symetrique de l'activation ci-dessus : ne re-synchronise
            // IsPaused a false que lorsque TOUS les level-up en attente sont
            // traites (donc que le jeu reprend vraiment), pas entre deux cartes
            // d'une meme rafale de level-up.
            if (GameManager.Instance != null)
                GameManager.Instance.SetPausedFlag(false);
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

        // AJOUTE - force une des 3 places a etre la carte de deblocage de l'arme
        // exclusive du personnage, une seule fois par run, tant qu'elle est
        // disponible dans le pool courant.
        if (!_hasOfferedCharacterUnlock)
        {
            UpgradeData characterUnlock = FindCharacterExclusiveUnlock(pool);
            if (characterUnlock != null && count > 0)
            {
                result.Add(characterUnlock);
                pool.Remove(characterUnlock);
                count--;
            }
            _hasOfferedCharacterUnlock = true;
        }

        for (int i = 0; i < count; i++)
        {
            int randomIndex = Random.Range(0, pool.Count);
            result.Add(pool[randomIndex]);
            pool.RemoveAt(randomIndex);
        }

        return result;
    }

    // AJOUTE - retrouve, dans le pool disponible, l'UpgradeData correspondant a
    // l'arme exclusive du personnage actuellement joue (via CharacterIdentity,
    // deja utilise par StartingUpgradeGranter pour le meme mapping).
    private UpgradeData FindCharacterExclusiveUnlock(List<UpgradeData> pool)
    {
        GameObject player = GameObject.FindWithTag("Player");
        if (player == null) return null;

        CharacterIdentity identity = player.GetComponent<CharacterIdentity>();
        if (identity == null) return null;

        UpgradeType targetType;
        switch (identity.Type)
        {
            case CharacterType.Aether: targetType = UpgradeType.Fireball; break;
            case CharacterType.Kael: targetType = UpgradeType.AuraUpgrade; break;
            case CharacterType.Lyra: targetType = UpgradeType.Knives; break;
            default: return null;
        }

        return pool.Find(u => u.upgradeType == targetType);
    }

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