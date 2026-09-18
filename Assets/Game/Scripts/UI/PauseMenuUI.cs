using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// A poser sur PausePanel LUI-MEME (le meme objet qui est deja active/desactive par
// ton systeme de pause existant). C'est volontaire : on se branche sur OnEnable/
// OnDisable, qui se declenchent automatiquement chaque fois que ce GameObject est
// active ou desactive, peu importe QUEL script fait ce SetActive(). Ca evite
// completement le piege qu'on a corrige plus tot dans UpgradeUI/LevelUpManager
// (un script qui vit sur un objet different de celui reellement affiche/masque).
public class PauseMenuUI : MonoBehaviour
{
    [Header("References upgrades")]
    [Tooltip("Le Content du Scroll View (UpgradesScrollView > Viewport > Content), la ou les slots sont instancies.")]
    [SerializeField] private Transform _gridContent;
    [Tooltip("Le prefab UpgradeSlot cree a l'etape 6, avec son composant UpgradeSlotRefs deja configure.")]
    [SerializeField] private GameObject _upgradeSlotPrefab;

    [Header("Parchemins par branche (memes sprites que sur UpgradeUI)")]
    [SerializeField] private Sprite _parchmentAether;    // rouge
    [SerializeField] private Sprite _parchmentKael;      // vert
    [SerializeField] private Sprite _parchmentLyra;      // bleu
    [SerializeField] private Sprite _parchmentUniversal; // dore

    [Header("Couleurs des pastilles (memes valeurs que sur UpgradeUI)")]
    [SerializeField] private Color _dotColorEmpty = new Color(0.35f, 0.30f, 0.25f, 0.6f);
    [SerializeField] private Color _dotColorFilled = new Color(0.176f, 0.831f, 0.812f, 1f); // #2DD4CF
    [SerializeField] private Color _dotColorMax = new Color(1f, 0.788f, 0.302f, 1f);        // #FFC94D

    [Header("Position de TierDotsRow selon presence du losange")]
    // Meme principe que _progressionRowOffsetWithUnlockDot/WithoutUnlockDot
    // sur UpgradeUI : la rangee (losange + 3 dots, ou juste 3 dots) doit se recentrer
    // differemment selon que le losange de deblocage est visible ou non, sinon les 3
    // dots seuls paraissent decales par rapport au centre du slot.
    [Tooltip("Anchored Position X quand le losange de deblocage EST visible (Orbital, Lightning, MudPuddle).")]
    [SerializeField] private float _slotDotsRowOffsetWithUnlockDot = 0f;
    [Tooltip("Anchored Position X quand il n'y a PAS de losange (la plupart des upgrades).")]
    [SerializeField] private float _slotDotsRowOffsetWithoutUnlockDot = -17.6f;

    [Header("Barre de stats (Header)")]
    [SerializeField] private TextMeshProUGUI _timeText;
    [SerializeField] private TextMeshProUGUI _killsText;
    [SerializeField] private TextMeshProUGUI _goldText;

