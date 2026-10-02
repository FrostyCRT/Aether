using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Composant "carte de references" a poser sur la racine du prefab UpgradeSlot.
// Sert uniquement a exposer ses elements internes de facon fiable dans l'Inspector,
// plutot que de les retrouver par nom de chemin (transform.Find("...")) qui casse
// silencieusement au moindre renommage ou reorganisation de la Hierarchy du prefab.
public class UpgradeSlotRefs : MonoBehaviour
{
    [Header("Fond et icone")]
    public Image background;
    public Image icon;

    // AJOUTE (2026-09-28, retour utilisateur : "je veux faire les réglages manuellement") - jusqu'ici ces 4
    // éléments (icône de fusion + les 2 icônes sources + le séparateur) étaient recréés ENTIÈREMENT EN CODE à
    // chaque peuplement de grille (GameUI.BuildFusionSourcesVisual) : aucune existence dans le prefab, donc
    // aucun moyen de les ajuster à la main dans l'Inspector - toute position/taille devait passer par du code.
    // Maintenant de vrais enfants du prefab : le code ne touche plus que leur sprite/couleur/visibilité, jamais
    // leur RectTransform - ajuste position/taille/rotation directement ici, ça persiste.
    [Header("Fusion uniquement (icône dédiée + visuel des 2 armes sources)")]
    [Tooltip("Icône affichée UNIQUEMENT sur une tuile de fusion (position/taille différentes de l'icône normale ci-dessus, qui reste cachée sur une tuile de fusion).")]
    public Image fusionIcon;
    public Image sourceIconTop;
    public Image sourceIconBottom;
    public Image fusionDivider;
    [Tooltip("Petit \"+\" entre les 2 icônes sources, pour montrer que la fusion combine ces 2 armes.")]
    public TextMeshProUGUI fusionPlusText;

    [Header("Nom")]
    public TextMeshProUGUI nameText;

    [Header("Pastilles")]
    [Tooltip("Le RectTransform du conteneur TierDotsRow lui-meme (pas un des dots) - necessaire pour recentrer la rangee en code selon la presence ou non du losange.")]
    public RectTransform tierDotsRow;
    [Tooltip("Assigne Dot1, Dot2, Dot3 dans cet ordre exact.")]
    public Image[] tierDots;
    public Image unlockDot;

    [Header("Compteur x1/x2/x3 (upgrades sans pastilles : Degats/Cadence/Soin)")]
    // AJOUTE - meme principe que sur les cartes de level-up : affiche a la place
    // des pastilles pour les upgrades qui n'en ont pas (cap eleve/illimite).
    public TextMeshProUGUI stackCountText;
}