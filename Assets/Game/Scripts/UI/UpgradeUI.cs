using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;
using TMPro;
using System.Collections.Generic;

public class UpgradeUI : MonoBehaviour
{
    [Header("Cartes d'upgrade")]
    [SerializeField] private UpgradeCard[] _cards;

    // AJOUTE (2026-09-30, retour utilisateur : "quand il y a la possibilité de choisir une fusion, en haut on
    // met FUSION DISPONIBLE ! sous le Niv.Sup, donc lever un peu le Niv sup uniquement si le message apparaît") -
    // bandeau optionnel affiché uniquement quand une des 3 upgrades proposées est une fusion ; le titre
    // "NIVEAU SUPÉRIEUR !" (jamais géré par ce script avant) remonte un peu pour lui faire de la place.
    [Header("Bandeau \"FUSION DISPONIBLE !\" (visible seulement si une fusion fait partie du choix)")]
    [Tooltip("Le texte \"NIVEAU SUPÉRIEUR !\" au-dessus des cartes - sa position Y d'origine est mémorisée au démarrage puis relevée quand le bandeau apparaît.")]
    [SerializeField] private RectTransform _levelUpTitleText;
    [SerializeField] private TextMeshProUGUI _fusionAvailableBanner;
    [Tooltip("Décalage vers le haut du titre \"NIVEAU SUPÉRIEUR !\" quand le bandeau de fusion est affiché.")]
    [SerializeField] private float _titleRaiseWithBanner = 24f;
    private float _titleDefaultY;

    // AJOUTÉ (2026-10-01) - retour utilisateur : "quand le level up panel arrive, une transition rapide pour
    // afficher les cartes, pas juste un écran qui spawn comme ça, fluide et dynamique". Le fond fait un court
    // fondu, le titre "NIVEAU SUPÉRIEUR !" (et le bandeau de fusion) apparaissent en "pop" avec un petit
    // dépassement élastique, puis les cartes montent depuis le bas en cascade (gauche -> droite), chacune avec un
    // fondu et un léger rebond d'échelle. Tout tourne en temps réel (unscaled) : le jeu est en pause pendant le choix.
    [Header("Animation d'arrivée du panel de level-up")]
    [Tooltip("Décoche pour revenir à l'apparition instantanée d'avant.")]
    [SerializeField] private bool _introEnabled = true;
    [Tooltip("Durée du fondu du fond/panneau entier.")]
    [SerializeField] private float _panelFadeDuration = 0.12f;
    [Tooltip("Durée du pop du titre \"NIVEAU SUPÉRIEUR !\" (et du bandeau de fusion).")]
    [SerializeField] private float _titlePopDuration = 0.28f;
    [Tooltip("Échelle de départ du titre avant son pop (1 = pas de pop).")]
    [SerializeField] private float _titleStartScale = 0.6f;
    [Tooltip("Délai avant l'apparition de la 1re carte.")]
    [SerializeField] private float _cardFirstDelay = 0.05f;
    [Tooltip("Délai entre l'apparition de chaque carte successive (effet de cascade).")]
    [SerializeField] private float _cardStagger = 0.07f;
    [Tooltip("Durée de l'animation d'UNE carte (montée + fondu + rebond d'échelle).")]
    [SerializeField] private float _cardDuration = 0.3f;
    [Tooltip("Distance (en pixels du Canvas) depuis laquelle chaque carte monte vers sa place.")]
    [SerializeField] private float _cardSlideOffset = 80f;
    [Tooltip("Échelle de départ de chaque carte (1 = pas de variation d'échelle).")]
    [SerializeField] private float _cardStartScale = 0.86f;

    [Header("Apparence des pastilles de palier")]
    [Tooltip("Pastille pas encore atteinte. Gris-brun neutre et discret, ne doit pas attirer l'oeil.")]
    [SerializeField] private Color _dotColorEmpty = new Color(0.35f, 0.30f, 0.25f, 0.6f); // #59503F a 60% alpha

    [Tooltip("Pastille atteinte, palier pas encore max. Cyan - reprend la couleur de la barre d'XP du HUD pour une coherence visuelle immediate.")]
    [SerializeField] private Color _dotColorFilled = new Color(0.176f, 0.831f, 0.812f, 1f); // #2DD4CF