    [Header("Défi (Header)")]
    // AJOUTE - affichage detaille du defi de la partie dans le menu pause : nom,
    // difficulte, description complete, recompense chiffree. Complementaire au
    // rappel court deja affiche dans le HUD (GameUI.UpdateChallengeDisplay) -
    // ici c'est la version detaillee, lue uniquement quand le joueur ouvre la
    // pause.
    // MODIFIE - le Statut (En cours/Echoue) est sorti de ce bloc de texte et
    // vit desormais dans _challengeStatusText, une colonne separee a droite
    // (avec un separateur vertical entre les deux) : sur 4 lignes empilees,
    // le bloc etait trop serre en hauteur dans l'espace dedie du HeaderRow
    // (retour utilisateur). 3 lignes ici + 1 ligne a part laisse largement
    // plus de respiration a chaque ligne.
    [SerializeField] private TextMeshProUGUI _challengeInfoText;
    [SerializeField] private TextMeshProUGUI _challengeStatusText;
    [SerializeField] private Image _challengeSeparator;
    // RETIRE (2026-09-14) - le positionnement/redimensionnement du texte de defi
    // en code (icone->texte, largeur dispo avant le separateur) ecrasait a
    // chaque ouverture du menu pause les ajustements manuels que l'utilisateur
    // fait a la main dans l'Inspector sur ChallengeHeaderIcon/ChallengeInfoText/
    // ChallengeSeparator/ChallengeStatusText (retour utilisateur : "j'ai refait
    // des ajustements mais quand j'ai lance ils se sont enleves"). Position et
    // taille de ChallengeInfoText restent entierement figees dans la scene
    // (Inspector). Le retrecissement de police pour les descriptions trop
    // longues reste actif, mais via le Auto Size natif de TMP configure une
    // bonne fois dans l'Inspector (min=18, max=21) plutot que pose en code a
    // chaque frame.
    // AJOUTE (2026-09-15) - le separateur ET la colonne Statut, eux, suivent
    // automatiquement la fin REELLE du texte affiche (retour utilisateur :
    // "l'encrage doit etre auto... la barre verticale et le statut se decale
    // automatiquement... selon la fin du texte de description le plus long").
    // Contrairement a la tentative precedente (retiree), ceci ne touche PAS a
    // la position/taille de ChallengeInfoText lui-meme - seuls le separateur
    // et le statut bougent, voir PositionSeparatorAndStatus().
    [Tooltip("Ecart entre la fin reelle du texte de defi et le separateur vertical.")]
    [SerializeField] private float _challengeTextToSeparatorGap = 24f;
    [Tooltip("Ecart entre le separateur vertical et le debut de la colonne Statut.")]
    [SerializeField] private float _challengeSeparatorToStatusGap = 24f;

    [Header("Défi - couleurs de difficulte")]
    // AJOUTE - code couleur par palier, pour que la difficulte du defi actif
    // se voie vraiment d'un coup d'oeil (retour utilisateur) plutot que de
    // reposer uniquement sur le mot "Facile/Moyen/Difficile" en texte neutre.
    [SerializeField] private Color _difficultyEasyColor = new Color(0.435f, 0.749f, 0.329f);   // #6FBF54
    [SerializeField] private Color _difficultyMediumColor = new Color(0.910f, 0.639f, 0.239f); // #E8A33D
    [SerializeField] private Color _difficultyHardColor = new Color(0.710f, 0.325f, 0.243f);   // #B5533E (meme rouille que le titre Game Over)
    [SerializeField] private Color _challengeStatusFailedColor = new Color(0.710f, 0.325f, 0.243f); // #B5533E
    // MODIFIE (2026-09-13) - blanc pur (retour utilisateur) au lieu de la
    // creme d'origine (#F2EDD9), sur les deux textes de base (nom/description/
    // recompense et statut "En cours") - seuls la difficulte et le statut
    // "Echoue" gardent une couleur dediee.
    [SerializeField] private Color _challengeInfoTextColor = Color.white;
    [SerializeField] private Color _challengeStatusNormalColor = Color.white;
    // AJOUTE (2026-09-13) - troisieme etat du Statut, retour utilisateur : le
    // defi peut deja etre acquis EN COURS de partie (ex. "Vaincre 1 boss"
    // apres avoir tue un boss) sans attendre la fin - voir
    // ChallengeManager.IsCurrentlySucceeding(). Meme vert que la difficulte
    // Facile, mais champ separe pour pouvoir l'ajuster independamment.
    [SerializeField] private Color _challengeStatusSuccessColor = new Color(0.435f, 0.749f, 0.329f); // #6FBF54

    [Header("Animation d'ouverture - fond et panneau")]
    [SerializeField] private Image _dimBackground;
    [SerializeField] private RectTransform _mainFrame;
    [Tooltip("Duree du fondu du voile noir (DimBackground).")]
    [SerializeField] private float _dimFadeDuration = 0.2f;
    [Tooltip("Alpha final du voile noir sur 255 (ex: 140 = ~55%).")]
    [SerializeField] private float _dimTargetAlpha255 = 140f;
    [Tooltip("Duree du fondu + pop d'echelle du MainFrame.")]
    [SerializeField] private float _frameFadeDuration = 0.3f;
    [Tooltip("Echelle de depart du MainFrame avant l'animation (1 = taille normale, donc <1 = leger zoom avant a l'ouverture).")]
    [SerializeField] private float _frameStartScale = 0.92f;

