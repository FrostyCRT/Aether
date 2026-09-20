using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

// AJOUTE (2026-09-19) - fiche personnage du panneau de droite de l'onglet Personnage
// (refonte complète, l'ancien panneau = un bloc de lore + une liste de "spécialités"
// en texte centré Liberation/italique, seul élément de l'écran hors charte).
//
// Principes (repris de ce que font les meilleurs roguelites : Hades, Slay the Spire,
// Vampire Survivors, Risk of Rain 2) :
//  - HIÉRARCHIE claire, du plus identitaire au plus actionnable : épithète + mots-clés,
//    arme exclusive, arbre de compétences, parcours, bouton d'action ;
//  - de l'ICONOGRAPHIE plutôt que des paragraphes : médaillons ronds identiques à ceux de
//    l'onglet Compétences (mêmes icônes, mêmes codes couleur par personnage) ;
//  - UNIQUEMENT des infos VRAIES et vivantes : progression réelle de l'arbre lue dans la
//    sauvegarde, parcours réel du personnage, mots-clés tirés des propriétés réelles de
//    son arme. Aucune stat inventée : les 3 personnages ont aujourd'hui les mêmes stats
//    de base, une barre PV/Vitesse identique partout serait du remplissage (à ajouter le
//    jour où les traits innés existent, voir NOTES.md) ;
//  - la même typographie que le reste du jeu (Bangers), alignement à gauche, une grille.
//
// Ce composant ne construit rien : la hiérarchie est dans la scène (modifiable à la
// main), il ne fait que la remplir et la teinter à la couleur du personnage.
public class CharacterInfoPanel : MonoBehaviour
{
    [System.Serializable]
    public class AccentTarget
    {
        public Graphic graphic;
        [Range(0f, 1f)] public float alpha = 1f;
    }

    [System.Serializable]
    public class Chip
    {
        public RectTransform root;
        public Image border;
        public Image fill;
        public TextMeshProUGUI label;
    }

    [System.Serializable]
    public class NodeSlot
    {
        public Image ring;
        public Image icon;
        public Image[] pips; // 3 : les nœuds à paliers utilisent les 3, les nœuds uniques seulement celui du milieu
    }

    public struct Info
    {
        public int characterIndex;
        public Color accent;
        public string displayName;
        public string[] tags;
        public string weaponName;
        public string weaponDescription;
        public string weaponUpgrades;
        public SkillTreeData.CharacterBranch branch;
        public string branchName;
        public string branchFocus;
    }

    [Header("Couleur du personnage")]
    [Tooltip("Tout ce qui prend la couleur d'accent du personnage (barre du haut, cadre, séparateurs...), avec son opacité propre.")]
    [SerializeField] private AccentTarget[] _accentTargets;

    [Header("En-tête")]
    [FormerlySerializedAs("_epithetText")]
    [Tooltip("Nom du personnage, en tête de fiche (couleur d'accent).")]
    [SerializeField] private TextMeshProUGUI _nameText;
    [SerializeField] private Chip[] _chips;

    [Header("Arme exclusive")]
    [SerializeField] private Image _weaponRing;
    [SerializeField] private Image _weaponIcon;
    [SerializeField] private TextMeshProUGUI _weaponName;
    [SerializeField] private TextMeshProUGUI _weaponDescription;
    [SerializeField] private TextMeshProUGUI _weaponUpgrades;
    [Tooltip("Icônes des armes exclusives : 0 Aether (Fireball), 1 Kael (Aura), 2 Lyra (Couteaux).")]
    [SerializeField] private Sprite[] _weaponIcons;

    [Header("Arbre de compétences")]
    [SerializeField] private TextMeshProUGUI _treeBranchName;
    [SerializeField] private NodeSlot[] _nodes;
    [SerializeField] private TextMeshProUGUI _treeFocus;
    [SerializeField] private TextMeshProUGUI _treeHint;
    [SerializeField] private TextMeshProUGUI _treeProgress;
    [SerializeField] private Button _treeButton;
    [Tooltip("15 icônes des nœuds, dans l'ordre : 5 Guerrier, 5 Gardien, 5 Fantôme (ordre de l'arbre, de 1 à 5).")]
    [SerializeField] private Sprite[] _nodeIcons;

    [Header("Parcours / déblocage (même emplacement, un seul visible à la fois)")]
    [SerializeField] private GameObject _recordGroup;
    [SerializeField] private TextMeshProUGUI[] _recordValues; // Parties, Meilleur temps, Victoires
    [SerializeField] private GameObject _unlockGroup;

    [Header("Transition")]
    [SerializeField] private CanvasGroup _group;

