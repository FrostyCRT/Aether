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
    // AJOUTE - meme principe que _progressionRowOffsetWithUnlockDot/WithoutUnlockDot
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

        // MODIFIE - suit desormais ObtainedOrder (ordre chronologique reel de pick)
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

        // MODIFIE - requiresUnlock calcule une seule fois, reutilise a la fois pour
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

    // AJOUTE - va chercher les valeurs directement sur GameManager (RunTimer,
    // KillCount, deja publics) et MetaProgressionManager (RunGold) a chaque
    // ouverture du menu pause. Aucun cablage externe necessaire : PauseMenuUI se
    // sert lui-meme, plutot que d'attendre qu'un autre script lui pousse les
    // valeurs. Si MetaProgressionManager.RunGold n'existe pas exactement sous ce
    // nom/cette signature chez toi, ce sera la seule ligne a corriger ici.
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