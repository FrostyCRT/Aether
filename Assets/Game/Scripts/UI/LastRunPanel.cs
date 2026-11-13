using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// AJOUTÉ (2026-11-07, demande utilisateur : "mettre en haut à gauche dans le menu accueil, les gains de la dernière
// partie. un peu stylisé, avec les symboles correspondants, il faut que ce soit un minimum graphique") - carte « Dernière
// partie » de la page Accueil, construite par le code au lancement (même habillage que HomeCommandPanelFX : bois
// procédural + cadre bronze/or). Lit SaveData.lastRun, écrit par MetaProgressionManager.SaveRunResults :
//  - en-tête : titre + pastille du résultat (Victoire / Défaite / Abandon), puis personnage · mode · palier · durée ;
//  - deux gros montants avec leur icône (Or, Éclats) ;
//  - barre de répartition de l'or (ennemis, survie, boss, victoire, défi) et sa légende ;
//  - ligne de stats (ennemis vaincus, niveau, boss).
// Les montants « comptent » depuis 0 la première fois que la carte s'affiche après une partie (pas aux visites suivantes).
// Masquée tant qu'aucune partie n'a été terminée.
// AJOUTÉ (2026-11-07, demande utilisateur : "qu'on puisse réduire ce panel, et le faire réapparaître quand on veut [...] une
// petite barre horizontale bouton comme pour réduire une fenêtre d'un navigateur") - bouton « — » en haut à droite de la
// carte : elle se réduit en une petite barre (titre + or gagné + icône d'agrandissement), cliquable pour la rouvrir.
// L'état est retenu entre les sessions (PlayerPrefs, confort d'affichage).
public class LastRunPanel : MonoBehaviour
{
    [SerializeField] private TMP_FontAsset _titleFont;   // Bangers (titres, gros chiffres)
    [SerializeField] private Sprite _goldIcon;
    [SerializeField] private Sprite _eclatIcon;
    [SerializeField] private Vector2 _size = new Vector2(470f, 336f);
    [SerializeField] private Vector2 _offset = new Vector2(28f, -26f);   // depuis le coin haut-gauche de MenuPanel

    private static readonly Color WoodTop = new Color(0.310f, 0.205f, 0.115f, 1f);
    private static readonly Color WoodBottom = new Color(0.165f, 0.105f, 0.062f, 1f);
    private static readonly Color Trim = new Color(0.80f, 0.61f, 0.27f, 1f);
    private static readonly Color Parchment = new Color(0.96f, 0.90f, 0.78f, 1f);
    private static readonly Color Muted = new Color(0.86f, 0.78f, 0.64f, 0.85f);
    private static readonly Color GoldText = new Color(1f, 0.82f, 0.36f, 1f);
    private static readonly Color EclatText = new Color(0.62f, 0.86f, 1f, 1f);

    // Couleurs des sources d'or (barre + légende), dans l'ordre de LastRunSummary.
    private static readonly Color[] SourceColors =
    {
        new Color(0.91f, 0.45f, 0.29f, 1f),   // ennemis
        new Color(0.44f, 0.72f, 0.91f, 1f),   // survie
        new Color(0.69f, 0.48f, 0.91f, 1f),   // boss
        new Color(0.95f, 0.76f, 0.31f, 1f),   // victoire
        new Color(0.50f, 0.82f, 0.50f, 1f),   // défi
    };

    private static long _lastAnimatedRun;   // partie dont les montants ont déjà « compté » (une seule fois par partie)

    private RectTransform _root;
    private TextMeshProUGUI _title, _resultText, _subtitle, _goldValue, _eclatValue, _eclatNote, _stats;
    private Image _resultPill;
    private RectTransform _bar;
    private Image _barBack, _divider2;
    private readonly List<Image> _barSegments = new List<Image>();
    private readonly List<GameObject> _legendRows = new List<GameObject>();
    private readonly List<TextMeshProUGUI> _legendLabels = new List<TextMeshProUGUI>();
    private readonly List<TextMeshProUGUI> _legendValues = new List<TextMeshProUGUI>();
    private Coroutine _anim;

