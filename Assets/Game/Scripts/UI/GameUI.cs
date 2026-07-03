using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameUI : MonoBehaviour
{
    public static GameUI Instance { get; private set; }

    [Header("XP")]
    [SerializeField] private Slider _xpBar;
    [SerializeField] private TextMeshProUGUI _levelText;

    [Header("HP")]
    [SerializeField] private Slider _hpBar;
    [SerializeField] private TextMeshProUGUI _hpText;
    [SerializeField] private Image _hpFillImage;

    [Header("Timer")]
    [SerializeField] private TextMeshProUGUI _timerText;

    [Header("Boss")]
    [SerializeField] private GameObject _bossHPBar;
    [SerializeField] private Slider _bossHPSlider;
    [SerializeField] private TextMeshProUGUI _bossNameText;
    [SerializeField] private GameObject _bossIcon;

    [Header("Game Over")]
    [SerializeField] private GameObject _gameOverPanel;
    [SerializeField] private TextMeshProUGUI _statsText;

    [Header("Gold")]
    [SerializeField] private TextMeshProUGUI _goldText;

    [Header("Kill Counter")]
    [SerializeField] private TextMeshProUGUI _killCountText;

    [Header("Dash")]
    [SerializeField] private Slider _dashCooldownBar;

    [Header("Cristal")]
    [SerializeField] private UnityEngine.UI.Image[] _crystalIcons;
    [SerializeField] private GameObject _ultReadyEffect;
    [SerializeField] private TextMeshProUGUI _ultStackText;

    [Header("Pause")]
    [SerializeField] private GameObject _pausePanel;
    [SerializeField] private TextMeshProUGUI _pauseStatsText;
    [SerializeField] private TextMeshProUGUI _pauseUpgradesText;
    [SerializeField] private GameObject _abandonConfirmPanel;

    [Header("Victoire")]
    [SerializeField] private GameObject _victoryPanel;
    [SerializeField] private TextMeshProUGUI _victoryStatsText;
    [SerializeField] private TextMeshProUGUI _victoryRecordsText;
    [SerializeField] private TextMeshProUGUI _victoryBuildListText;
    [SerializeField] private TextMeshProUGUI _victoryBuildListText2; // NOUVEAU
    [SerializeField] private int _buildListMaxLinesPerColumn = 8;

    [Header("HUD")]
    [SerializeField] private GameObject _hudPanel;

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

    public void UpdateUltStack(int stacks)
    {
        if (_ultStackText == null) return;

        if (stacks <= 0)
        {
            _ultStackText.gameObject.SetActive(false);
            return;
        }

        _ultStackText.gameObject.SetActive(true);
        _ultStackText.text = stacks == 2
            ? "<color=#FFD700>ULT x2</color>"
            : "<color=#00CFFF>ULT x1</color>";
    }
    public void UpdateCrystalCharge(int current, int max)
    {
        if (_crystalIcons == null) return;
        for (int i = 0; i < _crystalIcons.Length; i++)
        {
            if (_crystalIcons[i] == null) continue;

            bool isWithinMax = i < max;
            _crystalIcons[i].gameObject.SetActive(isWithinMax);

            if (isWithinMax)
                _crystalIcons[i].color = (i < current) ? new Color(0f, 0.8f, 1f) : new Color(0.2f, 0.2f, 0.2f);
        }
    }

    public void ShowVictory(float runTimer, int killCount, int goldEarned, int level)
    {
        if (_victoryPanel != null) _victoryPanel.SetActive(true);

        int mins = Mathf.FloorToInt(runTimer / 60f);
        int secs = Mathf.FloorToInt(runTimer % 60f);

        // OPTIMISATION : Utilisation de string.Format (ou interpolation directe compilée) plus propre pour le GC
        if (_victoryStatsText != null)
        {
            _victoryStatsText.text = $"Temps de survie : {mins:00}:{secs:00}\nEnnemis tués : {killCount}\nNiveau atteint : {level}\nGold gagné : {goldEarned}";
        }

        if (MetaProgressionManager.Instance != null && MetaProgressionManager.Instance.Data != null)
        {
            SaveData data = MetaProgressionManager.Instance.Data;
            int bestMins = Mathf.FloorToInt(data.bestTime / 60f);
            int bestSecs = Mathf.FloorToInt(data.bestTime % 60f);

            if (_victoryRecordsText != null)
            {
                _victoryRecordsText.text = $"Meilleur temps : {bestMins:00}:{bestSecs:00}\nMeilleur kills : {data.bestKills}\nRuns totales : {data.totalRuns}";
            }
        }

        if (_victoryBuildListText != null && LevelUpManager.Instance != null)
        {
            List<string> lines = LevelUpManager.Instance.GetUpgradesList();

            if (lines.Count <= _buildListMaxLinesPerColumn)
            {
                // Tout tient dans une seule colonne
                _victoryBuildListText.text = string.Join("\n", lines);
                if (_victoryBuildListText2 != null)
                    _victoryBuildListText2.text = "";
            }
            else
            {
                // Découpage en 2 colonnes, la première reçoit la moitié haute (arrondi vers le haut)
                int splitIndex = Mathf.CeilToInt(lines.Count / 2f);
                _victoryBuildListText.text = string.Join("\n", lines.GetRange(0, splitIndex));

                if (_victoryBuildListText2 != null)
                    _victoryBuildListText2.text = string.Join("\n", lines.GetRange(splitIndex, lines.Count - splitIndex));
            }
        }
    }

    public void SetCrystalReady(bool ready)
    {
        if (_ultReadyEffect != null)
            _ultReadyEffect.SetActive(ready);

        if (_crystalIcons == null) return;

        Color targetColor = ready ? Color.white : new Color(0.2f, 0.2f, 0.2f);
        foreach (var icon in _crystalIcons)
        {
            if (icon != null) icon.color = targetColor;
        }
    }

    public void SetHUDVisible(bool visible)
    {
        if (_hudPanel != null)
            _hudPanel.SetActive(visible);
    }

    public void ShowUltEffect(bool show)
    {
        Debug.Log(show ? "ULT ACTIF — ennemis ralentis !" : "ULT terminé");
    }

    public void UpdateDashCooldown(float percent)
    {
        if (_dashCooldownBar != null)
            _dashCooldownBar.value = Mathf.Clamp01(percent);
    }

    public void UpdateGold(int amount)
    {
        if (_goldText != null)
            _goldText.text = $"Or : {amount}"; // Ajout d'un label lisible
    }

    public void UpdateKillCount(int kills)
    {
        if (_killCountText != null)
            _killCountText.text = $"Kills : {kills}";
    }

    public void UpdateXPBar(float currentXP, float xpToNextLevel, int level)
    {
        if (_xpBar != null)
        {
            // CORRECTION CRITIQUE : Protection contre la division par zéro
            _xpBar.value = (xpToNextLevel > 0) ? currentXP / xpToNextLevel : 0f;
        }

        if (_levelText != null)
            _levelText.text = $"Niv. {level}";
    }

    public void UpdateHPBar(float currentHP, float maxHP)
    {
        if (_hpBar == null) return;

        // CORRECTION CRITIQUE : Protection contre la division par zéro
        float percent = (maxHP > 0) ? currentHP / maxHP : 0f;
        _hpBar.value = percent;

        if (_hpText != null)
        {
            _hpText.text = $"{Mathf.CeilToInt(Mathf.Max(0, currentHP))} / {Mathf.CeilToInt(maxHP)}";
        }

        if (_hpFillImage == null) return;

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
        if (_bossHPBar != null) _bossHPBar.SetActive(true);
        if (_bossNameText != null)
        {
            _bossNameText.gameObject.SetActive(true);
            _bossNameText.text = bossName;
        }
        if (_bossHPSlider != null) _bossHPSlider.value = 1f;
        if (_bossIcon != null) _bossIcon.SetActive(true);
    }

    public void UpdateBossHP(float current, float max)
    {
        if (_bossHPSlider != null)
        {
            _bossHPSlider.value = (max > 0) ? current / max : 0f;
        }
    }

    public void HideBossHP()
    {
        if (_bossHPBar != null) _bossHPBar.SetActive(false);
        if (_bossNameText != null) _bossNameText.gameObject.SetActive(false);
        if (_bossIcon != null) _bossIcon.SetActive(false);
    }

    public void ShowGameOver(float runTimer, int killCount, int goldEarned)
    {
        if (_gameOverPanel != null) _gameOverPanel.SetActive(true);

        int mins = Mathf.FloorToInt(runTimer / 60f);
        int secs = Mathf.FloorToInt(runTimer % 60f);

        if (_statsText != null)
        {
            _statsText.text = $"Temps de survie : {mins:00}:{secs:00}\nEnnemis tués : {killCount}\nGold gagné : {goldEarned}";
        }
    }

    public void ShowPausePanel(bool show)
    {
        if (_pausePanel != null) _pausePanel.SetActive(show);

        if (show)
        {
            float runTime = (GameManager.Instance != null) ? GameManager.Instance.RunTimer : 0f;
            int kills = (GameManager.Instance != null) ? GameManager.Instance.KillCount : 0;
            int gold = (MetaProgressionManager.Instance != null) ? MetaProgressionManager.Instance.RunGold : 0;

            int mins = Mathf.FloorToInt(runTime / 60f);
            int secs = Mathf.FloorToInt(runTime % 60f);

            if (_pauseStatsText != null)
            {
                _pauseStatsText.text = $"Temps : {mins:00}:{secs:00}\nEnnemis tués : {kills}\nGold : {gold}";
            }

            if (_pauseUpgradesText != null && LevelUpManager.Instance != null)
            {
                _pauseUpgradesText.text = LevelUpManager.Instance.GetUpgradesSummary();
            }

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