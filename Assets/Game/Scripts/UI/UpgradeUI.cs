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

    // AJOUTE (2026-10-11) - positions/réglages d'ORIGINE de chaque carte, mémorisés une fois au démarrage
    // (comme _titleDefaultY ci-dessus) plutôt que redupliqués en dur dans DisplayUpgrades : une carte normale
    // retrouve EXACTEMENT ce qu'elle avait dans la scène, même si ces valeurs sont retouchées plus tard dans
    // l'Inspector - seule la carte de FUSION s'écarte de cette référence, via des constantes dédiées.
    // AJOUTÉ (2026-10-25) - carte de fusion : bloc titre (3 lignes, ~100 px) / icône (215) / description (3 lignes au plus,
    // ~100 px) séparés de 28 px, centré sur le parchemin : de +235 à -235. Boîtes alignées en haut (titre, description).
    // MODIFIÉ (2026-10-26, retour utilisateur : "remonte en Y les titres, les icônes et les descriptions de seulement
    // quelques pixels, les icônes doivent être au même emplacement en Y entre les 2 types de cartes") - titres et
    // descriptions remontés de CardRaiseY (fusion et normale) ; icône à CardIconY sur les deux types (avant : -3 en
    // fusion, -9 en normale, valeur de la scène).
    private const float CardRaiseY = 8f;
    private const float CardIconY = 0f;
    private const float FusionNameY = 185f + CardRaiseY;          // boîte 100 px alignée en haut
    private const float FusionDescHeight = 110f;
    private const float FusionDescY = -138f - FusionDescHeight / 2f + CardRaiseY;
    private const string FusionSourcesHex = "7A3B1C";                  // GameUI.ParchmentInkColorLight

    // AJOUTÉ (2026-10-26, retour utilisateur : "pour les compétences classiques (tout sauf fusion), la description tienne
    // sur 2 lignes") - carte normale : police réduite juste ce qu'il faut pour 2 lignes (voir TextBalancer.FitToLines) au
    // lieu de l'auto-size, qui laissait 3 lignes.
    // MODIFIÉ (2026-10-26, retour utilisateur avec capture : "rétrécis un peu la longueur, ça va un peu trop sur les
    // côtés") - même largeur de 350 px pour les deux types de carte (380 avant). Mesuré dans les 11 langues à 350 px :
    // classiques en 2 lignes à 19 ou plus (22,5 en français).
    // MODIFIÉ (2026-10-26, retour utilisateur : "4 lignes c'est trop") - fusion : 3 lignes au plus. Les descriptions
    // trop longues ont été raccourcies (toutes tiennent en 3 lignes à la taille 26) ; FitToLines ne sert que de
    // filet de sécurité (texte futur trop long : police légèrement réduite plutôt qu'une 4e ligne).
    private const float DescWidth = 350f;
    private const int NormalDescMaxLines = 2;
    private const float NormalDescMaxSize = 28f;
    private const int FusionDescMaxLines = 3;
    private const float FusionDescMaxSize = 26f;
    private const float DescMinSize = 18f;

    // MODIFIÉ (2026-10-26, retour utilisateur : "je veux que les caractères soient de la même taille pour chaque cartes. Je
    // veux aussi que le format soit en centré, et on oublie les même caratères par ligne", compétences de base seulement)
    // - centré sur toute la largeur, sans équilibrage des lignes ; une seule taille pour toutes les cartes classiques du
    // tirage (la plus petite des tailles qui tiennent en 2 lignes, voir UniformNormalDescriptionSize). Pendant la roulette,
    // les fausses descriptions prennent cette même taille (réduite seulement si l'une dépasserait 2 lignes).
    private float _normalDescSize = NormalDescMaxSize;

    private static void FitNormalDescription(TextMeshProUGUI t, float maxSize = NormalDescMaxSize)
    {
        t.horizontalAlignment = HorizontalAlignmentOptions.Center;
        TextBalancer.FitToLines(t, NormalDescMaxLines, maxSize, DescMinSize, balance: false);
    }

    private void UniformNormalDescriptionSize(List<UpgradeData> upgrades)
    {
        _normalDescSize = NormalDescMaxSize;
        for (int i = 0; i < _cards.Length && i < upgrades.Count; i++)
            if (upgrades[i].upgradeType != UpgradeType.Fusion && _cards[i].descriptionText != null)
                _normalDescSize = Mathf.Min(_normalDescSize, _cards[i].descriptionText.fontSize);
        for (int i = 0; i < _cards.Length && i < upgrades.Count; i++)
        {
            if (upgrades[i].upgradeType == UpgradeType.Fusion || _cards[i].descriptionText == null) continue;
            _cards[i].descriptionText.fontSize = _normalDescSize;
            _cards[i].descriptionText.ForceMeshUpdate(true);
        }
    }

    private float[] _defaultNameY;
    private Vector2[] _defaultIconSize;
    private float[] _defaultDescY;
    private Vector2[] _defaultDescSize;

    // AJOUTÉ (2026-10-01) - retour utilisateur : "quand le level up panel arrive, une transition rapide pour
    // afficher les cartes, pas juste un écran qui spawn comme ça, fluide et dynamique". Le fond fait un court
    // fondu, le titre "NIVEAU SUPÉRIEUR !" (et le bandeau de fusion) apparaissent en "pop" avec un petit
    // dépassement élastique, puis les cartes montent depuis le bas en cascade (gauche -> droite), chacune avec un
    // fondu et un léger rebond d'échelle. Tout tourne en temps réel (unscaled) : le jeu est en pause pendant le choix.
    // MODIFIÉ (2026-10-20, chantier satisfaction - retour utilisateur : "quand arrive le level up panel, les 3
    // cartes s'affichent d'un coup, ça montre pas que c'est un vrai pas en avant") - la cascade existait déjà
    // (gauche -> droite, voir PlayIntro) mais son rythme (0,07s entre cartes, 0,3s par carte) était trop rapide
    // pour vraiment se LIRE comme un enchaînement délibéré - à ce tempo, l'œil perçoit un "pop" quasi groupé plutôt
    // qu'un vrai tirage carte par carte. Timings doublés/allongés + glissement plus ample (plus de distance
    // parcourue = plus de poids perçu), et un flash d'impact doré ajouté juste avant le pop du titre pour marquer
    // franchement l'INSTANT où l'écran de récompense s'ouvre (voir _impactFlash ci-dessous).
    [Header("Animation d'arrivée du panel de level-up")]
    [Tooltip("Décoche pour revenir à l'apparition instantanée d'avant.")]
    [SerializeField] private bool _introEnabled = true;
    [Tooltip("Image plein écran (sibling 0 du panel) qui flashe en doré à l'ouverture, avant le pop du titre.")]
    [SerializeField] private UnityEngine.UI.Image _impactFlash;
    [Tooltip("Alpha maximal atteint par le flash d'impact.")]
    [SerializeField] private float _impactFlashPeakAlpha = 0.45f;
    [Tooltip("Durée de montée du flash d'impact (0 -> pic).")]
    [SerializeField] private float _impactFlashInDuration = 0.08f;
    [Tooltip("Durée de retombée du flash d'impact (pic -> 0).")]
    [SerializeField] private float _impactFlashOutDuration = 0.35f;
    [Tooltip("Durée du fondu du fond/panneau entier.")]
    [SerializeField] private float _panelFadeDuration = 0.12f;
    [Tooltip("Durée du pop du titre \"NIVEAU SUPÉRIEUR !\" (et du bandeau de fusion).")]
    [SerializeField] private float _titlePopDuration = 0.32f;
    [Tooltip("Échelle de départ du titre avant son pop (1 = pas de pop).")]
    [SerializeField] private float _titleStartScale = 0.5f;
    [Tooltip("Délai avant l'apparition de la 1re carte.")]
    [SerializeField] private float _cardFirstDelay = 0.2f;
    // MODIFIÉ (2026-10-20, 6e retour : "le délai est légèrement trop long, met à 0,35") - 0,60s (5e essai) encore
    // un peu trop long. Valeur exacte demandée cette fois. Chaque carte (et sa roulette, voir _iconSpin* -> le
    // défilement de TOUTE la carte plus bas) suit son PROPRE délai : la 3e ne s'arrête donc jamais en même temps
    // que la 1re.
    [Tooltip("Délai entre l'apparition de chaque carte successive (effet de cascade).")]
    [SerializeField] private float _cardStagger = 0.35f;
    [Tooltip("Durée de l'animation d'UNE carte (montée + fondu + rebond d'échelle).")]
    [SerializeField] private float _cardDuration = 0.42f;
    [Tooltip("Distance (en pixels du Canvas) depuis laquelle chaque carte monte vers sa place.")]
    [SerializeField] private float _cardSlideOffset = 220f;
    [Tooltip("Échelle de départ de chaque carte (1 = pas de variation d'échelle).")]
    [SerializeField] private float _cardStartScale = 0.7f;

    // AJOUTÉ (2026-10-20, retour utilisateur : "comme si c'était une roulette pour montrer que c'est des vraies
    // compétences choisies aléatoirement") - pendant que la carte glisse puis après qu'elle soit posée, son ICÔNE
    // défile rapidement à travers d'autres icônes d'upgrade avant de se figer sur la vraie - décélération "machine
    // à sous" (défilement rapide au début, de plus en plus espacé vers la fin). _iconSpinDuration dépasse
    // VOLONTAIREMENT _cardDuration : le cadre de la carte se pose d'abord, puis l'icône continue de tourner un
    // instant de plus avant de s'arrêter - exactement le rythme d'une vraie roulette (le boîtier s'arrête avant
    // que la roue à l'intérieur ne finisse de ralentir).
    // MODIFIÉ (2026-10-20, 2e retour : "il y a que les icônes qui tournent, mais pas les descriptions et les noms,
    // et la couleur des parchemins") - le défilement porte maintenant sur TOUTE la carte (icône + nom + description
    // + parchemin), pas que l'icône seule : à chaque étape, les 4 proviennent de la MÊME upgrade piochée au hasard
    // (jamais mélangés entre eux), pour rester cohérents visuellement à tout instant du défilement, exactement
    // comme si la carte montrait vraiment cette upgrade-là. Réservé aux cartes NORMALES (pas de fusion) : la mise
    // en page d'une carte de fusion (titre "FUSION" + nom en 2e ligne, icône/description inversées) est fixée
    // AVANT le défilement par DisplayUpgrades() - y faire défiler le nom/la description d'une upgrade normale
    // entraînerait un texte mal positionné pendant le défilement. Les fusions gardent donc le défilement d'ICÔNE
    // SEUL (comportement d'origine de ce chantier), les cartes normales défilent intégralement.
    [Header("\"Roulette\" sur chaque carte (icône + nom + description + parchemin)")]
    [Tooltip("Décoche pour revenir à l'icône/au texte figés directement sur la bonne upgrade (pas de défilement).")]
    [SerializeField] private bool _iconSpinEnabled = true;
    [Tooltip("Durée totale du défilement d'une carte, à partir du même délai que son entrée.")]
    [SerializeField] private float _iconSpinDuration = 0.8f;
    [Tooltip("Nombre d'upgrades différentes montrées pendant le défilement (avant la vraie, incluse dans le compte).")]
    [SerializeField] private int _iconSpinSteps = 12;
    [Tooltip("Léger pop d'échelle de l'icône à chaque changement pendant le défilement (1 = aucun pop).")]
    [SerializeField] private float _iconSpinTickPopScale = 1.08f;

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
    // AJOUTE (2026-10-07, retour utilisateur : "la carte doit être différente, genre un style Violet qui montre
    // que c'est une fusion") - jusqu'ici une carte de fusion reprenait le parchemin de la BRANCHE du personnage
    // propriétaire (rouge/vert/bleu, comme n'importe quelle autre upgrade) : rien ne la distinguait visuellement
    // d'une upgrade normale au premier coup d'œil. Prioritaire sur le parchemin de branche dans GetParchmentFor()
    // ci-dessous ; retombe sur le parchemin de branche tant qu'aucune image n'est assignée ici (voir le prompt
    // MetaIA fourni à l'utilisateur pour générer ce parchemin, même famille visuelle que ParcheminRouge/Vert/Bleu).
    [Tooltip("Parchemin des cartes de FUSION, toutes branches confondues (prioritaire sur le parchemin de personnage). Laisser vide pour garder l'ancien comportement (couleur de branche) en attendant l'asset.")]
    [SerializeField] private Sprite _parchmentFusion;

    [Header("Position de la ligne de pastilles (ProgressionRow)")]
    [Tooltip("Anchored Position X a appliquer quand le losange de deblocage est visible (ex: Orbital, Eclair, Boue).")]
    [SerializeField] private float _progressionRowOffsetWithUnlockDot = -85f;
    [Tooltip("Anchored Position X a appliquer quand il n'y a que les 3 dots de palier, sans losange (ex: Couteaux, Aura, Fireball, Orbe Rebondissant).")]
    [SerializeField] private float _progressionRowOffsetWithoutUnlockDot = 0f;

    [Header("Delai et animation apres un pick - s'applique a TOUS les picks")]
    [Tooltip("Duree totale entre le clic et la confirmation du pick (fermeture du panel). Vise 1 a 1.5s.")]
    [SerializeField] private float _pickConfirmDelay = 1.3f;

    // AJOUTÉ (2026-10-20, chantier satisfaction) - avant ce correctif, les 2 cartes NON choisies restaient figées,
    // intactes, jusqu'à ce que SelectUpgrade() désactive BRUTALEMENT tout le panel (SetActive(false) instantané,
    // aucune transition) - la fin du choix n'avait donc aucun "bouclage" avec son arrivée en fanfare (voir
    // PlayIntro). Les cartes non retenues s'effacent maintenant (fondu + rétrécissement + léger recul) pendant la
    // toute fin du délai de confirmation déjà existant (aucune durée ajoutée au total, juste réutilisée) - la carte
    // CHOISIE reste seule, pleine taille, à l'écran jusqu'à la fermeture : clairement LE choix qui a été fait.
    [Tooltip("Duree, prise sur la fin de _pickConfirmDelay, pendant laquelle les 2 cartes NON choisies s'effacent.")]
    [SerializeField] private float _exitFadeDuration = 0.3f;

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

        _defaultNameY = new float[_cards.Length];
        _defaultIconSize = new Vector2[_cards.Length];
        _defaultDescY = new float[_cards.Length];
        _defaultDescSize = new Vector2[_cards.Length];

        for (int i = 0; i < _cards.Length; i++)
        {
            if (_cards[i].nameText != null)
                _defaultNameY[i] = ((RectTransform)_cards[i].nameText.transform).anchoredPosition.y;
            if (_cards[i].iconImage != null)
            {
                RectTransform iconRt = (RectTransform)_cards[i].iconImage.transform;
                _defaultIconSize[i] = iconRt.sizeDelta;
            }
            if (_cards[i].descriptionText != null)
            {
                RectTransform descRt = (RectTransform)_cards[i].descriptionText.transform;
                _defaultDescY[i] = descRt.anchoredPosition.y;
                _defaultDescSize[i] = descRt.sizeDelta;
            }

            if (_cards[i].chooseButton != null)
            {
                int index = i;
                _cards[i].chooseButton.onClick.RemoveAllListeners();
                _cards[i].chooseButton.onClick.AddListener(() => OnCardSelected(index));
            }

            // CORRIGÉ (2026-10-07, retour utilisateur : "il faut trouver une autre couleur de texte qui vient sur
            // les parchemins, parce que c'est blanc sur beige, donc on ne voit pas trop", puis après un 1er essai
            // au contour seul : "c'est toujours aussi clair, difficile de lire de loin") - texte blanc remplacé
            // par la couleur "encre" partagée avec les cartes du Pause/Build (GameUI.ParchmentInkColor), plus
            // lisible qu'un simple contour sur fond clair. ApplyReadableOutline fournit maintenant un halo CLAIR
            // (plus un contour sombre, qui n'a plus de sens sur du texte déjà sombre).
            _cards[i].nameText.color = GameUI.ParchmentInkColor;
            _cards[i].descriptionText.color = GameUI.ParchmentInkColor;
            GameUI.ApplyReadableOutline(_cards[i].nameText);
            // MODIFIÉ (2026-10-25) - la description est en police de lecture (GameFonts) : le halo clair rongeait son trait
            // fin et la rendait pâle. Encre foncée sur parchemin clair, elle se lit sans halo.
        }
    }

    // AJOUTÉ (2026-10-01) - choisit le parchemin d'une carte d'après la branche de son upgrade. Si le parchemin du
    // personnage n'est pas encore assigné dans l'Inspector, retombe sur le doré (jamais de carte sans fond).
    private Sprite GetParchmentFor(UpgradeData upgrade)
    {
        if (upgrade.upgradeType == UpgradeType.Fusion && _parchmentFusion != null)
            return _parchmentFusion;

        Sprite branchSprite = null;
        switch (upgrade.Branch)
        {
            case UpgradeBranch.Aether: branchSprite = _parchmentAether; break;
            case UpgradeBranch.Kael: branchSprite = _parchmentKael; break;
            case UpgradeBranch.Lyra: branchSprite = _parchmentLyra; break;
        }
        return branchSprite != null ? branchSprite : _parchmentGold;
    }

    // AJOUTÉ (2026-10-20, 2e retour - roulette sur toute la carte) - mémorisé pour que PlayIntro() (appelée juste
    // après, voir StartIntro en fin de méthode) sache quelle UpgradeData complète correspond à chaque index de
    // carte, sans avoir à la relire sur les champs déjà affichés (texte/sprite) de la carte elle-même.
    private List<UpgradeData> _currentCardUpgrades;

    public void DisplayUpgrades(List<UpgradeData> upgrades)
    {
        if (upgrades == null) return;

        _currentCardUpgrades = upgrades;
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
                // MODIFIE (2026-10-07, retour utilisateur : "le nom de la fusion doit apparaitre sous le texte
                // Fusion de la carte") - le mot générique "FUSION" reste la 1re ligne (titre), le nom spécifique
                // (ex: "Terre Calcinée") apparaît maintenant en 2e ligne, plus petit - jusqu'ici réservé à la
                // description, jamais visible dans ce titre.
                // MODIFIE (2026-10-11, retour utilisateur : "augmente la taille des caractères des noms pour les
                // fusions") - 2e ligne (le nom spécifique) agrandie de 65% à 85% de la taille de base ("FUSION"
                // reste à 100%, toujours la ligne la plus visible).
                // MODIFIÉ (2026-10-25, retour utilisateur : "la description est bien trop longue") - 3e ligne : les deux
                // armes fusionnées (« Orbitaux + Foudre »), retirées de la description qui ne les répète plus.
                string sources = isFusionCard ? upgrades[i].FusionSourcesLabel() : "";
                _cards[i].nameText.text = isFusionCard
                    ? Loc.T("FUSION") + $"\n<size=85%>{upgrades[i].Name}</size>" + (sources.Length > 0 ? $"\n<size=62%><color=#{FusionSourcesHex}>{sources}</color></size>" : "")
                    : upgrades[i].Name;
                _cards[i].descriptionText.text = upgrades[i].GetDynamicDescription();

                // CORRIGE (2026-10-11, retour utilisateur avec capture : "le grand vide en bas", "même écart entre
                // le haut et le bas de vide", "toutes les cartes doivent avoir la même position en Y", "même écart
                // entre le nom, l'icône et la description") - 3 corrections précédentes (positions à vue) avaient
                // seulement déplacé le trou sans jamais regarder le bilan de TOUTE la hauteur de la carte (800px,
                // mesuré - PAS 670 comme supposé lors des passes précédentes) : la vraie cause du grand vide du
                // bas, c'est que la boîte de description (85px) ne réservait de la place que pour ~2 lignes alors
                // que `enableAutoSizing` la LAISSAIT RÉTRÉCIR pour tenir dedans au lieu de prendre la place
                // disponible plus bas sur la carte - le bas de la carte restait donc vide quel que soit le texte.
                // Valeurs recalculées (résolues sur les 800px réels) pour répartir titre/icône/description à
                // écarts ÉGAUX (52,5px) avec des marges haut/bas de carte ÉGALES (90px), FIXES pour les 3 cartes
                // (pas de centrage par carte selon son propre
                // texte - potentiellement illisible par calcul en temps réel, les mêmes 3 valeurs s'appliquent à
                // toute upgrade de fusion, confirmé par mesure en Play Mode avec le vrai texte le plus long des 6
                // fusions).
                // CORRIGE (2026-10-11, retour utilisateur avec capture : "les noms sont trop hauts") - le titre
                // (FUSION + nom) était trop près du bord haut de la carte. Marge haut/bas remontée de 90 à 110px
                // (le titre descend de 20px) en gardant l'invariant du dessus (marge haut = marge bas, écart
                // titre→icône = écart icône→description) : seul le COMPROMIS marge/écart change, la somme
                // marge+écart (142,5) reste fixe tant que l'icône (225) et la description (190) gardent leur
                // taille - donc l'icône elle-même ne bouge pas (sa position ne dépend que de cette somme fixe),
                // seuls le titre (descend) et la description (remonte) se rapprochent d'elle symétriquement.
                // CORRIGE (2026-10-11, retour utilisateur avec capture : "baisse un peu l'ensemble pour les
                // fusions, et baisse aussi un peu les textes de nom de fusion et fusion, sans décaler autre
                // chose") - 2 décalages distincts cette fois (l'utilisateur a explicitement dit de ne PAS tout
                // redécaler pour garder la symétrie marge/écart comme avant) : (1) tout le groupe descend de 15px
                // (icône et description), (2) le titre descend de 15px DE PLUS que le reste (30px au total), lui
                // seul - rapproche visuellement le titre "FUSION" du haut des cartes voisines (Dégâts+/Cadence+,
                // dont le titre ne bouge jamais) sans toucher à l'icône ni à la description une 2e fois.
                // MODIFIÉ (2026-10-25, retour utilisateur : "les éléments sont mal répartis sur la carte, tout est trop bas")
                // - avec les descriptions raccourcies (3 lignes au plus), titre (3 lignes), icône et description forment
                // un bloc centré verticalement sur le parchemin (800 px, centre à 0), voir les constantes Fusion*.
                if (isFusionCard)
                {
                    // CORRIGE (2026-10-11, retour utilisateur avec capture : "baisse tous les titres, pour la carte
                    // d'up normal et pour la fusion" / "baisse un peu en Y la description en bas" pour la fusion) -
                    // titre 210 -> 190 (même baisse de 20px que les cartes normales, scène, nameText.y 218 -> 198) ;
                    // description -210 -> -230 (encore 20px de plus, rien d'autre ne bouge).
                    if (_cards[i].nameText != null)
                        ((RectTransform)_cards[i].nameText.transform).anchoredPosition = new Vector2(((RectTransform)_cards[i].nameText.transform).anchoredPosition.x, FusionNameY);
                    // CORRIGE (2026-10-11, retour utilisateur avec capture : "l'icone pour la carte de fusion doit
                    // être de la même taille que celle des autres, et les icones de fusions et de upgrade doivent
                    // être au même niveau en Y") - 2 corrections : (1) Y alignée sur CardIconY, la MÊME
                    // valeur que la branche "else" ci-dessous utilise pour les cartes normales (abandon du Y fixe à
                    // 30 qui plaçait l'icône de fusion plus haut que celles des cartes voisines) ; (2) boîte élargie
                    // à 400x215 (au lieu de la boîte carrée 225x225 héritée de la scène, partagée avec les cartes
                    // normales) - 5 des 6 sprites de fusion sont carrés (rendu correct même en boîte carrée), mais
                    // "Orbes Jumeaux" (DoubleTir+OrbeRebondissante) est un sprite 1600x993 très large avec
                    // beaucoup de vide transparent en haut/bas : en boîte 225x225, preserveAspect le contraint par
                    // la largeur et le réduit à ~225x140, visiblement plus petit que les icônes carrées. Boîte
                    // 400x215 contraint TOUJOURS par la hauteur (215) pour les ratios larg/haut jusqu'à ~1.86
                    // (le plus large des 6 sprites est à 1.61) : chaque icône de fusion est donc rendue à hauteur
                    // fixe ~215, identique à la hauteur réelle des icônes normales (~214-215 avec leur boîte 225x225
                    // et leurs sprites quasi carrés) - tailles perçues cohérentes dans les 2 cas.
                    if (_cards[i].iconImage != null)
                    {
                        RectTransform fusionIconRt = (RectTransform)_cards[i].iconImage.transform;
                        fusionIconRt.anchoredPosition = new Vector2(fusionIconRt.anchoredPosition.x, CardIconY);
                        fusionIconRt.sizeDelta = new Vector2(400f, 215f);
                    }
                    if (_cards[i].descriptionText != null)
                    {
                        RectTransform descRt = (RectTransform)_cards[i].descriptionText.transform;
                        descRt.anchoredPosition = new Vector2(descRt.anchoredPosition.x, FusionDescY);
                        descRt.sizeDelta = new Vector2(DescWidth, FusionDescHeight);
                        // "pas obligé de rendre le texte plus petit quand c'est long, il faut que ça prenne de la
                        // place" - taille 26, réduite seulement au-delà de 3 lignes (voir FusionDescMaxLines).
                        TextBalancer.FitToLines(_cards[i].descriptionText, FusionDescMaxLines, FusionDescMaxSize, DescMinSize);
                    }
                }
                else
                {
                    if (_cards[i].nameText != null)
                        ((RectTransform)_cards[i].nameText.transform).anchoredPosition = new Vector2(((RectTransform)_cards[i].nameText.transform).anchoredPosition.x, _defaultNameY[i] + CardRaiseY);
                    if (_cards[i].iconImage != null)
                    {
                        RectTransform normalIconRt = (RectTransform)_cards[i].iconImage.transform;
                        normalIconRt.anchoredPosition = new Vector2(normalIconRt.anchoredPosition.x, CardIconY);
                        normalIconRt.sizeDelta = _defaultIconSize[i];
                    }
                    if (_cards[i].descriptionText != null)
                    {
                        RectTransform descRt = (RectTransform)_cards[i].descriptionText.transform;
                        descRt.anchoredPosition = new Vector2(descRt.anchoredPosition.x, _defaultDescY[i] + CardRaiseY);
                        descRt.sizeDelta = new Vector2(DescWidth, _defaultDescSize[i].y);
                        FitNormalDescription(_cards[i].descriptionText);
                    }
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

        UniformNormalDescriptionSize(upgrades);

        // AJOUTÉ (2026-10-01) - lance la transition d'arrivée maintenant que les cartes sont configurées et
        // activées (voir StartIntro : si le panel n'est pas encore actif, elle démarre dans OnEnable).
        StartIntro();
    }

    private void ResetCardScales(UpgradeCard card)
    {
        // AJOUTÉ (2026-10-20, chantier satisfaction) - le fondu de sortie d'un pick précédent (voir
        // AnimatePickThenConfirm/otherCardGroups) laisse la carte NON choisie à alpha<1 et une échelle rétrécie sur
        // son propre CanvasGroup/RectTransform - sans cette remise à zéro, PlayIntro() capturerait CETTE échelle
        // réduite comme "position finale" de la prochaine apparition (AddIntroTarget lit rt.localScale tel quel),
        // et la carte resterait visuellement plus petite pour toujours après son 1er fondu de sortie.
        GameObject cardRootGO = card.cardRoot != null ? card.cardRoot : (card.chooseButton != null ? card.chooseButton.gameObject : null);
        if (cardRootGO != null)
        {
            cardRootGO.transform.localScale = Vector3.one;
            CanvasGroup grp = cardRootGO.GetComponent<CanvasGroup>();
            if (grp != null) grp.alpha = 1f;
        }

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
        // MODIFIE (2026-10-11, retour utilisateur : "mets le système de dots pour Dégâts+ et Cadence+ comme dans
        // le pause panel, donc 5 dots") - avant, seules les upgrades à 2-3 paliers affichaient des pastilles
        // (Dégâts/Cadence/Soin, maxLevel=5, tombaient dans le compteur "xN" à la place). Plafond supprimé : même
        // principe que GameUI.ConfigureBuildGridSlot (Pause/Victoire/Défaite), `dotCount` plafonné à 5 pastilles
        // quel que soit maxLevel réel (jamais dépassé ici en pratique, aucune upgrade n'a plus de 5 paliers).
        // MODIFIÉ (2026-10-18, retour utilisateur : "enlève les dots qu'il y a sur les cartes de Heal [...] parce
        // que ces dots ne servent à rien") - Soin a bien _maxLevel=5 (pour autoriser jusqu'à 5 picks par run), mais
        // chaque pick est un effet instantané identique (restaure 50% des PV max) : il n'y a pas de "palier" qui
        // monte en puissance d'un pick à l'autre, contrairement à Dégâts+/Cadence+ où les pastilles montrent une
        // vraie progression. Les pastilles n'avaient donc aucune information utile à transmettre sur cette carte.
        // MODIFIÉ (2026-10-27) - pastilles remises sur Soin : chaque pick augmente aussi les PV max (voir
        // UpgradeData.HealMaxHealthBonus), il y a donc de nouveau une vraie progression d'un palier à l'autre.
        bool showDots = maxLevel > 1 && card.tierDotsContainer != null && card.tierDots != null;
        int dotCount = Mathf.Clamp(maxLevel, 1, 5);

        if (card.tierDotsContainer != null)
            card.tierDotsContainer.SetActive(showDots);

        // AJOUTE - compteur x1/x2/x3 : repli pour une upgrade à 1 seul palier sans pastille adaptée (aucun cas
        // réel actuellement, showDots couvre maintenant tout maxLevel > 1) - gardé si une future upgrade à cap
        // élevé mais sans sens de "palier" (ex: un buff purement additif) en avait besoin.
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

        // AJOUTE (2026-10-11) - taille/espacement des pastilles recalculés selon leur NOMBRE (comme
        // GameUI.ConfigureBuildGridSlot : plus petites à partir de 4, pour que les 5 tiennent dans la même
        // largeur de conteneur (225px) que les 3 pastilles d'origine, toujours centrées dedans - aucune carte ne
        // bouge, seul l'intérieur de la rangée s'adapte.
        // CORRIGE (2026-10-11, retour utilisateur : "grossis un peu les 5 dots") - 36 -> 42px. Le centrage se
        // base sur containerWidth (225) et pas sur le total occupé par les points : même plus large que les
        // 225px (42*5 + 6*4 = 234), la rangée reste symétrique autour du centre du conteneur (aucun clipping sur
        // ce conteneur, c'est un simple point d'ancrage).
        float dotSize = dotCount > 3 ? 42f : 50f;
        float dotSpacing = dotCount > 3 ? 6f : 8f;
        float containerWidth = card.tierDotsContainer != null ? ((RectTransform)card.tierDotsContainer.transform).sizeDelta.x : 225f;
        float totalSpan = dotCount * dotSize + Mathf.Max(0, dotCount - 1) * dotSpacing;
        float startX = (containerWidth - totalSpan) / 2f + dotSize / 2f;

        for (int d = 0; d < card.tierDots.Length; d++)
        {
            if (card.tierDots[d] == null) continue;

            bool dotExists = d < dotCount;
            card.tierDots[d].gameObject.SetActive(dotExists);
            if (!dotExists) continue;

            RectTransform dotRt = (RectTransform)card.tierDots[d].transform;
            dotRt.sizeDelta = new Vector2(dotSize, dotSize);
            dotRt.anchoredPosition = new Vector2(startX + d * (dotSize + dotSpacing), dotRt.anchoredPosition.y);

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

    // AJOUTÉ (2026-10-20, retour utilisateur : "si on appuis sur 1, 2, 3 alors que les 3 cartes n'ont pas encore
    // été dévoilé, ça poursuit quand même le jeu [...] il faut pouvoir cliquer/appuyer sur 1/2/3 que si les 3 sont
    // dévoilés") - les cartes sont actives (GameObject.activeSelf) dès DisplayUpgrades(), bien avant la fin de leur
    // animation d'entrée/roulette (PlayIntro, encore en fondu/défilement) : rien n'empêchait jusqu'ici un clic ou un
    // raccourci clavier de confirmer un choix pendant que son contenu affiché était encore une fausse upgrade de la
    // roulette. Vrai dès que PlayIntro() démarre, faux dès qu'elle se termine (normalement OU interrompue via
    // StopIntro) - vérifié dans OnCardSelected(), le point d'entrée COMMUN à la souris et au clavier.
    private bool _introPlaying = false;

    // MODIFIÉ (2026-10-20, 2e retour utilisateur - voir le commentaire sur _iconSpinEnabled plus haut) : chaque
    // étape du défilement porte maintenant une upgrade COMPLÈTE (icône + nom + description + parchemin), pas
    // qu'un sprite isolé - classe (pas struct) : lastStepShown/tickStartTime sont modifiés en place chaque frame
    // par la boucle de PlayIntro, une struct copiée dans la liste ne le permettrait pas proprement.
    private class IconSpinState
    {
        public Image iconImage;
        public TextMeshProUGUI nameText;       // null pour une carte de fusion (texte non concerné par le défilement)
        public TextMeshProUGUI descriptionText; // idem
        public Image backgroundImage;           // idem (parchemin)
        public UpgradeData[] sequence; // dernière entrée = la VRAIE upgrade de cette carte
        public float delay;
        public float duration;
        public Vector3 baseScale;
        public int lastStepShown = -1;
        public float tickStartTime = -1f;

        // AJOUTÉ (2026-10-20, 3e retour utilisateur : "les dots sont fix, elles doivent aussi bouger") - les
        // pastilles de palier restaient figées sur l'état de la VRAIE upgrade pendant tout le défilement, alors que
        // le reste de la carte (icône/nom/description/parchemin) cyclait déjà sur de fausses upgrades : incohérent,
        // ça trahissait laquelle des 3 était la vraie avant même la fin du tirage. cardIndex/card permettent de
        // rappeler UpdateTierDots (même logique que l'affichage normal, voir DisplayUpgrades) à CHAQUE étape du
        // défilement - y compris sur la toute dernière (la vraie upgrade), ce qui remet naturellement les pastilles
        // dans leur état correct en fin de tirage, sans code de restauration séparé. Jamais pour une carte de
        // fusion (spinDots=false), par cohérence avec nameText/descriptionText/backgroundImage = null ci-dessus.
        public int cardIndex;
        public UpgradeCard card;
        public bool spinDots;
    }

    private readonly List<IconSpinState> _iconSpinStates = new List<IconSpinState>();
    private List<UpgradeData> _spinUpgradePool;

    // Construit une fois (mis en cache) la liste de toutes les upgrades NORMALES (jamais de fusion - voir
    // commentaire sur _iconSpinEnabled) piochées pour le défilement - indépendant des 3 upgrades réellement
    // proposées ce tirage-ci (on peut très bien "tomber" par hasard sur l'une d'elles en défilant, comme une
    // vraie roulette).
    private List<UpgradeData> GetSpinUpgradePool()
    {
        if (_spinUpgradePool != null) return _spinUpgradePool;

        _spinUpgradePool = new List<UpgradeData>();
        if (LevelUpManager.Instance != null && LevelUpManager.Instance.AllUpgrades != null)
        {
            foreach (UpgradeData u in LevelUpManager.Instance.AllUpgrades)
                if (u != null && u.icon != null && u.upgradeType != UpgradeType.Fusion) _spinUpgradePool.Add(u);
        }
        return _spinUpgradePool;
    }

    private UpgradeData[] BuildSpinSequence(UpgradeData finalUpgrade, List<UpgradeData> pool)
    {
        int steps = Mathf.Max(2, _iconSpinSteps);
        UpgradeData[] seq = new UpgradeData[steps];

        if (pool == null || pool.Count == 0)
        {
            for (int i = 0; i < steps; i++) seq[i] = finalUpgrade;
            return seq;
        }

        for (int i = 0; i < steps - 1; i++)
            seq[i] = pool[Random.Range(0, pool.Count)];
        seq[steps - 1] = finalUpgrade;
        return seq;
    }

    // Applique UNE étape du défilement (icône + éventuellement nom/description/parchemin) sur une carte.
    private void ApplySpinStep(IconSpinState s, UpgradeData upgrade)
    {
        s.iconImage.sprite = upgrade.icon;
        if (s.nameText != null) s.nameText.text = upgrade.Name;
        if (s.descriptionText != null)
        {
            s.descriptionText.text = upgrade.GetDynamicDescription();
            FitNormalDescription(s.descriptionText, _normalDescSize);   // descriptionText n'est renseigné que pour les cartes normales
        }
        if (s.backgroundImage != null) s.backgroundImage.sprite = GetParchmentFor(upgrade);
        if (s.spinDots && s.card != null) UpdateTierDots(s.cardIndex, s.card, upgrade);
    }

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
        _introPlaying = true;
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
        _introPlaying = false;
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
        if (_impactFlash != null)
        {
            Color c = _impactFlash.color;
            c.a = 0f;
            _impactFlash.color = c;
        }

        // Fige chaque carte sur sa VRAIE upgrade (dernière entrée de sa séquence) à l'échelle normale - une
        // interruption en plein défilement (carte reprise/relancée) ne doit jamais laisser une upgrade aléatoire.
        // CORRIGÉ (2026-10-20, retour utilisateur : "si c'est une upgrade à 5 dots [...] les dots se centrent, au
        // début après la roulette de la carte, et quand toutes les cartes ont été révélé, ces 5 dots bougent
        // légèrement en X") - cette méthode est AUSSI appelée à la fin NORMALE de toute l'intro (voir la fin de
        // PlayIntro()), pas seulement sur une interruption - elle rappelait donc ApplySpinStep (et donc
        // UpdateTierDots, qui recalcule la position X des pastilles à partir de la largeur ACTUELLE du conteneur)
        // une 2e fois pour CHAQUE carte, y compris celles dont le défilement était déjà terminé depuis longtemps
        // (ex: la 1re carte, figée sur la vraie upgrade dès ~1s, se faisait recalculer une 2e fois quand la 3e
        // carte finissait la sienne ~1s plus tard) - la mise en page ayant très légèrement varié entre les deux
        // lectures (dernières cartes qui finissent de s'installer), le 2e calcul ne retombait pas pile sur le même
        // X que le 1er, d'où le petit saut observé. Ne retouche plus une carte déjà figée sur sa vraie upgrade.
        for (int i = 0; i < _iconSpinStates.Count; i++)
        {
            IconSpinState s = _iconSpinStates[i];
            if (s.iconImage == null) continue;
            int lastIndex = s.sequence.Length - 1;
            if (s.lastStepShown != lastIndex) ApplySpinStep(s, s.sequence[lastIndex]);
            s.iconImage.rectTransform.localScale = s.baseScale;
        }
        _iconSpinStates.Clear();
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
        _iconSpinStates.Clear();
        List<UpgradeData> spinPool = _iconSpinEnabled ? GetSpinUpgradePool() : null;
        int shown = 0;
        for (int i = 0; i < _cards.Length; i++)
        {
            GameObject target = _cards[i].cardRoot != null ? _cards[i].cardRoot : (_cards[i].chooseButton != null ? _cards[i].chooseButton.gameObject : null);
            if (target == null || !target.activeSelf) continue;

            float cardDelay = _cardFirstDelay + shown * _cardStagger;
            AddIntroTarget((RectTransform)target.transform, cardDelay, _cardDuration, _cardStartScale, _cardSlideOffset);

            // "Roulette" : CETTE carte défile sur d'autres upgrades avant de se figer sur la vraie (déjà affectée
            // par DisplayUpgrades() avant que StartIntro() ne soit appelée - _currentCardUpgrades[i] donne
            // l'UpgradeData complète correspondante, jamais recalculée). Fusion -> icône seule (voir le
            // commentaire sur _iconSpinEnabled) ; carte normale -> icône + nom + description + parchemin.
            UpgradeData finalUpgrade = _currentCardUpgrades != null && i < _currentCardUpgrades.Count ? _currentCardUpgrades[i] : null;
            if (_iconSpinEnabled && finalUpgrade != null && _cards[i].iconImage != null && spinPool != null && spinPool.Count > 0)
            {
                bool isFusionCard = finalUpgrade.upgradeType == UpgradeType.Fusion;
                _iconSpinStates.Add(new IconSpinState
                {
                    iconImage = _cards[i].iconImage,
                    nameText = isFusionCard ? null : _cards[i].nameText,
                    descriptionText = isFusionCard ? null : _cards[i].descriptionText,
                    backgroundImage = isFusionCard ? null : _cards[i].backgroundImage,
                    sequence = BuildSpinSequence(finalUpgrade, spinPool),
                    delay = cardDelay,
                    duration = _iconSpinDuration,
                    baseScale = _cards[i].iconImage.rectTransform.localScale,
                    cardIndex = i,
                    card = _cards[i],
                    spinDots = !isFusionCard
                });
            }

            shown++;
        }

        // Etat de départ appliqué IMMÉDIATEMENT (avant le premier yield, donc avant le moindre rendu) : jamais une
        // image des cartes déjà à leur place avant que l'animation ne commence.
        _panelGroup.alpha = 0f;
        if (_impactFlash != null)
        {
            Color c0 = _impactFlash.color;
            c0.a = 0f;
            _impactFlash.color = c0;
        }
        float flashTotalDuration = _impactFlashInDuration + _impactFlashOutDuration;
        float totalDuration = Mathf.Max(_panelFadeDuration, flashTotalDuration);
        for (int i = 0; i < _introTargets.Count; i++)
        {
            IntroTarget t = _introTargets[i];
            t.group.alpha = 0f;
            t.rt.localScale = t.baseScale * t.startScale;
            t.rt.anchoredPosition = t.basePos + new Vector2(0f, -t.slideOffset);
            totalDuration = Mathf.Max(totalDuration, t.delay + t.duration);
        }

        // Cartes en défilement : contenu de départ = 1re étape de la séquence (jamais la vraie upgrade visible
        // avant que son délai ne démarre), échelle normale tant que le défilement n'a pas commencé.
        for (int i = 0; i < _iconSpinStates.Count; i++)
        {
            IconSpinState s = _iconSpinStates[i];
            ApplySpinStep(s, s.sequence[0]);
            s.iconImage.rectTransform.localScale = s.baseScale;
            totalDuration = Mathf.Max(totalDuration, s.delay + s.duration);
        }

        float elapsed = 0f;
        while (elapsed < totalDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            _panelGroup.alpha = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, _panelFadeDuration));

            if (_impactFlash != null)
            {
                float flashAlpha = elapsed < _impactFlashInDuration
                    ? Mathf.Lerp(0f, _impactFlashPeakAlpha, elapsed / Mathf.Max(0.001f, _impactFlashInDuration))
                    : Mathf.Lerp(_impactFlashPeakAlpha, 0f, (elapsed - _impactFlashInDuration) / Mathf.Max(0.001f, _impactFlashOutDuration));
                Color fc = _impactFlash.color;
                fc.a = Mathf.Clamp01(flashAlpha);
                _impactFlash.color = fc;
            }

            for (int i = 0; i < _introTargets.Count; i++)
            {
                IntroTarget t = _introTargets[i];
                if (t.rt == null) continue;

                float p = Mathf.Clamp01((elapsed - t.delay) / t.duration);

                t.group.alpha = Mathf.Clamp01(p * 2f);
                t.rt.localScale = t.baseScale * Mathf.LerpUnclamped(t.startScale, 1f, EaseOutBack(p));
                t.rt.anchoredPosition = t.basePos + new Vector2(0f, Mathf.Lerp(-t.slideOffset, 0f, EaseOutCubic(p)));
            }

            // "Roulette" : avance le défilement de chaque icône. Les temps de changement d'icône suivent une
            // courbe d'accélération quadratique (switchTime(i) = duration * (i/(N-1))²) - les écarts entre 2
            // changements sont COURTS au début (défilement rapide) et S'ALLONGENT vers la fin (ralentissement),
            // exactement le rythme d'une roulette qui s'arrête. Le sprite ne change que lorsqu'on franchit une
            // nouvelle étape (pas à chaque frame), avec un petit pop d'échelle qui se résorbe tout seul ensuite.
            for (int i = 0; i < _iconSpinStates.Count; i++)
            {
                IconSpinState s = _iconSpinStates[i];
                if (s.iconImage == null) continue;

                float localElapsed = elapsed - s.delay;
                if (localElapsed < 0f) continue;

                int lastIndex = s.sequence.Length - 1;
                if (localElapsed >= s.duration)
                {
                    if (s.lastStepShown != lastIndex)
                    {
                        s.lastStepShown = lastIndex;
                        ApplySpinStep(s, s.sequence[lastIndex]);
                    }
                    s.iconImage.rectTransform.localScale = s.baseScale;
                    continue;
                }

                float tSqrt = Mathf.Sqrt(Mathf.Clamp01(localElapsed / s.duration));
                int stepIndex = Mathf.Clamp(Mathf.FloorToInt(tSqrt * lastIndex), 0, lastIndex);
                if (stepIndex != s.lastStepShown)
                {
                    s.lastStepShown = stepIndex;
                    ApplySpinStep(s, s.sequence[stepIndex]);
                    s.tickStartTime = localElapsed;
                }

                const float TickPopDecay = 0.08f;
                float sincePop = s.tickStartTime >= 0f ? localElapsed - s.tickStartTime : TickPopDecay;
                float popFactor = Mathf.Clamp01(1f - sincePop / TickPopDecay);
                s.iconImage.rectTransform.localScale = s.baseScale * Mathf.Lerp(1f, _iconSpinTickPopScale, popFactor);
            }

            yield return null;
        }

        _introRoutine = null;
        _introPlaying = false;
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
        if (_introPlaying) return;

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

        // --- Cartes NON choisies : CanvasGroup pour le fondu de sortie (voir boucle plus bas) ---
        List<CanvasGroup> otherCardGroups = new List<CanvasGroup>();
        for (int i = 0; i < _cards.Length; i++)
        {
            if (i == index) continue;
            GameObject otherTarget = _cards[i].cardRoot != null ? _cards[i].cardRoot : (_cards[i].chooseButton != null ? _cards[i].chooseButton.gameObject : null);
            if (otherTarget == null || !otherTarget.activeSelf) continue;

            CanvasGroup grp = otherTarget.GetComponent<CanvasGroup>();
            if (grp == null) grp = otherTarget.AddComponent<CanvasGroup>();
            grp.alpha = 1f;
            otherCardGroups.Add(grp);
        }

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

            // Fondu de sortie des cartes NON choisies, sur la toute fin du délai (voir _exitFadeDuration) - la
            // carte choisie, elle, reste pleine taille/opaque jusqu'à la fermeture du panel.
            if (otherCardGroups.Count > 0)
            {
                float exitStart = _pickConfirmDelay - _exitFadeDuration;
                float exitT = Mathf.Clamp01((elapsed - exitStart) / Mathf.Max(0.01f, _exitFadeDuration));
                float exitEase = EaseOutCubic(exitT);
                foreach (CanvasGroup grp in otherCardGroups)
                {
                    if (grp == null) continue;
                    grp.alpha = 1f - exitEase;
                    grp.transform.localScale = Vector3.one * Mathf.Lerp(1f, 0.85f, exitEase);
                }
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