    [Header("Animation d'ouverture - grille d'upgrades")]
    [Tooltip("Duree du fondu individuel d'un slot.")]
    [SerializeField] private float _slotFadeDuration = 0.18f;
    [Tooltip("Delai ajoute entre l'apparition de chaque slot successif, pour un effet de cascade.")]
    [SerializeField] private float _slotStagger = 0.04f;
    [Tooltip("Nombre max de slots qui recoivent le delai de cascade individuellement avant que les suivants apparaissent tous en meme temps que le dernier - evite une cascade interminable si le joueur a 40 upgrades.")]
    [SerializeField] private int _maxStaggeredSlots = 12;

    private readonly List<GameObject> _spawnedSlots = new List<GameObject>();
    private CanvasGroup _mainFrameCanvasGroup;
    private Coroutine _openAnimCoroutine;

    private void Awake()
    {
        // Recupere ou ajoute automatiquement le CanvasGroup necessaire au fondu du
        // MainFrame - pas besoin de l'ajouter a la main dans l'Inspector, le script
        // s'en occupe tout seul au premier lancement.
        if (_mainFrame != null)
        {
            _mainFrameCanvasGroup = _mainFrame.GetComponent<CanvasGroup>();
            if (_mainFrameCanvasGroup == null)
                _mainFrameCanvasGroup = _mainFrame.gameObject.AddComponent<CanvasGroup>();
        }
    }

    private void OnEnable()
    {
        PopulateGrid();
        PullLiveStats();
        PullChallengeInfo();

        if (_openAnimCoroutine != null)
            StopCoroutine(_openAnimCoroutine);
        _openAnimCoroutine = StartCoroutine(PlayOpenAnimation());
    }

    private void OnDisable()
    {
        // Coupe proprement l'animation si le panel est desactive en plein milieu
        // (ex: le joueur spam Echap), pour eviter un etat visuel fige a mi-fondu
        // la prochaine fois que le panel se rouvre.
        if (_openAnimCoroutine != null)
        {
            StopCoroutine(_openAnimCoroutine);
            _openAnimCoroutine = null;
        }
    }

    // ------------------------------------------------------------------
    // Peuplement de la grille
    // ------------------------------------------------------------------

    private void PopulateGrid()
    {
        // Nettoie les slots de l'ouverture precedente avant de repeupler - la liste
        // d'upgrades obtenues a pu changer depuis la derniere fois que le menu pause
        // a ete ouvert (le joueur a probablement pick plusieurs upgrades entre-temps).
        foreach (GameObject oldSlot in _spawnedSlots)
        {
            if (oldSlot != null) Destroy(oldSlot);
        }
        _spawnedSlots.Clear();

        if (LevelUpManager.Instance == null || _gridContent == null || _upgradeSlotPrefab == null)
            return;

        // Suit desormais ObtainedOrder (ordre chronologique reel de pick)
        // plutot que AllUpgrades (ordre fixe du tableau de l'Inspector). Cette liste
        // ne contient deja que des upgrades reellement obtenues au moins une fois,
        // donc plus besoin de filtrer nous-memes ici.
        IReadOnlyList<UpgradeData> obtainedInOrder = LevelUpManager.Instance.ObtainedOrder;
        if (obtainedInOrder == null) return;

        foreach (UpgradeData upgrade in obtainedInOrder)
        {
            if (upgrade == null) continue;

            GameObject slotGO = Instantiate(_upgradeSlotPrefab, _gridContent);
            ConfigureSlot(slotGO, upgrade);
            _spawnedSlots.Add(slotGO);
        }
    }

