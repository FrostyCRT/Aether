using UnityEngine;
using UnityEngine.UI;
using TMPro;

// A poser sur ReputationPanel lui-meme, pour beneficier de OnEnable (se rafraichit
// automatiquement a chaque ouverture de la page, meme logique que PauseMenuUI).
public class ReputationUI : MonoBehaviour
{
    [Header("Monnaies (Header)")]
    [SerializeField] private TextMeshProUGUI _goldAmountText;
    [SerializeField] private TextMeshProUGUI _eclatsAmountText;

    [Header("Couleurs des pastilles (memes valeurs que le reste du jeu)")]
    [SerializeField] private Color _dotColorEmpty = new Color(0.35f, 0.30f, 0.25f, 0.6f);
    [SerializeField] private Color _dotColorFilled = new Color(0.176f, 0.831f, 0.812f, 1f); // #2DD4CF
    [SerializeField] private Color _dotColorMax = new Color(1f, 0.788f, 0.302f, 1f);        // #FFC94D

    private const int MaxReputationLevel = 5;

    // AJOUTE - une carte par stat de Reputation. nodeId doit correspondre EXACTEMENT
    // aux chaines utilisees dans MetaProgressionManager ("reputationDamage",
    // "reputationSpeed", "reputationRegen").
    [System.Serializable]
    public class ReputationStatCard
    {
        [Tooltip("Doit correspondre exactement a l'id utilise dans MetaProgressionManager : reputationDamage / reputationSpeed / reputationRegen")]
        public string nodeId;
        public TextMeshProUGUI currentValueText;
        public TextMeshProUGUI nextCostText;
        public Button buyButton;
        public TextMeshProUGUI buyButtonText;
        public Image[] tierDots; // assigne Dot1 a Dot5 dans l'ordre
    }

    [Header("Les 3 cartes de stats")]
    [SerializeField] private ReputationStatCard[] _statCards;

    [Header("Onglets personnage (visuel uniquement pour l'instant, skins pas encore actives)")]
    [SerializeField] private Image[] _characterTabImages;

    // AJOUTE - bouton reserve au developpement, PAS destine a la version finale
    // (demande explicitement par l'utilisateur comme outil de test).
    [Header("DEBUG UNIQUEMENT - a retirer avant release")]
    [SerializeField] private Button _debugResetButton;
    private static readonly Vector3 _activeTabScale = new Vector3(1.05f, 1.05f, 1f);
    private static readonly Vector3 _inactiveTabScale = Vector3.one;
    private static readonly Color _activeTabColor = Color.white;
    private static readonly Color _inactiveTabColor = new Color(0.75f, 0.75f, 0.75f, 1f);
    private int _selectedTabIndex = 0;

    private void Awake()
    {
        if (_debugResetButton != null)
        {
            _debugResetButton.onClick.RemoveAllListeners();
            _debugResetButton.onClick.AddListener(OnDebugResetClicked);
        }

        for (int i = 0; i < _statCards.Length; i++)
        {
            int index = i; // capture locale
            if (_statCards[i].buyButton != null)
            {
                _statCards[i].buyButton.onClick.RemoveAllListeners();
                _statCards[i].buyButton.onClick.AddListener(() => OnBuyClicked(index));
            }
        }

        for (int i = 0; i < _characterTabImages.Length; i++)
        {
            int index = i;
            Button tabButton = _characterTabImages[i] != null ? _characterTabImages[i].GetComponent<Button>() : null;
            if (tabButton != null)
            {
                tabButton.onClick.RemoveAllListeners();
                tabButton.onClick.AddListener(() => SelectCharacterTab(index));
            }
        }
    }

    private void OnEnable()
    {
        RefreshAll();
    }

    private void RefreshAll()
    {
        RefreshCurrencyDisplay();
        for (int i = 0; i < _statCards.Length; i++)
        {
            RefreshCard(_statCards[i]);
        }
        RefreshTabVisuals();
    }

    private void RefreshCurrencyDisplay()
    {
        if (MetaProgressionManager.Instance == null || MetaProgressionManager.Instance.Data == null) return;

        if (_goldAmountText != null)
            _goldAmountText.text = MetaProgressionManager.Instance.Data.totalGold.ToString();

        if (_eclatsAmountText != null)
            _eclatsAmountText.text = MetaProgressionManager.Instance.TotalEclats.ToString();
    }

