using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GameUI : MonoBehaviour
{
    public static GameUI Instance { get; private set; }

    [Header("XP")]
    [SerializeField] private Slider          _xpBar;
    [SerializeField] private TextMeshProUGUI _levelText;

    [Header("HP")]
    [SerializeField] private Slider          _hpBar;
    [SerializeField] private TextMeshProUGUI _hpText;
    [SerializeField] private Image           _hpFillImage;

    [Header("Timer")]
    [SerializeField] private TextMeshProUGUI _timerText;

    [Header("Boss")]
    [SerializeField] private GameObject      _bossHPBar;
    [SerializeField] private Slider          _bossHPSlider;
    [SerializeField] private TextMeshProUGUI _bossNameText;
    [SerializeField] private GameObject      _bossIcon;

    [Header("Game Over")]
    [SerializeField] private GameObject      _gameOverPanel;
    [SerializeField] private TextMeshProUGUI _statsText;

    [Header("Gold")]
    [SerializeField] private TextMeshProUGUI _goldText;

    [Header("Kill Counter")]
    [SerializeField] private TextMeshProUGUI _killCountText;

    [Header("Dash")]
    [SerializeField] private Slider _dashCooldownBar;

    [Header("Cristal")]
    [SerializeField] private UnityEngine.UI.Image[] _crystalIcons;
    [SerializeField] private GameObject             _ultReadyEffect;

    [Header("Pause")]
    [SerializeField] private GameObject      _pausePanel;
    [SerializeField] private TextMeshProUGUI _pauseStatsText;
    [SerializeField] private TextMeshProUGUI _pauseUpgradesText;
    [SerializeField] private GameObject      _abandonConfirmPanel;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        if (MetaProgressionManager.Instance != null)
            UpdateGold(MetaProgressionManager.Instance.RunGold);

        UpdateKillCount(0);
    }

    public void UpdateCrystalCharge(int current, int max)
    {
        for (int i = 0; i < _crystalIcons.Length; i++)
        {
            if (i < current)
                _crystalIcons[i].color = new Color(0f, 0.8f, 1f);
            else
                _crystalIcons[i].color = new Color(0.2f, 0.2f, 0.2f);
        }
    }

    public void SetCrystalReady(bool ready)
    {
        if (_ultReadyEffect != null)
            _ultReadyEffect.SetActive(ready);

        if (!ready)
        {
            foreach (var icon in _crystalIcons)
                icon.color = new Color(0.2f, 0.2f, 0.2f);
        }
        else
        {
            foreach (var icon in _crystalIcons)
                icon.color = Color.white;
        }
    }

    public void ShowUltEffect(bool show)
    {
        Debug.Log(show ? "ULT ACTIF — ennemis ralentis !" : "ULT terminé");
    }

    public void UpdateDashCooldown(float percent)
    {
        if (_dashCooldownBar != null)
            _dashCooldownBar.value = percent;
    }

    public void UpdateGold(int amount)
    {
        if (_goldText != null)
            _goldText.text = $"  : {amount}";
    }

    public void UpdateKillCount(int kills)
    {
        if (_killCountText != null)
            _killCountText.text = $"  : {kills}";    }

    public void UpdateXPBar(float currentXP, float xpToNextLevel, int level)
    {
        _xpBar.value    = currentXP / xpToNextLevel;
        _levelText.text = $"Niv. {level}";
    }

    public void UpdateHPBar(float currentHP, float maxHP)
    {
        float percent = currentHP / maxHP;
        _hpBar.value = percent;
        _hpText.text = $"{Mathf.CeilToInt(currentHP)} / {Mathf.CeilToInt(maxHP)}";

        if (percent > 0.6f)
            _hpFillImage.color = new Color(0f, 0.7f, 0f);
        else if (percent > 0.4f)
            _hpFillImage.color = new Color(0.4f, 0.8f, 0f);
        else if (percent > 0.2f)
            _hpFillImage.color = new Color(1f, 0.5f, 0f);
        else
            _hpFillImage.color = new Color(0.85f, 0.1f, 0.1f);
    }

    public void UpdateTimer(float seconds)
    {
        if (_timerText == null) return;
        int mins = Mathf.FloorToInt(seconds / 60f);
        int secs = Mathf.FloorToInt(seconds % 60f);
        _timerText.text = $"{mins:00}:{secs:00}";
    }

    public void ShowBossHP(string bossName)
    {
        _bossHPBar.SetActive(true);
        _bossNameText.gameObject.SetActive(true);
        _bossNameText.text  = bossName;
        _bossHPSlider.value = 1f;
        if (_bossIcon != null) _bossIcon.SetActive(true);
    }

    public void UpdateBossHP(float current, float max)
    {
        _bossHPSlider.value = current / max;
    }

    public void HideBossHP()
    {
        _bossHPBar.SetActive(false);
        _bossNameText.gameObject.SetActive(false);
        if (_bossIcon != null) _bossIcon.SetActive(false);
    }

    public void ShowGameOver(float runTimer, int killCount, int goldEarned)
    {
        _gameOverPanel.SetActive(true);

        int mins = Mathf.FloorToInt(runTimer / 60f);
        int secs = Mathf.FloorToInt(runTimer % 60f);

        _statsText.text = $"Temps de survie : {mins:00}:{secs:00}\n" +
                          $"Ennemis tués : {killCount}\n" +
                          $"Gold gagné : {goldEarned}";
    }

    public void ShowPausePanel(bool show)
    {
        _pausePanel.SetActive(show);

        if (show)
        {
            int mins = Mathf.FloorToInt(GameManager.Instance.RunTimer / 60f);
            int secs = Mathf.FloorToInt(GameManager.Instance.RunTimer % 60f);

            _pauseStatsText.text = $"Temps : {mins:00}:{secs:00}\n" +
                                   $"Ennemis tués : {GameManager.Instance.KillCount}\n" +
                                   $"Gold : {MetaProgressionManager.Instance.RunGold}";

            _pauseUpgradesText.text = LevelUpManager.Instance.GetUpgradesSummary();

            if (_abandonConfirmPanel != null)
                _abandonConfirmPanel.SetActive(false);
        }
    }

    public void ShowAbandonConfirm(bool show)
    {
        if (_abandonConfirmPanel != null)
            _abandonConfirmPanel.SetActive(show);
    }
}