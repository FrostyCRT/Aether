using UnityEngine;

// AJOUTE (2026-09-16, revu 2026-09-27) - raccourcis DEBUG/TEST uniquement, pour
// iterer vite sur le jeu sans attendre. Inerte en dehors de l'Editeur/d'un
// build de developpement (voir Awake) - jamais actif en production. A retirer
// du composant sur PausePanel/Game avant release, comme les autres restes de
// debug deja notes dans NOTES.md (ResetSkillTree, DebugUnlockAllCharacters, etc.).
//
// MODIFIE (2026-09-27) - jeu de raccourcis entierement redefini a la demande
// utilisateur : F1/F2/F3 amenent directement aux 3 boss classiques (5/10/15
// min), F4 donne toutes les ameliorations SAUF les fusions (pour tester une
// fusion precise sans l'avoir deja toute faite), F5 declenche un level-up,
// F6 bascule l'invincibilite du joueur, F7 bascule l'invincibilite de TOUS les
// ennemis (normaux + boss). Remplace l'ancien jeu (F5 boss3/F6 build complete
// avec fusions/F7 dieu/F8 boss seul).
public class DebugCheats : MonoBehaviour
{
    [Header("Raccourcis")]
    [Tooltip("Avance le chrono a 5 min et spawn directement le Boss 1 (comme si on venait de l'atteindre en Classique).")]
    [SerializeField] private KeyCode _reachBoss1Key = KeyCode.F1;
    [Tooltip("Avance le chrono a 10 min et spawn directement le Boss 2.")]
    [SerializeField] private KeyCode _reachBoss2Key = KeyCode.F2;
    [Tooltip("Avance le chrono a 15 min et spawn directement le Boss 3.")]
    [SerializeField] private KeyCode _reachBoss3Key = KeyCode.F3;
    [Tooltip("Applique toutes les ameliorations disponibles jusqu'a leur palier max, SAUF les fusions (respecte les armes exclusives par personnage) - pour tester une fusion precise sans que le build soit deja fusionne partout.")]
    [SerializeField] private KeyCode _grantFullBuildKey = KeyCode.F4;
    [Tooltip("Declenche un level-up (fait apparaitre les 3 cartes a choisir), comme si le joueur venait de monter de niveau.")]
    [SerializeField] private KeyCode _forceLevelUpKey = KeyCode.F5;
    [Tooltip("Bascule l'invincibilite du joueur (degats totalement ignores) et remonte les PV au max a l'activation.")]
    [SerializeField] private KeyCode _toggleGodModeKey = KeyCode.F6;
    [Tooltip("Bascule l'invincibilite de TOUS les ennemis (normaux ET boss) - pour observer un combat/pattern sans rien tuer.")]
    [SerializeField] private KeyCode _toggleEnemiesInvincibleKey = KeyCode.F7;

    // Securite max par ameliorations pour eviter une boucle infinie si
    // IsAvailable() ne redevient jamais false pour une raison inattendue.
    private const int MaxPicksPerUpgrade = 10;

    // Calibre pour le Classique (3 boss a 5/10/15 min) - voir GameManager.DebugSetRunTimer.
    private const float Boss1Minute = 5f;
    private const float Boss2Minute = 10f;
    private const float Boss3Minute = 15f;

    private bool _godModeActive = false;

    private void Awake()
    {
        // Inerte hors Editeur / build de developpement - jamais actif dans un
        // build final entre les mains d'un joueur.
        if (!Application.isEditor && !Debug.isDebugBuild)
        {
            enabled = false;
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(_reachBoss1Key))
            ReachBoss(Boss1Minute);

        if (Input.GetKeyDown(_reachBoss2Key))
            ReachBoss(Boss2Minute);

        if (Input.GetKeyDown(_reachBoss3Key))
            ReachBoss(Boss3Minute);

        if (Input.GetKeyDown(_grantFullBuildKey))
            GrantFullBuild();

        if (Input.GetKeyDown(_forceLevelUpKey))
            ForceLevelUp();

        if (Input.GetKeyDown(_toggleGodModeKey))
            ToggleGodMode();

        if (Input.GetKeyDown(_toggleEnemiesInvincibleKey))
            ToggleEnemiesInvincible();
    }

