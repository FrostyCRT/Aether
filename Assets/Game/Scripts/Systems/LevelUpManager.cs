using UnityEngine;
using System.Collections.Generic;

public class LevelUpManager : MonoBehaviour
{
    public static LevelUpManager Instance { get; private set; }

    // AJOUTÉ (2026-10-16, audit version finale) - somme des bonus "Dégâts+" / "Cadence+" pris pendant CETTE partie
    // (ex. 3 picks de +20 % = 0.6). Sert à appliquer ces bonus de façon ADDITIVE et identique sur toutes les armes
    // (et à rattraper les armes obtenues plus tard) - voir UpgradeData.Apply / ApplyPastGlobalBonuses.
    public float DamageBonusTotal { get; set; }
    public float FireRateBonusTotal { get; set; }

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

    // AJOUTÉ (2026-10-20, chantier satisfaction - "ça montre pas que c'est un vrai pas en avant et une récompense
    // pour le joueur") - avant ce correctif, DisplayLevelUp() gelait Time.timeScale=0 et affichait les 3 cartes
    // DANS LA MÊME FRAME que la montée de niveau : aucun instant pour que le joueur PERÇOIVE que quelque chose de
    // bien vient de se produire avant d'être déjà face à un choix. Les meilleurs survivors du genre (Vampire
    // Survivors, Brotato) marquent ce moment par un bref "temps fort" JOUABLE (le jeu continue de tourner un
    // instant) avant d'ouvrir l'écran de choix - un vrai rythme en 2 temps : (1) la récompense arrive et se VOIT
    // à l'endroit même où le joueur vient de se battre, (2) puis seulement le choix se fige. _preShowTimer tourne
    // en temps RÉEL NON affecté (Time.deltaTime normal, le jeu n'est pas encore en pause à ce stade).
    [Header("Fanfare avant l'ouverture du panel (temps réel, jeu pas encore en pause)")]
    [Tooltip("Durée du temps fort jouable avant que l'écran de choix ne se fige (anneau doré + étincelles au joueur).")]
    [SerializeField] private float _preShowFanfareDuration = 0.3f;
    [SerializeField] private float _preShowRingRadius = 2.4f;
    [SerializeField] private float _preShowRingDuration = 0.4f;
    private static readonly Color FanfareGold = new Color(1f, 0.788f, 0.302f); // même or que _dotColorMax (UpgradeUI) - identité "palier franchi"
    private bool _preShowPending = false;
    private float _preShowTimer = 0f;

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

    // AJOUTÉ (2026-10-18, retour utilisateur : "si on prend un niveau et qu'on meurt, ou gagne la partie [...] il
    // peut encore y avoir le levelup panel") - appelée par GameManager dès que TriggerGameOver()/TriggerVictory()
    // se déclenche (AVANT le délai de _gameOverDelay/_victoryDelay), pour fermer immédiatement un panneau de
    // level-up resté ouvert ou en file (rafale de plusieurs niveaux d'un coup). Remet EXACTEMENT le même état que
    // la sortie normale de SelectUpgrade() (Time.timeScale/IsPaused/HUD), mais sans appliquer de carte : sans ce
    // rattrapage, le jeu restait figé en pause (Time.timeScale=0, posé par DisplayLevelUp) derrière l'écran de fin,
    // qui ne gère lui-même aucun minuteur capable de le débloquer.
    public void CancelForGameEnd()
    {
        bool wasShowing = _waitingForChoice || _showingDelay || _preShowPending;
        _waitingForChoice = false;
        _showingDelay = false;
        _preShowPending = false;
        _pendingLevelUps = 0;

        if (_levelUpPanel != null)
            _levelUpPanel.SetActive(false);
        if (_upgradeUI != null)
            _upgradeUI.gameObject.SetActive(false);

        if (!wasShowing) return;

        Time.timeScale = 1f;
        if (GameManager.Instance != null)
            GameManager.Instance.SetPausedFlag(false);
        if (GameUI.Instance != null)
            GameUI.Instance.SetHUDVisible(true);
    }

