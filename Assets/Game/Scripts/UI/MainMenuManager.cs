using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;
public class MainMenuManager : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject _upgradesPanel;
    [SerializeField] private GameObject _menuPanel;
    [SerializeField] private GameObject _settingsPanel;
    [SerializeField] private GameObject _characterSelectPanel;
    // AJOUTE - panel de la nouvelle page Reputation
    [SerializeField] private GameObject _reputationPanel;

    [Header("Onglets (rubans)")]
    [SerializeField] private Image _upgradesTabImage;
    [SerializeField] private Image _menuTabImage;
    [SerializeField] private Image _settingsTabImage;
    [SerializeField] private Image _characterSelectTabImage;
    // AJOUTE - onglet correspondant
    [SerializeField] private Image _reputationTabImage;

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
        _characterSelectPanel.SetActive(false);
        // AJOUTE
        if (_reputationPanel != null) _reputationPanel.SetActive(false);

        panel.SetActive(true);
        SetTabState(_upgradesTabImage, panel == _upgradesPanel);
        SetTabState(_menuTabImage, panel == _menuPanel);
        SetTabState(_settingsTabImage, panel == _settingsPanel);
        SetTabState(_characterSelectTabImage, panel == _characterSelectPanel);
        // AJOUTE
        SetTabState(_reputationTabImage, panel == _reputationPanel);
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
    public void ShowCharacterSelect() => ShowPanel(_characterSelectPanel);
    // AJOUTE
    public void ShowReputation() => ShowPanel(_reputationPanel);
    public void PlayGame()
    {
        // MODIFIE (2026-09-14) - passe par SceneLoader/LoadingScreen (vrai
        // chargement async + retour visuel) au lieu d'un SceneManager.LoadScene
        // brut et synchrone : c'est la transition la plus lourde du jeu (Jeu
        // contient bien plus de systemes/assets que MainMenu), donc celle qui
        // avait le plus besoin d'un vrai chargement asynchrone. Voir
        // SceneLoader.cs.
        SceneLoader.LoadScene("Game");
    }
    public void QuitGame()
    {
        Application.Quit();
    }
}