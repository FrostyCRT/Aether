using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class MainMenuManager : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject _upgradesPanel;
    [SerializeField] private GameObject _menuPanel;
    [SerializeField] private GameObject _settingsPanel;

    [Header("TopBar")]
    [SerializeField] private TextMeshProUGUI _goldDisplay;
    [SerializeField] private TextMeshProUGUI _gemsDisplay;

    private void Start()
    {
        // Attend que MetaProgressionManager soit prêt
        if (MetaProgressionManager.Instance != null)
        {
            SaveData data = SaveSystem.Load();
            _goldDisplay.text = $": {data.totalGold}";
            _gemsDisplay.text = $": {data.totalGems}";
        }
        else
        {
            _goldDisplay.text = " : 0";
            _gemsDisplay.text = " : 0";
        }

        ShowPanel(_menuPanel);
    }

    public void ShowPanel(GameObject panel)
    {
        _upgradesPanel.SetActive(false);
        _menuPanel.SetActive(false);
        _settingsPanel.SetActive(false);
        panel.SetActive(true);
    }

    public void ShowUpgrades() => ShowPanel(_upgradesPanel);
    public void ShowMenu() => ShowPanel(_menuPanel);
    public void ShowSettings() => ShowPanel(_settingsPanel);

    public void PlayGame()
    {
        SceneManager.LoadScene(1); // 1 = Game dans Build Settings
    }

    public void QuitGame()
    {
        Application.Quit();
    }
}