    private void Update()
    {
        // Temps réel NORMAL (Time.deltaTime, pas unscaled) - à ce stade le jeu n'est pas encore en pause,
        // contrairement à _showingDelay ci-dessous (qui tourne lui pendant une rafale déjà figée). Le joueur garde
        // le contrôle pendant ce bref temps fort : s'il meurt ou que la partie se termine pendant cette fenêtre,
        // CancelForGameEnd() l'interrompt proprement (voir garde IsGameOver dans DisplayLevelUp()).
        if (_preShowPending)
        {
            _preShowTimer -= Time.deltaTime;
            if (_preShowTimer <= 0f)
            {
                _preShowPending = false;
                DisplayLevelUp();
            }
            return;
        }

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
                if (GameInput.Down(GameAction.Card1)) _upgradeUI.SelectCardByIndex(0);
                if (GameInput.Down(GameAction.Card2)) _upgradeUI.SelectCardByIndex(1);
                if (GameInput.Down(GameAction.Card3)) _upgradeUI.SelectCardByIndex(2);
            }
        }
    }

    public void ShowLevelUp()
    {
        // AJOUTÉ (2026-10-18, retour utilisateur : "si on prend un niveau et qu'on meurt, ou gagne la partie [...]
        // il peut encore y avoir le levelup panel") - XPSystem.AddXP() a bien un garde IsGameOver, mais seulement
        // au moment où l'XP est ajoutée ; rien n'empêchait un palier déjà en file (_pendingLevelUps) de s'afficher
        // plus tard via la cascade de SelectUpgrade(), ni un appel direct de ShowLevelUp() passé entre les mailles,
        // une fois la partie terminée. Garde défensive directement ici, en plus de celle de XPSystem.
        if (GameManager.Instance != null && GameManager.Instance.IsGameOver) return;
        _pendingLevelUps++;
        if (_waitingForChoice || _showingDelay || _preShowPending) return;
        BeginPreShowFanfare();
    }

    // AJOUTÉ (2026-10-18, diagnostic complet - mécanique du coffre de boss) - point d'entrée dédié pour une
    // récompense qui n'est PAS de l'XP (le coffre de boss accorde directement des paliers, sans jamais passer par
    // XPSystem.AddXP/LevelUp). Réutilise exactement le même pipeline que les level-up normaux (file d'attente,
    // cascade de plusieurs choix d'affilée, pause, 3 cartes à chaque fois) - un coffre qui "accorde 2 upgrades"
    // déclenche donc 2 VRAIS tirages complets à la suite, pas un choix unique parmi 2 cartes.
    public void GrantFreePicks(int count)
    {
        if (GameManager.Instance != null && GameManager.Instance.IsGameOver) return;
        if (count <= 0) return;
        _pendingLevelUps += count;
        if (_waitingForChoice || _showingDelay) return;
        DisplayLevelUp();
    }

    // AJOUTÉ (2026-10-20, chantier satisfaction) - temps fort JOUABLE déclenché à l'INSTANT où le joueur franchit un
    // palier (pas une fois le panel déjà ouvert) : anneau doré qui s'étend depuis le joueur, pendant que le jeu
    // tourne encore normalement - le joueur VOIT la récompense arriver à l'endroit même où il combat, avant que
    // l'écran ne se fige sur le choix. Reprend exactement les mêmes primitives déjà prouvées (ExpandingRingVFX,
    // ImpactSparkVFX) que BossChest.OnTriggerEnter, pour la même identité visuelle "récompense" dans tout le jeu.
    private void BeginPreShowFanfare()
    {
        GameObject playerGO = GameObject.FindWithTag("Player");
        Vector3 origin = playerGO != null ? playerGO.transform.position + Vector3.up * 0.3f : Vector3.zero;

        ExpandingRingVFX.Spawn(origin, _preShowRingRadius, FanfareGold, _preShowRingDuration);
        ImpactSparkVFX.Spawn(origin, FanfareGold, 1.6f);

        _preShowPending = true;
        _preShowTimer = _preShowFanfareDuration;
    }

    private void DisplayLevelUp()
    {
        // Même garde que ci-dessus : DisplayLevelUp() est aussi rappelée par la cascade (choix suivant d'une rafale
        // de plusieurs niveaux d'un coup) - si la partie se termine PENDANT cette rafale, elle ne doit pas continuer
        // à proposer de nouvelles cartes.
        if (GameManager.Instance != null && GameManager.Instance.IsGameOver)
        {
            CancelForGameEnd();
            return;
        }
        if (_pendingLevelUps <= 0) return;

        _pendingLevelUps--;

        // Build complet (toutes les cartes au maximum, Soin compris) : rien à proposer, on ne fige pas le jeu pour un
        // écran vide. Le niveau est quand même gagné ; on passe au niveau en attente suivant s'il y en a.
        List<UpgradeData> choices = GetRandomUpgrades(3);
        if (choices.Count == 0)
        {
            if (_pendingLevelUps > 0)
            {
                DisplayLevelUp();
            }
            else
            {
                // CORRIGÉ (2026-10-06, retour utilisateur : "je prends plusieurs niveaux d'affilée, la dernière
                // carte prise complète ma dernière fusion possible, l'écran se fige" - figé en pause, obligé
                // d'appuyer sur Échap pour reprendre). Cause : ce cas ("plus rien à proposer") n'est atteint SANS
                // passer par SelectUpgrade() - uniquement SelectUpgrade() remet `Time.timeScale`/`IsPaused`/le HUD
                // dans leur état normal (son bloc `else`, voir plus bas). Si CE palier-ci (dans une rafale de
                // plusieurs niveaux d'un coup) tombe sur "plus rien à offrir" en dernier, le jeu restait figé en
                // pause pour toujours, sans carte à l'écran ni façon d'en sortir autrement qu'Échap (qui force sa
                // propre remise à `Time.timeScale = 1`). Remise en état IDENTIQUE à SelectUpgrade() - idempotente :
                // si le jeu n'était pas en pause (ex. ce palier est le tout premier de la rafale, jamais mis en
                // pause), ces 3 lignes ne changent rien.
                Time.timeScale = 1f;
                if (GameManager.Instance != null)
                    GameManager.Instance.SetPausedFlag(false);
                if (GameUI.Instance != null)
                    GameUI.Instance.SetHUDVisible(true);
            }
            return;
        }

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

        _currentChoices = choices;

        if (_levelUpPanel != null)
        {
            _levelUpPanel.SetActive(true);
            // AJOUTÉ (2026-10-20, retour utilisateur : "quand on met un ult, l'écran est bleu, et si on level up
            // pendant l'absorption, l'écran est encore un peu bleu même avec les cartes") - l'effet plein écran de
            // l'Ultime (GameUI._ultVfxRoot) s'ajoute toujours en DERNIER enfant du Canvas (SetAsLastSibling, voir
            // EnsureUltimateVFXBuilt) pour être sûr de passer au-dessus du HUD - mais si le level-up panel était
            // déjà placé APRÈS lui dans l'ordre des enfants (ou le devient par un futur changement d'ordre), il
            // peinturlure la teinte bleue PAR-DESSUS les cartes elles-mêmes tant que l'effet n'a pas fini de
            // s'éteindre tout seul (~1,3s, voir UltimateVFXInRoutine). Remonter le panel en DERNIER enfant À SON
            // TOUR, à chaque ouverture, garantit qu'il reste toujours au-dessus de tout effet plein écran déjà
            // présent : le bleu reste visible derrière le fond sombre du panel/les cartes, jamais par-dessus.
            _levelUpPanel.transform.SetAsLastSibling();
        }

        if (_upgradeUI != null)
            _upgradeUI.gameObject.SetActive(true);

        // AJOUTE (2026-09-27, retour utilisateur) - masque le HUD pendant tout l'écran de level-up (barres, minimap,
        // etc. n'ont rien à faire derrière un choix de carte) ; remis dans SelectUpgrade() uniquement une fois
        // TOUS les level-up en attente traités (même logique que GameManager.SetPausedFlag ci-dessus - pas
        // remontré puis re-masqué entre deux cartes d'une même rafale de montées de niveau).
        // MODIFIÉ (2026-10-19, retour utilisateur : "je veux que tu affiche quand même la barre de vie... si il y a
        // l'upgrade Soin qui tombe, le joueur ne peut pas savoir à l'avance combien de vie il a") - keepHealthBar
        // garde uniquement la barre de vie visible, tout le reste du HUD disparaît comme avant.
        if (GameUI.Instance != null)
            GameUI.Instance.SetHUDVisible(false, keepHealthBar: true);

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

            // AJOUTE (2026-09-27, retour utilisateur) - symetrique du masquage dans DisplayLevelUp().
            if (GameUI.Instance != null)
                GameUI.Instance.SetHUDVisible(true);
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
            if (kvp.Value > 1) summary.AppendLine($"\u2022 {Loc.T(kvp.Key)} x{kvp.Value}");
            else summary.AppendLine($"\u2022 {Loc.T(kvp.Key)}");
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
            lines.Add(kvp.Value > 1 ? $"\u2022 {Loc.T(kvp.Key)} x{kvp.Value}" : $"\u2022 {Loc.T(kvp.Key)}");
        }

        return lines;
    }
}