    // Ordre des nœuds dans l'arbre, par branche (Guerrier, Gardien, Fantôme) : identique à
    // la numérotation des icônes (1. Damage, 2. Cadence...) et à la disposition de l'onglet Compétences.
    private static readonly string[][] BranchNodeIds =
    {
        new[] { "concentration", "cadence", "fragmentation", "crystalDamage", "overpower" },
        new[] { "vitality", "recuperation", "armor", "secondWind", "manaShield" },
        new[] { "impulsionNova", "dash", "novaRadius", "crystalMastery", "phantomDash" },
    };

    private static readonly Color PanelDark = new Color32(0x0C, 0x0A, 0x12, 0xFF);
    // Nœud non acquis : l'anneau garde la teinte du personnage mais éteinte (comme dans
    // l'onglet Compétences, où un nœud verrouillé reste reconnaissable à sa couleur) ;
    // l'icône est assombrie sans disparaître (un gris trop sombre sur le disque = illisible).
    private static readonly Color InactiveIcon = new Color(0.58f, 0.58f, 0.64f, 0.95f);
    private static readonly Color EmptyPip = new Color32(0x4A, 0x44, 0x58, 0xFF);
    private static readonly Color Muted = new Color32(0x9C, 0x93, 0xAE, 0xFF);

    private Coroutine _fade;

    private void Awake()
    {
        if (_treeButton != null)
        {
            _treeButton.onClick.RemoveAllListeners();
            _treeButton.onClick.AddListener(OpenSkillTree);
        }
    }

    // Toute la zone "arbre" est un bouton : c'est le raccourci naturel vers l'onglet Compétences.
    private void OpenSkillTree()
    {
        MainMenuManager menu = FindFirstObjectByType<MainMenuManager>();
        if (menu != null) menu.ShowUpgrades();
    }

    // --- API publique ----------------------------------------------------

    public void Show(Info info)
    {
        ApplyAccent(info.accent);
        ApplyHeader(info);
        ApplyWeapon(info);
        ApplyTree(info);
        ApplyRecord(info.characterIndex);
    }

    // Bascule l'emplacement "Parcours" (perso débloqué) / "Déblocage" (perso verrouillé) :
    // un parcours à zéro sur un personnage qu'on ne peut pas encore jouer n'aurait aucun sens.
    public void SetLocked(bool locked)
    {
        if (_recordGroup != null) _recordGroup.SetActive(!locked);
        if (_unlockGroup != null) _unlockGroup.SetActive(locked);
    }

    // Fondu rapide (changement de personnage) : indépendant de Time.timeScale.
    public void FadeTo(float target, float duration)
    {
        if (_group == null) return;
        if (_fade != null) StopCoroutine(_fade);
        if (!isActiveAndEnabled || duration <= 0f) { _group.alpha = target; return; }
        _fade = StartCoroutine(FadeRoutine(target, duration));
    }

