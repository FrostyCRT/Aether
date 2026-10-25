using UnityEngine;
public class XPSystem : MonoBehaviour
{
    public static XPSystem Instance { get; private set; }
    [Header("Stats")]
    // MODIFIÉ (2026-10-16, audit version finale) - 50 -> 55 (valeur de la scène Game aussi) : le reste d'XP < 10 n'est
    // plus perdu à chaque mort (XPGemSpawner._xpCarry, ~+6 % d'XP sur une partie) et les phases de spawn complètes
    // (jusqu'à 160 ennemis) donnent plus de kills - courbe allégée de 10 %, à affiner en playtest.
    // MODIFIÉ (2026-10-21, retour utilisateur : "tu as réduit l'xp que donnait les ennemis, mais le problème c'est
    // que maintenant [...] un ennemi ne donne pas dans tous les cas de l'xp, ce qui n'est pas normal dans un
    // survivor [...] on devrait plutôt rallonger l'xp qu'il faut selon le niveau de la barre, surtout dans les
    // hauts niveaux") - les 2 précédents correctifs de rythme (suites 17/19/20) réduisaient directement l'XP
    // DONNÉE par chaque ennemi (jusqu'à x0,27 au final) : sous le seuil d'une gemme "Small" (10, voir
    // XPGemSpawner.CalculateGems), une bonne partie des kills ne faisait plus apparaître AUCUNE gemme visible au
    // sol (juste un report interne invisible, `XPGemSpawner._xpCarry`) - cassait le geste de base d'un survivor
    // ("ramasser plein d'XP au sol"). Annulé (voir `GlobalXpMultiplier`, retiré de `EnemyBase.Die()`/
    // `BossBase.Die()` - chaque ennemi redonne sa pleine valeur, donc au moins 1 gemme visible par kill la plupart
    // du temps) - le même ralentissement global est maintenant obtenu en rallongeant le COÛT d'un niveau plutôt que
    // la RÉCOMPENSE d'un kill : `_baseXP` monte à 70 (était 55) ET l'exposant de la courbe à 2 (était 1,5, voir
    // XPToNextLevel ci-dessous) - un exposant plus fort pèse de plus en plus à mesure que le niveau augmente, donc
    // l'allongement se fait "surtout dans les hauts niveaux" comme demandé, sans trop changer le rythme des tout
    // premiers niveaux. Calibré sur le même point de mesure réel que les correctifs précédents (niveau 15 à 5 min
    // en Apocalyptique, avec x0,9 déjà actif à ce moment-là -> total brut extrapolé sans aucune réduction ≈ 19550
    // XP) : avec ce nouveau barème, ce même total atterrit à peine avant le niveau 10 (cumul pour l'atteindre
    // ≈ 19950) - cohérent avec la cible "~niveau 9-10 à 5 min" discutée.
    // MODIFIÉ (2026-10-24, retour utilisateur : "augmente l'xp qu'il faut pour passer d'un niveau à un autre dans la
    // globalité des difficultés, augmente un peu pour avoir une différence, mais pas trop") - 70 -> 80 (+14 % sur
    // CHAQUE niveau, même exposant 2, valeur de la scène Game aussi) : ~niveau 9 au lieu de ~10 au même total d'XP.
    [SerializeField] private float _baseXP = 80f;
    public int CurrentLevel { get; private set; } = 1;
    public float CurrentXP { get; private set; } = 0f;
    // Utilisation de la formule mathématique simplifiée : $XP = \lfloor baseXP \times Level^{2} \rfloor$
    public float XPToNextLevel => Mathf.Floor(_baseXP * Mathf.Pow(CurrentLevel, 2f));
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
        // Ruée de boss : le joueur doit monter plus vite (XP ×1,5 - corrigé 2026-10-18, diagnostic complet : le
        // commentaire disait ×3, GameModes.RushXpMultiplier vaut bien 1,5), voir GameModes.RushXpMultiplier.
        amount *= GameModes.XpMultiplier;
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

        // AJOUTE - notifie le systeme de defis a chaque montee de niveau (defi
        // "Ascension Rapide" = atteindre le niveau 18 avant la 10e minute, voir ChallengeManager.FastAscentLevel).
        // Doit etre verifie ICI, au moment exact ou le niveau est atteint, pas en
        // fin de run - un joueur qui atteint le niveau 20 tot puis continue la run
        // plus longtemps doit quand meme valider le defi.
        if (ChallengeManager.Instance != null)
            ChallengeManager.Instance.NotifyLevelChanged(CurrentLevel);

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