    [Tooltip("Pastille atteinte ET upgrade au palier max. Dore - reprend la couleur du compteur d'Or du HUD.")]
    [SerializeField] private Color _dotColorMax = new Color(1f, 0.788f, 0.302f, 1f); // #FFC94D

    [Header("Sprites optionnels des pastilles (contour vide / disque plein)")]
    [Tooltip("FACULTATIF. Si les deux sont assignes, la pastille change aussi de FORME selon l'etat, en plus de la couleur. Laisse les deux champs vides pour garder ton sprite actuel.")]
    [SerializeField] private Sprite _dotSpriteEmpty;
    [SerializeField] private Sprite _dotSpriteFilled;

    // MODIFIE (2026-10-01) - retour utilisateur : nouveaux parchemins. Doré pour toutes les cartes (upgrades
    // universelles), et un parchemin de couleur par personnage pour ses upgrades à lui (le choix est fait par
    // UpgradeData.Branch : Fireball = Aether, Aura = Kael, Couteaux = Lyra, et les fusions réservées à un personnage).
    // Remplace l'ancien parchemin unique "sobre" (2026-09-27) ; FormerlySerializedAs conserve l'image déjà
    // assignée dans le champ d'origine, qui devient le parchemin doré.
    [Header("Parchemins (doré par défaut, un par personnage pour ses upgrades)")]
    [Tooltip("Parchemin des upgrades universelles (et de toute carte dont un parchemin de personnage n'est pas assigné).")]
    [FormerlySerializedAs("_parchmentSober")]
    [SerializeField] private Sprite _parchmentGold;
    [Tooltip("Parchemin des upgrades d'Aether (Fireball et ses fusions exclusives).")]
    [SerializeField] private Sprite _parchmentAether;
    [Tooltip("Parchemin des upgrades de Kael (Aura et ses fusions exclusives).")]
    [SerializeField] private Sprite _parchmentKael;
    [Tooltip("Parchemin des upgrades de Lyra (Couteaux et ses fusions exclusives).")]
    [SerializeField] private Sprite _parchmentLyra;

    [Header("Position de la ligne de pastilles (ProgressionRow)")]
    [Tooltip("Anchored Position X a appliquer quand le losange de deblocage est visible (ex: Orbital, Eclair, Boue).")]
    [SerializeField] private float _progressionRowOffsetWithUnlockDot = -85f;
    [Tooltip("Anchored Position X a appliquer quand il n'y a que les 3 dots de palier, sans losange (ex: Couteaux, Aura, Fireball, Orbe Rebondissant).")]
    [SerializeField] private float _progressionRowOffsetWithoutUnlockDot = 0f;

    [Header("Delai et animation apres un pick - s'applique a TOUS les picks")]
    [Tooltip("Duree totale entre le clic et la confirmation du pick (fermeture du panel). Vise 1 a 1.5s.")]
    [SerializeField] private float _pickConfirmDelay = 1.3f;

    [Tooltip("Duree du remplissage fluide (lerp de couleur) d'une pastille ou du losange de deblocage.")]
    [SerializeField] private float _dotFillAnimDuration = 0.3f;

    [Tooltip("Echelle atteinte par la pastille qui se remplit pendant son pop (1 = pas de pop).")]
    [SerializeField] private float _dotFillPopScale = 1.35f;

    [Tooltip("Duree du pop de l'icone de la carte, joue sur CHAQUE pick (y compris les upgrades sans pastilles).")]
    [SerializeField] private float _iconPopDuration = 0.35f;

    [Tooltip("Echelle atteinte par l'icone pendant son pop.")]
    [SerializeField] private float _iconPopScale = 1.12f;

    [Header("Feedback additionnel reserve au palier max")]
    [Tooltip("Echelle atteinte par TOUTE la rangee de 3 pastilles quand ce pick atteint le palier max (en plus du remplissage de la pastille elle-meme) - pop plus ample et plus lent qu'un pick normal, pour marquer le moment.")]
    [SerializeField] private float _maxTierRowPopScale = 1.25f;

    [Header("Son (optionnel, joue uniquement au palier max)")]
    [Tooltip("FACULTATIF. Si les deux champs sont assignes, un son est joue au moment ou une upgrade atteint son palier max. Laisse vide pour ne pas jouer de son.")]
    [SerializeField] private AudioSource _sfxSource;
    [SerializeField] private AudioClip _maxTierSfx;

