using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class CharacterSelectUI : MonoBehaviour
{
    // MODIFIE (2026-09-17) - refonte visuelle de l'onglet Personnage (retour
    // utilisateur : "pas fini, pas beau par rapport aux autres onglets" - il
    // n'y avait que 2 aplats de couleur + une diagonale plate, alors que
    // Réputation/Paramètres ont une vraie illustration de fond). Remplace les
    // 2 aplats de couleur (_backgroundRight, plus l'ex-BackgroundLeft) par une
    // vraie image de fond partagée, dédiée par personnage (le cristal flottant
    // change de couleur selon le perso - même image de base retouchée 3 fois
    // pour garder une architecture identique, seul le cristal change).
    [Header("Fond de scène (par personnage)")]
    [Tooltip("Image qui affiche le fond dédié au personnage courant (voir _panelBackgroundImage pour le cadrage 'cover').")]
    [SerializeField] private Image _panelBackground;
    [Tooltip("Enfant de _panelBackground qui porte le vrai sprite - redimensionné en code (FitCover) pour remplir le cadre sans déformer l'image, quel que soit son ratio d'origine.")]
    [SerializeField] private RectTransform _panelBackgroundImageRect;
    [SerializeField] private Sprite _backgroundAether;
    [SerializeField] private Sprite _backgroundKael;
    [SerializeField] private Sprite _backgroundLyra;

    [Header("Personnage")]
    [SerializeField] private Image _characterImage;
    [SerializeField] private Image _characterName;

    // AJOUTE (2026-09-17) - retour utilisateur : en retirant le fond beige des
    // portraits pour les poser sur la nouvelle illustration de fond, les lueurs
    // magiques peintes dans l'image d'origine (poussière ambiante autour du
    // corps, halo du cristal du bâton) ont disparu avec (la détourure ne garde
    // que le sujet, pas les halos diffus qui débordaient dessus). Recréées ici
    // procéduralement (ProceduralGlowUI, déjà utilisé sur le LoadingScreen) -
    // "fluide, vivante, dynamique" comme demandé : pulsation douce + légère
    // dérive, pas une image statique. Un seul rig partagé entre les 3 persos
    // (repositionné/reteint à chaque changement), comme le reste du portrait.
    [Header("Lueurs magiques (par personnage)")]
    [Tooltip("Petit halo vif bleu-blanc au niveau du cristal du bâton - couleur cohérente sur les 3 persos (le cristal lui-même est bleu chez les 3).")]
    [SerializeField] private ProceduralGlowUI _crystalGlow;
    [Tooltip("Halo ambiant plus large et plus doux, teinté à la couleur d'identité du perso (recrée la poussière/aura magique perdue avec le fond).")]
    [SerializeField] private ProceduralGlowUI _auraGlow;
    [Tooltip("Petites étincelles qui dérivent doucement autour de l'aura - teintées comme l'aura. Facultatif (tableau vide = désactivé).")]
    [SerializeField] private ProceduralGlowUI[] _sparkles = new ProceduralGlowUI[0];

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
        public Sprite backgroundSprite;
        public Vector2 crystalGlowOffset;
        public Vector2 auraGlowOffset;
        public Color auraColor;
        public string lore;
        public string specialities;
    }

    // AJOUTE - teinte bleu-blanc du cristal, identique sur les 3 persos (voir
    // _crystalGlow). Constante plutôt que dans CharacterData : ne dépend pas du
    // perso, seul son ANCRAGE (crystalGlowOffset) change d'un portrait à l'autre.
    private static readonly Color CrystalGlowColor = new Color(0.72f, 0.88f, 1f);

    private CharacterData[] _characters;
    private int _currentIndex = 0;
    private bool _isTransitioning = false;

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
                backgroundSprite     = _backgroundAether,
                crystalGlowOffset    = new Vector2(107f, 271f),
                auraGlowOffset       = new Vector2(-75f, -166f),
                auraColor            = new Color(1f, 0.78f, 0.35f), // poussière ambrée/dorée d'Aether
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
                backgroundSprite     = _backgroundKael,
                crystalGlowOffset    = new Vector2(112f, 282f),
                auraGlowOffset       = new Vector2(-48f, 23f),
                auraColor            = new Color(0.42f, 0.9f, 0.46f), // volute verte de Kael
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
                backgroundSprite     = _backgroundLyra,
                crystalGlowOffset    = new Vector2(91f, 309f),
                auraGlowOffset       = new Vector2(-192f, -226f),
                auraColor            = new Color(0.56f, 0.55f, 1f), // volute bleu-violet de Lyra
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
        ApplyPanelBackground();

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
        ApplyPanelBackground();

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

        ApplyGlowFX(data);
        ApplySelectButtonState();
    }

    // AJOUTE (2026-09-17) - repositionne/reteint le rig de lueurs magiques
    // partagé pour le personnage courant. _crystalGlow garde toujours la même
    // teinte (le cristal est bleu chez les 3 persos) - seul son ancrage change.
    // _auraGlow et les étincelles changent à la fois d'ancrage ET de couleur
    // (poussière ambrée/volute verte/volute bleu-violet selon le perso).
    private void ApplyGlowFX(CharacterData data)
    {
        if (_crystalGlow != null)
        {
            _crystalGlow.SetColor(CrystalGlowColor);
            _crystalGlow.SetBasePosition(data.crystalGlowOffset);
        }

        if (_auraGlow != null)
        {
            _auraGlow.SetColor(data.auraColor);
            _auraGlow.SetBasePosition(data.auraGlowOffset);
        }

        if (_sparkles != null)
        {
            EnsureSparkleRelativeOffsets();
            for (int i = 0; i < _sparkles.Length; i++)
            {
                ProceduralGlowUI sparkle = _sparkles[i];
                if (sparkle == null) continue;
                sparkle.SetColor(data.auraColor);
                sparkle.SetBasePosition(data.auraGlowOffset + _sparkleRelativeOffsets[i]);
            }
        }
    }

    // AJOUTE - décalage FIXE de chaque étincelle par rapport au centre de
    // l'aura, calculé une seule fois à partir de sa position posée à la main
    // dans l'éditeur (autour du 1er personnage affiché) par rapport à l'ancrage
    // d'aura de ce même personnage. Ce décalage relatif ne change ensuite
    // jamais - seul l'ancrage d'aura (data.auraGlowOffset) bouge selon le
    // perso, donc les étincelles suivent l'aura en gardant leur dispersion
    // relative les unes des autres.
    private Vector2[] _sparkleRelativeOffsets;

    private void EnsureSparkleRelativeOffsets()
    {
        if (_sparkleRelativeOffsets != null) return;

        _sparkleRelativeOffsets = new Vector2[_sparkles.Length];
        Vector2 referenceAuraOffset = _characters[0].auraGlowOffset;
        for (int i = 0; i < _sparkles.Length; i++)
        {
            if (_sparkles[i] == null) continue;
            Vector2 editorPosition = _sparkles[i].GetComponent<RectTransform>().anchoredPosition;
            _sparkleRelativeOffsets[i] = editorPosition - referenceAuraOffset;
        }
    }

    // MODIFIE (2026-09-17) - remplace l'ancien aplat de couleur par une vraie
    // image de fond dédiée au personnage. FitCover reproduit un comportement
    // "background-size: cover" (CSS) : l'image remplit tout le cadre sans se
    // déformer, quel que soit son ratio d'origine (2048x1152 vs 2096x1184 selon
    // le fond) - elle déborde légèrement d'un côté et RectMask2D sur le parent
    // (_panelBackground) découpe le surplus, au lieu d'étirer l'image pour
    // qu'elle colle exactement au cadre (ce qui déformerait l'architecture
    // peinte, visible sur les colonnes/arches).
    private void ApplyPanelBackground()
    {
        Sprite sprite = _characters[_currentIndex].backgroundSprite;
        if (_panelBackground == null || _panelBackgroundImageRect == null || sprite == null) return;

        // CORRIGE - le sprite doit aller sur l'Image de l'ENFANT
        // (_panelBackgroundImageRect, celui qui est redimensionne par FitCover),
        // pas sur celle du conteneur (_panelBackground, qui ne sert qu'a porter
        // le RectMask2D qui decoupe le debordement).
        Image image = _panelBackgroundImageRect.GetComponent<Image>();
        if (image != null) image.sprite = sprite;

        FitCover(sprite);
    }

    private void FitCover(Sprite sprite)
    {
        if (_panelBackgroundImageRect == null) return;

        RectTransform container = _panelBackground.rectTransform;
        float containerWidth = container.rect.width;
        float containerHeight = container.rect.height;
        if (containerWidth <= 0f || containerHeight <= 0f) return;

        float imageAspect = sprite.rect.width / sprite.rect.height;
        float containerAspect = containerWidth / containerHeight;

        float width, height;
        if (imageAspect > containerAspect)
        {
            // Image relativement plus large que le cadre : cale la hauteur,
            // laisse déborder en largeur (débordement gauche/droite découpé).
            height = containerHeight;
            width = containerHeight * imageAspect;
        }
        else
        {
            // Image relativement plus haute/étroite : cale la largeur, laisse
            // déborder en hauteur (débordement haut/bas découpé).
            width = containerWidth;
            height = containerWidth / imageAspect;
        }

        _panelBackgroundImageRect.sizeDelta = new Vector2(width, height);
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