using System.Collections.Generic;

public static class SkillTreeData
{
    public class NodeData
    {
        // AJOUTE - chaque noeud connait desormais son propre id (auparavant
        // seule la clef du Dictionary le connaissait) - necessaire pour pouvoir
        // enumerer tous les noeuds (via All) et retrouver leur id d'origine,
        // par exemple pour chercher "le noeud le plus proche d'etre debloque".
        public string id;
        public string displayName;
        public string description;
        public bool isUnique;
        public string level1Desc;
        public string level2Desc;
        public string level3Desc;
        public int costLevel1;
        public int costLevel2;
        public int costLevel3;
        public string[] prerequisites;
        public CharacterBranch branch; // AJOUTÉ — pour le grisage selon le personnage
        // AJOUTÉ (2026-10-27, refonte de l'économie - retour utilisateur : "les 2 nœuds les plus bas, pas de contrepartie,
        // les 2 au milieu, pour les acheter il faut avoir remporté une partie en Modéré, et le dernier tout en haut [...]
        // avoir réussi une partie en Violent") - palier de difficulté (DifficultyTier) dans lequel le personnage de la
        // branche doit avoir gagné une partie du Classique pour acheter ce nœud ; une victoire dans un palier plus dur
        // compte aussi. -1 = aucune condition (les 2 nœuds du bas). Lu par MetaProgressionManager.IsNodeUnlockable.
        public int requiredWinTier = -1;
    }

    // AJOUTÉ — enum pour identifier la branche de chaque nœud
    public enum CharacterBranch
    {
        Guerrier,
        Gardien,
        Fantome
    }

    private static readonly Dictionary<string, NodeData> _nodesCache;

    static SkillTreeData()
    {
        _nodesCache = new Dictionary<string, NodeData>();
        PopulateDatabase();
    }

    public static NodeData Get(string nodeId)
    {
        if (string.IsNullOrEmpty(nodeId)) return null;
        return _nodesCache.TryGetValue(nodeId, out NodeData node) ? node : null;
    }

    // AJOUTE - enumere tous les noeuds enregistres, toutes branches confondues.
    // Sert par exemple a MetaProgressionManager.GetNextUnlockPreview() pour
    // chercher le noeud le plus proche d'etre debloque, sans dupliquer la
    // liste des ids ailleurs.
    public static IEnumerable<NodeData> All => _nodesCache.Values;

    // AJOUTE - petit wrapper qui assigne automatiquement l'id sur le NodeData
    // au moment de l'enregistrement, pour ne jamais desynchroniser la clef du
    // Dictionary et le champ NodeData.id (une seule ecriture de l'id, ici,
    // plutot que de le dupliquer a la main sur chacune des entrees ci-dessous).
    private static void AddNode(string id, NodeData node)
    {
        node.id = id;
        _nodesCache.Add(id, node);
    }