    private struct PickAnimContext
    {
        public bool showDots;
        public int currentLevel;
        public int maxLevel;
        public bool willReachMax;
        public bool requiresUnlockDot;
        public bool wasUnlockedBeforePick;
    }
    private PickAnimContext[] _pickContext;

    [System.Serializable]
    public class UpgradeCard
    {
        public GameObject cardRoot;
        public TextMeshProUGUI nameText;
        public TextMeshProUGUI descriptionText;
        public Button chooseButton;

        [Header("Fond et icone")]
        public Image backgroundImage;
        public Image iconImage;

        [Header("Pastilles de palier (3 max)")]
        public GameObject tierDotsContainer;
        public Image[] tierDots;

        [Header("Compteur x1/x2/x3 (upgrades sans pastilles : Degats/FireRate/Heal)")]
        // AJOUTE - meme emplacement que tierDotsContainer dans le prefab, affiche
        // a la place quand l'upgrade n'a pas de pastilles (cap eleve/illimite),
        // pour ne plus laisser cet espace vide en bas de carte.
        public TextMeshProUGUI stackCountText;

        [Header("Pastille de deblocage (upgrades a debloquer uniquement)")]
        public Image unlockDot;

        [Header("Repositionnement de la ligne selon presence du losange")]
        public RectTransform progressionRow;
    }

    private void Awake()
    {
        _pickContext = new PickAnimContext[_cards.Length];

        if (_levelUpTitleText != null)
            _titleDefaultY = _levelUpTitleText.anchoredPosition.y;

        for (int i = 0; i < _cards.Length; i++)
        {
            if (_cards[i].chooseButton != null)
            {
                int index = i;
                _cards[i].chooseButton.onClick.RemoveAllListeners();
                _cards[i].chooseButton.onClick.AddListener(() => OnCardSelected(index));
            }
        }
    }

    // AJOUTÉ (2026-10-01) - choisit le parchemin d'une carte d'après la branche de son upgrade. Si le parchemin du
    // personnage n'est pas encore assigné dans l'Inspector, retombe sur le doré (jamais de carte sans fond).
    private Sprite GetParchmentFor(UpgradeData upgrade)
    {
        Sprite branchSprite = null;
        switch (upgrade.Branch)
        {
            case UpgradeBranch.Aether: branchSprite = _parchmentAether; break;
            case UpgradeBranch.Kael: branchSprite = _parchmentKael; break;
            case UpgradeBranch.Lyra: branchSprite = _parchmentLyra; break;
        }
        return branchSprite != null ? branchSprite : _parchmentGold;
    }

