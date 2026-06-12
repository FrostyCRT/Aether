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

    public void DisplayUpgrades(System.Collections.Generic.List<UpgradeData> upgrades)
    {
        for (int i = 0; i < _cards.Length; i++)
        {
            if (i < upgrades.Count)
            {
                int index = i;
                _cards[i].nameText.text        = upgrades[i].upgradeName;
                _cards[i].descriptionText.text = upgrades[i].description;
                _cards[i].chooseButton.onClick.RemoveAllListeners();
                _cards[i].chooseButton.onClick.AddListener(() =>
                {
                    Debug.Log($"Bouton {index} cliqué !");
                    LevelUpManager.Instance.SelectUpgrade(index);
                });
                _cards[i].chooseButton.gameObject.SetActive(true);
            }
            else
            {
                _cards[i].chooseButton.gameObject.SetActive(false);
            }
        }
    }
}