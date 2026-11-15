using UnityEngine;

// AJOUTÉ (2026-11-15, chantier Grimoire décidé avec l'utilisateur) - collection des premières découvertes, avec une petite
// prime d'éclats pour chacune. Décisions de l'utilisateur :
//  - une découverte = une ACTION du joueur : carte CHOISIE au level-up (coffre compris), fusion OBTENUE, boss BATTU (les échos
//    invoqués par la Source ne comptent pas, ni les cartes données d'office comme le build complet du Choc des titans) ;
//  - prime : 5 éclats par carte, 15 par fusion, 20 par boss (205 pour tout compléter, environ une victoire en Modéré),
//    versée avec les éclats de la partie (ligne « Découvertes » de Dernière partie), sans multiplicateur de palier ni de mode ;
//  - pas d'annonce en jeu (retiré le 2026-11-15 à la demande de l'utilisateur : Grimoire passif, comme un bestiaire qu'on
//    découvre en le rouvrant) ;
//  - les ennemis normaux pourront s'ajouter plus tard (modèles pas encore définitifs).
// Identifiants enregistrés dans SaveData.grimoire : "card:<UpgradeType>", "fusion:<nom de l'asset>", "boss:<1-3>".
// La page elle-même est GrimoireUI (bouton livre en bas à gauche de l'Accueil).
public static class Grimoire
{
    public const int CardEclats = 5;
    public const int FusionEclats = 15;
    public const int BossEclats = 20;

    public static string UpgradeId(UpgradeData upgrade) =>
        upgrade.upgradeType == UpgradeType.Fusion ? "fusion:" + upgrade.name : "card:" + upgrade.upgradeType;

    public static int UpgradeEclats(UpgradeData upgrade) =>
        upgrade.upgradeType == UpgradeType.Fusion ? FusionEclats : CardEclats;

    public static string BossId(int bossNumber) => "boss:" + bossNumber;

    // Les 3 boss ont chacun leur script : la Source corrompue (3), le Cerf (2), le Dragon (BossBase seul, 1).
    public static int BossNumberOf(BossBase boss) => boss is BossCorruptedSource ? 3 : boss is BossDeer ? 2 : 1;

    public static bool IsDiscovered(SaveData data, string id) => data != null && data.grimoire != null && data.grimoire.Contains(id);

    // Carte choisie au level-up (LevelUpManager.SelectUpgrade).
    public static void DiscoverUpgrade(UpgradeData upgrade)
    {
        if (upgrade == null) return;
        Discover(UpgradeId(upgrade), upgrade.Name, UpgradeEclats(upgrade));
    }

    // Boss vaincu (BossBase, mort d'un boss non invoqué).
    public static void DiscoverBoss(BossBase boss, string displayName)
    {
        if (boss == null || boss.IsSummoned) return;
        Discover(BossId(BossNumberOf(boss)), displayName, BossEclats);
    }

    private static void Discover(string id, string displayName, int eclats)
    {
        MetaProgressionManager meta = MetaProgressionManager.Instance;
        if (meta != null) meta.RegisterDiscovery(id, eclats);
    }
}
