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
        // CORRIGÉ — SetSelectedCharacter appelé ici et UNIQUEMENT ici
        if (MetaProgressionManager.Instance != null)
            MetaProgressionManager.Instance.SetSelectedCharacter(_currentIndex);

        if (SkillTreeUI.Instance != null)
            SkillTreeUI.Instance.RefreshAllNodes();

        ApplySelectButtonState();
    }

    private void ApplySelectButtonState()
    {
        if (MetaProgressionManager.Instance == null) return;

        int savedIndex = MetaProgressionManager.Instance.GetSelectedCharacterIndex();
        bool isSelected = savedIndex == _currentIndex;

        _selectButton.interactable = !isSelected;

        if (_selectButtonText != null)
            _selectButtonText.color = isSelected
                ? new Color(0.5f, 0.5f, 0.5f, 1f)
                : Color.white;
    }
}