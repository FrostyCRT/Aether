// Données statiques de l'arbre de compétences — prérequis et descriptions
// Structure :
//   GUERRIER  : damage → fragmentation → overpower
//               cadence → crystalDamage → overpower
//   GARDIEN   : vitality → secondWind → manaShield
//               regen → armor → manaShield
//   FANTÔME   : agility → crystalMastery → phantomDash
//               dash → novaRadius → phantomDash

public static class SkillTreeData
{
    public class NodeData
    {
        public string displayName;
        public string description;
        public bool isUnique;
        public string level1Desc;
        public string level2Desc;
        public string level3Desc;
        public int costLevel1;   // coût du niveau 1 (ou achat unique)
        public int costLevel2;
        public int costLevel3;
        public string[] prerequisites; // IDs des nœuds requis (au moins niveau 1 chacun)
    }

    public static NodeData Get(string nodeId)
    {
        switch (nodeId)
        {
            // ── GUERRIER ────────────────────────────────────────────────────

            case "damage":
                return new NodeData
                {
                    displayName = "Dégâts",
                    description = "Augmente les dégâts de toutes tes armes.",
                    isUnique = false,
                    level1Desc = "+10%",
                    level2Desc = "+25%",
                    level3Desc = "+50%",
                    costLevel1 = 50,
                    costLevel2 = 100,
                    costLevel3 = 200,
                    prerequisites = new string[0]   // accessible dès le départ
                };

            case "cadence":
                return new NodeData
                {
                    displayName = "Cadence",
                    description = "Augmente la vitesse de tir de toutes tes armes.",
                    isUnique = false,
                    level1Desc = "+10%",
                    level2Desc = "+20%",
                    level3Desc = "+35%",
                    costLevel1 = 50,
                    costLevel2 = 100,
                    costLevel3 = 200,
                    prerequisites = new string[0]
                };

            case "fragmentation":
                return new NodeData
                {
                    displayName = "Fragmentation",
                    description = "Les projectiles ont 20% de chance d'exploser à l'impact.",
                    isUnique = true,
                    costLevel1 = 150,
                    prerequisites = new string[] { "damage" }
                };

            case "crystalDamage":
                return new NodeData
                {
                    displayName = "Dégâts Cristal",
                    description = "Augmente les dégâts de l'ultime et de la Nova.",
                    isUnique = false,
                    level1Desc = "+25%",
                    level2Desc = "+50%",
                    level3Desc = "+100%",
                    costLevel1 = 75,
                    costLevel2 = 150,
                    costLevel3 = 300,
                    prerequisites = new string[] { "cadence" }
                };

            case "overpower":
                return new NodeData
                {
                    displayName = "Surpuissance",
                    description = "Après l'ultime, tes dégâts sont doublés pendant 5 secondes.",
                    isUnique = true,
                    costLevel1 = 400,
                    prerequisites = new string[] { "fragmentation", "crystalDamage" }
                };

            // ── GARDIEN ─────────────────────────────────────────────────────

            case "vitality":
                return new NodeData
                {
                    displayName = "Vitalité",
                    description = "Augmente tes points de vie maximum.",
                    isUnique = false,
                    level1Desc = "+15%",
                    level2Desc = "+30%",
                    level3Desc = "+50%",
                    costLevel1 = 50,
                    costLevel2 = 100,
                    costLevel3 = 200,
                    prerequisites = new string[0]
                };

            case "regen":
                return new NodeData
                {
                    displayName = "Régénération",
                    description = "Régénère des HP chaque seconde.",
                    isUnique = false,
                    level1Desc = "+1 HP/s",
                    level2Desc = "+2 HP/s",
                    level3Desc = "+4 HP/s",
                    costLevel1 = 50,
                    costLevel2 = 100,
                    costLevel3 = 200,
                    prerequisites = new string[0]
                };

            case "secondWind":
                return new NodeData
                {
                    displayName = "Second Souffle",
                    description = "Une fois par run, survit à un coup fatal avec 1 HP.",
                    isUnique = true,
                    costLevel1 = 150,
                    prerequisites = new string[] { "vitality" }
                };

            case "armor":
                return new NodeData
                {
                    displayName = "Armure",
                    description = "Réduit les dégâts reçus de tous les ennemis.",
                    isUnique = false,
                    level1Desc = "-8%",
                    level2Desc = "-15%",
                    level3Desc = "-25%",
                    costLevel1 = 75,
                    costLevel2 = 150,
                    costLevel3 = 300,
                    prerequisites = new string[] { "regen" }
                };

            case "manaShield":
                return new NodeData
                {
                    displayName = "Bouclier de Mana",
                    description = "Absorbe automatiquement 1 projectile ennemi toutes les 8 secondes.",
                    isUnique = true,
                    costLevel1 = 400,
                    prerequisites = new string[] { "secondWind", "armor" }
                };

            // ── FANTÔME ─────────────────────────────────────────────────────

            case "agility":
                return new NodeData
                {
                    displayName = "Agilité",
                    description = "Augmente ta vitesse de déplacement.",
                    isUnique = false,
                    level1Desc = "+8%",
                    level2Desc = "+18%",
                    level3Desc = "+30%",
                    costLevel1 = 50,
                    costLevel2 = 100,
                    costLevel3 = 200,
                    prerequisites = new string[0]
                };

            case "dash":
                return new NodeData
                {
                    displayName = "Dash Amélioré",
                    description = "Réduit le temps de recharge du dash.",
                    isUnique = false,
                    level1Desc = "-0.3s",
                    level2Desc = "-0.6s",
                    level3Desc = "-1.0s",
                    costLevel1 = 50,
                    costLevel2 = 100,
                    costLevel3 = 200,
                    prerequisites = new string[0]
                };

            case "crystalMastery":
                return new NodeData
                {
                    displayName = "Maîtrise du Cristal",
                    description = "Réduit le nombre de charges nécessaires pour l'ultime.",
                    isUnique = true,
                    costLevel1 = 150,
                    prerequisites = new string[] { "agility" }
                };

            case "novaRadius":
                return new NodeData
                {
                    displayName = "Nova Étendue",
                    description = "Augmente le rayon de la Nova de Cristal.",
                    isUnique = false,
                    level1Desc = "+30%",
                    level2Desc = "+60%",
                    level3Desc = "+100%",
                    costLevel1 = 75,
                    costLevel2 = 150,
                    costLevel3 = 300,
                    prerequisites = new string[] { "dash" }
                };

            case "phantomDash":
                return new NodeData
                {
                    displayName = "Dash Fantôme",
                    description = "Le dash laisse un clone qui attire les ennemis pendant 2 secondes.",
                    isUnique = true,
                    costLevel1 = 400,
                    prerequisites = new string[] { "crystalMastery", "novaRadius" }
                };

            default:
                return null;
        }
    }
}