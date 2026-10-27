using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// AJOUTÉ (2026-10-25, retour utilisateur : "vérification complète des textes longs sur plusieurs lignes, s'ils sont bien
// cadrés et égaux en termes de lettres, et pas faire des choses bizarres comme des parenthèses qui commencent une ligne
// et qui terminent la ligne d'après") - s'applique à TOUS les textes d'interface (TextMeshProUGUI) du jeu, sans rien à
// poser dans les scènes : abonné à l'événement global de TextMeshPro, déclenché à chaque régénération d'un texte.
// 1) Insécables (balises <nobr> en texte riche, voir ProtectSpaces) : les espaces à l'intérieur d'une parenthèse deviennent insécables (une parenthèse n'est plus jamais
//    coupée entre deux lignes), ainsi que l'espace avant « : ; ! ? % » et à l'intérieur des guillemets (typographie
//    française : jamais de ponctuation seule en début de ligne).
// 2) Lignes équilibrées, textes CENTRÉS uniquement (comme « text-wrap: balance » du web) : dès
//    qu'un texte passe sur 2 lignes ou plus, on cherche la largeur la plus étroite qui garde le MÊME nombre de lignes
//    et la MÊME taille de police (textes à taille automatique), puis on resserre le texte à cette largeur via ses
//    marges, des deux côtés - "une longue ligne + un mot seul" devient deux lignes de longueur
//    proche, sans jamais déborder du cadre prévu (la largeur ne fait que diminuer).
//    Exception : les textes passés par FitToLines(balance: false) (descriptions des compétences de base du level up).
// 3) Justifié (2026-10-26, demande utilisateur : "pour les descriptions qui ont plus de 2 lignes, je veux que tu mettes au
//    format Justifier") : tout paragraphe (police de lecture Fira, voir GameFonts) ALIGNÉ À GAUCHE qui fait 3 lignes ou
//    plus passe en Justifié (jamais si cela ajoute une ligne : on garde alors l'alignement d'origine). Espacement
//    réparti entre les mots seulement (wordWrappingRatios = 0 : pas de lettres écartées).
//    Textes centrés exclus (décision utilisateur, 2026-10-26, après essai sur les cartes de fusion) : 4-5 mots par
//    ligne, le Justifié y creusait de grands trous entre les mots ; ils gardent l'équilibrage du 2).
//    Même chose pour un paragraphe dont une ligne s'élargirait trop (MaxJustifyStretch) : il reste aligné à gauche.
// Textes ignorés : une seule ligne, sans retour à la ligne, non centrés (à gauche, à droite, justifiés), ou dont la largeur dépend de leur propre
// contenu (ContentSizeFitter horizontal, HorizontalLayoutGroup qui pilote la largeur) - resserrer les marges y
// changerait la largeur du cadre lui-même.
public static class TextBalancer
{
    private class State
    {
        public TextMeshProUGUI text;
        public Vector4 baseMargin;
        public string lastText;
        public float lastWidth;
        public bool skip;
        public HorizontalAlignmentOptions baseAlign;
        public bool justified;     // alignement passé en Justifié par nous (baseAlign = celui d'origine)
        public bool noBalance;     // centré sans équilibrage des lignes (voir FitToLines)
    }