    public void DisplayUpgrades(List<UpgradeData> upgrades)
    {
        if (upgrades == null) return;

        _pickInProgress = false;

        bool hasFusion = false;
        for (int i = 0; i < upgrades.Count; i++)
        {
            if (upgrades[i] != null && upgrades[i].upgradeType == UpgradeType.Fusion) { hasFusion = true; break; }
        }
        if (_fusionAvailableBanner != null)
            _fusionAvailableBanner.gameObject.SetActive(hasFusion);
        if (_levelUpTitleText != null)
        {
            Vector2 titlePos = _levelUpTitleText.anchoredPosition;
            titlePos.y = hasFusion ? _titleDefaultY + _titleRaiseWithBanner : _titleDefaultY;
            _levelUpTitleText.anchoredPosition = titlePos;
        }

        for (int i = 0; i < _cards.Length; i++)
        {
            if (i < upgrades.Count)
            {
                bool isFusionCard = upgrades[i].upgradeType == UpgradeType.Fusion;
                // MODIFIE (2026-09-30, retour utilisateur : "sur la carte, en haut c'est écrit Fusion, en bas
                // du Fusion l'icône, et sous l'icône la description") - une carte de fusion affiche toujours le
                // mot générique "FUSION" comme titre (pas le nom spécifique de la fusion, réservé à sa
                // description) et inverse l'ordre icône/description (normalement description au-dessus de
                // l'icône, ici l'inverse) - repositionnement direct des 2 éléments, aucune fusion n'a de
                // pastilles ni de losange de déblocage (RequiresUnlockPick=false, maxLevel=1) donc l'espace en
                // dessous est libre.
                _cards[i].nameText.text = isFusionCard ? "FUSION" : upgrades[i].upgradeName;
                _cards[i].descriptionText.text = upgrades[i].GetDynamicDescription();
                if (_cards[i].iconImage != null)
                {
                    RectTransform iconRt = (RectTransform)_cards[i].iconImage.transform;
                    iconRt.anchoredPosition = new Vector2(iconRt.anchoredPosition.x, isFusionCard ? 10f : -13f);
                }
                if (_cards[i].descriptionText != null)
                {
                    RectTransform descRt = (RectTransform)_cards[i].descriptionText.transform;
                    descRt.anchoredPosition = new Vector2(descRt.anchoredPosition.x, isFusionCard ? -160f : 140f);
                }

                // MODIFIÉ (2026-10-01) - parchemin choisi selon la branche de l'upgrade (voir GetParchmentFor).
                if (_cards[i].backgroundImage != null)
                    _cards[i].backgroundImage.sprite = GetParchmentFor(upgrades[i]);

                if (_cards[i].iconImage != null)
                {
                    _cards[i].iconImage.sprite = upgrades[i].icon;
                    // AJOUTE (2026-09-27, retour utilisateur : "certaines icones sont déformés") - les icônes de
                    // fusion n'ont pas toutes le même ratio W/H que les icônes d'upgrade de base ; sans
                    // preserveAspect, Image les étire pour remplir tout le cadre carré de la carte.
                    _cards[i].iconImage.preserveAspect = true;
                }

                ResetCardScales(_cards[i]);

                UpdateTierDots(i, _cards[i], upgrades[i]);

                if (_cards[i].chooseButton != null)
                    _cards[i].chooseButton.interactable = true;

                if (_cards[i].cardRoot != null)
                    _cards[i].cardRoot.SetActive(true);
                else
                    _cards[i].chooseButton.gameObject.SetActive(true);
            }
            else
            {
                if (_cards[i].cardRoot != null)
                    _cards[i].cardRoot.SetActive(false);
                else
                    _cards[i].chooseButton.gameObject.SetActive(false);
            }
        }

        // AJOUTÉ (2026-10-01) - lance la transition d'arrivée maintenant que les cartes sont configurées et
        // activées (voir StartIntro : si le panel n'est pas encore actif, elle démarre dans OnEnable).
        StartIntro();
    }

    private void ResetCardScales(UpgradeCard card)
    {
        if (card.iconImage != null)
            card.iconImage.rectTransform.localScale = Vector3.one;

        if (card.tierDotsContainer != null)
            card.tierDotsContainer.transform.localScale = Vector3.one;

        if (card.tierDots != null)
        {
            foreach (Image dot in card.tierDots)
            {
                if (dot != null)
                    dot.rectTransform.localScale = Vector3.one;
            }
        }

        if (card.unlockDot != null)
            card.unlockDot.rectTransform.localScale = Vector3.one;
    }

