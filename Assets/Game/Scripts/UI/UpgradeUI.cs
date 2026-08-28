using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class UpgradeUI : MonoBehaviour
{
    [Header("Cartes d'upgrade")]
    [SerializeField] private UpgradeCard[] _cards;

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

    [Header("Parchemins par branche")]
    [Tooltip("Assigne les 4 sprites de parchemin generes (couleur deja cuite dans l'image, glow inclus).")]
    [SerializeField] private Sprite _parchmentAether;    // rouge
    [SerializeField] private Sprite _parchmentKael;      // vert
    [SerializeField] private Sprite _parchmentLyra;      // bleu
    [SerializeField] private Sprite _parchmentUniversal; // dore

    [Header("Position de la ligne de pastilles (ProgressionRow)")]
    [Tooltip("Anchored Position X a appliquer quand le losange de deblocage est visible (ex: Orbital, Eclair, Boue).")]
    [SerializeField] private float _progressionRowOffsetWithUnlockDot = -85f;
    [Tooltip("Anchored Position X a appliquer quand il n'y a que les 3 dots de palier, sans losange (ex: Couteaux, Aura, Fireball, Orbe Rebondissant).")]
    [SerializeField] private float _progressionRowOffsetWithoutUnlockDot = 0f;

    [Header("Delai et animation apres un pick - s'applique a TOUS les picks")]
    // MODIFIE - ne se limite plus au palier max. Chaque clic sur une carte declenche
    // maintenant ce delai, pendant lequel les pastilles/icones s'animent, avant que
    // le pick soit confirme et la carte fermee. Objectif : donner au joueur le temps
    // de VOIR ce qu'il vient de choisir, ce qui manquait totalement avant.
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

    // AJOUTE - remplace l'ancien _willReachMaxOnPick (bool[]) par un contexte plus
    // riche par carte, necessaire pour savoir QUOI animer au clic (quelle pastille,
    // faut-il aussi animer le losange de deblocage, etc.), pas seulement SI on doit
    // flasher. Recalcule a chaque DisplayUpgrades() via UpdateTierDots().
    private struct PickAnimContext
    {
        public bool showDots;              // ce type d'upgrade affiche-t-il des pastilles de palier ?
        public int currentLevel;           // palier actuel AVANT ce pick
        public int maxLevel;                // palier maximum de cette upgrade
        public bool willReachMax;          // ce pick amene-t-il au palier max ?
        public bool requiresUnlockDot;     // cette upgrade a-t-elle un losange de deblocage ?
        public bool wasUnlockedBeforePick; // etait-elle deja debloquee AVANT ce pick ?
    }
    private PickAnimContext[] _pickContext;

    [System.Serializable]
    public class UpgradeCard
    {
        public GameObject cardRoot; // <- Assigne le conteneur parent de la carte ici
        public TextMeshProUGUI nameText;
        public TextMeshProUGUI descriptionText;
        public Button chooseButton;

        [Header("Fond et icone")]
        public Image backgroundImage; // fond parchemin, sprite swappe selon la branche de l'upgrade
        public Image iconImage; // icone de l'upgrade, sprite pris directement sur l'asset UpgradeData

        [Header("Pastilles de palier (3 max)")]
        public GameObject tierDotsContainer; // <- Conteneur horizontal des 3 pastilles
        public Image[] tierDots; // <- Assigne les 3 Image dans l'ordre : palier 1, 2, 3

        [Header("Pastille de deblocage (upgrades a debloquer uniquement)")]
        public Image unlockDot; // <- Assigne l'Image seule, pas besoin de container separe

        [Header("Repositionnement de la ligne selon presence du losange")]
        public RectTransform progressionRow;
    }

    private void Awake()
    {
        _pickContext = new PickAnimContext[_cards.Length];

        // On lie les boutons une seule fois au demarrage pour eviter toute allocation de GC
        for (int i = 0; i < _cards.Length; i++)
        {
            if (_cards[i].chooseButton != null)
            {
                int index = i; // Capture locale securisee pour le scope du Awake
                _cards[i].chooseButton.onClick.RemoveAllListeners();
                _cards[i].chooseButton.onClick.AddListener(() => OnCardSelected(index));
            }
        }
    }

    public void DisplayUpgrades(List<UpgradeData> upgrades)
    {
        if (upgrades == null) return;

        for (int i = 0; i < _cards.Length; i++)
        {
            if (i < upgrades.Count)
            {
                _cards[i].nameText.text = upgrades[i].upgradeName;
                _cards[i].descriptionText.text = upgrades[i].GetDynamicDescription();

                if (_cards[i].backgroundImage != null)
                    _cards[i].backgroundImage.sprite = GetParchmentSprite(upgrades[i].Branch);

                if (_cards[i].iconImage != null)
                    _cards[i].iconImage.sprite = upgrades[i].icon;

                // Reinitialise l'echelle des elements animables : une carte peut etre
                // reutilisee d'un level-up a l'autre, et pourrait garder un scale
                // residuel si DisplayUpgrades() est rappele pendant qu'une anim tournait.
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
    }

    // AJOUTE - remet a Vector3.one l'echelle de l'icone et de la rangee de pastilles,
    // par securite entre deux affichages de cartes.
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

    private Sprite GetParchmentSprite(UpgradeBranch branch)
    {
        switch (branch)
        {
            case UpgradeBranch.Aether: return _parchmentAether;
            case UpgradeBranch.Kael: return _parchmentKael;
            case UpgradeBranch.Lyra: return _parchmentLyra;
            default: return _parchmentUniversal;
        }
    }

    // Pastilles de palier (1/2/3) affichees en bas de la carte. N'affiche les
    // pastilles que pour les upgrades a 3 paliers max. Les fillers a cap eleve ou
    // illimites (Damage, FireRate, Heal, DoubleShot) masquent le conteneur entier.
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

        int currentLevel = showDots ? upgrade.GetDisplayLevel() : 0;
        bool alreadyMaxed = showDots && currentLevel >= maxLevel;
        bool willReachMax = showDots && (currentLevel + 1 >= maxLevel);

        // Enregistre tout le contexte necessaire pour l'animation au clic.
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

    private void OnCardSelected(int index)
    {
        if (LevelUpManager.Instance == null) return;

        if (gameObject.activeInHierarchy)
        {
            StartCoroutine(AnimatePickThenConfirm(index));
        }
        else
        {
            // Filet de securite : si le panel est deja inactif au moment du clic
            // (cas limite), on confirme directement sans animation plutot que de
            // risquer un crash StartCoroutine sur un objet desactive.
            LevelUpManager.Instance.SelectUpgrade(index);
        }
    }

    // MODIFIE - remplace FlashMaxTierThenConfirm(). Tourne desormais sur CHAQUE
    // pick (pas seulement le palier max) : anime la pastille (ou le losange) qui
    // vient de changer d'etat avec un remplissage fluide + un pop, fait toujours
    // un petit pop sur l'icone de la carte (meme sans pastilles), et si ce pick
    // atteint le palier max, ajoute un pop plus ample sur toute la rangee + le son
    // optionnel. Utilise Time.unscaledDeltaTime car Time.timeScale est
    // probablement a 0 pendant l'ecran de level-up.
    private System.Collections.IEnumerator AnimatePickThenConfirm(int index)
    {
        UpgradeCard card = _cards[index];
        PickAnimContext ctx = index < _pickContext.Length ? _pickContext[index] : default;

        if (card.chooseButton != null)
            card.chooseButton.interactable = false;

        // --- Preparation des elements a animer ---

        // Pastille de palier qui vient d'etre obtenue par ce pick (s'il y en a une).
        // MODIFIE - sur les upgrades a deblocage separe (Orbital, Lightning, MudPuddle),
        // le PREMIER pick (celui qui debloque, wasUnlockedBeforePick == false) ne fait
        // QUE debloquer : il n'avance aucun palier, seul le losange doit s'animer. Sans
        // cette condition, la pastille de palier 1 s'animait aussi des le deblocage,
        // donnant l'impression (a tort) d'avoir gagne 2 choses en un seul pick, alors
        // que la donnee reelle (GetDisplayLevel()) ne bouge qu'au pick SUIVANT.
        bool animateDot = ctx.showDots && ctx.currentLevel < ctx.maxLevel
            && card.tierDots != null && ctx.currentLevel < card.tierDots.Length
            && card.tierDots[ctx.currentLevel] != null
            && (!ctx.requiresUnlockDot || ctx.wasUnlockedBeforePick);
        Image dotToFill = animateDot ? card.tierDots[ctx.currentLevel] : null;
        Color dotTargetColor = ctx.willReachMax ? _dotColorMax : _dotColorFilled;
        Vector3 dotOriginalScale = animateDot ? dotToFill.rectTransform.localScale : Vector3.one;

        // Losange de deblocage, uniquement si ce pick est celui qui debloque l'upgrade.
        bool animateUnlock = ctx.requiresUnlockDot && !ctx.wasUnlockedBeforePick && card.unlockDot != null;
        Vector3 unlockOriginalScale = animateUnlock ? card.unlockDot.rectTransform.localScale : Vector3.one;

        // Icone de la carte : pop joue sur TOUS les picks, avec ou sans pastilles.
        bool animateIcon = card.iconImage != null;
        Vector3 iconOriginalScale = animateIcon ? card.iconImage.rectTransform.localScale : Vector3.one;

        // Rangee entiere de pastilles : pop supplementaire, reserve au palier max.
        bool animateRow = ctx.willReachMax && card.tierDotsContainer != null;
        Vector3 rowOriginalScale = animateRow ? card.tierDotsContainer.transform.localScale : Vector3.one;

        if (ctx.willReachMax && _sfxSource != null && _maxTierSfx != null)
            _sfxSource.PlayOneShot(_maxTierSfx);

        // --- Boucle d'animation, sur toute la duree du delai avant confirmation ---

        float elapsed = 0f;
        while (elapsed < _pickConfirmDelay)
        {
            elapsed += Time.unscaledDeltaTime;

            // Remplissage fluide de la pastille de palier : couleur + pop, sur _dotFillAnimDuration.
            if (animateDot)
            {
                float fillT = Mathf.Clamp01(elapsed / _dotFillAnimDuration);
                dotToFill.color = Color.Lerp(_dotColorEmpty, dotTargetColor, fillT);

                float popFactor = Mathf.Sin(fillT * Mathf.PI); // 0 -> 1 -> 0, pic a mi-parcours du remplissage
                float scale = 1f + (_dotFillPopScale - 1f) * popFactor;
                dotToFill.rectTransform.localScale = dotOriginalScale * scale;
            }

            // Meme traitement pour le losange de deblocage, s'il vient d'etre debloque.
            if (animateUnlock)
            {
                float fillT = Mathf.Clamp01(elapsed / _dotFillAnimDuration);
                card.unlockDot.color = Color.Lerp(_dotColorEmpty, _dotColorFilled, fillT);

                float popFactor = Mathf.Sin(fillT * Mathf.PI);
                float scale = 1f + (_dotFillPopScale - 1f) * popFactor;
                card.unlockDot.rectTransform.localScale = unlockOriginalScale * scale;
            }

            // Pop de l'icone, sur CHAQUE pick - c'est le seul feedback anime pour
            // les upgrades sans pastilles (Degats, Cadence, Soin, Tir x2...).
            if (animateIcon)
            {
                float iconT = Mathf.Clamp01(elapsed / _iconPopDuration);
                float popFactor = Mathf.Sin(iconT * Mathf.PI);
                float scale = 1f + (_iconPopScale - 1f) * popFactor;
                card.iconImage.rectTransform.localScale = iconOriginalScale * scale;
            }

            // Pop plus ample de toute la rangee de pastilles, reserve au palier max,
            // etale sur la totalite du delai pour bien marquer le moment.
            if (animateRow)
            {
                float rowT = Mathf.Clamp01(elapsed / _pickConfirmDelay);
                float popFactor = Mathf.Sin(rowT * Mathf.PI);
                float scale = 1f + (_maxTierRowPopScale - 1f) * popFactor;
                card.tierDotsContainer.transform.localScale = rowOriginalScale * scale;
            }

            yield return null;
        }

        // --- Fin d'anim : on fige les etats finaux avant de confirmer, pour eviter
        // tout artefact d'arrondi flottant (ex: couleur pas tout a fait a 100%). ---

        if (animateDot)
        {
            dotToFill.color = dotTargetColor;
            dotToFill.rectTransform.localScale = dotOriginalScale;
            if (_dotSpriteFilled != null) dotToFill.sprite = _dotSpriteFilled;
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