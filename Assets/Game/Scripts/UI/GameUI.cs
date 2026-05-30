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

    [Header("Vague et Timer")]
    [SerializeField] private TextMeshProUGUI _waveText;
    [SerializeField] private TextMeshProUGUI _timerText;

    [Header("Boss")]
    [SerializeField] private GameObject      _bossHPBar;
    [SerializeField] private Slider          _bossHPSlider;
    [SerializeField] private TextMeshProUGUI _bossNameText;
    [SerializeField] private GameObject      _bossIcon; // ← ajoute ça

    [Header("Game Over")]
    [SerializeField] private GameObject      _gameOverPanel;
    [SerializeField] private TextMeshProUGUI _statsText;


    [Header("Gold")]
    [SerializeField] private TextMeshProUGUI _goldText;

    [Header("Dash")]
    [SerializeField] private Slider _dashCooldownBar;

    [Header("Cristal")]
    [SerializeField] private UnityEngine.UI.Image[] _crystalIcons; // 15 icônes
    [SerializeField] private GameObject             _ultReadyEffect;

    public void UpdateCrystalCharge(int current, int max)
    {
        for (int i = 0; i < _crystalIcons.Length; i++)
        {
            if (i < current)
                _crystalIcons[i].color = new Color(0f, 0.8f, 1f); // Bleu cyan allumé
            else
                _crystalIcons[i].color = new Color(0.2f, 0.2f, 0.2f); // Gris éteint
        }
    }

    public void SetCrystalReady(bool ready)
    {
        if (_ultReadyEffect != null)
            _ultReadyEffect.SetActive(ready);

        if (!ready)
        {
            // Reset complet en gris
            foreach (var icon in _crystalIcons)
                icon.color = new Color(0.2f, 0.2f, 0.2f);
        }
        else
        {
            // Tous en blanc pour signaler que c'est prêt
            foreach (var icon in _crystalIcons)
                icon.color = Color.white;
        }
    }

    public void ShowUltEffect(bool show)
    {
        // On peut ajouter un overlay visuel plus tard
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
            _goldText.text = $"Gold : {amount}";
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

    }

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

    public void UpdateWave(int wave)
    {
        if (_waveText == null) return;

        if (wave == -1)
        {
            _waveText.text  = "BOSS !";
            _waveText.color = Color.red;
        }
        else
        {
            _waveText.text  = $"Vague {wave}";
            _waveText.color = Color.white;
        }
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
        if (_bossIcon != null) _bossIcon.SetActive(true); // ← ajoute ça
   }

   public void UpdateBossHP(float current, float max)
   {
        _bossHPSlider.value = current / max;
   }

   public void HideBossHP()
   {
        _bossHPBar.SetActive(false);
        _bossNameText.gameObject.SetActive(false);
        if (_bossIcon != null) _bossIcon.SetActive(false); // ← ajoute ça
   }

   public void ShowGameOver(float runTimer, int killCount, int wave, int goldEarned)
   {
        _gameOverPanel.SetActive(true);

        int mins = Mathf.FloorToInt(runTimer / 60f);
        int secs = Mathf.FloorToInt(runTimer % 60f);

        _statsText.text = $"Temps de survie : {mins:00}:{secs:00}\n" +
                          $"Ennemis tués : {killCount}\n" +
                          $"Vague atteinte : {wave}\n" +
                          $"Gold gagné : {goldEarned}";
    }

    private void Start()
    {
        if (MetaProgressionManager.Instance != null)
            UpdateGold(MetaProgressionManager.Instance.RunGold);
    }
}