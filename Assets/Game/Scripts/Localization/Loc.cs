using System;
using System.Collections.Generic;
using UnityEngine;

// AJOUTÉ (2026-10-14) - langue du jeu (Paramètres > Langue). Le français est la langue d'écriture du jeu ; traductions :
// anglais, allemand, espagnol, portugais (Brésil), italien, polonais, russe, chinois simplifié, japonais, coréen (une table par langue :
// LocTableEN, LocTableDE...).
//
// Principe : le TEXTE FRANÇAIS EST LA CLÉ. Le code continue d'écrire ses textes en français, simplement enveloppés :
//   label.text = Loc.T("Reprendre");                         // texte fixe
//   label.text = Loc.F("Niveau {0}", level);                 // texte avec valeurs (string.Format)
// En français, T/F renvoient le texte tel quel (le jeu reste strictement identique) ; sinon la traduction vient de la
// table de la langue. Une clé absente : repli sur l'anglais (puis le français) ET signalement une seule fois dans la
// console (Éditeur / build de dev), pour ne jamais laisser passer un oubli silencieusement.
// La table ANGLAISE est la référence de la liste des clés : toute nouvelle clé s'y ajoute d'abord, puis dans les autres
// (contrôle : node Tools/loc/langs.js check).
//
// Textes posés à la main dans les scènes / prefabs : composant LocalizedText (même table, même règle).
// Changement de langue EN DIRECT : événement Loc.Changed (les pages et le HUD se rafraîchissent d'eux-mêmes).
// Polices : russe, chinois, japonais et coréen ne sont pas dans Bangers. Cyrillique ajouté à la police Bangers elle-même
// (Tools/patch_bangers_cyrillic.py) ; chinois, japonais, coréen en polices de repli (Noto Sans SC / JP / KR,
// Aether > Rebuild Language Fonts), celle de la langue courante passant en tête (LanguageFonts).
// MODIFIÉ (2026-10-14) - les valeurs de l'enum sont enregistrées dans les réglages : ne jamais les renuméroter.
// MODIFIÉ (2026-10-15) - ajout du russe (7), du chinois simplifié (8), du japonais (9) et du coréen (10).
public enum GameLanguage { French = 0, English = 1, German = 2, Spanish = 3, PortugueseBR = 4, Italian = 5, Polish = 6, Russian = 7, ChineseSimplified = 8, Japanese = 9, Korean = 10 }

public static class Loc
{
    public static event Action Changed;

    public const int LanguageCount = 11;

    private static bool _init;
    private static GameLanguage _lang;
    private static readonly HashSet<string> _reportedMissing = new HashSet<string>();

    public static GameLanguage Current { get { Init(); return _lang; } }
    public static bool IsEnglish => Current == GameLanguage.English;
    public static bool IsFrench => Current == GameLanguage.French;