    private static readonly Dictionary<int, State> _states = new Dictionary<int, State>();
    private static bool _busy;
    private static readonly StringBuilder _sb = new StringBuilder(256);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        _states.Clear();
        TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(OnTextChanged);
        TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnTextChanged);
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        // Passe de nettoyage en LateUpdate (toujours avant la passe de layout des Canvas de la même frame). Essai
        // abandonné : Canvas.preWillRenderCanvases, mesuré comme ne déclenchant jamais le recalcul attendu.
        if (_runner == null)
        {
            GameObject go = new GameObject("TextBalancer") { hideFlags = HideFlags.HideAndDontSave };
            Object.DontDestroyOnLoad(go);
            _runner = go.AddComponent<Runner>();
        }
    }

    private static Runner _runner;

    private class Runner : MonoBehaviour
    {
        private void LateUpdate() => ResetStaleMargins();
    }

    // CORRIGÉ (2026-10-25, retour utilisateur : "la barre de séparation sous la description, des fois, n'est pas sous
    // la dernière ligne, il y a un cran ou deux de décalage") - mesuré en Play sur les 15 nœuds de l'arbre : la hauteur
    // réservée par le VerticalLayoutGroup comptait parfois 4-5 lignes pour une description de 3. Cause : les marges
    // resserrées pour le texte PRÉCÉDENT restaient en place quand le texte changeait, et la passe de layout (qui a
    // lieu AVANT la régénération du texte, donc avant OnTextChanged) mesurait le nouveau texte avec cette largeur
    // trop étroite. Les marges d'origine sont donc remises ici, juste avant la passe de layout, dès que le texte ou
    // la largeur a changé depuis l'équilibrage : le layout mesure le texte à sa vraie largeur (même nombre de
    // lignes que la version équilibrée, voir Balance), puis OnTextChanged le resserre de nouveau.
    // Mesuré ensuite (Concentration puis Second Souffle) : même avec les marges remises, TextMeshPro gardait en cache la
    // hauteur préférée du texte précédent (4 lignes pour 3 rendues) - changer `margin` ne l'invalide pas. Tout texte
    // dont l'équilibrage a modifié les marges redemande donc un calcul de layout complet à la passe suivante.
    // Et le setter de `margin` ne recalcule même pas la largeur utile interne de TextMeshPro (seule une régénération le
    // fait) : la hauteur préférée restait mesurée à l'ancienne largeur resserrée. D'où la régénération forcée au
    // moment de remettre les marges, qui rééquilibre aussi le nouveau texte tout de suite (OnTextChanged), avant le layout.
    private static readonly List<TextMeshProUGUI> _needLayout = new List<TextMeshProUGUI>();
    private static readonly List<int> _dead = new List<int>();
    private static readonly List<State> _stale = new List<State>();

    private static void ResetStaleMargins()
    {
        for (int i = 0; i < _needLayout.Count; i++)
            if (_needLayout[i] != null) _needLayout[i].SetLayoutDirty();
        _needLayout.Clear();

        foreach (KeyValuePair<int, State> kv in _states)
        {
            State s = kv.Value;
            TextMeshProUGUI t = s.text;
            if (t == null) { _dead.Add(kv.Key); continue; }
            if (s.skip || !t.isActiveAndEnabled) continue;
            if (t.margin == s.baseMargin && !s.justified && !CanJustify(t, s.baseAlign)) continue;
            if (t.text != s.lastText || Mathf.Abs(t.rectTransform.rect.width - s.lastWidth) >= 0.5f) _stale.Add(s);
        }
        for (int i = 0; i < _dead.Count; i++) _states.Remove(_dead[i]);
        _dead.Clear();

        // Hors de l'itération du dictionnaire : ForceMeshUpdate rappelle OnTextChanged.
        // AJOUTÉ (2026-10-26, Justifié) : les textes qui peuvent passer en Justifié sont aussi traités ici, avant le
        // layout - le Justifié peut retirer une ligne (espaces resserrés), le layout mesurerait sinon la version d'avant.
        for (int i = 0; i < _stale.Count; i++)
        {
            State s = _stale[i];
            TextMeshProUGUI t = s.text;
            bool wasAdjusted = t.margin != s.baseMargin || s.justified;
            t.margin = s.baseMargin;
            RestoreAlignment(t, s);   // même piège que les marges : le Justifié tolère des lignes 5 % plus longues
            t.ForceMeshUpdate();
            if (wasAdjusted || s.justified || t.margin != s.baseMargin) t.SetLayoutDirty();
        }
        _stale.Clear();
    }

    // Les textes de la scène précédente sont détruits : on oublie leur état (identifiants d'instance périmés).
    private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode) => _states.Clear();

    private static void OnTextChanged(Object obj)
    {
        if (_busy) return;
        if (!(obj is TextMeshProUGUI t) || t == null || !t.isActiveAndEnabled) return;

        State s = GetState(t);
        if (s.skip) return;

        // Alignement changé par un script pendant que le texte était justifié : c'est le nouvel alignement d'origine.
        if (!s.justified) s.baseAlign = t.horizontalAlignment;
        else if (t.horizontalAlignment != HorizontalAlignmentOptions.Justified) { s.justified = false; s.baseAlign = t.horizontalAlignment; }

        float width = t.rectTransform.rect.width;
        string text = t.text;
        if (text == s.lastText && Mathf.Abs(width - s.lastWidth) < 0.5f) return;

        string fixedText = ProtectSpaces(text, t.richText);
        s.lastText = fixedText;
        s.lastWidth = width;
        // Cas le plus courant (compteurs, chrono, libellés courts) : la génération qui vient d'avoir lieu tient déjà sur
        // une ligne avec les marges d'origine - rien à recalculer, aucune régénération supplémentaire.
        if (fixedText == text && t.textInfo.lineCount < 2 && t.margin == s.baseMargin && !s.justified) return;

        _busy = true;
        try
        {
            if (fixedText != text) t.text = fixedText;
            RestoreAlignment(t, s);
            t.margin = s.baseMargin;
            if (!Justify(t, s) && !s.noBalance) Balance(t, s.baseMargin, width, s.baseAlign);
            _needLayout.Add(t);
        }
        finally { _busy = false; }
    }

    private static State GetState(TextMeshProUGUI t)
    {
        int id = t.GetInstanceID();
        if (!_states.TryGetValue(id, out State s))
        {
            s = new State { text = t, baseMargin = t.margin, baseAlign = t.horizontalAlignment, skip = IsWidthContentDriven(t) };
            _states[id] = s;
        }
        return s;
    }

    // AJOUTÉ (2026-10-26, retour utilisateur : "pour les compétences classiques, la description tienne sur 2 lignes, c'est
    // pas grave s'il n'y a pas le même nombre de mots par ligne") - plus grande taille de police (pas de 0,5) à laquelle
    // le texte tient en `maxLines` lignes, mesurée à la largeur d'origine du cadre (marges non resserrées : l'équilibrage
    // garde ensuite ce nombre de lignes et cette taille). Remplace l'auto-size de TextMeshPro, qui ne réduit la police
    // que pour tenir en HAUTEUR et acceptait donc 3 petites lignes. Appelé juste après avoir changé le texte.
    // MODIFIÉ (2026-10-26, retour utilisateur : "on oublie les même caractères par ligne", descriptions des compétences de
    // base du level up) - balance = false : texte centré sur toute la largeur du cadre, sans équilibrage des lignes, y
    // compris lors des régénérations suivantes (jusqu'au prochain appel avec balance = true, même texte réutilisé en fusion).
    public static void FitToLines(TextMeshProUGUI t, int maxLines, float maxSize, float minSize, bool balance = true)
    {
        if (t == null) return;
        State s = GetState(t);
        s.noBalance = !balance;
        _busy = true;
        try
        {
            RestoreAlignment(t, s);
            t.margin = s.baseMargin;
            t.enableAutoSizing = false;
            string fixedText = ProtectSpaces(t.text, t.richText);
            if (fixedText != t.text) t.text = fixedText;
            for (float size = maxSize; ; size -= 0.5f)
            {
                t.fontSize = Mathf.Max(size, minSize);
                t.ForceMeshUpdate(true);   // le panneau n'est pas toujours encore actif à ce moment-là
                if (t.textInfo.lineCount <= maxLines || size <= minSize) break;
            }
            if (t.isActiveAndEnabled && !s.skip)
            {
                float width = t.rectTransform.rect.width;
                s.lastText = t.text;
                s.lastWidth = width;
                if (!Justify(t, s) && balance) Balance(t, s.baseMargin, width, s.baseAlign);
                _needLayout.Add(t);
            }
            else s.lastText = null;   // équilibré à sa prochaine génération (activation du panneau)
        }
        finally { _busy = false; }
    }

    private static bool IsWidthContentDriven(TextMeshProUGUI t)
    {
        ContentSizeFitter fitter = t.GetComponent<ContentSizeFitter>();
        if (fitter != null && fitter.horizontalFit != ContentSizeFitter.FitMode.Unconstrained) return true;
        Transform parent = t.transform.parent;
        if (parent != null)
        {
            HorizontalLayoutGroup h = parent.GetComponent<HorizontalLayoutGroup>();
            if (h != null && h.enabled && h.childControlWidth) return true;
        }
        return false;
    }

    private static void Balance(TextMeshProUGUI t, Vector4 baseMargin, float width, HorizontalAlignmentOptions h)
    {
        if (t.textWrappingMode == TextWrappingModes.NoWrap || t.textWrappingMode == TextWrappingModes.PreserveWhitespaceNoWrap) return;
        // MODIFIÉ (2026-10-25, décision utilisateur) - textes CENTRÉS uniquement (cartes, annonces, descriptions de
        // mode...). Un paragraphe aligné à gauche (fiche de l'arbre de compétences, etc.) garde toute la largeur de son
        // cadre : resserré, il laissait un grand vide à droite sous des séparateurs pleine largeur.
        if (h != HorizontalAlignmentOptions.Center && h != HorizontalAlignmentOptions.Geometry) return;

        t.margin = baseMargin;
        t.ForceMeshUpdate();
        int lines = t.textInfo.lineCount;
        float fontSize = t.fontSize;
        float full = width - baseMargin.x - baseMargin.z;
        if (lines < 2 || full <= 0f || t.isTextOverflowing) return;

        // Recherche dichotomique de la plus petite largeur qui garde le même nombre de lignes et la même taille.
        float lo = full / lines * 0.85f, hi = full;
        for (int i = 0; i < 9 && hi - lo > 2f; i++)
        {
            float mid = (lo + hi) * 0.5f;
            ApplyWidth(t, baseMargin, full, mid);
            t.ForceMeshUpdate();
            bool same = t.textInfo.lineCount == lines && Mathf.Abs(t.fontSize - fontSize) < 0.01f && !t.isTextOverflowing;
            if (same) hi = mid; else lo = mid;
        }

        ApplyWidth(t, baseMargin, full, Mathf.Min(full, hi + 3f));   // petite marge de sécurité : jamais pile à la limite d'un retour à la ligne
        t.ForceMeshUpdate();
        if (t.textInfo.lineCount != lines || Mathf.Abs(t.fontSize - fontSize) >= 0.01f)
        {
            t.margin = baseMargin;   // garde-fou : jamais un résultat différent de l'original
            t.ForceMeshUpdate();
        }
    }

    private static void ApplyWidth(TextMeshProUGUI t, Vector4 baseMargin, float full, float w)
    {
        float extra = Mathf.Max(0f, full - w);
        Vector4 m = baseMargin;
        m.x += extra * 0.5f;   // texte centré : resserré des deux côtés, il reste centré
        m.z += extra * 0.5f;
        t.margin = m;
    }

    private static void RestoreAlignment(TextMeshProUGUI t, State s)
    {
        if (!s.justified) return;
        t.horizontalAlignment = s.baseAlign;
        s.justified = false;
    }

    // Paragraphe (police de lecture), aligné à gauche, avec retour à la ligne.
    private static bool CanJustify(TextMeshProUGUI t, HorizontalAlignmentOptions h)
    {
        if (h != HorizontalAlignmentOptions.Left) return false;
        if (t.font == null || t.font != GameFonts.Body) return false;
        return t.textWrappingMode != TextWrappingModes.NoWrap && t.textWrappingMode != TextWrappingModes.PreserveWhitespaceNoWrap;
    }

    private static bool Justify(TextMeshProUGUI t, State s)
    {
        HorizontalAlignmentOptions h = s.baseAlign;
        if (!CanJustify(t, h)) return false;

        t.ForceMeshUpdate();
        int lines = t.textInfo.lineCount;
        float fontSize = t.fontSize;
        // Pas de test de débordement ici : quand le texte vient de changer, son cadre a encore la hauteur de l'ancien
        // texte (le layout passe après) et TextMeshPro signale un débordement qui n'en est pas un.
        if (lines < 3) return false;
        if (MaxStretch(t) > MaxJustifyStretch) return false;

        t.horizontalAlignment = HorizontalAlignmentOptions.Justified;
        t.wordWrappingRatios = 0f;
        t.ForceMeshUpdate();
        // Le Justifié de TextMeshPro accepte une ligne jusqu'à 5 % trop longue en resserrant ses espaces : un texte de 3
        // lignes peut finir sur 2, c'est voulu (comme un logiciel de mise en page). Jamais plus de lignes, en revanche.
        if (t.textInfo.lineCount > lines || Mathf.Abs(t.fontSize - fontSize) >= 0.01f)
        {
            t.horizontalAlignment = h;   // garde-fou : le Justifié ne doit jamais allonger le texte
            t.ForceMeshUpdate();
            return false;
        }
        s.justified = true;
        return true;
    }

    // AJOUTÉ (2026-10-26) : au-delà de cet élargissement par espace (en fraction de la taille de police), une ligne
    // justifiée paraît trouée (ex. « Bulle de 3 charges qui bloque les » suivi d'un mot long) : le paragraphe reste à gauche.
    private const float MaxJustifyStretch = 0.55f;

    // Plus grand élargissement par espace qu'imposerait le Justifié, sur les lignes non finales (mise en page actuelle).
    private static float MaxStretch(TextMeshProUGUI t)
    {
        TMP_TextInfo ti = t.textInfo;
        float width = t.rectTransform.rect.width - t.margin.x - t.margin.z;
        float worst = 0f;
        for (int l = 0; l < ti.lineCount - 1; l++)
        {
            TMP_LineInfo li = ti.lineInfo[l];
            int spaces = 0;
            for (int c = li.firstVisibleCharacterIndex + 1; c < li.lastVisibleCharacterIndex; c++)
                if (ti.characterInfo[c].character == ' ') spaces++;
            if (spaces > 0) worst = Mathf.Max(worst, (width - li.length) / spaces / t.fontSize);
        }
        return worst;
    }

    private const char Nbsp = '\u00A0';
    private const string NoBreakOpen = "<nobr>", NoBreakClose = "</nobr>";

    // Insécables : dans les parenthèses, avant « : ; ! ? % », après « et avant ». Les balises <...> sont recopiées telles
    // quelles (jamais d'espace modifié à l'intérieur d'une balise de texte riche).
    // MODIFIÉ (2026-10-26, Justifié) : en texte riche, balises <nobr> plutôt que U+00A0 - TextMeshPro n'élargit jamais
    // un U+00A0 en Justifié mais le compte quand même parmi les espaces à élargir : chaque ligne qui en contenait
    // s'arrêtait avant le bord droit. Un espace normal dans <nobr> ne coupe pas la ligne mais s'élargit comme les autres.
    public static string ProtectSpaces(string text, bool richText = true)
    {
        if (string.IsNullOrEmpty(text) || text.IndexOf(' ') < 0) return text;
        if (richText && text.IndexOf(NoBreakOpen, System.StringComparison.Ordinal) >= 0) return text;   // déjà traité
        _sb.Clear();
        int depth = 0;
        bool inTag = false;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (inTag) { if (c == '>') inTag = false; _sb.Append(c); continue; }
            if (richText && c == '<' && text.IndexOf('>', i) > i) { inTag = true; _sb.Append(c); continue; }
            if (c == '(')
            {
                if (depth++ == 0 && richText) _sb.Append(NoBreakOpen);
                _sb.Append(c);
                continue;
            }
            if (c == ')' && depth > 0)
            {
                _sb.Append(c);
                if (--depth == 0 && richText) _sb.Append(NoBreakClose);
                continue;
            }

            if (c == ' ')
            {
                char next = richText ? NextVisible(text, i + 1) : (i + 1 < text.Length ? text[i + 1] : '\0');
                char prev = i > 0 ? text[i - 1] : '\0';
                bool punct = next == ':' || next == ';' || next == '!' || next == '?' || next == '%' || next == '»' || prev == '«';
                if (!richText && (depth > 0 || punct)) { _sb.Append(Nbsp); continue; }
                if (richText && depth == 0 && punct) { _sb.Append(NoBreakOpen).Append(' ').Append(NoBreakClose); continue; }
            }
            _sb.Append(c);
        }
        if (richText && depth > 0) _sb.Append(NoBreakClose);   // parenthèse jamais refermée
        return _sb.ToString();
    }

    // Caractère visible suivant, en sautant les balises (« <color=#...>: » compte comme « : »).
    private static char NextVisible(string text, int i)
    {
        while (i < text.Length)
        {
            if (text[i] == '<')
            {
                int close = text.IndexOf('>', i);
                if (close < 0) return text[i];
                i = close + 1;
                continue;
            }
            return text[i];
        }
        return '\0';
    }
}