    // AJOUTÉ (2026-11-12, retour utilisateur : « voir la source des gains d'éclats dans Dernière Partie, avec un bouton ») -
    // le bouton sous les éclats fait basculer la barre et sa légende entre la répartition de l'or et celle des éclats ;
    // le montant qui n'est pas détaillé s'estompe. La prime de 1re victoire devient une ligne de la légende des éclats.
    private bool _showEclats;
    private RectTransform _sourceButton;
    private TextMeshProUGUI _sourceLabel;
    private Graphic[] _goldBlock, _eclatBlock;
    private static readonly Color[] EclatSourceColors =
    {
        new Color(0.44f, 0.72f, 0.91f, 1f),   // survie
        new Color(0.69f, 0.48f, 0.91f, 1f),   // boss
        new Color(0.95f, 0.76f, 0.31f, 1f),   // victoire
        new Color(0.00f, 0.93f, 0.89f, 1f),   // prime de 1re victoire
        new Color(0f, 0f, 0f, 0f),
    };

    // Réduction / agrandissement
    private const string CollapsedPref = "lastRunPanelCollapsed";
    private RectTransform _mini;
    private CanvasGroup _cardGroup, _miniGroup;
    private TextMeshProUGUI _miniTitle, _miniGold;
    private bool _collapsed;
    private Coroutine _toggleAnim;

    private void Awake()
    {
        try { _collapsed = PlayerPrefs.GetInt(CollapsedPref, 0) == 1; } catch { _collapsed = false; }
        Build();
        BuildMini();
    }

    private void OnEnable()
    {
        Loc.Changed += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        Loc.Changed -= Refresh;
        if (_anim != null) { StopCoroutine(_anim); _anim = null; }
        if (_toggleAnim != null) { StopCoroutine(_toggleAnim); _toggleAnim = null; ResetToggleVisuals(); }
    }

    // ── Construction ────────────────────────────────────────────────────────────────────────────────────────────────

