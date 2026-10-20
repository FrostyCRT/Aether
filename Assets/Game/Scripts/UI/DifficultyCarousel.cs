using TMPro;
using UnityEngine;
using UnityEngine.UI;

// AJOUTÉ (2026-10-20, refonte du carrousel de difficulté - "au lieu de mettre des cases moches [...] je veux faire
// quelque chose de graphique, un vrai carrousel en 2D, avec une image minimalisée de la map [...] stylisée en
// cartoon avec l'éclairage de la difficulté, donc jour/crépuscule/nuit") - remplace DifficultySelector.cs (toujours
// sur le disque, plus utilisé sur cette page) : flèches gauche/droite qui font défiler les 3 paliers, nom du
// palier, et un emplacement de prévisualisation (_previewImage) prêt à recevoir le futur diorama 3D de
// l'utilisateur (modèles générés via Tripo3D - voir A_MODELISER_3D.md) - en attendant, affiche un dégradé de ciel
// procédural (MenuSprites.SkyGradient) teinté avec la VRAIE couleur de lumière du palier en jeu
// (DifficultyTiers.LightColor/LightIntensity, voir GameSceneSettingsApplier), assombri progressivement pour lire
// clairement comme jour/crépuscule/nuit - retour utilisateur explicite : "ça change l'éclairage selon ce qu'on voit
// en jeu". Remplacer _previewImage par un RawImage affichant la future RenderTexture du diorama n'affectera ni ce
// script ni la mise en page : seul le contenu visuel de l'emplacement change.
//
// Visible en Classique, Sans fin ET Ruée de boss (plus seulement le Classique, voir DifficultyTiers - le palier a
// maintenant un vrai effet de jeu sur ces 3 modes). Jamais de palier verrouillé : revenir à Modéré reste gratuit,
// même logique que l'ancien DifficultySelector.
public class DifficultyCarousel : MonoBehaviour
{
    [SerializeField] private Button _leftButton;
    [SerializeField] private Button _rightButton;
    [SerializeField] private TextMeshProUGUI _tierNameText;
    [SerializeField] private Image _previewImage;
    [Tooltip("Comme DifficultySelector._contentRoot : seule la racine (ce composant) doit rester active en permanence pour que Update() continue de tourner et puisse réafficher ce contenu plus tard.")]
    [SerializeField] private GameObject _contentRoot;

    [SerializeField] private Color _activeColor = new Color(1f, 0.79f, 0.30f, 1f);

    // Assombrissement progressif appliqué PAR-DESSUS la vraie teinte de lumière du palier (DifficultyTiers.LightColor)
    // pour que le dégradé de prévisualisation se lise sans ambiguïté comme jour -> crépuscule -> nuit, même si les
    // teintes réelles du jeu (or chaud / ambre / violet) ne sont pas conçues pour varier en LUMINOSITÉ par elles-mêmes.
    private static readonly float[] TimeOfDayDarkness = { 0.95f, 0.55f, 0.20f };

    private void Awake()
    {
        if (_leftButton != null)
        {
            _leftButton.onClick.RemoveAllListeners();
            _leftButton.onClick.AddListener(() => Step(-1));
        }
        if (_rightButton != null)
        {
            _rightButton.onClick.RemoveAllListeners();
            _rightButton.onClick.AddListener(() => Step(1));
        }
    }

    private void OnEnable() => Refresh();

    // Polling léger (même style que DifficultySelector.Update(), aucune référence directe entre les 2 scripts) -
    // ce carrousel n'a de sens que pour les 3 modes dont le palier a un vrai effet de jeu.
    private void Update()
    {
        if (_contentRoot == null) return;
        bool shouldShow = GameModes.IsClassic || GameModes.IsEndless || GameModes.IsBossRush;
        if (_contentRoot.activeSelf != shouldShow)
        {
            _contentRoot.SetActive(shouldShow);
            if (shouldShow) Refresh();
        }
    }

    private void Step(int direction)
    {
        int count = System.Enum.GetValues(typeof(DifficultyTier)).Length;
        int next = ((int)DifficultyTiers.Current + direction + count) % count;
        DifficultyTiers.Current = (DifficultyTier)next;
        Refresh();
    }

    private void Refresh()
    {
        DifficultyTier tier = DifficultyTiers.Current;

        if (_tierNameText != null)
        {
            _tierNameText.text = DifficultyTiers.TierName(tier);
            _tierNameText.color = _activeColor;
        }

        if (_previewImage != null)
        {
            Color lightTint = DifficultyTiers.LightColor(tier);
            float darkness = TimeOfDayDarkness[(int)tier];
            _previewImage.sprite = MenuSprites.SkyGradient(lightTint * darkness);
            _previewImage.color = Color.white;
        }
    }
}
