using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuManager : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject _upgradesPanel;
    [SerializeField] private GameObject _menuPanel;
    [SerializeField] private GameObject _settingsPanel;
    [SerializeField] private GameObject _characterSelectPanel; // AJOUTÉ

    [Header("Onglets (rubans)")]
    [SerializeField] private Image _upgradesTabImage;
    [SerializeField] private Image _menuTabImage;
    [SerializeField] private Image _settingsTabImage;
    [SerializeField] private Image _characterSelectTabImage; // AJOUTÉ

    private static readonly Color _activeTabColor = Color.white;
    private static readonly Color _inactiveTabColor = new Color(0.7f, 0.7f, 0.7f, 1f);
    private static readonly Vector3 _activeTabScale = new Vector3(1.08f, 1.08f, 1f);
    private static readonly Vector3 _inactiveTabScale = Vector3.one;

    [SerializeField] private AudioMixer _mainAudioMixer;

    private void Start()
    {
        ShowPanel(_menuPanel);
    }

    public void ShowPanel(GameObject panel)
    {
        _upgradesPanel.SetActive(false);
        _menuPanel.SetActive(false);
        _settingsPanel.SetActive(false);
        _characterSelectPanel.SetActive(false); // AJOUTÉ

        panel.SetActive(true);

        SetTabState(_upgradesTabImage,       panel == _upgradesPanel);
        SetTabState(_menuTabImage,           panel == _menuPanel);
        SetTabState(_settingsTabImage,       panel == _settingsPanel);
        SetTabState(_characterSelectTabImage, panel == _characterSelectPanel); // AJOUTÉ
    }

    private void SetTabState(Image tabImage, bool isActive)
    {
        if (tabImage == null) return;
        tabImage.color = isActive ? _activeTabColor : _inactiveTabColor;
        tabImage.rectTransform.localScale = isActive ? _activeTabScale : _inactiveTabScale;
    }

    public void ShowUpgrades()        => ShowPanel(_upgradesPanel);
    public void ShowMenu()            => ShowPanel(_menuPanel);
    public void ShowSettings()        => ShowPanel(_settingsPanel);
    public void ShowCharacterSelect() => ShowPanel(_characterSelectPanel); // AJOUTÉ

    public void PlayGame()
    {
        SceneManager.LoadScene(1);
    }

    public void QuitGame()
    {
        Application.Quit();
    }
}