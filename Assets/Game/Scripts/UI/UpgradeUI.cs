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

    private void OnCardSelected(int index)
    {
        if (LevelUpManager.Instance == null) return;

        if (gameObject.activeInHierarchy)
        {
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