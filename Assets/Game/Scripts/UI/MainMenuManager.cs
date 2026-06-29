using UnityEngine;
using UnityEngine.UI;
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

    [Header("Onglets (rubans)")]
    [SerializeField] private Image _upgradesTabImage;
    [SerializeField] private Image _menuTabImage;
    [SerializeField] private Image _settingsTabImage;

    private static readonly Color _activeTabColor = Color.white;
    private static readonly Color _inactiveTabColor = new Color(0.7f, 0.7f, 0.7f, 1f);
    private static readonly Vector3 _activeTabScale = new Vector3(1.08f, 1.08f, 1f);
    private static readonly Vector3 _inactiveTabScale = Vector3.one;

    private void Start()
    {
        // Attend que MetaProgressionManager soit prêt
        if (MetaProgressionManager.Instance != null)
        {
            SaveData data = SaveSystem.Load();
            _goldDisplay.text = $": {data.totalGold}";
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

        SetTabState(_upgradesTabImage, panel == _upgradesPanel);
        SetTabState(_menuTabImage, panel == _menuPanel);
        SetTabState(_settingsTabImage, panel == _settingsPanel);
    }

    private void SetTabState(Image tabImage, bool isActive)
    {
        if (tabImage == null) return;
        tabImage.color = isActive ? _activeTabColor : _inactiveTabColor;
        tabImage.rectTransform.localScale = isActive ? _activeTabScale : _inactiveTabScale;
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