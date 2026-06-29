using UnityEngine;

public class XPSystem : MonoBehaviour
{
    public static XPSystem Instance { get; private set; }

    [Header("Stats")]
    [SerializeField] private float _baseXP = 50f;

    public int CurrentLevel { get; private set; } = 1;
    public float CurrentXP { get; private set; } = 0f;

    // Utilisation de la formule mathématique simplifiée : $XP = \lfloor baseXP \times Level^{1.5} \rfloor$
    public float XPToNextLevel => Mathf.Floor(_baseXP * Mathf.Pow(CurrentLevel, 1.5f));

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
        // Initialisation propre de la barre d'XP au lancement de la run
        if (GameUI.Instance != null)
        {
            GameUI.Instance.UpdateXPBar(CurrentXP, XPToNextLevel, CurrentLevel);
        }
    }

    public void AddXP(float amount)
    {
        if (GameManager.Instance != null && GameManager.Instance.IsGameOver) return;

        // Application du bonus méta d'XP s'il existe
        if (MetaProgressionManager.Instance != null)
        {
            float bonusXP = MetaProgressionManager.Instance.GetBonusXP();
            amount += amount * bonusXP;
        }

        CurrentXP += amount;

        // CORRECTION CRITIQUE : Sauvegarde du palier actuel avant que le niveau ne change
        while (CurrentXP >= XPToNextLevel)
        {
            float xpRequiredForThisLevel = XPToNextLevel; // On fige la valeur
            LevelUp();
            CurrentXP -= xpRequiredForThisLevel; // On soustrait le bon palier fige
        }

        // Mise à jour de l'UI une seule fois APRÈS la boucle de Level Up pour économiser du CPU
        if (GameUI.Instance != null)
        {
            GameUI.Instance.UpdateXPBar(CurrentXP, XPToNextLevel, CurrentLevel);
        }
    }

    private void LevelUp()
    {
        CurrentLevel++;

        if (LevelUpManager.Instance != null)
        {
            LevelUpManager.Instance.ShowLevelUp();
        }

        // Débloque l'attraction des gemmes au niveau 3
        if (XPGemSpawner.Instance != null)
        {
            XPGemSpawner.Instance.OnLevelUp(CurrentLevel);
        }
    }
}