    private void RefreshCard(ReputationStatCard card)
    {
        if (MetaProgressionManager.Instance == null || string.IsNullOrEmpty(card.nodeId)) return;

        int currentLevel = MetaProgressionManager.Instance.GetNodeLevel(card.nodeId);
        int cost = MetaProgressionManager.Instance.GetNodeCost(card.nodeId);
        bool isMaxed = cost == -1;

        // AJOUTE - formatage du texte de valeur actuelle, different selon la stat
        // (Degats/Vitesse en pourcentage, Regen en valeur brute PV/s).
        if (card.currentValueText != null)
            card.currentValueText.text = FormatCurrentValue(card.nodeId);

        if (card.nextCostText != null)
            card.nextCostText.text = isMaxed ? "Palier maximum atteint" : $"Suivant : {cost} Éclats";

        if (card.buyButton != null)
        {
            bool canAfford = !isMaxed && MetaProgressionManager.Instance.TotalEclats >= cost;
            card.buyButton.interactable = canAfford;

            if (card.buyButtonText != null)
            {
                if (isMaxed)
                {
                    card.buyButtonText.text = "MAX";
                }
                else if (!canAfford)
                {
                    card.buyButtonText.text = "Éclats insuffisants";
                }
                else
                {
                    card.buyButtonText.text = "AMÉLIORER";
                }
            }
        }

        // Pastilles : meme logique 3 couleurs que le reste du jeu (vide / rempli
        // cyan / rempli dore si palier max), MaxReputationLevel = 5 fixe.
        if (card.tierDots != null)
        {
            for (int d = 0; d < card.tierDots.Length; d++)
            {
                if (card.tierDots[d] == null) continue;

                bool dotExists = d < MaxReputationLevel;
                card.tierDots[d].gameObject.SetActive(dotExists);
                if (!dotExists) continue;

                bool filled = d < currentLevel;
                if (!filled)
                    card.tierDots[d].color = _dotColorEmpty;
                else
                    card.tierDots[d].color = isMaxed ? _dotColorMax : _dotColorFilled;
            }
        }
    }

    private string FormatCurrentValue(string nodeId)
    {
        if (MetaProgressionManager.Instance == null) return "";

        switch (nodeId)
        {
            case "reputationDamage":
                return $"+{Mathf.RoundToInt(MetaProgressionManager.Instance.GetReputationBonusDamage() * 100f)}% Dégâts";
            case "reputationSpeed":
                return $"+{Mathf.RoundToInt(MetaProgressionManager.Instance.GetReputationBonusSpeed() * 100f)}% Vitesse";
            case "reputationRegen":
                return $"+{MetaProgressionManager.Instance.GetReputationBonusRegen():0.#} PV/s";
            default:
                return "";
        }
    }

    // AJOUTE
    private void OnDebugResetClicked()
    {
        if (MetaProgressionManager.Instance == null) return;
        MetaProgressionManager.Instance.DebugResetReputation();
        RefreshAll();
    }

    private void OnBuyClicked(int cardIndex)
    {
        if (cardIndex < 0 || cardIndex >= _statCards.Length) return;
        if (MetaProgressionManager.Instance == null) return;

        string nodeId = _statCards[cardIndex].nodeId;
        if (string.IsNullOrEmpty(nodeId)) return;

        if (MetaProgressionManager.Instance.TryBuyNode(nodeId))
        {
            RefreshAll();
        }
    }

    // AJOUTE - bascule visuelle des onglets personnage (meme principe que les
    // onglets du menu principal : couleur/echelle active vs inactive). Ne change
    // encore le contenu d'aucune grille de skins - c'est le point d'accroche pret
    // pour quand le systeme de skins sera construit.
    private void SelectCharacterTab(int index)
    {
        _selectedTabIndex = index;
        RefreshTabVisuals();
        // TODO - une fois le systeme de skins construit : charger/filtrer la
        // grille de skins du personnage correspondant a _selectedTabIndex ici.
    }

    private void RefreshTabVisuals()
    {
        for (int i = 0; i < _characterTabImages.Length; i++)
        {
            if (_characterTabImages[i] == null) continue;

            bool isActive = i == _selectedTabIndex;
            _characterTabImages[i].color = isActive ? _activeTabColor : _inactiveTabColor;
            _characterTabImages[i].rectTransform.localScale = isActive ? _activeTabScale : _inactiveTabScale;
        }
    }
}