    // CORRIGE (2026-09-27) - ne fait plus QUE avancer le chrono : ne spawne plus le boss directement (c'était le bug
    // signalé, "le boss spawne mais le temps n'avance pas" - WaveManager.Update() s'arrête avant de rafraîchir le
    // HUD dès qu'un boss est en vie, donc le chrono affiché restait figé sur sa valeur d'AVANT ce raccourci). Le boss
    // apparaît de lui-même dès la frame suivante, via la règle normale du jeu (WaveManager.Update()) : si le joueur a
    // déjà battu les boss précédents, c'est le bon qui spawne (ex: F2 sans avoir battu le Boss 1 refait spawner le
    // Boss 1, exactement comme s'il avait fallu 10 min pour l'atteindre - comportement normal, pas un bug).
    private void ReachBoss(float minute)
    {
        if (GameManager.Instance == null)
        {
            Debug.LogWarning("[DebugCheats] GameManager introuvable.");
            return;
        }

        GameManager.Instance.DebugSetRunTimer(minute * 60f);
        Debug.Log($"[DebugCheats] Chrono avancé à {minute:00} min.");
    }

    private void GrantFullBuild()
    {
        if (LevelUpManager.Instance == null || LevelUpManager.Instance.AllUpgrades == null)
        {
            Debug.LogWarning("[DebugCheats] LevelUpManager introuvable.");
            return;
        }

        int totalPicks = 0;
        foreach (UpgradeData upgrade in LevelUpManager.Instance.AllUpgrades)
        {
            // Exclut les fusions : le but de ce raccourci est de préparer un build presque complet SANS fusion
            // déjà faite, pour pouvoir en tester une précise ensuite.
            if (upgrade == null || upgrade.upgradeType == UpgradeType.Fusion) continue;

            int picksThisUpgrade = 0;
            while (upgrade.IsAvailable() && picksThisUpgrade < MaxPicksPerUpgrade)
            {
                upgrade.Apply();
                picksThisUpgrade++;
                totalPicks++;
            }
        }

        Debug.Log($"[DebugCheats] Build complète appliquée, fusions exclues ({totalPicks} paliers).");
    }

    private void ForceLevelUp()
    {
        if (LevelUpManager.Instance == null)
        {
            Debug.LogWarning("[DebugCheats] LevelUpManager introuvable.");
            return;
        }

        LevelUpManager.Instance.ShowLevelUp();
        Debug.Log("[DebugCheats] Level-up déclenché.");
    }

    private void ToggleGodMode()
    {
        GameObject player = GameObject.FindWithTag("Player");
        if (player == null)
        {
            Debug.LogWarning("[DebugCheats] Joueur introuvable.");
            return;
        }

        HealthSystem health = player.GetComponent<HealthSystem>();
        if (health == null)
        {
            Debug.LogWarning("[DebugCheats] HealthSystem introuvable sur le joueur.");
            return;
        }

        _godModeActive = !_godModeActive;

        if (_godModeActive)
        {
            health.AddExternalInvincibility();
            health.Heal(1f);
        }
        else
        {
            health.RemoveExternalInvincibility();
        }

        Debug.Log($"[DebugCheats] Invincibilité {(_godModeActive ? "ACTIVÉE" : "désactivée")}.");
    }

    // Bascule les DEUX interrupteurs (ennemis normaux + boss) ensemble : un seul raccourci pour "plus rien ne meurt".
    private void ToggleEnemiesInvincible()
    {
        bool newState = !EnemyBase.DebugInvincible;
        EnemyBase.DebugInvincible = newState;
        BossBase.DebugInvincible = newState;
        Debug.Log($"[DebugCheats] Invincibilité des ennemis (normaux + boss) {(newState ? "ACTIVÉE" : "désactivée")}.");
    }
}