    private void Build()
    {
        _root = new GameObject("LastRunCard", typeof(RectTransform)).GetComponent<RectTransform>();
        _root.SetParent(transform, false);
        _root.anchorMin = _root.anchorMax = new Vector2(0f, 1f);
        _root.pivot = new Vector2(0f, 1f);
        _root.anchoredPosition = _offset;
        _root.sizeDelta = _size;
        _cardGroup = _root.gameObject.AddComponent<CanvasGroup>();

        Image wood = NewImage("Wood", _root, MenuSprites.Wood(WoodTop, WoodBottom, 0.5f, 512, 384), Color.white);
        Stretch(wood.rectTransform, 0f);
        Image frame = NewImage("Frame", _root, HudSprites.Frame, Color.white);
        frame.type = Image.Type.Sliced;
        Stretch(frame.rectTransform, 0f);

        float pad = 22f, w = _size.x - pad * 2f, y = -18f;

        _title = NewText("Title", _root, _titleFont, 30f, Trim, TextAlignmentOptions.MidlineLeft);
        Place(_title.rectTransform, pad, y + 5f, w - 190f, 36f);   // +5 : Bangers s'assoit bas, centré en Y sur la pastille

        // Bouton « réduire » (barre horizontale, comme une fenêtre de navigateur), coin haut-droit.
        RectTransform minimize = NewButton("Minimize", _root, () => SetCollapsed(true));
        Place(minimize, _size.x - pad - 32f, y + 1f, 32f, 32f);
        CenterPivot(minimize);   // le grossissement au survol part du centre du rond
        Image dash = NewImage("Dash", minimize, null, Parchment);
        dash.rectTransform.anchorMin = dash.rectTransform.anchorMax = dash.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        dash.rectTransform.sizeDelta = new Vector2(14f, 3f);
        dash.rectTransform.anchoredPosition = Vector2.zero;   // centré, comme un vrai « — »

        _resultPill = NewImage("ResultPill", _root, HudSprites.Pill, Color.white);
        _resultPill.type = Image.Type.Sliced;
        Place(_resultPill.rectTransform, _size.x - pad - 32f - 10f - 132f, y + 1f, 132f, 34f);
        _resultText = NewText("ResultText", _resultPill.rectTransform, _titleFont, 21f, Color.white, TextAlignmentOptions.Center);
        Stretch(_resultText.rectTransform, 0f);
        y -= 40f;

        _subtitle = NewText("Subtitle", _root, GameFonts.Body, 17f, Muted, TextAlignmentOptions.MidlineLeft);
        Place(_subtitle.rectTransform, pad, y, w, 24f);
        y -= 32f;

        Image divider = NewImage("Divider", _root, null, new Color(Trim.r, Trim.g, Trim.b, 0.55f));
        Place(divider.rectTransform, pad, y, w, 2f);
        y -= 12f;

        // Deux montants : Or à gauche, Éclats à droite.
        float half = w * 0.5f;
        _goldValue = AmountBlock("Gold", pad, y, half, _goldIcon, GoldText);
        _eclatValue = AmountBlock("Eclats", pad + half + 8f, y, half - 8f, _eclatIcon, EclatText, 57f);   // texte un peu plus près de l'icône (retour utilisateur)
        _eclatNote = NewText("EclatNote", _root, GameFonts.Body, 14f, Muted, TextAlignmentOptions.TopLeft);
        Place(_eclatNote.rectTransform, pad + half + 8f, y - 60f, half - 8f, 20f);   // -4 px (retour utilisateur 2026-11-08), reste au-dessus de la barre
        _eclatNote.gameObject.SetActive(false);   // remplacée par le bouton ci-dessous (2026-11-12)
        _goldBlock = new Graphic[] { _root.Find("GoldIcon").GetComponent<Image>(), _goldValue };
        _eclatBlock = new Graphic[] { _root.Find("EclatsIcon").GetComponent<Image>(), _eclatValue };

        _sourceButton = NewButton("SourceToggle", _root, () => { _showEclats = !_showEclats; Refresh(); });
        Place(_sourceButton, pad + half + 8f, y - 58f, 190f, 22f);
        CenterPivot(_sourceButton);
        _sourceLabel = NewText("Label", _sourceButton, GameFonts.Body, 14f, Parchment, TextAlignmentOptions.Center);
        Stretch(_sourceLabel.rectTransform, 0f);
        _sourceLabel.enableAutoSizing = true; _sourceLabel.fontSizeMax = 14f; _sourceLabel.fontSizeMin = 10f;
        _sourceLabel.margin = new Vector4(10f, 0f, 10f, 0f);
        y -= 82f;

        // Barre de répartition de l'or.
        Image barBack = NewImage("BarBack", _root, HudSprites.Pill, new Color(0f, 0f, 0f, 0.45f));
        barBack.type = Image.Type.Sliced;
        Place(barBack.rectTransform, pad, y, w, 14f);
        _barBack = barBack;
        _bar = new GameObject("Bar", typeof(RectTransform), typeof(RectMask2D)).GetComponent<RectTransform>();
        _bar.SetParent(_root, false);
        Place(_bar, pad + 2f, y - 2f, w - 4f, 10f);
        for (int i = 0; i < SourceColors.Length; i++)
        {
            Image seg = NewImage("Seg" + i, _bar, null, SourceColors[i]);
            seg.rectTransform.anchorMin = new Vector2(0f, 0f);
            seg.rectTransform.anchorMax = new Vector2(0f, 1f);
            seg.rectTransform.pivot = new Vector2(0f, 0.5f);
            _barSegments.Add(seg);
        }
        y -= 22f;

        // Légende sur 2 colonnes (jusqu'à 5 lignes : 3 + 2).
        for (int i = 0; i < SourceColors.Length; i++)
        {
            int col = i < 3 ? 0 : 1, row = i < 3 ? i : i - 3;
            var go = new GameObject("Legend" + i, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(_root, false);
            Place(rt, pad + col * (half + 8f), y - row * 24f, half - 8f, 22f);
            Image dot = NewImage("Dot", rt, HudSprites.Disc, SourceColors[i]);
            Place(dot.rectTransform, 0f, -5f, 12f, 12f);
            TextMeshProUGUI label = NewText("Label", rt, GameFonts.Body, 16f, Parchment, TextAlignmentOptions.MidlineLeft);
            Place(label.rectTransform, 18f, 0f, half - 130f, 22f);
            TextMeshProUGUI value = NewText("Value", rt, GameFonts.Body, 16f, Parchment, TextAlignmentOptions.MidlineRight);
            Place(value.rectTransform, half - 126f, 0f, 114f, 22f);   // assez large pour « 20 786 Or »
            _legendRows.Add(go); _legendLabels.Add(label); _legendValues.Add(value);
        }
        y -= 76f;

        Image divider2 = NewImage("Divider2", _root, null, new Color(Trim.r, Trim.g, Trim.b, 0.35f));
        Place(divider2.rectTransform, pad, y, w, 1f);
        _divider2 = divider2;
        y -= 6f;
        _stats = NewText("Stats", _root, GameFonts.Body, 16f, Muted, TextAlignmentOptions.MidlineLeft);
        Place(_stats.rectTransform, pad, y, w, 24f);
        // Hauteur coupée juste sous la ligne « Ennemis vaincus... » (retour utilisateur : espace vide en bas du panneau)
        _size.y = -y + 24f + 15f;   // même écart texte-cadre que séparateur-texte (mesuré)
        _root.sizeDelta = _size;

        // Langues longues (allemand, polonais, russe...) : les petites lignes rétrécissent au lieu d'être coupées.
        foreach (TextMeshProUGUI t in new[] { _title, _subtitle, _eclatNote, _stats, _resultText })
        {
            t.enableAutoSizing = true;
            t.fontSizeMax = t.fontSize;
            t.fontSizeMin = Mathf.Round(t.fontSize * 0.7f);
        }
        foreach (TextMeshProUGUI t in _legendValues) { t.enableAutoSizing = true; t.fontSizeMax = t.fontSize; t.fontSizeMin = 12f; }
        foreach (TextMeshProUGUI t in _legendLabels) { t.enableAutoSizing = true; t.fontSizeMax = t.fontSize; t.fontSizeMin = 12f; }
    }

    private TextMeshProUGUI AmountBlock(string name, float x, float y, float width, Sprite icon, Color color, float textX = 62f)
    {
        Image ic = NewImage(name + "Icon", _root, icon, Color.white);
        ic.preserveAspect = true;
        Place(ic.rectTransform, x, y - 4f, 54f, 54f);
        TextMeshProUGUI value = NewText(name + "Value", _root, _titleFont, 42f, color, TextAlignmentOptions.MidlineLeft);
        value.enableAutoSizing = true;
        value.fontSizeMin = 26f;
        value.fontSizeMax = 42f;
        value.outlineWidth = 0.18f;
        value.outlineColor = new Color32(30, 18, 8, 255);
        Place(value.rectTransform, x + textX, y - 2f, width - textX, 52f);
        return value;
    }

    // Barre réduite : titre, or gagné, icône « agrandir » ; toute la barre est cliquable.
    private void BuildMini()
    {
        _mini = NewButton("LastRunMini", transform, () => SetCollapsed(false));
        _mini.anchorMin = _mini.anchorMax = new Vector2(0f, 1f);
        _mini.pivot = new Vector2(0f, 1f);
        _mini.anchoredPosition = _offset;
        _mini.sizeDelta = new Vector2(340f, 48f);
        CenterPivot(_mini);
        _miniGroup = _mini.gameObject.AddComponent<CanvasGroup>();
        Image back = _mini.GetComponent<Image>();
        back.sprite = MenuSprites.Wood(WoodTop, WoodBottom, 0.5f, 512, 96);
        back.type = Image.Type.Simple;
        back.color = Color.white;
        Image frame = NewImage("Frame", _mini, HudSprites.Frame, Color.white);
        frame.type = Image.Type.Sliced;
        Stretch(frame.rectTransform, 0f);

        _miniTitle = NewText("Title", _mini, _titleFont, 22f, Trim, TextAlignmentOptions.MidlineLeft);
        Place(_miniTitle.rectTransform, 16f, -4f, 150f, 34f);   // relevé de 3 px : centré en Y (Bangers s'assoit bas)
        _miniTitle.enableAutoSizing = true; _miniTitle.fontSizeMax = 22f; _miniTitle.fontSizeMin = 15f;

        Image coin = NewImage("Coin", _mini, _goldIcon, Color.white);
        coin.preserveAspect = true;
        Place(coin.rectTransform, 172f, -10f, 28f, 28f);
        _miniGold = NewText("Gold", _mini, _titleFont, 22f, GoldText, TextAlignmentOptions.MidlineLeft);
        Place(_miniGold.rectTransform, 204f, -5f, 92f, 34f);
        _miniGold.enableAutoSizing = true; _miniGold.fontSizeMax = 22f; _miniGold.fontSizeMin = 14f;

        // Icône « agrandir » : petit cadre dont le haut est plus épais (fenêtre).
        var icon = new GameObject("Restore", typeof(RectTransform)).GetComponent<RectTransform>();
        icon.SetParent(_mini, false);
        Place(icon, 340f - 34f, -17f, 16f, 14f);
        Color c = Parchment;
        Image top = NewImage("Top", icon, null, c); Place(top.rectTransform, 0f, 0f, 16f, 3f);
        Image bottom = NewImage("Bottom", icon, null, c); Place(bottom.rectTransform, 0f, -12f, 16f, 2f);
        Image left = NewImage("Left", icon, null, c); Place(left.rectTransform, 0f, 0f, 2f, 14f);
        Image right = NewImage("Right", icon, null, c); Place(right.rectTransform, 14f, 0f, 2f, 14f);
    }

    private void SetCollapsed(bool collapsed)
    {
        if (collapsed == _collapsed) return;
        _collapsed = collapsed;
        try { PlayerPrefs.SetInt(CollapsedPref, collapsed ? 1 : 0); PlayerPrefs.Save(); } catch { }
        if (_toggleAnim != null) { StopCoroutine(_toggleAnim); ResetToggleVisuals(); }
        _toggleAnim = StartCoroutine(Toggle(collapsed));
    }

    private void ResetToggleVisuals()
    {
        foreach (RectTransform r in new[] { _root, _mini }) r.localScale = Vector3.one;
        _cardGroup.alpha = _miniGroup.alpha = 1f;
        _cardGroup.interactable = _miniGroup.interactable = true;
    }

    // Fondu enchaîné : l'élément qui part s'efface, puis l'autre apparaît (sans changement de taille).
    private IEnumerator Toggle(bool collapsed)
    {
        RectTransform from = collapsed ? _root : _mini, to = collapsed ? _mini : _root;
        CanvasGroup fromG = collapsed ? _cardGroup : _miniGroup, toG = collapsed ? _miniGroup : _cardGroup;
        fromG.interactable = false;
        const float d = 0.14f;
        for (float t = 0f; t < d; t += Time.unscaledDeltaTime)
        {
            float k = t / d;
            fromG.alpha = 1f - k;
            yield return null;
        }
        from.gameObject.SetActive(false);
        from.localScale = Vector3.one;
        fromG.alpha = 1f;
        fromG.interactable = true;
        to.gameObject.SetActive(true);
        toG.alpha = 0f;
        _toggleAnim = null;
        if (!collapsed) Refresh();
        for (float t = 0f; t < d; t += Time.unscaledDeltaTime)
        {
            float k = t / d;
            toG.alpha = k;
            yield return null;
        }
        toG.alpha = 1f;
        to.localScale = Vector3.one;
    }

    // ── Contenu ─────────────────────────────────────────────────────────────────────────────────────────────────────

    private void Refresh()
    {
        if (_root == null) return;
        MetaProgressionManager meta = MetaProgressionManager.Instance;
        LastRunSummary run = meta != null && meta.Data != null ? meta.Data.lastRun : null;
        bool show = run != null && run.valid;
        _root.gameObject.SetActive(show && !_collapsed);
        _mini.gameObject.SetActive(show && _collapsed);
        if (!show) return;
        _miniTitle.text = Loc.T("DERNIÈRE PARTIE");
        _miniGold.text = Loc.PlusSign + Loc.Int(run.gold);
        if (_collapsed) return;   // carte cachée : ses montants « compteront » à sa réouverture

        _title.text = Loc.T("DERNIÈRE PARTIE");
        string result; Color pill;
        if (run.victory) { result = Loc.T("VICTOIRE"); pill = new Color(0.36f, 0.62f, 0.27f, 1f); }
        else if (run.abandoned) { result = Loc.T("ABANDON"); pill = new Color(0.40f, 0.37f, 0.33f, 1f); }
        else { result = Loc.T("DÉFAITE"); pill = new Color(0.66f, 0.22f, 0.18f, 1f); }
        _resultText.text = result;
        _resultPill.color = pill;

        string[] heroes = { "Aether", "Kael", "Lyra" };
        string hero = run.character >= 0 && run.character < heroes.Length ? heroes[run.character] : heroes[0];
        int secs = Mathf.FloorToInt(run.runTime);
        _subtitle.text = hero + "  ·  " + Capitalize(GameModes.ShortName((GameMode)run.mode)) + "  ·  "
                         + Capitalize(DifficultyTiers.TierName((DifficultyTier)run.tier)) + "  ·  "
                         + TimeText.SpaceColons((secs / 60) + ":" + (secs % 60).ToString("00"));   // « 15 : 00 », comme partout (2026-11-11)

        // Détail des éclats disponible seulement pour les parties enregistrées depuis le 2026-11-12 (parts == total).
        bool eclatDetail = run.eclats > 0 && run.eclatsTime + run.eclatsBosses + run.eclatsVictory + run.eclatsFirstWin == run.eclats;
        if (!eclatDetail) _showEclats = false;
        _sourceButton.gameObject.SetActive(eclatDetail);
        _sourceLabel.text = _showEclats ? Loc.T("Sources de l'or") : Loc.T("Sources des éclats");

        int[] parts = _showEclats
            ? new[] { run.eclatsTime, run.eclatsBosses, run.eclatsVictory, run.eclatsFirstWin, 0 }
            : new[] { run.goldKills, run.goldTime, run.goldBosses, run.goldVictory, run.goldChallenge };
        string[] labels = _showEclats
            ? new[] { Loc.T("Survie"), Loc.T("Boss"), Loc.T("Victoire"), Loc.T("1re victoire"), "" }
            : new[] { Loc.T("Ennemis"), Loc.T("Survie"), Loc.T("Boss"), Loc.T("Victoire"), Loc.T("Défi") };
        Color[] colors = _showEclats ? EclatSourceColors : SourceColors;
        string unit = _showEclats ? Loc.T("Éclats") : Loc.T("Or");
        for (int i = 0; i < parts.Length; i++)
        {
            _legendRows[i].SetActive(parts[i] > 0);
            _legendLabels[i].text = labels[i];
            _legendValues[i].text = Loc.Int(parts[i]) + " " + unit;   // le cumul est en or / éclats (retour utilisateur : le préciser, sans symbole)
            _barSegments[i].color = colors[i];
            _legendRows[i].transform.Find("Dot").GetComponent<Image>().color = colors[i];
        }
        foreach (Graphic g in _goldBlock) g.color = WithAlpha(g.color, _showEclats ? 0.45f : 1f);   // montant non détaillé estompé

        Layout(eclatDetail, parts);
        _stats.text = Loc.F("Ennemis vaincus : {0}  ·  Niv. {1}  ·  Boss : {2}", Loc.Int(run.kills), run.level, run.bossKills);

        bool animate = run.unixTime != _lastAnimatedRun && isActiveAndEnabled;
        _lastAnimatedRun = run.unixTime;
        if (_anim != null) StopCoroutine(_anim);
        if (animate) _anim = StartCoroutine(CountUp(run, parts));
        else ApplyAmounts(run, parts, 1f);
    }

    // AJOUTÉ (2026-11-11, retour utilisateur : "adapter la taille automatiquement du panel de dernière partie lorsqu'il n'est pas
    // rempli par toutes les valeurs, pour éviter les vides") - sous les montants, tout se place selon ce qui est présent :
    // la note « Prime de 1re victoire » (sinon 12 px de moins), la barre et la légende (absentes si la partie n'a rapporté aucun
    // or), une légende de 1 à 3 lignes au lieu de 3 toujours réservées. Les lignes visibles se tassent (pas de trou quand une
    // source vaut 0), puis le séparateur, les stats et la hauteur de la carte suivent. Mêmes écarts que Build().
    private void Layout(bool hasNote, int[] parts)
    {
        const float pad = 22f;
        float half = (_size.x - pad * 2f) * 0.5f;
        float y = -18f - 40f - 32f - 12f;              // haut des montants
        y -= hasNote ? 82f : 70f;                      // sans note : 12 px sous les icônes, comme sous le séparateur du haut

        int shown = 0;
        foreach (int p in parts) if (p > 0) shown++;
        bool bar = shown > 0;
        _barBack.gameObject.SetActive(bar);
        _bar.gameObject.SetActive(bar);
        if (bar)
        {
            _barBack.rectTransform.anchoredPosition = new Vector2(pad, y);
            _bar.anchoredPosition = new Vector2(pad + 2f, y - 2f);
            y -= 22f;
            int index = 0;
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i] <= 0) continue;
                int col = index < 3 ? 0 : 1, row = index < 3 ? index : index - 3;
                ((RectTransform)_legendRows[i].transform).anchoredPosition = new Vector2(pad + col * (half + 8f), y - row * 24f);
                index++;
            }
            y -= Mathf.Min(shown, 3) * 24f + 4f;
        }

        _divider2.rectTransform.anchoredPosition = new Vector2(pad, y);
        y -= 6f;
        _stats.rectTransform.anchoredPosition = new Vector2(pad, y);
        _size.y = -y + 24f + 15f;
        _root.sizeDelta = _size;
    }

    private IEnumerator CountUp(LastRunSummary run, int[] parts)
    {
        ApplyAmounts(run, parts, 0f);
        yield return new WaitForSecondsRealtime(0.35f);
        const float duration = 1.1f;
        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            float k = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / duration), 3f);
            ApplyAmounts(run, parts, k);
            yield return null;
        }
        ApplyAmounts(run, parts, 1f);
        _anim = null;
    }

    private void ApplyAmounts(LastRunSummary run, int[] parts, float k)
    {
        _goldValue.text = Loc.PlusSign + Loc.Int(Mathf.RoundToInt(run.gold * k));
        _eclatValue.text = Loc.PlusSign + Loc.Int(Mathf.RoundToInt(run.eclats * k));

        int total = 0;
        foreach (int p in parts) total += Mathf.Max(0, p);
        float width = _bar.rect.width, x = 0f;
        for (int i = 0; i < _barSegments.Count; i++)
        {
            float segW = total > 0 ? width * parts[i] / total * k : 0f;
            RectTransform rt = _barSegments[i].rectTransform;
            rt.anchoredPosition = new Vector2(x, 0f);
            rt.sizeDelta = new Vector2(Mathf.Max(0f, segW - (segW > 3f ? 2f : 0f)), 0f);   // fin liseré entre segments
            x += segW;
        }
    }

    private static Color WithAlpha(Color c, float a) { c.a = a; return c; }

    private static string Capitalize(string upper)
    {
        if (string.IsNullOrEmpty(upper)) return upper;
        string lower = upper.ToLowerInvariant();
        return char.ToUpperInvariant(lower[0]) + lower.Substring(1);
    }

    // ── Petits utilitaires ──────────────────────────────────────────────────────────────────────────────────────────

    private static Image NewImage(string name, Transform parent, Sprite sprite, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        Image img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    // Zone cliquable avec un fond (survol / clic teintés), pour le bouton « réduire » et la barre réduite.
    private static RectTransform NewButton(string name, Transform parent, UnityEngine.Events.UnityAction onClick)
    {
        Image bg = NewImage(name, parent, HudSprites.Pill, new Color(0f, 0f, 0f, 0.35f));
        bg.type = Image.Type.Sliced;
        bg.raycastTarget = true;
        // CORRIGÉ (2026-11-07, retours utilisateur) - plus de teinte ni de voile clair (« halo blanc » au survol et au
        // clic) : seul le petit grossissement commun à tous les boutons du jeu (ButtonHoverFX) signale le survol.
        Button b = bg.gameObject.AddComponent<Button>();
        b.transition = Selectable.Transition.None;
        b.navigation = new Navigation { mode = Navigation.Mode.None };
        b.onClick.AddListener(onClick);
        return bg.rectTransform;
    }

    private static TextMeshProUGUI NewText(string name, Transform parent, TMP_FontAsset font, float size, Color color, TextAlignmentOptions align)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI t = go.GetComponent<TextMeshProUGUI>();
        if (font != null) { t.font = font; t.fontSharedMaterial = font.material; }
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.enableWordWrapping = false;
        t.overflowMode = TextOverflowModes.Ellipsis;
        t.raycastTarget = false;
        return t;
    }

    // Place un élément par son coin haut-gauche (x vers la droite, y vers le bas en négatif) dans son parent.
    // Pivot au centre sans déplacer l'élément (pour qu'il grossisse depuis son centre).
    private static void CenterPivot(RectTransform rt)
    {
        Vector2 d = new Vector2(0.5f, 0.5f) - rt.pivot;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition += new Vector2(d.x * rt.sizeDelta.x, d.y * rt.sizeDelta.y);
    }

    private static void Place(RectTransform rt, float x, float y, float width, float height)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(width, height);
    }

    private static void Stretch(RectTransform rt, float inset)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, inset);
        rt.offsetMax = new Vector2(-inset, -inset);
    }
}
