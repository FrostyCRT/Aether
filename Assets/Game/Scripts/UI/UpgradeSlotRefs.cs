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

    [Header("Nom")]
    public TextMeshProUGUI nameText;

    [Header("Pastilles")]
    [Tooltip("Le RectTransform du conteneur TierDotsRow lui-meme (pas un des dots) - necessaire pour recentrer la rangee en code selon la presence ou non du losange.")]
    public RectTransform tierDotsRow;
    [Tooltip("Assigne Dot1, Dot2, Dot3 dans cet ordre exact.")]
    public Image[] tierDots;
    public Image unlockDot;
}