    private void UpdateTierDots(int index, UpgradeCard card, UpgradeData upgrade)
    {
        bool requiresUnlockDot = upgrade.RequiresUnlockPick;
        bool wasUnlockedBeforePick = upgrade.IsUnlocked();

        if (card.unlockDot != null)
        {
            card.unlockDot.gameObject.SetActive(requiresUnlockDot);
            if (requiresUnlockDot)
                card.unlockDot.color = wasUnlockedBeforePick ? _dotColorFilled : _dotColorEmpty;
        }

        if (card.progressionRow != null)
        {
            Vector2 pos = card.progressionRow.anchoredPosition;
            pos.x = requiresUnlockDot ? _progressionRowOffsetWithUnlockDot : _progressionRowOffsetWithoutUnlockDot;
            card.progressionRow.anchoredPosition = pos;
        }

        int maxLevel = upgrade.MaxLevel;
        bool showDots = maxLevel > 1 && maxLevel <= 3 && card.tierDotsContainer != null && card.tierDots != null;

        if (card.tierDotsContainer != null)
            card.tierDotsContainer.SetActive(showDots);

        // AJOUTE - compteur x1/x2/x3 : uniquement pour les upgrades a cap eleve/
        // illimite (Degats/Cadence/Soin, maxLevel > 3 ou tres grand), PAS pour
        // Tir x2 (maxLevel == 1, deblocage binaire ou "x1" n'aurait aucun sens).
        // Ne s'affiche qu'a partir du 1er pick deja effectue (jamais "x0" sur une
        // carte encore jamais prise, pour ne pas alourdir un premier choix).
        int rawLevelForStack = upgrade.GetCurrentLevel();
        bool showStackCount = !showDots && maxLevel > 1 && rawLevelForStack >= 1;
        if (card.stackCountText != null)
        {
            card.stackCountText.gameObject.SetActive(showStackCount);
            if (showStackCount)
                card.stackCountText.text = $"x{rawLevelForStack}";
        }

        int currentLevel = showDots ? upgrade.GetDisplayLevel() : 0;
        bool alreadyMaxed = showDots && currentLevel >= maxLevel;
        bool willReachMax = showDots && (currentLevel + 1 >= maxLevel);

        if (index < _pickContext.Length)
        {
            _pickContext[index] = new PickAnimContext
            {
                showDots = showDots,
                currentLevel = currentLevel,
                maxLevel = maxLevel,
                willReachMax = willReachMax,
                requiresUnlockDot = requiresUnlockDot,
                wasUnlockedBeforePick = wasUnlockedBeforePick
            };
        }

        if (!showDots) return;

        for (int d = 0; d < card.tierDots.Length; d++)
        {
            if (card.tierDots[d] == null) continue;

            bool dotExists = d < maxLevel;
            card.tierDots[d].gameObject.SetActive(dotExists);
            if (!dotExists) continue;

            bool dotIsFilled = d < currentLevel;

            if (!dotIsFilled)
                card.tierDots[d].color = _dotColorEmpty;
            else
                card.tierDots[d].color = alreadyMaxed ? _dotColorMax : _dotColorFilled;

            if (_dotSpriteEmpty != null && _dotSpriteFilled != null)
                card.tierDots[d].sprite = dotIsFilled ? _dotSpriteFilled : _dotSpriteEmpty;
        }
    }

    // ------------------------------------------------------------------
    // Transition d'arrivée du panel (2026-10-01)
    // ------------------------------------------------------------------

    private struct IntroTarget
    {
        public RectTransform rt;
        public CanvasGroup group;
        public Vector2 basePos;
        public Vector3 baseScale;
        public float delay;
        public float duration;
        public float startScale;
        public float slideOffset;
    }

    private readonly List<IntroTarget> _introTargets = new List<IntroTarget>();
    private CanvasGroup _panelGroup;
    private Coroutine _introRoutine;
    private bool _introPending = false;

    // Si le panel n'est pas encore actif au moment où DisplayUpgrades() est appelé (LevelUpManager peut afficher les
    // cartes AVANT d'activer le panel), l'animation est mise en attente et démarre à l'activation (OnEnable).
    private void StartIntro()
    {
        if (!_introEnabled) return;

        if (!isActiveAndEnabled)
        {
            _introPending = true;
            return;
        }

        _introPending = false;
        StopIntro();
        _introRoutine = StartCoroutine(PlayIntro());
    }

    private void OnEnable()
    {
        if (_introPending) StartIntro();
    }

    // Interrompt une animation en cours et remet TOUT dans son état final (opaque, taille et position normales) :
    // un panel rouvert ou une animation relancée ne doit jamais hériter d'un état à mi-chemin.
    private void StopIntro()
    {
        if (_introRoutine != null)
        {
            StopCoroutine(_introRoutine);
            _introRoutine = null;
        }
        ApplyIntroFinalState();
    }

    private void ApplyIntroFinalState()
    {
        for (int i = 0; i < _introTargets.Count; i++)
        {
            IntroTarget t = _introTargets[i];
            if (t.rt == null) continue;
            if (t.group != null) t.group.alpha = 1f;
            t.rt.localScale = t.baseScale;
            t.rt.anchoredPosition = t.basePos;
        }
        _introTargets.Clear();

        if (_panelGroup != null) _panelGroup.alpha = 1f;
    }

