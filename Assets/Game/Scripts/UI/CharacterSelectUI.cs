using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class CharacterSelectUI : MonoBehaviour
{
    [Header("Backgrounds")]
    [SerializeField] private Image _backgroundRight;

    [Header("Personnage")]
    [SerializeField] private Image _characterImage;
    [SerializeField] private Image _characterName;

    [Header("Textes")]
    [SerializeField] private TextMeshProUGUI _loreText;
    [SerializeField] private TextMeshProUGUI _specialitiesText;

    [Header("Navigation")]
    [SerializeField] private Button _leftArrow;
    [SerializeField] private Button _rightArrow;
    [SerializeField] private Button _selectButton;
    [SerializeField] private TextMeshProUGUI _selectButtonText;

    [Header("Verrouillage personnage")]
    [Tooltip("Panneau affiché par-dessus le portrait quand le personnage est verrouillé (cadenas, voile sombre...). Facultatif.")]
    [SerializeField] private GameObject _lockedOverlay;
    [Tooltip("Texte qui indique la condition de déblocage, ex : \"Atteins le Boss 2 dans une partie\".")]
    [SerializeField] private TextMeshProUGUI _unlockConditionText;
    [Tooltip("Bouton pour débloquer le perso contre des Éclats (filet anti-blocage). Facultatif.")]
    [SerializeField] private Button _unlockWithEclatsButton;
    [SerializeField] private TextMeshProUGUI _unlockWithEclatsButtonText;
    [Tooltip("Opacité du portrait quand le personnage est verrouillé.")]
    [SerializeField] private float _lockedPortraitAlpha = 0.35f;

    [Header("DEBUG UNIQUEMENT - à retirer avant release")]
    [Tooltip("Remet les déblocages à l'état 'première partie' (seul Aether). Se câble tout seul, glisse juste le bouton ici.")]
    [SerializeField] private Button _debugResetUnlocksButton;

    [Header("Transition")]
    [SerializeField] private float _slideDuration = 0.35f;
    [SerializeField] private AnimationCurve _slideCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Sprites Personnages")]
    [SerializeField] private Sprite _spriteAether;
    [SerializeField] private Sprite _spriteKael;
    [SerializeField] private Sprite _sprayteLyra;

    [Header("Logos Noms")]
    [SerializeField] private Sprite _logoAether;
    [SerializeField] private Sprite _logoKael;
    [SerializeField] private Sprite _logoLyra;

    private struct CharacterData
    {
        public Sprite characterSprite;
        public Sprite nameLogoSprite;
        public Color backgroundRightColor;
        public string lore;
        public string specialities;
    }

    private CharacterData[] _characters;
    private int _currentIndex = 0;
    private bool _isTransitioning = false;

    private static readonly Color ColorAether = new Color(0.239f, 0.122f, 0.000f);
    private static readonly Color ColorKael   = new Color(0.051f, 0.169f, 0.051f);
    private static readonly Color ColorLyra   = new Color(0.024f, 0.157f, 0.157f);

    private RectTransform _characterRect;
    private RectTransform _nameRect;
    private Vector2 _characterOriginalPos;
    private Vector2 _nameOriginalPos;
    private float _screenWidth;

    private void Awake()
    {
        _characterRect = _characterImage.GetComponent<RectTransform>();
        _nameRect = _characterName.GetComponent<RectTransform>();
        _characterOriginalPos = _characterRect.anchoredPosition;
        _nameOriginalPos = _nameRect.anchoredPosition;

        Canvas canvas = GetComponentInParent<Canvas>();
        _screenWidth = canvas != null
            ? canvas.GetComponent<RectTransform>().rect.width
            : 1920f;

        BuildCharacterData();

        _leftArrow.onClick.RemoveAllListeners();
        _leftArrow.onClick.AddListener(() => Navigate(-1));

        _rightArrow.onClick.RemoveAllListeners();
        _rightArrow.onClick.AddListener(() => Navigate(1));

        _selectButton.onClick.RemoveAllListeners();
        _selectButton.onClick.AddListener(OnSelectClicked);

        if (_unlockWithEclatsButton != null)
        {
            _unlockWithEclatsButton.onClick.RemoveAllListeners();
            _unlockWithEclatsButton.onClick.AddListener(OnUnlockWithEclatsClicked);
        }

        if (_debugResetUnlocksButton != null)
        {
            _debugResetUnlocksButton.onClick.RemoveAllListeners();
            _debugResetUnlocksButton.onClick.AddListener(OnDebugResetUnlocksClicked);
        }
    }

    private void OnEnable()
    {
        if (MetaProgressionManager.Instance != null)
            _currentIndex = MetaProgressionManager.Instance.GetSelectedCharacterIndex();

        ApplyCharacter(instant: true);
        ApplySelectButtonState();
    }

    private void BuildCharacterData()
    {
        _characters = new CharacterData[]
        {
            new CharacterData
            {
                characterSprite      = _spriteAether,
                nameLogoSprite       = _logoAether,
                backgroundRightColor = ColorAether,
                lore =
                    "Né au creux d'une tempête de mana, Aether a grandi entre les ruines d'un sanctuaire oublié. " +
                    "Il ne cherche pas la gloire — il cherche des réponses. " +
                    "Son cristal ne lui appartient pas : il l'a trouvé. Et depuis, il ne peut plus s'en séparer.",
                specialities =
                    "― Attaque au cristal de mana\n" +
                    "― Maîtrise des projectiles élémentaires\n" +
                    "― Fragmentation à l'impact\n" +
                    "― Surpuissance post-ultime"
            },
            new CharacterData
            {
                characterSprite      = _spriteKael,
                nameLogoSprite       = _logoKael,
                backgroundRightColor = ColorKael,
                lore =
                    "Kael n'a jamais reculé. Pas une fois. " +
                    "Ancien protecteur d'une cité engloutie, il porte encore sur lui le poids de ceux qu'il n'a pas pu sauver. " +
                    "Son bâton pulse au rythme de sa volonté — et sa volonté ne faiblit jamais.",
                specialities =
                    "― Aura de mana permanente au corps-à-corps\n" +
                    "― Absorption et résistance aux dégâts\n" +
                    "― Régénération et endurance\n" +
                    "― Bouclier de mana automatique"
            },
            new CharacterData
            {
                characterSprite      = _sprayteLyra,
                nameLogoSprite       = _logoLyra,
                backgroundRightColor = ColorLyra,
                lore =
                    "On ne la voit jamais venir. On ne la voit jamais partir. " +
                    "Lyra opère dans les espaces entre les secondes — là où personne ne regarde. " +
                    "Elle n'explique pas ses motivations. Elle livre ses résultats.",
                specialities =
                    "― Salve de shurikens de mana perforants\n" +
                    "― Dash ultra-rapide et impulsion Nova\n" +
                    "― Maîtrise du cristal et recharge d'ultime accélérée\n" +
                    "― Clone fantôme attirant les ennemis"
            }
        };
    }

    private void Navigate(int direction)
    {
        if (_isTransitioning) return;
        int newIndex = (_currentIndex + direction + _characters.Length) % _characters.Length;
        StartCoroutine(SlideTransition(direction, newIndex));
    }

    private IEnumerator SlideTransition(int direction, int newIndex)
    {
        _isTransitioning = true;
        _leftArrow.interactable  = false;
        _rightArrow.interactable = false;

        float slideOutTarget = direction > 0 ? -_screenWidth :  _screenWidth;
        float slideInStart   = direction > 0 ?  _screenWidth : -_screenWidth;

        // Phase 1 — slide OUT
        float t = 0f;
        while (t < _slideDuration)
        {
            t += Time.deltaTime;
            float ratio = _slideCurve.Evaluate(Mathf.Clamp01(t / _slideDuration));
            _characterRect.anchoredPosition = Vector2.Lerp(
                _characterOriginalPos,
                new Vector2(slideOutTarget, _characterOriginalPos.y), ratio);
            _nameRect.anchoredPosition = Vector2.Lerp(
                _nameOriginalPos,
                new Vector2(slideOutTarget * 0.8f, _nameOriginalPos.y), ratio);
            yield return null;
        }

        // Phase 2 — swap contenu, PAS de SetSelectedCharacter ici
        _currentIndex = newIndex;
        ApplyCharacterContent();
        ApplyBackgroundColor();

        _characterRect.anchoredPosition = new Vector2(slideInStart, _characterOriginalPos.y);
        _nameRect.anchoredPosition      = new Vector2(slideInStart * 0.8f, _nameOriginalPos.y);

        // Phase 3 — slide IN
        t = 0f;
        while (t < _slideDuration)
        {
            t += Time.deltaTime;
            float ratio = _slideCurve.Evaluate(Mathf.Clamp01(t / _slideDuration));
            _characterRect.anchoredPosition = Vector2.Lerp(
                new Vector2(slideInStart, _characterOriginalPos.y),
                _characterOriginalPos, ratio);
            _nameRect.anchoredPosition = Vector2.Lerp(
                new Vector2(slideInStart * 0.8f, _nameOriginalPos.y),
                _nameOriginalPos, ratio);
            yield return null;
        }

        // Snap final
        _characterRect.anchoredPosition = _characterOriginalPos;
        _nameRect.anchoredPosition      = _nameOriginalPos;

        // SUPPRIMÉ — plus de SetSelectedCharacter ici, uniquement dans OnSelectClicked

        _leftArrow.interactable  = true;
        _rightArrow.interactable = true;
        _isTransitioning = false;
    }

    private void ApplyCharacter(bool instant)
    {
        ApplyCharacterContent();
        ApplyBackgroundColor();

        if (!instant) return;
        _characterRect.anchoredPosition = _characterOriginalPos;
        _nameRect.anchoredPosition      = _nameOriginalPos;
    }

    private void ApplyCharacterContent()
    {
        CharacterData data = _characters[_currentIndex];

        if (_characterImage != null && data.characterSprite != null)
            _characterImage.sprite = data.characterSprite;

        if (_characterName != null && data.nameLogoSprite != null)
            _characterName.sprite = data.nameLogoSprite;

        if (_loreText != null)
            _loreText.text = data.lore;

        if (_specialitiesText != null)
            _specialitiesText.text = data.specialities;

        ApplySelectButtonState();
    }

    private void ApplyBackgroundColor()
    {
        if (_backgroundRight != null)
            _backgroundRight.color = _characters[_currentIndex].backgroundRightColor;
    }

    public void OnSelectClicked()
    {
        if (MetaProgressionManager.Instance == null) return;
        if (!MetaProgressionManager.Instance.IsCharacterUnlocked(_currentIndex)) return;

        // CORRIGÉ — SetSelectedCharacter appelé ici et UNIQUEMENT ici
        MetaProgressionManager.Instance.SetSelectedCharacter(_currentIndex);

        if (SkillTreeUI.Instance != null)
            SkillTreeUI.Instance.RefreshAllNodes();

        ApplySelectButtonState();
    }

    // Filet anti-blocage : débloquer le perso courant contre des Éclats, puis le
    // sélectionner directement (on vient de payer pour lui).
    public void OnUnlockWithEclatsClicked()
    {
        if (MetaProgressionManager.Instance == null) return;

        if (MetaProgressionManager.Instance.TryUnlockCharacterWithEclats(_currentIndex))
        {
            MetaProgressionManager.Instance.SetSelectedCharacter(_currentIndex);
            if (SkillTreeUI.Instance != null)
                SkillTreeUI.Instance.RefreshAllNodes();
            ApplyCharacterContent(); // rafraîchit portrait dé-grisé + boutons
        }
        else
        {
            ApplySelectButtonState(); // Éclats insuffisants : juste re-refléter l'état
        }
    }

    // DEBUG - à retirer avant release. Simule un premier lancement : seul Aether
    // débloqué, retour sur Aether, panneau rafraîchi.
    public void OnDebugResetUnlocksClicked()
    {
        if (MetaProgressionManager.Instance == null) return;

        MetaProgressionManager.Instance.DebugResetCharacterUnlocks();
        _currentIndex = 0;
        ApplyCharacter(instant: true);

        if (SkillTreeUI.Instance != null)
            SkillTreeUI.Instance.RefreshAllNodes();
    }

    private static string GetUnlockConditionText(int index)
    {
        switch (index)
        {
            case 1: return "Atteins le Boss 2 dans une partie pour débloquer Kael.";
            case 2: return "Termine une partie (bats le Boss 3) pour débloquer Lyra.";
            default: return "";
        }
    }

    // Reflète l'état du personnage courant : verrouillé (portrait grisé, condition
    // + bouton Éclats) ou débloqué (bouton Sélectionner).
    private void ApplySelectButtonState()
    {
        if (MetaProgressionManager.Instance == null) return;

        bool unlocked = MetaProgressionManager.Instance.IsCharacterUnlocked(_currentIndex);

        if (_characterImage != null)
        {
            Color c = _characterImage.color;
            c.a = unlocked ? 1f : _lockedPortraitAlpha;
            _characterImage.color = c;
        }

        if (_lockedOverlay != null)
            _lockedOverlay.SetActive(!unlocked);

        if (!unlocked)
        {
            if (_unlockConditionText != null)
            {
                _unlockConditionText.gameObject.SetActive(true);
                _unlockConditionText.text = GetUnlockConditionText(_currentIndex);
            }

            _selectButton.gameObject.SetActive(false);

            if (_unlockWithEclatsButton != null)
            {
                int cost = MetaProgressionManager.Instance.GetCharacterUnlockEclatsCost(_currentIndex);
                bool canAfford = MetaProgressionManager.Instance.TotalEclats >= cost;

                _unlockWithEclatsButton.gameObject.SetActive(true);
                _unlockWithEclatsButton.interactable = canAfford;

                if (_unlockWithEclatsButtonText != null)
                {
                    _unlockWithEclatsButtonText.text = $"Débloquer — {cost} Éclats";
                    _unlockWithEclatsButtonText.color = canAfford
                        ? Color.white
                        : new Color(0.8f, 0.3f, 0.3f);
                }
            }
            return;
        }

        // --- Personnage débloqué ---
        if (_unlockConditionText != null) _unlockConditionText.gameObject.SetActive(false);
        if (_unlockWithEclatsButton != null) _unlockWithEclatsButton.gameObject.SetActive(false);
        _selectButton.gameObject.SetActive(true);

        int savedIndex = MetaProgressionManager.Instance.GetSelectedCharacterIndex();
        bool isSelected = savedIndex == _currentIndex;

        _selectButton.interactable = !isSelected;

        if (_selectButtonText != null)
            _selectButtonText.color = isSelected
                ? new Color(0.5f, 0.5f, 0.5f, 1f)
                : Color.white;
    }
}