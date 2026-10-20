using UnityEngine;
using UnityEngine.UI;

// AJOUTÉ (2026-10-20, refonte de la page Accueil - retour utilisateur : "le sélecteur de mode... et pour la
// sélection de modes [et la difficulté]... pour faire quelque chose de plus propre") - jusqu'ici ModeSelector
// (une "Pill" + flèches) et DifficultySelector (une rangée de 3 boutons isolés) étaient 2 widgets flottants sans
// langage visuel commun. Ce composant habille UNE SEULE FOIS, au lancement, le panneau qui les regroupe désormais
// (voir HomeCommandPanel dans MenuPanel) avec le MÊME langage que MenuTabBarFX (le pied de page de CETTE PAGE) -
// bois procédural (MenuSprites.Wood, copie exacte de MenuTabBarFX.BuildWoodSprite) + cadre biseauté bronze/or
// (HudSprites.Frame) - plutôt que d'inventer un 2e style : la page lit comme un seul ensemble cohérent, du pied de
// page jusqu'au bouton Jouer. Aucune logique de sélection ici (ModeSelector.cs/DifficultySelector.cs inchangés) :
// ce composant ne fait QUE poser les sprites/couleurs une fois, les 2 scripts gardent la main sur leurs propres
// couleurs actives/inactives (ex: DifficultySelector.Refresh() continue de teindre _tierBorders lui-même).
public class HomeCommandPanelFX : MonoBehaviour
{
    [Header("Carte (fond du panneau)")]
    [SerializeField] private Image _cardBackground;
    [SerializeField] private Image _cardBorder;

    [Header("Séparateur Mode / Difficulté")]
    [SerializeField] private Image _divider;

    [Header("Fond de la pastille du nom de mode")]
    [SerializeField] private Image _modePillBackground;

    [Header("Segments de difficulté (teintés en direct par DifficultySelector)")]
    [SerializeField] private Image[] _tierBackgrounds;

    [Header("Bouton Jouer")]
    [SerializeField] private Image _playButtonBackground;
    [SerializeField] private Image _playButtonBorder;

    [Header("Couleurs (même famille que MenuTabBarFX)")]
    [SerializeField] private Color _woodTop = new Color(0.310f, 0.205f, 0.115f, 1f);
    [SerializeField] private Color _woodBottom = new Color(0.165f, 0.105f, 0.062f, 1f);
    [SerializeField] private Color _trimColor = new Color(0.80f, 0.61f, 0.27f, 1f);
    [SerializeField] private Color _playColor = new Color(1f, 0.79f, 0.30f, 1f);
    [SerializeField] private Color _pillBackdropColor = new Color(0.07f, 0.05f, 0.03f, 0.65f);

    private void Awake()
    {
        if (_cardBackground != null)
        {
            _cardBackground.sprite = MenuSprites.Wood(_woodTop, _woodBottom, 0.5f, 512, 512);
            _cardBackground.type = Image.Type.Simple;
            _cardBackground.color = Color.white;
        }

        if (_cardBorder != null)
        {
            _cardBorder.sprite = HudSprites.Frame;
            _cardBorder.type = Image.Type.Sliced;
            _cardBorder.color = Color.white;
            _cardBorder.raycastTarget = false;
        }

        if (_divider != null) _divider.color = _trimColor;

        if (_modePillBackground != null)
        {
            _modePillBackground.sprite = HudSprites.Pill;
            _modePillBackground.type = Image.Type.Sliced;
            _modePillBackground.color = _pillBackdropColor;
        }

        if (_tierBackgrounds != null)
        {
            foreach (Image b in _tierBackgrounds)
            {
                if (b == null) continue;
                b.sprite = HudSprites.Pill;
                b.type = Image.Type.Sliced;
                // Couleur NON posée ici : DifficultySelector.Refresh() la pilote déjà (actif = or, inactif = gris).
            }
        }

        if (_playButtonBackground != null)
        {
            _playButtonBackground.sprite = HudSprites.Pill;
            _playButtonBackground.type = Image.Type.Sliced;
            _playButtonBackground.color = _playColor;
        }

        if (_playButtonBorder != null)
        {
            _playButtonBorder.sprite = HudSprites.Frame;
            _playButtonBorder.type = Image.Type.Sliced;
            _playButtonBorder.color = Color.white;
            _playButtonBorder.raycastTarget = false;
        }
    }
}
