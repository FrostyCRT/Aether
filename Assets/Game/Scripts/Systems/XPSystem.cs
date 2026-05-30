using UnityEngine;

public class XPSystem : MonoBehaviour
{
    // Singleton
    public static XPSystem Instance { get; private set; }

    [Header("Stats")]
    [SerializeField] private float _baseXP = 50f; // XP nécessaire au niveau 1

    public int   CurrentLevel { get; private set; } = 1;
    public float CurrentXP    { get; private set; } = 0f;
    public float XPToNextLevel => Mathf.Floor(_baseXP * Mathf.Pow(CurrentLevel, 1.5f));
    // Formule : plus on monte en niveau, plus il faut d'XP

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void AddXP(float amount)
    {
        if (GameManager.Instance.IsGameOver) return;

        // Applique le bonus méta d'XP
        float bonusXP = MetaProgressionManager.Instance.GetBonusXP();
        amount += amount * bonusXP;

        CurrentXP += amount;
        GameUI.Instance.UpdateXPBar(CurrentXP, XPToNextLevel, CurrentLevel);

        while (CurrentXP >= XPToNextLevel)
        {
            CurrentXP -= XPToNextLevel;
            LevelUp();
            GameUI.Instance.UpdateXPBar(CurrentXP, XPToNextLevel, CurrentLevel);
        }
    }

    private void LevelUp()
    {
        CurrentLevel++;
        Debug.Log($"LEVEL UP ! Niveau {CurrentLevel}");
        LevelUpManager.Instance.ShowLevelUp();
    }
}