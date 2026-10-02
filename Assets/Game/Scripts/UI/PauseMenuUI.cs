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

    // MODIFIE (2026-09-27) - les 4 parchemins colorés supprimés (retour utilisateur, problèmes de cadrage/lueur) :
    // un seul parchemin sobre pour toutes les cartes, la couleur par branche vit maintenant dans une lueur
    // dynamique DERRIÈRE la carte (voir GameUI.ConfigureBuildGridSlot/BuildCardGlow).
    [Header("Parchemin (sobre, identique pour toutes les cartes)")]
    [SerializeField] private Sprite _parchmentSober;

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

    // MODIFIE (2026-09-27) - la grille était une liste défilante des upgrades OBTENUES, dans leur ordre de pick.
    // Elle est passée par une grille 3x3 pleine hauteur avec ScrollRect (retour utilisateur : cartes trop petites),
    // puis - nouveau retour utilisateur - revenue sur un format COMPACT sans scrollbar ("j'ai jamais vu une barre
    // comme ça dans la pause pour voir des informations de jeu") : 2 rangées de 4 cases (8 au total), Soin exclu
    // ("pas informatif sur du build, c'est même pas un passif"). Utilise GameUI.PopulatePauseGrid (voir sa
    // description, GetPauseArsenalItems) - layout dédié, différent du 3x3 réutilisé tel quel par Victoire/Défaite.
    // MODIFIE (2026-09-27) - retour utilisateur : la barre de description fixe en bas prenait de la place utile aux
    // cartes ("je préfère que tu l'utilise pour agrandir les cartes") - supprimée, cet espace rendu au ScrollView.
    // La description s'affiche maintenant dans un petit panneau flottant qui apparaît À L'ENDROIT DU CLIC (voir
    // _descriptionTooltip/ShowUpgradeDescription), pas dans une zone fixe.
    [Header("Taille des tuiles (Pause : grille compacte 2x4, sans défilement)")]
    [SerializeField] private Vector2 _cellSize = new Vector2(350f, 265f);
    [SerializeField] private Vector2 _cellSpacing = new Vector2(16f, 16f);
    [SerializeField] private int _gridColumns = 4;

    // AJOUTE (2026-09-28, retour utilisateur : "les positions ne correspondent pas du tout") - la colonne de
    // pastilles (Dot1/2/3 + losange) DOIT rester calculée en code (le nombre de pastilles varie de 1 à 5 selon
    // l'arme, impossible à figer dans le prefab sans perdre le centrage automatique) - mais sa taille/son
    // espacement étaient des constantes codées en dur, invisibles et impossibles à ajuster sans redemander un
    // changement de code. Exposés ici : réglables directement dans l'Inspector, comme le reste.
    [Header("Pastilles (taille/espacement - la position reste calculée automatiquement, voir GameUI.ConfigureBuildGridSlot)")]
    [Tooltip("Taille (largeur/hauteur) d'une pastille quand il y en a 3 ou moins.")]
    [SerializeField] private float _dotSizeFew = 22f;
    [Tooltip("Taille d'une pastille quand il y en a plus de 3 (4 ou 5) - plus petite pour que tout tienne.")]
    [SerializeField] private float _dotSizeMany = 16f;
    [SerializeField] private float _dotSpacing = 10f;
    [SerializeField] private float _unlockDotSize = 26f;
    [Tooltip("Ecart entre le losange de déblocage et la première pastille.")]
    [SerializeField] private float _unlockDotGap = 10f;

    [Header("Description au clic (retour utilisateur : \"aucun moyen de savoir ce qu'une carte fait\")")]
    [Tooltip("Le petit panneau flottant lui-même (racine avec le fond) - masqué par défaut, positionné et activé au clic sur une carte.")]
    [SerializeField] private RectTransform _descriptionTooltip;
    [Tooltip("Le texte à l'intérieur du panneau - la description statique de l'upgrade (UpgradeData.description).")]
    [SerializeField] private TextMeshProUGUI _descriptionText;
    [SerializeField] private float _tooltipFadeInDuration = 0.15f;
    [SerializeField] private float _tooltipFadeOutDuration = 0.2f;
    [SerializeField] private float _tooltipHoverDismissDelay = 1f;

    private CanvasGroup _tooltipCanvasGroup;
    private UpgradeData _activeTooltipUpgrade;
    private Coroutine _tooltipFadeCoroutine;
    private Coroutine _tooltipHoverDismissCoroutine;

    private void PopulateGrid()
    {
        if (GameUI.Instance == null || _gridContent == null || _upgradeSlotPrefab == null)
            return;

        GameUI.Instance.PopulatePauseGrid(_gridContent, _upgradeSlotPrefab, _spawnedSlots, _cellSize, _cellSpacing, _gridColumns,
            _parchmentSober, _dotColorEmpty, _dotColorFilled, _dotColorMax, ShowUpgradeDescription, OnCardHoverEnter, OnCardHoverExit,
            _dotSizeFew, _dotSizeMany, _dotSpacing, _unlockDotSize, _unlockDotGap);

        // Le panneau de description repart toujours masqué à l'ouverture du menu (pas de description périmée d'une
        // précédente ouverture affichée avant même d'avoir cliqué quoi que ce soit) - coupe aussi net toute
        // animation de fondu et tout minuteur de fermeture en cours (retour utilisateur : "attention aux bugs si
        // on spam les clics" - une réouverture du menu ne doit jamais laisser un coroutine de l'ouverture
        // précédente continuer à tourner en arrière-plan).
        StopTooltipCoroutines();
        _activeTooltipUpgrade = null;
        if (_descriptionTooltip != null)
        {
            _descriptionTooltip.gameObject.SetActive(false);
            if (_tooltipCanvasGroup == null) _tooltipCanvasGroup = _descriptionTooltip.GetComponent<CanvasGroup>();
            if (_tooltipCanvasGroup == null) _tooltipCanvasGroup = _descriptionTooltip.gameObject.AddComponent<CanvasGroup>();
            _tooltipCanvasGroup.alpha = 0f;
        }

        // Etat de depart pour l'animation en cascade : invisible tant que PlayOpenAnimation() ne l'a pas fait
        // apparaitre. Ajoute un CanvasGroup a chaque slot si le prefab n'en a pas deja un.
        foreach (GameObject slotGO in _spawnedSlots)
        {
            if (slotGO == null) continue;
            CanvasGroup cg = slotGO.GetComponent<CanvasGroup>();
            if (cg == null) cg = slotGO.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
        }
    }

    private void StopTooltipCoroutines()
    {
        if (_tooltipFadeCoroutine != null) { StopCoroutine(_tooltipFadeCoroutine); _tooltipFadeCoroutine = null; }
        if (_tooltipHoverDismissCoroutine != null) { StopCoroutine(_tooltipHoverDismissCoroutine); _tooltipHoverDismissCoroutine = null; }
    }

    // MODIFIE (2026-09-27) - callback de clic passé à GameUI.PopulatePauseGrid : affiche la description STATIQUE de
    // l'upgrade (UpgradeData.description, ex: "FUSION — Aura + Orbitaux : ..."), pas GetDynamicDescription() (qui
    // décrit le PROCHAIN palier et n'a donc plus de sens une fois une carte déjà maxée). Positionne le petit
    // panneau À L'ENDROIT DU CLIC (retour utilisateur) - converti en coordonnées locales de MainFrame (Canvas en
    // Screen Space Overlay, camera=null) via Input.mousePosition (input legacy, déjà utilisé partout ailleurs dans
    // le projet - voir GameInput.cs). Léger décalage bas-droite pour que le curseur ne masque pas le texte, puis
    // bloqué dans les bords de MainFrame pour ne jamais déborder de l'écran.
    private static readonly Vector2 TooltipCursorOffset = new Vector2(18f, -18f);

    // Marge intérieure du panneau (doit correspondre aux offsetMin/Max du texte à l'intérieur, réglés dans la
    // scène : 16/12 de chaque côté) et largeur de texte max avant retour à la ligne, pour qu'une description
    // longue s'enroule au lieu de produire un panneau démesurément large (retour utilisateur : "le panel doit être
    // de la taille du texte").
    private static readonly Vector2 TooltipPadding = new Vector2(32f, 24f);
    private const float TooltipMaxTextWidth = 380f;

    private void ShowUpgradeDescription(UpgradeData upgrade)
    {
        if (_descriptionTooltip == null || _descriptionText == null || upgrade == null) return;
        if (string.IsNullOrEmpty(upgrade.description)) return;

        // Un nouveau clic (même sur une autre carte pendant qu'un fondu de sortie était en cours - retour
        // utilisateur : "attention aux bugs si on spam les clics") annule tout fondu/minuteur en cours AVANT de
        // toucher au texte ou à la taille.
        StopTooltipCoroutines();

        // CORRIGE (2026-09-27, retour utilisateur : "au premier clique... une lettre par ligne, en 1 colonne") -
        // le panneau était activé APRÈS avoir déjà changé son texte : tant que le GameObject est inactif dans la
        // hiérarchie, TMP met la mise à jour du texte en attente au lieu de la traiter tout de suite, et la
        // reprend au réveil avec un état de mise en page pas encore à jour (largeur pas encore fiable) - d'où le
        // texte retourné à la ligne sur presque chaque caractère la toute première fois. Activer le GameObject
        // D'ABORD, puis changer le texte et forcer la reconstruction du maillage, règle ça définitivement.
        _descriptionTooltip.gameObject.SetActive(true);
        _descriptionText.text = upgrade.description;
        _descriptionText.ForceMeshUpdate();
        GameUI.ApplyReadableOutline(_descriptionText);

        // Taille du panneau = taille du texte (+ marge intérieure) - plus de format fixe, un texte court donne un
        // petit panneau, un texte long s'enroule à TooltipMaxTextWidth puis grandit en hauteur.
        Vector2 preferred = _descriptionText.GetPreferredValues(upgrade.description, TooltipMaxTextWidth, 0f);
        _descriptionTooltip.sizeDelta = new Vector2(Mathf.Min(preferred.x, TooltipMaxTextWidth) + TooltipPadding.x, preferred.y + TooltipPadding.y);

        RectTransform parentRt = _descriptionTooltip.parent as RectTransform;
        if (parentRt != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRt, Input.mousePosition, null, out Vector2 localPoint))
        {
            Vector2 pos = localPoint + TooltipCursorOffset;

            Vector2 halfParent = parentRt.rect.size * 0.5f;
            // Le pivot du panneau est en haut-gauche (0,1) : il s'étend vers la droite et le bas depuis 'pos'.
            pos.x = Mathf.Clamp(pos.x, -halfParent.x, halfParent.x - _descriptionTooltip.sizeDelta.x);
            pos.y = Mathf.Clamp(pos.y, -halfParent.y + _descriptionTooltip.sizeDelta.y, halfParent.y);

            _descriptionTooltip.anchoredPosition = pos;
        }

        _activeTooltipUpgrade = upgrade;
        _tooltipFadeCoroutine = StartCoroutine(FadeTooltip(1f, _tooltipFadeInDuration, false));
        _tooltipJustUpdatedThisFrame = true;
    }

    // AJOUTE (2026-09-27, retour utilisateur : "si au survol on enlève la souris de la carte, le panel disparait
    // en fondu au bout de 1 sec") - ne démarre le minuteur QUE si la carte quittée est celle dont la description
    // est actuellement affichée (survoler puis quitter une AUTRE carte, sans jamais l'avoir cliquée, ne doit rien
    // fermer). Revenir sur cette même carte avant la fin du délai annule la fermeture.
    private void OnCardHoverExit(UpgradeData upgrade)
    {
        if (upgrade != _activeTooltipUpgrade || _descriptionTooltip == null || !_descriptionTooltip.gameObject.activeSelf) return;
        if (_tooltipHoverDismissCoroutine != null) StopCoroutine(_tooltipHoverDismissCoroutine);
        _tooltipHoverDismissCoroutine = StartCoroutine(DismissTooltipAfterDelay());
    }

    private void OnCardHoverEnter(UpgradeData upgrade)
    {
        if (upgrade != _activeTooltipUpgrade || _tooltipHoverDismissCoroutine == null) return;
        StopCoroutine(_tooltipHoverDismissCoroutine);
        _tooltipHoverDismissCoroutine = null;
    }

    private IEnumerator DismissTooltipAfterDelay()
    {
        yield return new WaitForSecondsRealtime(_tooltipHoverDismissDelay);
        _tooltipHoverDismissCoroutine = null;
        HideTooltip();
    }

    private void HideTooltip()
    {
        _activeTooltipUpgrade = null;
        if (_tooltipFadeCoroutine != null) StopCoroutine(_tooltipFadeCoroutine);
        _tooltipFadeCoroutine = StartCoroutine(FadeTooltip(0f, _tooltipFadeOutDuration, true));
    }

    // Fondu générique (temps réel, indépendant de Time.timeScale=0 pendant la pause - même principe que
    // PlayOpenAnimation ci-dessous). deactivateAtEnd : désactive le GameObject une fois à alpha 0 (fermeture),
    // jamais lors d'une ouverture (alpha visé = 1).
    private IEnumerator FadeTooltip(float target, float duration, bool deactivateAtEnd)
    {
        if (_tooltipCanvasGroup == null) _tooltipCanvasGroup = _descriptionTooltip.GetComponent<CanvasGroup>();
        float start = _tooltipCanvasGroup.alpha;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            _tooltipCanvasGroup.alpha = Mathf.Lerp(start, target, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }
        _tooltipCanvasGroup.alpha = target;
        if (deactivateAtEnd && target <= 0f)
            _descriptionTooltip.gameObject.SetActive(false);
        _tooltipFadeCoroutine = null;
    }

    // AJOUTE (2026-09-27, retour utilisateur : "si on clique hors du panel, le panel doit disparaître") -
    // LateUpdate (pas Update) : l'EventSystem traite les clics sur les boutons des cartes pendant sa propre
    // Update(), donc à ce stade ShowUpgradeDescription() a déjà pu s'exécuter CETTE MÊME frame si une carte vient
    // d'être cliquée - _tooltipJustUpdatedThisFrame évite de refermer immédiatement le panneau qu'on vient tout
    // juste d'afficher/repositionner sur ce même clic.
    private bool _tooltipJustUpdatedThisFrame = false;

    private void LateUpdate()
    {
        if (_descriptionTooltip == null || !_descriptionTooltip.gameObject.activeSelf)
        {
            _tooltipJustUpdatedThisFrame = false;
            return;
        }

        if (Input.GetMouseButtonDown(0) && !_tooltipJustUpdatedThisFrame
            && !RectTransformUtility.RectangleContainsScreenPoint(_descriptionTooltip, Input.mousePosition, null))
        {
            HideTooltip();
        }

        _tooltipJustUpdatedThisFrame = false;
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
            // Les défis n'existent qu'en Classique (voir ChallengeManager.Start).
            _challengeInfoText.text = GameModes.CountsForProgression ? "" : "Pas de défi dans ce mode.";
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