    private void AddIntroTarget(RectTransform rt, float delay, float duration, float startScale, float slideOffset)
    {
        if (rt == null) return;

        CanvasGroup group = rt.GetComponent<CanvasGroup>();
        if (group == null) group = rt.gameObject.AddComponent<CanvasGroup>();

        _introTargets.Add(new IntroTarget
        {
            rt = rt,
            group = group,
            basePos = rt.anchoredPosition,
            baseScale = rt.localScale,
            delay = delay,
            duration = Mathf.Max(0.01f, duration),
            startScale = startScale,
            slideOffset = slideOffset
        });
    }

    private static float EaseOutCubic(float t)
    {
        float u = 1f - t;
        return 1f - u * u * u;
    }

    // Rebond léger : dépasse un peu la valeur finale puis revient (même famille que le pop des pastilles).
    private static float EaseOutBack(float t)
    {
        const float c1 = 1.2f;
        const float c3 = c1 + 1f;
        float u = t - 1f;
        return 1f + c3 * u * u * u + c1 * u * u;
    }

    private System.Collections.IEnumerator PlayIntro()
    {
        _introTargets.Clear();

        // Fond / panneau entier : court fondu.
        if (_panelGroup == null)
        {
            _panelGroup = GetComponent<CanvasGroup>();
            if (_panelGroup == null) _panelGroup = gameObject.AddComponent<CanvasGroup>();
        }

        // Les cartes peuvent être rangées par un LayoutGroup : on force la mise en page AVANT de mémoriser leurs
        // positions finales, sinon on retiendrait une position pas encore calculée.
        RectTransform cardsParent = null;
        for (int i = 0; i < _cards.Length; i++)
        {
            GameObject target = _cards[i].cardRoot != null ? _cards[i].cardRoot : (_cards[i].chooseButton != null ? _cards[i].chooseButton.gameObject : null);
            if (target != null && target.activeSelf) { cardsParent = target.transform.parent as RectTransform; break; }
        }
        if (cardsParent != null) LayoutRebuilder.ForceRebuildLayoutImmediate(cardsParent);

        // Titre "NIVEAU SUPÉRIEUR !" : pop d'échelle + fondu (sa position Y est déjà réglée par DisplayUpgrades).
        AddIntroTarget(_levelUpTitleText, 0f, _titlePopDuration, _titleStartScale, 0f);

        // Bandeau "FUSION DISPONIBLE !" (s'il est affiché) : même pop, un cran après le titre.
        if (_fusionAvailableBanner != null && _fusionAvailableBanner.gameObject.activeSelf)
            AddIntroTarget(_fusionAvailableBanner.rectTransform, 0.1f, _titlePopDuration, _titleStartScale, 0f);

        // Cartes : montée depuis le bas + fondu + rebond d'échelle, en cascade de gauche à droite.
        int shown = 0;
        for (int i = 0; i < _cards.Length; i++)
        {
            GameObject target = _cards[i].cardRoot != null ? _cards[i].cardRoot : (_cards[i].chooseButton != null ? _cards[i].chooseButton.gameObject : null);
            if (target == null || !target.activeSelf) continue;

            AddIntroTarget((RectTransform)target.transform, _cardFirstDelay + shown * _cardStagger, _cardDuration, _cardStartScale, _cardSlideOffset);
            shown++;
        }

        // Etat de départ appliqué IMMÉDIATEMENT (avant le premier yield, donc avant le moindre rendu) : jamais une
        // image des cartes déjà à leur place avant que l'animation ne commence.
        _panelGroup.alpha = 0f;
        float totalDuration = _panelFadeDuration;
        for (int i = 0; i < _introTargets.Count; i++)
        {
            IntroTarget t = _introTargets[i];
            t.group.alpha = 0f;
            t.rt.localScale = t.baseScale * t.startScale;
            t.rt.anchoredPosition = t.basePos + new Vector2(0f, -t.slideOffset);
            totalDuration = Mathf.Max(totalDuration, t.delay + t.duration);
        }

        float elapsed = 0f;
        while (elapsed < totalDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            _panelGroup.alpha = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, _panelFadeDuration));

            for (int i = 0; i < _introTargets.Count; i++)
            {
                IntroTarget t = _introTargets[i];
                if (t.rt == null) continue;

                float p = Mathf.Clamp01((elapsed - t.delay) / t.duration);

                t.group.alpha = Mathf.Clamp01(p * 2f);
                t.rt.localScale = t.baseScale * Mathf.LerpUnclamped(t.startScale, 1f, EaseOutBack(p));
                t.rt.anchoredPosition = t.basePos + new Vector2(0f, Mathf.Lerp(-t.slideOffset, 0f, EaseOutCubic(p)));
            }