    private void ConfigureSlot(GameObject slotGO, UpgradeData upgrade)
    {
        UpgradeSlotRefs refs = slotGO.GetComponent<UpgradeSlotRefs>();
        if (refs == null)
        {
            Debug.LogWarning("[PauseMenuUI] Le prefab UpgradeSlot n'a pas de composant UpgradeSlotRefs assigne.");
            return;
        }

        if (refs.background != null)
            refs.background.sprite = GetParchmentSprite(upgrade.Branch);

        if (refs.icon != null)
            refs.icon.sprite = upgrade.icon;

        if (refs.nameText != null)
            refs.nameText.text = upgrade.upgradeName;

        int maxLevel = upgrade.MaxLevel;
        int currentLevel = upgrade.GetDisplayLevel();
        bool alreadyMaxed = currentLevel >= maxLevel;
        bool showDots = maxLevel > 1 && maxLevel <= 3 && refs.tierDots != null;

        if (refs.tierDots != null)
        {
            for (int d = 0; d < refs.tierDots.Length; d++)
            {
                if (refs.tierDots[d] == null) continue;

                bool dotExists = showDots && d < maxLevel;
                refs.tierDots[d].gameObject.SetActive(dotExists);
                if (!dotExists) continue;

                bool filled = d < currentLevel;
                if (!filled)
                    refs.tierDots[d].color = _dotColorEmpty;
                else
                    refs.tierDots[d].color = alreadyMaxed ? _dotColorMax : _dotColorFilled;
            }
        }

        // Compteur x1/x2/x3, meme condition que sur les cartes de
        // level-up : uniquement pour les upgrades sans pastilles (cap eleve/
        // illimite comme Degats/Cadence/Soin), jamais pour Tir x2 (maxLevel==1),
        // et seulement si deja pris au moins une fois.
        bool showStackCount = !showDots && maxLevel > 1 && currentLevel >= 1;
        if (refs.stackCountText != null)
        {
            refs.stackCountText.gameObject.SetActive(showStackCount);
            if (showStackCount)
                refs.stackCountText.text = $"x{currentLevel}";
        }

        // requiresUnlock calcule une seule fois, reutilise a la fois pour
        // le losange ET pour repositionner TierDotsRow, meme si l'un des deux
        // champs n'est pas assigne dans l'Inspector (les deux restent independants).
        bool requiresUnlock = upgrade.RequiresUnlockPick;

        if (refs.unlockDot != null)
        {
            refs.unlockDot.gameObject.SetActive(requiresUnlock);
            if (requiresUnlock)
                refs.unlockDot.color = upgrade.IsUnlocked() ? _dotColorFilled : _dotColorEmpty;
        }

        if (refs.tierDotsRow != null)
        {
            Vector2 rowPos = refs.tierDotsRow.anchoredPosition;
            rowPos.x = requiresUnlock ? _slotDotsRowOffsetWithUnlockDot : _slotDotsRowOffsetWithoutUnlockDot;
            refs.tierDotsRow.anchoredPosition = rowPos;
        }

        // Etat de depart pour l'animation en cascade : invisible tant que
        // PlayOpenAnimation() ne l'a pas fait apparaitre. Ajoute un CanvasGroup au
        // slot si le prefab n'en a pas deja un, meme logique que pour MainFrame.
        CanvasGroup slotCanvasGroup = slotGO.GetComponent<CanvasGroup>();
        if (slotCanvasGroup == null)
            slotCanvasGroup = slotGO.AddComponent<CanvasGroup>();
        slotCanvasGroup.alpha = 0f;
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

    // ------------------------------------------------------------------
    // Stats (Temps / Kills / Or) - a brancher depuis ton systeme existant
    // ------------------------------------------------------------------

    // Va chercher les valeurs directement sur GameManager (RunTimer,
    // KillCount, deja publics) et MetaProgressionManager (RunGold) a chaque
    // ouverture du menu pause. Aucun cablage externe necessaire : PauseMenuUI se
    // sert lui-meme, plutot que d'attendre qu'un autre script lui pousse les
    // valeurs.
    private void PullLiveStats()
    {
        if (GameManager.Instance == null) return;

        int gold = MetaProgressionManager.Instance != null
            ? MetaProgressionManager.Instance.RunGold
            : 0;

        RefreshStats(GameManager.Instance.RunTimer, GameManager.Instance.KillCount, gold);
    }

    // Reste public et utilisable directement si tu veux forcer un rafraichissement
    // des stats a un autre moment (ex: en cours de pause, sans fermer/rouvrir le menu).
    public void RefreshStats(float elapsedSeconds, int kills, int gold)
    {
        if (_timeText != null)
        {
            int minutes = Mathf.FloorToInt(elapsedSeconds / 60f);
            int seconds = Mathf.FloorToInt(elapsedSeconds % 60f);
            _timeText.text = $"{minutes:00}:{seconds:00}";
        }

        if (_killsText != null)
            _killsText.text = kills.ToString();

        if (_goldText != null)
            _goldText.text = gold.ToString();
    }

    // ------------------------------------------------------------------
    // Défi de la partie - affichage détaillé
    // ------------------------------------------------------------------

    // AJOUTE - remplit le texte detaille du defi : nom, palier de difficulte
    // (colore selon _difficultyXxxColor - retour utilisateur, "montrer
    // vraiment si il est difficile ou pas"), description complete, recompense
    // chiffree (via ChallengeManager.GetCurrentRewardPercent()). Le Statut vit
    // a part dans _challengeStatusText (voir PullChallengeStatus) - 3 lignes
    // ici au lieu de 4, plus de place par ligne dans l'espace dedie du
    // HeaderRow. Appele a chaque ouverture du menu pause, comme PullLiveStats().
    private void PullChallengeInfo()
    {
        PullChallengeStatus();

        if (_challengeInfoText == null) return;

        if (ChallengeManager.Instance == null || ChallengeManager.Instance.CurrentChallenge == null)
        {
            _challengeInfoText.text = "";
            return;
        }

        // AJOUTE (2026-09-15) - si le defi de cette heure a deja ete reussi
        // lors d'une run precedente (meme heure), le refaire ne rapporte plus
        // rien (ChallengeManager.ApplyGoldReward l'ignore deja) - le retour
        // utilisateur demande explicitement de ne plus le presenter comme "a
        // faire" pour le reste de l'heure, avec un message d'attente a la
        // place. PullChallengeStatus() a deja cache separateur/statut dans ce
        // cas (voir plus bas).
        if (ChallengeManager.Instance.IsRewardAlreadyClaimedThisHour)
        {
            int minutes = ChallengeManager.Instance.GetMinutesUntilNextChallenge();
            _challengeInfoText.color = _challengeInfoTextColor;
            // MODIFIE (2026-09-15) - minutes <= 0 (voir GetMinutesUntilNextChallenge)
            // signifie qu'on a deja depasse l'heure de bascule EN COURS DE RUN -
            // le nouveau defi est deja "du" mais ne sera tire qu'au prochain
            // chargement de la scene Jeu, jamais un compte a rebours fige et
            // trompeur ("moins d'une minute" pendant potentiellement des heures).
            _challengeInfoText.text = minutes > 0
                ? $"Défi déjà réussi cette heure !\nNouveau défi dans {minutes} min."
                : "Défi déjà réussi cette heure !\nNouveau défi à ta prochaine partie !";
            return;
        }

        ChallengeDefinition challenge = ChallengeManager.Instance.CurrentChallenge;
        string difficultyLabel = GetDifficultyLabel(challenge.difficulty);
        string difficultyColorHex = ColorUtility.ToHtmlStringRGB(GetDifficultyColor(challenge.difficulty));
        // MODIFIE (2026-09-13) - "Nom : Niv. Difficulte" au lieu de "Nom
        // (Difficulte)" (retour utilisateur) ; recompense en "Or xN" au lieu
        // de "+X%" (voir ChallengeManager.FormatRewardMultiplier).
        string rewardText = ChallengeManager.FormatRewardMultiplier(ChallengeManager.Instance.GetCurrentRewardPercent());

        _challengeInfoText.color = _challengeInfoTextColor;
        _challengeInfoText.text =
            $"{challenge.displayName} : Niv. <color=#{difficultyColorHex}>{difficultyLabel}</color>\n" +
            $"{challenge.description}\n" +
            $"Récompense : {rewardText}";
        // Position et taille de ChallengeInfoText restent figees dans
        // l'Inspector - seuls le separateur et le statut s'ajustent, voir
        // PositionSeparatorAndStatus().
        PositionSeparatorAndStatus();
    }

    // AJOUTE - colonne "Statut" separee (voir _challengeStatusText), centree
    // verticalement a droite du separateur. Rouge (_challengeStatusFailedColor,
    // meme rouille que le titre Game Over) si echoue, couleur normale sinon -
    // le mot seul ne suffisait pas a alerter au premier coup d'oeil.
    // MODIFIE (2026-09-13) - 3e etat "Reussi" (vert) : le statut restait
    // bloque sur "En cours" meme quand la condition etait deja acquise en
    // cours de partie (ex. "Vaincre 1 boss" apres un boss tue) - retour
    // utilisateur. Voir ChallengeManager.IsCurrentlySucceeding().
    // MODIFIE (2026-09-15) - separateur ET statut caches (pas seulement le
    // statut mis a vide) quand le defi de cette heure a deja ete reussi :
    // PullChallengeInfo affiche a la place un message "Nouveau defi dans X
    // min", la colonne Statut n'a plus de sens dans cet etat.
    private void PullChallengeStatus()
    {
        if (_challengeStatusText == null) return;

        bool hasChallenge = ChallengeManager.Instance != null && ChallengeManager.Instance.CurrentChallenge != null;
        bool alreadyClaimed = hasChallenge && ChallengeManager.Instance.IsRewardAlreadyClaimedThisHour;
        bool showStatusColumn = hasChallenge && !alreadyClaimed;

        if (_challengeSeparator != null) _challengeSeparator.gameObject.SetActive(showStatusColumn);

        if (!showStatusColumn)
        {
            _challengeStatusText.text = "";
            return;
        }

        string statusLabel;
        Color statusColor;
        if (ChallengeManager.Instance.IsFailed)
        {
            statusLabel = "Échoué";
            statusColor = _challengeStatusFailedColor;
        }
        else if (ChallengeManager.Instance.IsCurrentlySucceeding())
        {
            statusLabel = "Réussi";
            statusColor = _challengeStatusSuccessColor;
        }
        else
        {
            statusLabel = "En cours";
            statusColor = _challengeStatusNormalColor;
        }

        string colorHex = ColorUtility.ToHtmlStringRGB(statusColor);
        _challengeStatusText.text = $"Statut\n<color=#{colorHex}>{statusLabel}</color>";
    }

    // AJOUTE (2026-09-15) - decale le separateur vertical ET la colonne Statut
    // pour qu'ils suivent la fin REELLE du texte affiche (retour utilisateur),
    // plutot que de rester a une position fixe quelle que soit la longueur de
    // la description. ChallengeInfoText, lui, ne bouge pas (position/taille
    // figees dans l'Inspector - voir plus haut) : seule sa police peut varier
    // (Auto Size natif de TMP), donc la largeur reellement occupee varie d'un
    // defi a l'autre meme si la boite qui le contient ne change pas.
    // ForceMeshUpdate() avant de mesurer : sans ca, GetPreferredValues()
    // utiliserait encore la taille de police d'AVANT le changement de texte
    // qu'on vient de faire (TMP ne recalcule qu'au prochain passage de rendu,
    // pas de facon synchrone des qu'on assigne .text).
    private void PositionSeparatorAndStatus()
    {
        if (_challengeInfoText == null || _challengeSeparator == null || _challengeStatusText == null) return;

        _challengeInfoText.ForceMeshUpdate();

        RectTransform textRt = _challengeInfoText.rectTransform;
        float widest = 0f;
        foreach (string line in _challengeInfoText.text.Split('\n'))
        {
            Vector2 pref = _challengeInfoText.GetPreferredValues(line, 0f, 0f);
            if (pref.x > widest) widest = pref.x;
        }

        // Pivot.x=0 sur ChallengeInfoText : anchoredPosition.x EST deja son
        // bord gauche.
        float textRight = textRt.anchoredPosition.x + widest;

        RectTransform sepRt = _challengeSeparator.rectTransform;
        RectTransform statusRt = _challengeStatusText.rectTransform;
        RectTransform block = textRt.parent as RectTransform;
        float blockWidth = block != null ? block.rect.width : 0f;

        // Separateur : pivot 0.5/ancrage point a gauche du bloc -
        // anchoredPosition.x EST le centre de sa propre barre.
        float sepCenterX = textRight + _challengeTextToSeparatorGap + sepRt.sizeDelta.x / 2f;
        sepRt.anchoredPosition = new Vector2(sepCenterX, sepRt.anchoredPosition.y);

        // Statut : pivot.x=1/ancrage a DROITE du bloc - anchoredPosition.x est
        // mesure depuis le bord droit du bloc, d'ou la conversion via blockWidth.
        float statusLeft = sepCenterX + sepRt.sizeDelta.x / 2f + _challengeSeparatorToStatusGap;
        float statusRight = statusLeft + statusRt.sizeDelta.x;
        statusRt.anchoredPosition = new Vector2(statusRight - blockWidth, statusRt.anchoredPosition.y);
    }

    private Color GetDifficultyColor(ChallengeDifficulty difficulty)
    {
        switch (difficulty)
        {
            case ChallengeDifficulty.Easy: return _difficultyEasyColor;
            case ChallengeDifficulty.Medium: return _difficultyMediumColor;
            case ChallengeDifficulty.Hard: return _difficultyHardColor;
            default: return Color.white;
        }
    }

    // MODIFIE (2026-09-13) - tout en majuscules (retour utilisateur), le reste
    // de la ligne ("Fortune : Niv. ...") garde sa casse normale.
    private string GetDifficultyLabel(ChallengeDifficulty difficulty)
    {
        switch (difficulty)
        {
            case ChallengeDifficulty.Easy: return "FACILE";
            case ChallengeDifficulty.Medium: return "MOYEN";
            case ChallengeDifficulty.Hard: return "DIFFICILE";
            default: return "";
        }
    }

    // ------------------------------------------------------------------
    // Animation d'ouverture
    // ------------------------------------------------------------------

    private IEnumerator PlayOpenAnimation()
    {
        // --- Etat de depart : tout invisible avant que l'animation ne commence ---

        if (_dimBackground != null)
        {
            Color c = _dimBackground.color;
            c.a = 0f;
            _dimBackground.color = c;
        }

        if (_mainFrameCanvasGroup != null)
        {
            _mainFrameCanvasGroup.alpha = 0f;
            _mainFrameCanvasGroup.interactable = false;
            _mainFrameCanvasGroup.blocksRaycasts = false;
        }

        if (_mainFrame != null)
            _mainFrame.localScale = Vector3.one * _frameStartScale;

        // --- Phase 1 : fondu du voile noir + fondu/pop du MainFrame, en parallele ---

        float elapsed = 0f;
        float phase1Duration = Mathf.Max(_dimFadeDuration, _frameFadeDuration);

        while (elapsed < phase1Duration)
        {
            elapsed += Time.unscaledDeltaTime;

            if (_dimBackground != null)
            {
                float dimT = Mathf.Clamp01(elapsed / _dimFadeDuration);
                Color c = _dimBackground.color;
                c.a = Mathf.Lerp(0f, _dimTargetAlpha255 / 255f, dimT);
                _dimBackground.color = c;
            }

            if (_mainFrameCanvasGroup != null && _mainFrame != null)
            {
                float frameT = Mathf.Clamp01(elapsed / _frameFadeDuration);
                // Ease-out simple (1 - (1-t)^2) : demarre vite, ralentit en approchant
                // de la valeur finale - plus fluide qu'une interpolation lineaire brute.
                float eased = 1f - (1f - frameT) * (1f - frameT);
                _mainFrameCanvasGroup.alpha = eased;
                _mainFrame.localScale = Vector3.one * Mathf.Lerp(_frameStartScale, 1f, eased);
            }

            yield return null;
        }

        if (_dimBackground != null)
        {
            Color c = _dimBackground.color;
            c.a = _dimTargetAlpha255 / 255f;
            _dimBackground.color = c;
        }

        if (_mainFrameCanvasGroup != null)
        {
            _mainFrameCanvasGroup.alpha = 1f;
            _mainFrameCanvasGroup.interactable = true;
            _mainFrameCanvasGroup.blocksRaycasts = true;
        }

        if (_mainFrame != null)
            _mainFrame.localScale = Vector3.one;

        // --- Phase 2 : apparition en cascade des slots de la grille ---

        for (int i = 0; i < _spawnedSlots.Count; i++)
        {
            if (_spawnedSlots[i] == null) continue;

            // Au-dela de _maxStaggeredSlots, on ne rajoute plus de delai
            // supplementaire - tous les slots restants demarrent leur fondu au meme
            // moment que le dernier stagger applique, pour ne pas faire attendre le
            // joueur une eternite si son build a 30+ upgrades.
            int staggerIndex = Mathf.Min(i, _maxStaggeredSlots);
            StartCoroutine(FadeInSlot(_spawnedSlots[i], staggerIndex * _slotStagger));
        }

        _openAnimCoroutine = null;
    }

    private IEnumerator FadeInSlot(GameObject slot, float delay)
    {
        if (delay > 0f)
            yield return new WaitForSecondsRealtime(delay);

        if (slot == null) yield break;

        CanvasGroup cg = slot.GetComponent<CanvasGroup>();
        if (cg == null) yield break;

        float elapsed = 0f;
        while (elapsed < _slotFadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            cg.alpha = Mathf.Clamp01(elapsed / _slotFadeDuration);
            yield return null;
        }

        cg.alpha = 1f;
    }
}