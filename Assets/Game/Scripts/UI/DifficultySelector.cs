using TMPro;
using UnityEngine;
using UnityEngine.UI;

// AJOUTÉ (2026-10-18, diagnostic complet - "accès à la sélection de difficulté dans le menu") - sélecteur du palier
// de difficulté du mode Classique (DifficultyTiers.cs), placé juste sous GameModeSelector dans la page Accueil.
// Rangée FIXE de 3 boutons (Modéré/Violent/Apocalyptique), tous toujours cliquables - contrairement à
// GameModeSelector (flèches + mode verrouillable), rien n'est jamais verrouillé ici : revenir à Modéré est gratuit,
// exactement ce que demande l'utilisateur. Visible uniquement quand GameModes.Current == GameMode.Classic (la
// difficulté n'a de sens que pour ce mode) - se montre/se cache tout seul si le joueur change de mode avec les
// flèches de GameModeSelector, sans dépendance directe entre les deux scripts (même style de polling léger que
// GameModeSelector/ReputationUI ailleurs dans ce projet).
public class DifficultySelector : MonoBehaviour
{
    [System.Serializable]
    public class TierButton
    {
        public Button button;
        public TextMeshProUGUI label;
        public Image border;
    }

    [Tooltip("Exactement 3 entrées, dans l'ordre de l'enum DifficultyTier (Modéré, Violent, Apocalyptique).")]
    [SerializeField] private TierButton[] _tierButtons;
    [SerializeField] private TextMeshProUGUI _descriptionText;
    // Contenu visuel (rangée de boutons + description) séparé de CE GameObject : seul lui doit rester actif en
    // permanence pour que son propre Update() continue de tourner et puisse un jour réafficher ce contenu -
    // désactiver la racine elle-même la figerait hors du mode Classique, sans aucun moyen de se "réveiller".
    [SerializeField] private GameObject _contentRoot;

    [SerializeField] private Color _activeColor = new Color(1f, 0.79f, 0.30f, 1f);
    [SerializeField] private Color _inactiveColor = new Color(0.75f, 0.73f, 0.70f, 1f);

    private void Awake()
    {
        for (int i = 0; i < _tierButtons.Length; i++)
        {
            int tier = i;
            if (_tierButtons[i].button == null) continue;
            _tierButtons[i].button.onClick.RemoveAllListeners();
            _tierButtons[i].button.onClick.AddListener(() => Select((DifficultyTier)tier));
        }
    }

    private void OnEnable()
    {
        Refresh();
        Loc.Changed += OnLanguageChanged;
    }

    private void OnDisable() => Loc.Changed -= OnLanguageChanged;

    private void OnLanguageChanged() => Refresh();

    private void Update()
    {
        if (_contentRoot == null) return;
        bool shouldShow = GameModes.IsClassic;
        if (_contentRoot.activeSelf != shouldShow)
        {
            _contentRoot.SetActive(shouldShow);
            if (shouldShow) Refresh();
        }
    }

    private void Select(DifficultyTier tier)
    {
        if (DifficultyTiers.Current == tier) return;
        DifficultyTiers.Current = tier;
        Refresh();
    }

    private void Refresh()
    {
        DifficultyTier current = DifficultyTiers.Current;
        for (int i = 0; i < _tierButtons.Length; i++)
        {
            DifficultyTier tier = (DifficultyTier)i;
            bool active = tier == current;
            if (_tierButtons[i].label != null)
            {
                _tierButtons[i].label.text = DifficultyTiers.TierName(tier);
                _tierButtons[i].label.color = active ? Color.white : _inactiveColor;
            }
            if (_tierButtons[i].border != null)
                _tierButtons[i].border.color = active ? _activeColor : _inactiveColor;
        }
        if (_descriptionText != null) _descriptionText.text = DifficultyTiers.TierDescription(current);
    }
}