    // MODIFIÉ (2026-10-27, refonte de l'économie - retour utilisateur : "il faut que ce soit bien plus long de tout
    // débloquer pour un personnage, pour forcer le joueur à faire plusieurs parties") - SEULE source des coûts de l'arbre
    // (MetaProgressionManager.GetNodeCost les lit ici). Une branche complète coûte ~30 600 or, soit ~30 parties avec le
    // barème de gains de MetaProgressionManager (victoire ~1 000 or en Modéré, ~1 700 en Violent, ~3 000 en Apocalyptique,
    // mesuré sur 6 parties automatiques). Bas : 150 / 600 / 1 800 (unique : 2 500), premier achat dès la 1re partie.
    // Milieu : 1 000 / 2 500 / 5 000 (unique : 5 000). Haut : 12 000.
    private static void PopulateDatabase()
    {
        // ── GUERRIER ────────────────────────────────────────────────────

        // AJOUTÉ — remplace "damage" comme point d'entrée sans prérequis
        AddNode("concentration", new NodeData
        {
            displayName = "Concentration",
            // CORRIGÉ (2026-10-25, relecture des descriptions) - le texte annonçait +8 %/s à tous les paliers, alors que
            // la montée réelle est de 2/3/5 %/s depuis le 2026-09-16 (MetaProgressionManager.GetConcentrationRampPerSecond).
            description = "Chaque seconde sans subir de dégâts augmente tes dégâts, jusqu'à un plafond. Le bonus retombe à zéro à chaque coup reçu.",
            isUnique = false,
            level1Desc = "+2% / s, plafond +15%",
            level2Desc = "+3% / s, plafond +30%",
            level3Desc = "+5% / s, plafond +50%",
            costLevel1 = 150,
            costLevel2 = 600,
            costLevel3 = 1800,
            prerequisites = System.Array.Empty<string>(),
            branch = CharacterBranch.Guerrier
        });

        AddNode("cadence", new NodeData
        {
            displayName = "Cadence",
            // CORRIGÉ (2026-10-25) - le bonus n'est lu que par WeaponBase (tir de base), pas par les autres armes.
            description = "Augmente la cadence de ton tir de base.",
            isUnique = false,
            level1Desc = "+10%",
            level2Desc = "+20%",
            level3Desc = "+35%",
            costLevel1 = 150,
            costLevel2 = 600,
            costLevel3 = 1800,
            prerequisites = System.Array.Empty<string>(),
            branch = CharacterBranch.Guerrier
        });

        AddNode("fragmentation", new NodeData
        {
            displayName = "Fragmentation",
            // CORRIGÉ (2026-10-25) - seuls les projectiles du tir de base (WeaponBase.FireProjectile) peuvent exploser.
            // MODIFIÉ (2026-10-27, retour utilisateur : "très peu efficace en jeu") - 1 projectile sur 4, dégâts complets,
            // grand rayon (au lieu de 15 % de chance, 50 % des dégâts, rayon 2). Voir WeaponBase.FireProjectile.
            description = "Un projectile sur 4 de ton tir de base explose à l'impact et inflige tous ses dégâts aux ennemis proches.",
            isUnique = true,
            costLevel1 = 5000,
            prerequisites = new[] { "concentration" }, // MODIFIÉ — était "damage"
            requiredWinTier = 0,
            branch = CharacterBranch.Guerrier
        });

        AddNode("crystalDamage", new NodeData
        {
            displayName = "Dégâts Cristal",
            description = "Augmente les dégâts de l'ultime et de la Nova.",
            isUnique = false,
            level1Desc = "+25%",
            level2Desc = "+50%",
            level3Desc = "+100%",
            costLevel1 = 1000,
            costLevel2 = 2500,
            costLevel3 = 5000,
            prerequisites = new[] { "cadence" },
            requiredWinTier = 0,
            branch = CharacterBranch.Guerrier
        });

        AddNode("overpower", new NodeData
        {
            displayName = "Surpuissance",
            // CORRIGÉ (2026-10-25) - la durée réelle est de 10 s depuis le 2026-10-22 (CrystalSystem.OverpowerBuff).
            description = "Après l'Ultime, tes dégâts sont doublés pendant 10 secondes.",
            isUnique = true,
            costLevel1 = 12000,
            prerequisites = new[] { "fragmentation", "crystalDamage" },
            requiredWinTier = 1,
            branch = CharacterBranch.Guerrier
        });

        // ── GARDIEN ─────────────────────────────────────────────────────

        AddNode("vitality", new NodeData
        {
            displayName = "Vitalité",
            description = "Augmente tes points de vie maximum.",
            isUnique = false,
            level1Desc = "+15%",
            level2Desc = "+30%",
            level3Desc = "+50%",
            costLevel1 = 150,
            costLevel2 = 600,
            costLevel3 = 1800,
            prerequisites = System.Array.Empty<string>(),
            branch = CharacterBranch.Gardien
        });

        // AJOUTÉ — remplace "regen" comme point d'entrée sans prérequis
        AddNode("recuperation", new NodeData
        {
            displayName = "Récupération",
            // CORRIGÉ (2026-10-25) - le Bouclier de Mana passe aussi par CrystalSystem.AbsorbProjectile, donc soigne aussi.
            description = "Chaque projectile absorbé, par le dash ou par le Bouclier de Mana, restaure des PV.",
            isUnique = false,
            level1Desc = "+20 PV par absorption",
            level2Desc = "+50 PV par absorption",
            level3Desc = "+80 PV par absorption",
            costLevel1 = 150,
            costLevel2 = 600,
            costLevel3 = 1800,
            prerequisites = System.Array.Empty<string>(),
            branch = CharacterBranch.Gardien
        });

        AddNode("secondWind", new NodeData
        {
            displayName = "Second Souffle",
            // CORRIGÉ (2026-10-25) - ajoute les 3 s d'invincibilité de HealthSystem.TriggerSecondWind, jamais mentionnées.
            description = "Une fois par partie, tu survis à un coup fatal avec 1 PV, puis tu deviens invincible pendant 3 secondes.",
            isUnique = true,
            costLevel1 = 5000,
            prerequisites = new[] { "vitality" },
            requiredWinTier = 0,
            branch = CharacterBranch.Gardien
        });

        AddNode("armor", new NodeData
        {
            displayName = "Armure",
            description = "Réduit les dégâts reçus de tous les ennemis.",
            isUnique = false,
            level1Desc = "-8%",
            level2Desc = "-15%",
            level3Desc = "-25%",
            costLevel1 = 1000,
            costLevel2 = 2500,
            costLevel3 = 5000,
            prerequisites = new[] { "recuperation" }, // MODIFIÉ — était "regen"
            requiredWinTier = 0,
            branch = CharacterBranch.Gardien
        });

        AddNode("manaShield", new NodeData
        {
            displayName = "Bouclier de Mana",
            // CORRIGÉ (2026-10-25) - l'ancien texte décrivait une version abandonnée (1 projectile toutes les 8 s). Valeurs
            // réelles : ManaShield._maxCharges / _rechargeInterval / _breakLockoutDuration.
            description = "Bulle de 3 charges qui bloque les projectiles ennemis et les absorbe comme le dash. Recharge : 1 charge toutes les 3 secondes, bloquée 4 secondes si la bulle est brisée.",
            isUnique = true,
            costLevel1 = 12000,
            prerequisites = new[] { "secondWind", "armor" },
            requiredWinTier = 1,
            branch = CharacterBranch.Gardien
        });

        // ── FANTÔME ─────────────────────────────────────────────────────

        // AJOUTÉ — remplace "agility" comme point d'entrée sans prérequis
        AddNode("impulsionNova", new NodeData
        {
            displayName = "Impulsion Nova",
            description = "Si la Nova déclenchée par une absorption tue au moins un ennemi, le temps de recharge du dash est immédiatement réinitialisé.",
            isUnique = true,
            costLevel1 = 2500,
            prerequisites = System.Array.Empty<string>(),
            branch = CharacterBranch.Fantome
        });

        AddNode("dash", new NodeData
        {
            displayName = "Dash Amélioré",
            description = "Réduit le temps de recharge du dash.",
            isUnique = false,
            level1Desc = "Recharge -0,3 s",
            level2Desc = "Recharge -0,6 s",
            level3Desc = "Recharge -1 s",
            costLevel1 = 150,
            costLevel2 = 600,
            costLevel3 = 1800,
            prerequisites = System.Array.Empty<string>(),
            branch = CharacterBranch.Fantome
        });

        AddNode("crystalMastery", new NodeData
        {
            displayName = "Maîtrise du Cristal",
            // CORRIGÉ (2026-10-18, diagnostic complet - audit §7 "sans chiffre, contrairement aux autres
            // compétences") - valeur réelle lue dans CrystalSystem.Start() : _maxCharges 6 -> 5.
            // CORRIGÉ (2026-10-18, Unity reconnecté en cours de session - vérifié en direct) : "→" (U+2192) n'est
            // couvert ni par Bangers (langues latines) ni par les polices CJK réduites du jeu (zh/ja/ko) - glyphe
            // manquant détecté par Tools/build_language_fonts.py après reconnexion d'Unity. Remplacé par "à".
            description = "Réduit de 1 le nombre de charges de Nova nécessaires pour obtenir l'Ultime (6 à 5).",
            isUnique = true,
            costLevel1 = 5000,
            prerequisites = new[] { "impulsionNova" }, // MODIFIÉ — était "agility"
            requiredWinTier = 0,
            branch = CharacterBranch.Fantome
        });

        AddNode("novaRadius", new NodeData
        {
            displayName = "Nova Étendue",
            description = "Augmente le rayon de la Nova de Cristal.",
            isUnique = false,
            level1Desc = "+30%",
            level2Desc = "+60%",
            level3Desc = "+100%",
            costLevel1 = 1000,
            costLevel2 = 2500,
            costLevel3 = 5000,
            prerequisites = new[] { "dash" },
            requiredWinTier = 0,
            branch = CharacterBranch.Fantome
        });

        AddNode("phantomDash", new NodeData
        {
            // CORRIGÉ (2026-10-18, diagnostic complet - audit §7 "le nom ne correspond pas à l'effet") - l'ancien
            // nom "Dash Fantôme" datait d'une version où le dash lui-même déclenchait l'effet (voir commentaire plus
            // bas) ; ce n'est plus un dash du tout aujourd'hui, mais un clone invoqué par une touche dédiée.
            displayName = "Clone Spectral",
            // MODIFIE - l'ancien texte ("le dash laisse un clone") decrivait un
            // declenchement automatique via le dash, qui ne correspond plus a
            // l'implementation actuelle (touche dediee, independante du dash).
            // Pas de nom de touche ici : depend des futurs parametres de remapping.
            // CORRIGÉ (2026-10-25) - valeurs réelles du prefab de Lyra : 3 s (et non 2), recharge 8 s, TOUS les ennemis
            // ciblent le clone (PlayerController.TryAttractToPhantom, sans rayon) et le joueur est invincible pendant ce temps.
            description = "Débloque un Clone spectral, activable via une touche dédiée : pendant 3 secondes, il attire tous les ennemis et tu deviens invincible. Recharge : 8 secondes.",
            isUnique = true,
            costLevel1 = 12000,
            prerequisites = new[] { "crystalMastery", "novaRadius" },
            requiredWinTier = 1,
            branch = CharacterBranch.Fantome
        });
    }
}