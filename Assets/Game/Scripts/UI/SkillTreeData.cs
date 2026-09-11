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

    private static void PopulateDatabase()
    {
        // ── GUERRIER ────────────────────────────────────────────────────

        // AJOUTÉ — remplace "damage" comme point d'entrée sans prérequis
        AddNode("concentration", new NodeData
        {
            displayName = "Concentration",
            description = "Chaque seconde sans recevoir de dégâts augmente tes dégâts (+8% / sec). Le bonus se réinitialise à chaque coup reçu.",
            isUnique = false,
            level1Desc = "+8% / sec, plafond +15%",
            level2Desc = "+8% / sec, plafond +30%",
            level3Desc = "+8% / sec, plafond +50%",
            costLevel1 = 100,
            costLevel2 = 300,
            costLevel3 = 700,
            prerequisites = System.Array.Empty<string>(),
            branch = CharacterBranch.Guerrier
        });

        AddNode("cadence", new NodeData
        {
            displayName = "Cadence",
            description = "Augmente la vitesse de tir de toutes tes armes.",
            isUnique = false,
            level1Desc = "+10%",
            level2Desc = "+20%",
            level3Desc = "+35%",
            costLevel1 = 100,
            costLevel2 = 300,
            costLevel3 = 700,
            prerequisites = System.Array.Empty<string>(),
            branch = CharacterBranch.Guerrier
        });

        AddNode("fragmentation", new NodeData
        {
            displayName = "Fragmentation",
            description = "Les projectiles ont 20% de chance d'exploser à l'impact.",
            isUnique = true,
            costLevel1 = 500,
            prerequisites = new[] { "concentration" }, // MODIFIÉ — était "damage"
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
            costLevel1 = 100,
            costLevel2 = 300,
            costLevel3 = 700,
            prerequisites = new[] { "cadence" },
            branch = CharacterBranch.Guerrier
        });

        AddNode("overpower", new NodeData
        {
            displayName = "Surpuissance",
            description = "Après l'ultime, tes dégâts sont doublés pendant 5 secondes.",
            isUnique = true,
            costLevel1 = 1000,
            prerequisites = new[] { "fragmentation", "crystalDamage" },
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
            costLevel1 = 100,
            costLevel2 = 300,
            costLevel3 = 700,
            prerequisites = System.Array.Empty<string>(),
            branch = CharacterBranch.Gardien
        });

        // AJOUTÉ — remplace "regen" comme point d'entrée sans prérequis
        AddNode("recuperation", new NodeData
        {
            displayName = "Récupération",
            description = "Chaque projectile absorbé par le dash restaure des PV.",
            isUnique = false,
            level1Desc = "+20 PV par absorption",
            level2Desc = "+50 PV par absorption",
            level3Desc = "+80 PV par absorption",
            costLevel1 = 100,
            costLevel2 = 300,
            costLevel3 = 700,
            prerequisites = System.Array.Empty<string>(),
            branch = CharacterBranch.Gardien
        });

        AddNode("secondWind", new NodeData
        {
            displayName = "Second Souffle",
            description = "Une fois par partie, survit à un coup fatal avec 1 HP.",
            isUnique = true,
            costLevel1 = 500,
            prerequisites = new[] { "vitality" },
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
            costLevel1 = 100,
            costLevel2 = 300,
            costLevel3 = 700,
            prerequisites = new[] { "recuperation" }, // MODIFIÉ — était "regen"
            branch = CharacterBranch.Gardien
        });

        AddNode("manaShield", new NodeData
        {
            displayName = "Bouclier de Mana",
            description = "Absorbe automatiquement 1 projectile ennemi toutes les 8 secondes.",
            isUnique = true,
            costLevel1 = 900,
            prerequisites = new[] { "secondWind", "armor" },
            branch = CharacterBranch.Gardien
        });

        // ── FANTÔME ─────────────────────────────────────────────────────

        // AJOUTÉ — remplace "agility" comme point d'entrée sans prérequis
        AddNode("impulsionNova", new NodeData
        {
            displayName = "Impulsion Nova",
            description = "Si la Nova déclenchée par une absorption tue au moins un ennemi, le cooldown du dash est immédiatement réinitialisé.",
            isUnique = true,
            costLevel1 = 500,
            prerequisites = System.Array.Empty<string>(),
            branch = CharacterBranch.Fantome
        });

        AddNode("dash", new NodeData
        {
            displayName = "Dash Amélioré",
            description = "Réduit le temps de recharge du dash.",
            isUnique = false,
            level1Desc = "-0.3s",
            level2Desc = "-0.6s",
            level3Desc = "-1.0s",
            costLevel1 = 100,
            costLevel2 = 300,
            costLevel3 = 700,
            prerequisites = System.Array.Empty<string>(),
            branch = CharacterBranch.Fantome
        });

        AddNode("crystalMastery", new NodeData
        {
            displayName = "Maîtrise du Cristal",
            description = "Réduit le nombre de charges nécessaires pour l'ultime.",
            isUnique = true,
            costLevel1 = 600,
            prerequisites = new[] { "impulsionNova" }, // MODIFIÉ — était "agility"
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
            costLevel1 = 100,
            costLevel2 = 300,
            costLevel3 = 700,
            prerequisites = new[] { "dash" },
            branch = CharacterBranch.Fantome
        });

        AddNode("phantomDash", new NodeData
        {
            displayName = "Dash Fantôme",
            // MODIFIE - l'ancien texte ("le dash laisse un clone") decrivait un
            // declenchement automatique via le dash, qui ne correspond plus a
            // l'implementation actuelle (touche dediee, independante du dash).
            // Pas de nom de touche ici : depend des futurs parametres de remapping.
            description = "Débloque un Clone spectral, activable via une touche dédiée : il attire les ennemis proches pendant 2 secondes.",
            isUnique = true,
            costLevel1 = 1200,
            prerequisites = new[] { "crystalMastery", "novaRadius" },
            branch = CharacterBranch.Fantome
        });
    }
}