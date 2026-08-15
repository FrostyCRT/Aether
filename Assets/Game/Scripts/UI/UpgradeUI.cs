using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
public class UpgradeUI : MonoBehaviour
{
    [Header("Cartes d'upgrade")]
    [SerializeField] private UpgradeCard[] _cards;
    [System.Serializable]
    public class UpgradeCard
    {
        public GameObject cardRoot; // ← Assigne le conteneur parent de la carte ici
        public TextMeshProUGUI nameText;
        public TextMeshProUGUI descriptionText;
        public Button chooseButton;
    }
    private void Awake()
    {
        // On lie les boutons une seule fois au démarrage pour éviter toute allocation de GC
        for (int i = 0; i < _cards.Length; i++)
        {
            if (_cards[i].chooseButton != null)
            {
                int index = i; // Capture locale sécurisée pour le scope du Awake
                _cards[i].chooseButton.onClick.RemoveAllListeners();
                _cards[i].chooseButton.onClick.AddListener(() => OnCardSelected(index));
            }
        }
    }
    public void DisplayUpgrades(List<UpgradeData> upgrades)
    {
        if (upgrades == null) return;
        for (int i = 0; i < _cards.Length; i++)
        {
            if (i < upgrades.Count)
            {
                _cards[i].nameText.text = upgrades[i].upgradeName;
                // MODIFIÉ — texte dynamique par palier (ex: "+40% dégâts.") au lieu du
                // champ 'description' statique, qui ne peut pas décrire 3 paliers différents
                // avec un seul texte fixe.
                _cards[i].descriptionText.text = upgrades[i].GetDynamicDescription();
                if (_cards[i].cardRoot != null)
                    _cards[i].cardRoot.SetActive(true);
                else
                    _cards[i].chooseButton.gameObject.SetActive(true);
            }
            else
            {
                // Masque proprement la carte entière (ou le bouton si cardRoot n'est pas assigné)
                if (_cards[i].cardRoot != null)
                    _cards[i].cardRoot.SetActive(false);
                else
                    _cards[i].chooseButton.gameObject.SetActive(false);
            }
        }
    }
    private void OnCardSelected(int index)
    {
        if (LevelUpManager.Instance != null)
        {
            LevelUpManager.Instance.SelectUpgrade(index);
        }
    }
}