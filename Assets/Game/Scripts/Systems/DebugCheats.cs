using UnityEngine;

// AJOUTE (2026-09-16) - raccourcis DEBUG/TEST uniquement, pour iterer vite sur
// le combat du Boss 3 (retour utilisateur : "battre les 2 boss et attendre le
// delai entre les boss c'est long" + "il faut avoir les competences card et un
// nombre de vie illimite pour faire des meilleurs tests"). Inerte en dehors de
// l'Editeur/d'un build de developpement (voir Awake) - jamais actif en
// production. A retirer du composant sur PausePanel/Game avant release, comme
// les autres restes de debug deja notes dans NOTES.md (ResetSkillTree,
// DebugUnlockAllCharacters, etc.).
public class DebugCheats : MonoBehaviour
{
    [Header("Raccourcis")]
    [Tooltip("Detruit le boss en cours s'il y en a un, puis spawn directement le Boss 3 - sans avoir a battre les Boss 1/2 ni attendre les delais entre eux.")]
    [SerializeField] private KeyCode _skipToBoss3Key = KeyCode.F5;
    [Tooltip("Applique toutes les ameliorations disponibles jusqu'a leur palier max (respecte les armes exclusives par personnage).")]
    [SerializeField] private KeyCode _grantFullBuildKey = KeyCode.F6;
    [Tooltip("Bascule l'invincibilite du joueur (dégâts totalement ignorés) et remonte les PV au max a l'activation.")]
    [SerializeField] private KeyCode _toggleGodModeKey = KeyCode.F7;
    [Tooltip("Bascule l'invincibilite du boss actif (dégâts totalement ignorés) - pour observer un combat/pattern sans qu'il ne meure trop vite.")]
    [SerializeField] private KeyCode _toggleBossInvincibleKey = KeyCode.F8;

    // Securite max par ameliorations pour eviter une boucle infinie si
    // IsAvailable() ne redevient jamais false pour une raison inattendue.
    private const int MaxPicksPerUpgrade = 10;

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
        if (Input.GetKeyDown(_skipToBoss3Key))
            SkipToBoss3();

        if (Input.GetKeyDown(_grantFullBuildKey))
            GrantFullBuild();

        if (Input.GetKeyDown(_toggleGodModeKey))
            ToggleGodMode();

        if (Input.GetKeyDown(_toggleBossInvincibleKey))
            ToggleBossInvincible();
    }

    private void SkipToBoss3()
    {
        if (WaveManager.Instance == null)
        {
            Debug.LogWarning("[DebugCheats] WaveManager introuvable.");
            return;
        }

        WaveManager.Instance.DebugSkipToBoss3();
        Debug.Log("[DebugCheats] Boss 3 spawné directement.");
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
            if (upgrade == null) continue;

            int picksThisUpgrade = 0;
            while (upgrade.IsAvailable() && picksThisUpgrade < MaxPicksPerUpgrade)
            {
                upgrade.Apply();
                picksThisUpgrade++;
                totalPicks++;
            }
        }

        Debug.Log($"[DebugCheats] Build complète appliquée ({totalPicks} paliers).");
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

    private void ToggleBossInvincible()
    {
        BossBase.DebugInvincible = !BossBase.DebugInvincible;
        Debug.Log($"[DebugCheats] Invincibilité du boss {(BossBase.DebugInvincible ? "ACTIVÉE" : "désactivée")}.");
    }
}
