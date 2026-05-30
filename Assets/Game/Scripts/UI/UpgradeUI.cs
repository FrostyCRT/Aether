using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UpgradeUI : MonoBehaviour
{
    [Header("Cartes d'upgrade")]
    [SerializeField] private UpgradeCard[] _cards;

    [System.Serializable]
    public class UpgradeCard
    {
        public TextMeshProUGUI nameText;
        public TextMeshProUGUI descriptionText;
        public Button          chooseButton;
    }

    // Appelé par LevelUpManager pour afficher les upgrades
    public void DisplayUpgrades(System.Collections.Generic.List<UpgradeData> upgrades)
    {
        for (int i = 0; i < _cards.Length; i++)
        {
            if (i < upgrades.Count)
            {
                int index = i; // Capture pour le lambda
                _cards[i].nameText.text        = upgrades[i].upgradeName;
                _cards[i].descriptionText.text = upgrades[i].description;

                // On remet le bouton à zéro avant d'ajouter le listener
                _cards[i].chooseButton.onClick.RemoveAllListeners();
                _cards[i].chooseButton.onClick.AddListener(() =>
                    LevelUpManager.Instance.SelectUpgrade(index)
                );

                _cards[i].chooseButton.gameObject.SetActive(true);
            }
            else
            {
                _cards[i].chooseButton.gameObject.SetActive(false);
            }
        }
    }
}