    private IEnumerator FadeRoutine(float target, float duration)
    {
        float from = _group.alpha, t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            _group.alpha = Mathf.Lerp(from, target, Mathf.SmoothStep(0f, 1f, t / duration));
            yield return null;
        }
        _group.alpha = target;
        _fade = null;
    }

    // --- remplissage -----------------------------------------------------

    private void ApplyAccent(Color accent)
    {
        if (_accentTargets == null) return;
        foreach (AccentTarget t in _accentTargets)
        {
            if (t == null || t.graphic == null) continue;
            Color c = accent;
            c.a = t.alpha;
            t.graphic.color = c;
        }
    }

    private void ApplyHeader(Info info)
    {
        if (_nameText != null)
        {
            _nameText.text = info.displayName;
            _nameText.color = Color.Lerp(info.accent, Color.white, 0.12f);
        }

        if (_chips == null) return;
        const float gap = 10f, padX = 16f;
        float x = 0f;
        Color chipText = Color.Lerp(info.accent, Color.white, 0.45f);
        Color chipFill = Color.Lerp(PanelDark, info.accent, 0.16f);

        for (int i = 0; i < _chips.Length; i++)
        {
            Chip chip = _chips[i];
            if (chip == null || chip.root == null) continue;

            bool used = info.tags != null && i < info.tags.Length && !string.IsNullOrEmpty(info.tags[i]);
            chip.root.gameObject.SetActive(used);
            if (!used) continue;

            chip.label.text = info.tags[i];
            chip.label.color = chipText;
            float width = chip.label.GetPreferredValues(info.tags[i]).x + padX * 2f;
            chip.root.sizeDelta = new Vector2(width, chip.root.sizeDelta.y);
            chip.root.anchoredPosition = new Vector2(x, chip.root.anchoredPosition.y);
            x += width + gap;

            if (chip.border != null) chip.border.color = new Color(info.accent.r, info.accent.g, info.accent.b, 0.85f);
            if (chip.fill != null) chip.fill.color = chipFill;
        }
    }

    private void ApplyWeapon(Info info)
    {
        if (_weaponRing != null) _weaponRing.color = info.accent;

        if (_weaponIcon != null && _weaponIcons != null && info.characterIndex >= 0 && info.characterIndex < _weaponIcons.Length)
        {
            _weaponIcon.sprite = _weaponIcons[info.characterIndex];
            _weaponIcon.enabled = _weaponIcon.sprite != null;
        }

        if (_weaponName != null) _weaponName.text = info.weaponName;
        if (_weaponDescription != null) _weaponDescription.text = info.weaponDescription;
        if (_weaponUpgrades != null)
            _weaponUpgrades.text = $"<color=#{ColorUtility.ToHtmlStringRGB(Muted)}>PALIERS</color>   {info.weaponUpgrades}";
    }

    private void ApplyTree(Info info)
    {
        if (_treeBranchName != null)
        {
            _treeBranchName.text = info.branchName;
            _treeBranchName.color = info.accent;
        }
        if (_treeFocus != null) _treeFocus.text = info.branchFocus;

        int branchIndex = Mathf.Clamp((int)info.branch, 0, BranchNodeIds.Length - 1);
        string[] ids = BranchNodeIds[branchIndex];
        MetaProgressionManager meta = MetaProgressionManager.Instance;

        int totalLevels = 0, doneLevels = 0;

        for (int i = 0; _nodes != null && i < _nodes.Length && i < ids.Length; i++)
        {
            NodeSlot slot = _nodes[i];
            if (slot == null) continue;

            SkillTreeData.NodeData node = SkillTreeData.Get(ids[i]);
            bool unique = node != null && node.isUnique;
            int max = unique ? 1 : 3;
            int level = 0;
            if (meta != null)
                level = unique ? (meta.IsNodePurchased(ids[i]) ? 1 : 0) : Mathf.Clamp(meta.GetNodeLevel(ids[i]), 0, max);

            totalLevels += max;
            doneLevels += level;
            bool active = level > 0;

            if (slot.ring != null) slot.ring.color = active ? info.accent : Color.Lerp(PanelDark, info.accent, 0.42f);
            if (slot.icon != null)
            {
                int iconIndex = branchIndex * 5 + i;
                if (_nodeIcons != null && iconIndex < _nodeIcons.Length) slot.icon.sprite = _nodeIcons[iconIndex];
                slot.icon.color = active ? Color.white : InactiveIcon;
            }

            for (int p = 0; slot.pips != null && p < slot.pips.Length; p++)
            {
                Image pip = slot.pips[p];
                if (pip == null) continue;

                // nœud unique : seul le point central existe ; nœud à paliers : les 3 points
                bool exists = unique ? p == 1 : true;
                pip.gameObject.SetActive(exists);
                if (!exists) continue;

                bool filled = unique ? level > 0 : p < level;
                pip.color = filled ? info.accent : EmptyPip;
            }
        }

        if (_treeProgress != null)
        {
            string mutedHex = ColorUtility.ToHtmlStringRGB(Muted);
            _treeProgress.text = $"{doneLevels}<size=62%><color=#{mutedHex}> / {totalLevels}</color></size>";
        }

        if (_treeHint != null)
        {
            bool complete = totalLevels > 0 && doneLevels >= totalLevels;
            _treeHint.text = complete ? "ARBRE COMPLET" : "VOIR L'ARBRE";
            _treeHint.color = complete ? info.accent : Muted;
        }
    }

    private void ApplyRecord(int characterIndex)
    {
        if (_recordValues == null || _recordValues.Length < 3) return;

        MetaProgressionManager meta = MetaProgressionManager.Instance;
        int runs = meta != null ? meta.GetCharacterRuns(characterIndex) : 0;
        int wins = meta != null ? meta.GetCharacterWins(characterIndex) : 0;
        float best = meta != null ? meta.GetCharacterBestTime(characterIndex) : 0f;

        // Jamais de tiret vide : un vrai 0 / --:-- grisé se lit comme un compteur qui n'a pas
        // encore bougé, pas comme une donnée manquante.
        SetRecord(_recordValues[0], runs.ToString(), runs > 0);
        SetRecord(_recordValues[1], best > 0f ? $"{Mathf.FloorToInt(best / 60f):00}:{Mathf.FloorToInt(best % 60f):00}" : "--:--", best > 0f);
        SetRecord(_recordValues[2], wins.ToString(), wins > 0);
    }

    // Une valeur à zéro est grisée (rien à se vanter) ; sinon blanche.
    private static void SetRecord(TextMeshProUGUI label, string value, bool has)
    {
        if (label == null) return;
        label.text = value;
        label.color = has ? Color.white : new Color(1f, 1f, 1f, 0.32f);
    }
}