    // Langue proposée au tout premier lancement : celle de Windows si le jeu la parle, l'anglais sinon.
    public static GameLanguage SystemDefault
    {
        get
        {
            switch (Application.systemLanguage)
            {
                case SystemLanguage.French: return GameLanguage.French;
                case SystemLanguage.German: return GameLanguage.German;
                case SystemLanguage.Spanish: return GameLanguage.Spanish;
                case SystemLanguage.Portuguese: return GameLanguage.PortugueseBR;
                case SystemLanguage.Italian: return GameLanguage.Italian;
                case SystemLanguage.Polish: return GameLanguage.Polish;
                case SystemLanguage.Russian: return GameLanguage.Russian;
                // chinois traditionnel (Taïwan, Hong Kong) : le simplifié reste bien plus lisible pour eux que l'anglais
                case SystemLanguage.Chinese:
                case SystemLanguage.ChineseSimplified:
                case SystemLanguage.ChineseTraditional: return GameLanguage.ChineseSimplified;
                case SystemLanguage.Japanese: return GameLanguage.Japanese;
                case SystemLanguage.Korean: return GameLanguage.Korean;
                default: return GameLanguage.English;
            }
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Boot() => Init();

    private static void Init()
    {
        if (_init) return;
        _init = true;
        _lang = ReadSetting();
        GameSettings.Changed += key =>
        {
            if (key != GameSettings.Language) return;
            GameLanguage now = ReadSetting();
            if (now == _lang) return;
            _lang = now;
            Changed?.Invoke();
        };
    }

    private static GameLanguage ReadSetting()
    {
        int v = GameSettings.GetInt(GameSettings.Language);
        return v >= 0 && v < LanguageCount ? (GameLanguage)v : GameLanguage.English;
    }

    private static Dictionary<string, string> TableFor(GameLanguage lang)
    {
        switch (lang)
        {
            case GameLanguage.English: return LocTableEN.Table;
            case GameLanguage.German: return LocTableDE.Table;
            case GameLanguage.Spanish: return LocTableES.Table;
            case GameLanguage.PortugueseBR: return LocTablePT.Table;
            case GameLanguage.Italian: return LocTableIT.Table;
            case GameLanguage.Polish: return LocTablePL.Table;
            case GameLanguage.Russian: return LocTableRU.Table;
            case GameLanguage.ChineseSimplified: return LocTableZH.Table;
            case GameLanguage.Japanese: return LocTableJA.Table;
            case GameLanguage.Korean: return LocTableKO.Table;
            default: return null;
        }
    }

    /// <summary>Texte (écrit en français) dans la langue courante.</summary>
    public static string T(string fr)
    {
        if (string.IsNullOrEmpty(fr)) return fr;
        GameLanguage lang = Current;
        if (lang == GameLanguage.French) return fr;
        if (TableFor(lang).TryGetValue(fr, out string tr)) return tr;
        if (!HasLetters(fr)) return fr;   // "+10%", "-0.3s", "12" : rien à traduire
        ReportMissing(fr, lang);
        return lang != GameLanguage.English && LocTableEN.Table.TryGetValue(fr, out string en) ? en : fr;
    }

    /// <summary>Texte à trous ({0}, {1}...) dans la langue courante, puis rempli (string.Format).</summary>
    public static string F(string frFormat, params object[] args) => string.Format(T(frFormat), args);

    /// <summary>Choix singulier / pluriel (les deux formes sont des clés françaises, avec {0} pour le nombre).</summary>
    public static string Plural(int n, string frOne, string frMany) => F(PluralKey(n, frOne, frMany), n);

    /// <summary>Mot seul accordé au nombre ("victoire" / "victoires"), déjà traduit.</summary>
    public static string PluralWord(int n, string frOne, string frMany) => T(PluralKey(n, frOne, frMany));

    // Règle de pluriel de la langue courante. Polonais et russe ont une 3e forme (2-4, sauf 12-14 : "2 wygrane" mais
    // "5 wygranych", "2 победы" mais "5 побед") : clé française du pluriel suivie de "#few", présente seulement dans
    // la table de ces langues (absente : forme du pluriel). Le russe prend le singulier pour 1, 21, 31... (sauf 11)
    // ("21 победа"). Chinois, japonais et coréen n'accordent pas : toujours la forme du pluriel.
    private static string PluralKey(int n, string frOne, string frMany)
    {
        int d = Math.Abs(n) % 10, h = Math.Abs(n) % 100;
        bool few = d >= 2 && d <= 4 && (h < 12 || h > 14);
        switch (Current)
        {
            case GameLanguage.French:
                return n > 1 ? frMany : frOne;
            case GameLanguage.Polish:
                if (n == 1) return frOne;
                return few && LocTablePL.Table.ContainsKey(frMany + "#few") ? frMany + "#few" : frMany;
            case GameLanguage.Russian:
                if (d == 1 && h != 11) return frOne;
                return few && LocTableRU.Table.ContainsKey(frMany + "#few") ? frMany + "#few" : frMany;
            case GameLanguage.ChineseSimplified:
            case GameLanguage.Japanese:
            case GameLanguage.Korean:
                return frMany;
            default:
                return n == 1 ? frOne : frMany;
        }
    }

    /// <summary>Séparateur décimal de la langue courante : point en anglais, chinois, japonais et coréen, virgule ailleurs.</summary>
    public static string DecimalSeparator
    {
        get
        {
            GameLanguage l = Current;
            return l == GameLanguage.English || l == GameLanguage.ChineseSimplified || l == GameLanguage.Japanese || l == GameLanguage.Korean ? "." : ",";
        }
    }

    /// <summary>Nombre à virgule écrit selon la langue (format .NET, ex. "0.#") : "1,5" en français, "1.5" en anglais.
    /// Ne dépend pas de la langue de Windows.</summary>
    public static string Num(float value, string format)
        => value.ToString(format, System.Globalization.CultureInfo.InvariantCulture).Replace(".", DecimalSeparator);

    // AJOUTÉ (2026-10-18, diagnostic complet - audit §9 "les grands nombres (or, Éclats) s'affichent sans séparateur
    // de milliers") - séparateur des milliers selon la langue, en dur comme DecimalSeparator ci-dessus (jamais la
    // langue de Windows ni CultureInfo.GetCultureInfo, qui peut ne pas exister sur toutes les plateformes). 3 cas
    // réels parmi les 11 langues : virgule (anglais/chinois/japonais/coréen, qui ont aussi un point décimal - voir
    // DecimalSeparator), point (allemand/espagnol/portugais/italien, qui ont une virgule décimale), espace
    // (français/polonais/russe, qui ont AUSSI une virgule décimale mais groupent avec un espace, pas un point).
    public static string ThousandsSeparator
    {
        get
        {
            switch (Current)
            {
                case GameLanguage.English:
                case GameLanguage.ChineseSimplified:
                case GameLanguage.Japanese:
                case GameLanguage.Korean:
                    return ",";
                case GameLanguage.German:
                case GameLanguage.Spanish:
                case GameLanguage.PortugueseBR:
                case GameLanguage.Italian:
                    return ".";
                default: // French, Polish, Russian
                    return " ";
            }
        }
    }

    /// <summary>Grand nombre entier groupé par milliers selon la langue ("12 500" en français, "12,500" en anglais,
    /// "12.500" en allemand) : utiliser pour tout Or/Éclats/score affiché au joueur au lieu d'un simple ToString().</summary>
    public static string Int(long value)
    {
        bool negative = value < 0;
        string digits = Math.Abs(value).ToString(System.Globalization.CultureInfo.InvariantCulture);
        System.Text.StringBuilder sb = new System.Text.StringBuilder(digits.Length + 4);
        int sinceGroup = 0;
        for (int i = digits.Length - 1; i >= 0; i--)
        {
            sb.Insert(0, digits[i]);
            sinceGroup++;
            if (sinceGroup == 3 && i > 0)
            {
                sb.Insert(0, ThousandsSeparator);
                sinceGroup = 0;
            }
        }
        return negative ? "-" + sb : sb.ToString();
    }

    /// <summary>Vrai si `text` est la traduction de `fr` dans l'une des langues (sert à LocalizedText).</summary>
    public static bool IsTranslationOf(string fr, string text)
    {
        if (string.IsNullOrEmpty(fr) || string.IsNullOrEmpty(text)) return false;
        for (int i = 1; i < LanguageCount; i++)
            if (TableFor((GameLanguage)i).TryGetValue(fr, out string tr) && tr == text) return true;
        return false;
    }

    /// <summary>Vrai si ce texte français a une traduction (sert à LocalizedText pour ignorer les textes de remplissage).</summary>
    public static bool Has(string fr) => !string.IsNullOrEmpty(fr) && LocTableEN.Table.ContainsKey(fr);

    private static bool HasLetters(string s)
    {
        int run = 0;
        foreach (char c in s)
        {
            run = char.IsLetter(c) ? run + 1 : 0;
            if (run >= 2) return true;
        }
        return false;
    }

    private static void ReportMissing(string fr, GameLanguage lang)
    {
        if (!(Application.isEditor || Debug.isDebugBuild)) return;
        if (!_reportedMissing.Add(lang + "|" + fr)) return;
        Debug.LogWarning("[Loc] Traduction manquante (" + lang + ") : \"" + fr.Replace("\n", "\\n") + "\"");
    }
}