            yield return null;
        }

        _introRoutine = null;
        ApplyIntroFinalState();
    }

    // CORRIGE (2026-09-26) - cliquer vite sur plusieurs cartes lançait l'animation de choix sur TOUTES
    // (une coroutine par clic : seule la première carte doit être prise en compte, et la seule animée).
    // Verrou levé à chaque nouvel affichage des cartes (DisplayUpgrades) et à la fermeture du panneau.
    private bool _pickInProgress = false;

    private void OnDisable()
    {
        _pickInProgress = false;
        StopIntro();
    }

    private void OnCardSelected(int index)
    {
        if (LevelUpManager.Instance == null) return;
        if (_pickInProgress) return;

        if (gameObject.activeInHierarchy)
        {
            _pickInProgress = true;

            // Les autres cartes ne doivent plus rien capter pendant l'animation de la carte choisie.
            for (int i = 0; i < _cards.Length; i++)
            {
                if (i != index && _cards[i].chooseButton != null)
                    _cards[i].chooseButton.interactable = false;
            }

            StartCoroutine(AnimatePickThenConfirm(index));
        }
        else
        {
            LevelUpManager.Instance.SelectUpgrade(index);
        }
    }

    // AJOUTE - point d'entree public pour une selection par CLAVIER (touches 1/2/3
    // dans LevelUpManager.Update()). Avant ce correctif, le clavier appelait
    // LevelUpManager.SelectUpgrade() directement, court-circuitant entierement
    // cette classe - meme probleme que l'ancien listener persistant sur les boutons
    // qu'on avait corrige plus tot : un deuxieme point d'entree qui ignore le delai
    // et l'animation. Desormais le clavier passe par le MEME chemin que le clic
    // souris (OnCardSelected), donc la meme coroutine d'animation se declenche.
    public void SelectCardByIndex(int index)
    {
        if (index < 0 || index >= _cards.Length) return;

        // Une carte non affichee actuellement (ex: touche "3" pressee alors qu'il
        // n'y a que 2 upgrades disponibles ce level-up) ne doit rien declencher.
        bool cardVisible = _cards[index].cardRoot != null
            ? _cards[index].cardRoot.activeSelf
            : (_cards[index].chooseButton != null && _cards[index].chooseButton.gameObject.activeSelf);

        if (!cardVisible) return;

        OnCardSelected(index);
    }

    private System.Collections.IEnumerator AnimatePickThenConfirm(int index)
    {
        UpgradeCard card = _cards[index];
        PickAnimContext ctx = index < _pickContext.Length ? _pickContext[index] : default;

        if (card.chooseButton != null)
            card.chooseButton.interactable = false;

        // --- Preparation des elements a animer ---

        bool animateDot = ctx.showDots && ctx.currentLevel < ctx.maxLevel
            && card.tierDots != null && ctx.currentLevel < card.tierDots.Length
            && card.tierDots[ctx.currentLevel] != null
            && (!ctx.requiresUnlockDot || ctx.wasUnlockedBeforePick);
        Image dotToFill = animateDot ? card.tierDots[ctx.currentLevel] : null;
        Color dotTargetColor = ctx.willReachMax ? _dotColorMax : _dotColorFilled;
        Vector3 dotOriginalScale = animateDot ? dotToFill.rectTransform.localScale : Vector3.one;

        // AJOUTE - liste des pastilles DEJA remplies avant ce pick (donc cyan), qui
        // doivent elles aussi basculer au dore EN MEME TEMPS que la nouvelle pastille,
        // uniquement quand ce pick atteint le palier max. Sans ca, seule la derniere
        // pastille change de couleur pendant l'animation, et les precedentes restent
        // cyan jusqu'au prochain affichage de la carte - donnant l'impression fausse
        // que le palier max n'est pas encore vraiment atteint dans son ensemble.
        List<Image> additionalGoldDots = new List<Image>();
        if (ctx.willReachMax && card.tierDots != null)
        {
            for (int d = 0; d < ctx.currentLevel && d < card.tierDots.Length; d++)
            {
                if (card.tierDots[d] != null) additionalGoldDots.Add(card.tierDots[d]);
            }
        }

        bool animateUnlock = ctx.requiresUnlockDot && !ctx.wasUnlockedBeforePick && card.unlockDot != null;
        Vector3 unlockOriginalScale = animateUnlock ? card.unlockDot.rectTransform.localScale : Vector3.one;

        bool animateIcon = card.iconImage != null;
        Vector3 iconOriginalScale = animateIcon ? card.iconImage.rectTransform.localScale : Vector3.one;

        bool animateRow = ctx.willReachMax && card.tierDotsContainer != null;
        Vector3 rowOriginalScale = animateRow ? card.tierDotsContainer.transform.localScale : Vector3.one;

        if (ctx.willReachMax && _sfxSource != null && _maxTierSfx != null)
            _sfxSource.PlayOneShot(_maxTierSfx);

        // --- Boucle d'animation, sur toute la duree du delai avant confirmation ---

        float elapsed = 0f;
        while (elapsed < _pickConfirmDelay)
        {
            elapsed += Time.unscaledDeltaTime;

            // MODIFIE - fillT calcule une seule fois par frame, partage entre la
            // pastille qui se remplit ET les pastilles precedentes a recolorer,
            // pour que toutes progressent exactement en synchro vers le dore.
            float fillT = Mathf.Clamp01(elapsed / _dotFillAnimDuration);

            if (animateDot)
            {
                dotToFill.color = Color.Lerp(_dotColorEmpty, dotTargetColor, fillT);

                float popFactor = Mathf.Sin(fillT * Mathf.PI);
                float scale = 1f + (_dotFillPopScale - 1f) * popFactor;
                dotToFill.rectTransform.localScale = dotOriginalScale * scale;
            }

            // AJOUTE - recolore en parallele toutes les pastilles deja remplies,
            // cyan -> dore, en synchro avec la nouvelle pastille ci-dessus.
            if (additionalGoldDots.Count > 0)
            {
                foreach (Image dot in additionalGoldDots)
                {
                    dot.color = Color.Lerp(_dotColorFilled, _dotColorMax, fillT);
                }
            }

            if (animateUnlock)
            {
                card.unlockDot.color = Color.Lerp(_dotColorEmpty, _dotColorFilled, fillT);

                float popFactor = Mathf.Sin(fillT * Mathf.PI);
                float scale = 1f + (_dotFillPopScale - 1f) * popFactor;
                card.unlockDot.rectTransform.localScale = unlockOriginalScale * scale;
            }

            if (animateIcon)
            {
                float iconT = Mathf.Clamp01(elapsed / _iconPopDuration);
                float popFactor = Mathf.Sin(iconT * Mathf.PI);
                float scale = 1f + (_iconPopScale - 1f) * popFactor;
                card.iconImage.rectTransform.localScale = iconOriginalScale * scale;
            }

            if (animateRow)
            {
                float rowT = Mathf.Clamp01(elapsed / _pickConfirmDelay);
                float popFactor = Mathf.Sin(rowT * Mathf.PI);
                float scale = 1f + (_maxTierRowPopScale - 1f) * popFactor;
                card.tierDotsContainer.transform.localScale = rowOriginalScale * scale;
            }

            yield return null;
        }

        // --- Fin d'anim : on fige les etats finaux avant de confirmer ---

        if (animateDot)
        {
            dotToFill.color = dotTargetColor;
            dotToFill.rectTransform.localScale = dotOriginalScale;
            if (_dotSpriteFilled != null) dotToFill.sprite = _dotSpriteFilled;
        }

        // AJOUTE - fige aussi les pastilles precedentes en dore (securite anti-arrondi
        // flottant, meme raison que pour dotToFill juste au-dessus).
        if (additionalGoldDots.Count > 0)
        {
            foreach (Image dot in additionalGoldDots)
            {
                dot.color = _dotColorMax;
            }
        }

        if (animateUnlock)
        {
            card.unlockDot.color = _dotColorFilled;
            card.unlockDot.rectTransform.localScale = unlockOriginalScale;
        }
        if (animateIcon)
        {
            card.iconImage.rectTransform.localScale = iconOriginalScale;
        }
        if (animateRow)
        {
            card.tierDotsContainer.transform.localScale = rowOriginalScale;
        }

        LevelUpManager.Instance.SelectUpgrade(index);
    }
}