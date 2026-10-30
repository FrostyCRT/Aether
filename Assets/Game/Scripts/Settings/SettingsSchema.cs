using System;
using System.Collections.Generic;
using UnityEngine;

// AJOUTE (2026-09-19) - DÉFINITION de la page Paramètres : catégories et lignes, en données.
//
// La page (SettingsPage) ne connaît aucun réglage en particulier : elle lit cette liste et construit les lignes
// (curseur, interrupteur, choix, touche...). Ajouter un réglage = 1 ligne dans GameSettings (clé + défaut) + 1 ligne
// ici + (si le jeu doit réagir) 1 case dans SettingsApplier / le script concerné.
public enum RowKind { Header, Note, Slider, Toggle, Choice, Keybind, Presets, Swatches }

public class RowDef
{
    public RowKind kind;
    public string label;
    public string description;

    public string key;                          // clé GameSettings (Slider / Toggle / Choice)
    public float min, max;                      // Slider : bornes affichées (en valeur réelle)
    public string[] options;                    // Choice : libellés
    public Func<string[]> dynamicOptions;       // Choice : libellés calculés à l'ouverture (résolutions)
    public Func<int> getIndex;                  // Choice : index courant (sinon GetInt(key))
    public Action<int> setIndex;                // Choice : écriture (sinon SetInt(key))
    public Color[] swatches;                    // Swatches : une pastille par couleur, index enregistré dans key

    public Func<bool> isEnabled;                // ligne grisée quand faux
    public string disabledHint;                 // explication affichée à la place de la description

    public GameAction action;                   // Keybind
    public bool keyColumns;                     // Header : titres des colonnes de touches (Clavier / souris, Manette)
    public bool affectsDisplay;                 // change l'écran : déclenche la confirmation "conserver ?"

    public Func<bool> isDefault;                // par défaut : GameSettings.IsDefault(key)
    public Action reset;                        // par défaut : GameSettings.Reset(key)

    // clés qu'une "remise à zéro de la page" doit remettre (calculées depuis les lignes)
    public IEnumerable<string> Keys()
    {
        if (!string.IsNullOrEmpty(key)) yield return key;
    }
}

public class CategoryDef
{
    public string id;
    public string title;
    public string subtitle;
    public List<RowDef> rows = new List<RowDef>();
}

public static class SettingsSchema
{
    public static readonly string[] QualityNames = { "Performance", "Équilibré", "Élevé" };
    public static readonly string[] DisplayModeNames = { "Plein écran exclusif", "Fenêtré sans bordure", "Fenêtré" };
    public static readonly string[] MenuEffectNames = { "Désactivés", "Activés" };

    private static string[] FpsCapNames()
    {
        string[] n = new string[GameSettings.FpsCaps.Length];
        for (int i = 0; i < n.Length; i++) n[i] = GameSettings.FpsCaps[i] <= 0 ? Loc.T("Illimitée") : GameSettings.FpsCaps[i] + " " + Loc.T("IPS");
        return n;
    }

    private static string[] ResolutionNames()
    {
        List<Vector2Int> list = SettingsApplier.AvailableResolutions();
        string[] n = new string[list.Count];
        for (int i = 0; i < n.Length; i++) n[i] = list[i].x + " × " + list[i].y;
        return n;
    }

    private static int ResolutionIndex()
    {
        List<Vector2Int> list = SettingsApplier.AvailableResolutions();
        if (SettingsApplier.TryParseResolution(GameSettings.GetString(GameSettings.Resolution), out Vector2Int saved))
        {
            int i = list.IndexOf(saved);
            if (i >= 0) return i;
        }
        // rien d'enregistré (ou résolution disparue) : la résolution actuelle de la fenêtre
        Vector2Int cur = new Vector2Int(Screen.currentResolution.width, Screen.currentResolution.height);
        int idx = list.IndexOf(cur);
        return idx >= 0 ? idx : list.Count - 1;
    }

    private static void SetResolutionIndex(int i)
    {
        List<Vector2Int> list = SettingsApplier.AvailableResolutions();
        if (i < 0 || i >= list.Count) return;
        GameSettings.SetString(GameSettings.Resolution, SettingsApplier.ResolutionKey(list[i]));
    }

    // AJOUTÉ (2026-10-29, retour utilisateur : "choisir sur quel écran le joueur veut que le jeu s'affiche, dans les
    // paramètres") - un choix par écran branché, dans l'ordre de Windows (« Écran 1 », « Écran 2 »... + sa définition).
    private static string[] MonitorNames()
    {
        List<DisplayInfo> list = SettingsApplier.Monitors();
        string[] n = new string[Mathf.Max(1, list.Count)];
        if (list.Count == 0) { n[0] = Loc.F("Écran {0}", 1); return n; }
        for (int i = 0; i < list.Count; i++)
            n[i] = Loc.F("Écran {0}", i + 1) + "  ·  " + list[i].width + " × " + list[i].height;
        return n;
    }

    private static int MonitorIndex() => SettingsApplier.CurrentMonitorIndex();

    private static void SetMonitorIndex(int i)
    {
        if (i < 0 || i >= SettingsApplier.Monitors().Count) return;
        GameSettings.SetString(GameSettings.Monitor, i.ToString());
    }

    private static RowDef Header(string label) => new RowDef { kind = RowKind.Header, label = label };
    private static RowDef Note(string text) => new RowDef { kind = RowKind.Note, label = text };

    private static RowDef Toggle(string label, string description, string key) =>
        new RowDef { kind = RowKind.Toggle, label = label, description = description, key = key };

    private static RowDef Slider(string label, string description, string key, float min, float max) =>
        new RowDef { kind = RowKind.Slider, label = label, description = description, key = key, min = min, max = max };

    private static RowDef Choice(string label, string description, string key, string[] options) =>
        new RowDef { kind = RowKind.Choice, label = label, description = description, key = key, options = options };

    private static RowDef Key(GameAction a) =>
        new RowDef { kind = RowKind.Keybind, label = GameInput.ActionLabel(a), description = GameInput.ActionHint(a), action = a };

    public static List<CategoryDef> Build()
    {
        List<CategoryDef> cats = new List<CategoryDef>();

        // ---------------------------------------------------------------- AFFICHAGE
        CategoryDef display = new CategoryDef { id = "display", title = "AFFICHAGE", subtitle = "Fenêtre, résolution et fluidité de l'image" };
        display.rows.Add(new RowDef
        {
            kind = RowKind.Choice, label = "Mode d'affichage", key = GameSettings.DisplayMode, options = DisplayModeNames,
            description = "Plein écran, fenêtre sans bordure ou fenêtre classique",
            affectsDisplay = true
        });
        display.rows.Add(new RowDef
        {
            kind = RowKind.Choice, label = "Écran", key = GameSettings.Monitor,
            description = "Écran sur lequel le jeu s'affiche",
            dynamicOptions = MonitorNames, getIndex = MonitorIndex, setIndex = SetMonitorIndex,
            isEnabled = () => SettingsApplier.Monitors().Count > 1,
            disabledHint = "Un seul écran est branché.",
            affectsDisplay = true
        });
        display.rows.Add(new RowDef
        {
            kind = RowKind.Choice, label = "Résolution", key = GameSettings.Resolution,
            description = "Définition de l'image en plein écran et en mode fenêtré",
            dynamicOptions = ResolutionNames, getIndex = ResolutionIndex, setIndex = SetResolutionIndex,
            isEnabled = () => GameSettings.GetInt(GameSettings.DisplayMode) != 1,
            disabledHint = "Le mode sans bordure utilise toujours la résolution du bureau.",
            affectsDisplay = true
        });
        display.rows.Add(Toggle("Synchronisation verticale", "Évite les déchirures de l'image. Limite la fluidité à la fréquence de l'écran.", GameSettings.VSync));
        display.rows.Add(new RowDef
        {
            kind = RowKind.Choice, label = "Limite d'images par seconde", key = GameSettings.FpsCap, options = FpsCapNames(),
            description = "Plafonne le nombre d'images affichées par seconde",
            isEnabled = () => !GameSettings.GetBool(GameSettings.VSync),
            disabledHint = "Sans effet tant que la synchronisation verticale est activée."
        });
        display.rows.Add(Slider("Luminosité", "Éclaircit ou assombrit toute l'image, menus compris.", GameSettings.Brightness, 0.50f, 1.20f));
        display.rows.Add(Toggle("Afficher les images par seconde", "Affiche le nombre d'images par seconde en haut à gauche.", GameSettings.ShowFps));
        display.rows.Add(new RowDef
        {
            kind = RowKind.Swatches, label = "Couleur du compteur d'images", key = GameSettings.FpsColor, swatches = GameSettings.FpsColors,
            description = "Couleur des images par seconde et des millisecondes affichées en haut à gauche.",
            isEnabled = () => GameSettings.GetBool(GameSettings.ShowFps),
            disabledHint = "Active d'abord l'affichage des images par seconde."
        });
        cats.Add(display);

        // ---------------------------------------------------------------- GRAPHISMES
        CategoryDef gfx = new CategoryDef { id = "graphics", title = "GRAPHISMES", subtitle = "Qualité de l'image et effets visuels" };
        gfx.rows.Add(Choice("Qualité graphique", "Préréglage global du rendu : éclairage, ombres et netteté.", GameSettings.Quality, QualityNames));
        gfx.rows.Add(Toggle("Ombres", "Ombres projetées par les personnages et les ennemis.", GameSettings.Shadows));
        gfx.rows.Add(new RowDef
        {
            kind = RowKind.Choice, label = "Effets d'ambiance des menus", key = GameSettings.MenuEffects, options = MenuEffectNames,
            description = "Poussières et halos lumineux animés dans les menus",
            getIndex = () => GameSettings.GetInt(GameSettings.MenuEffects) > 0 ? 1 : 0,
            setIndex = i => GameSettings.SetInt(GameSettings.MenuEffects, i == 0 ? 0 : 2)
        });
        cats.Add(gfx);

        // ---------------------------------------------------------------- AUDIO
        CategoryDef audio = new CategoryDef { id = "audio", title = "AUDIO", subtitle = "Volumes et comportement du son" };
        audio.rows.Add(Slider("Volume général", "Règle le volume de tout le jeu.", GameSettings.Master, 0f, 1f));
        audio.rows.Add(Slider("Musique", "Volume de la musique des menus et des parties.", GameSettings.Music, 0f, 1f));
        audio.rows.Add(Slider("Effets sonores", "Volume des bruitages : coups, sorts, interface.", GameSettings.Sfx, 0f, 1f));
        audio.rows.Add(Toggle("Couper le son en arrière-plan", "Coupe tout le son quand la fenêtre du jeu n'est pas active.", GameSettings.MuteInBackground));
        cats.Add(audio);

        // ---------------------------------------------------------------- JEU
        CategoryDef game = new CategoryDef { id = "gameplay", title = "JEU", subtitle = "Visée, pause et retours de combat" };
        game.rows.Add(new RowDef
        {
            kind = RowKind.Choice, label = "Visée", key = GameSettings.AutoAim,
            // MODIFIÉ (2026-10-29) - la visée manuelle se fait aussi au stick droit de la manette (viseur en partie : AimReticle)
            description = "Automatique : les tirs visent l'ennemi le plus proche. Manuelle : tu vises avec la souris ou le stick droit de la manette",
            options = new[] { "Manuelle", "Automatique" },
            getIndex = () => GameSettings.GetBool(GameSettings.AutoAim) ? 1 : 0,
            setIndex = i => GameSettings.SetBool(GameSettings.AutoAim, i == 1)
        });
        game.rows.Add(Toggle("Pause automatique", "Met la partie en pause quand la fenêtre du jeu n'est plus au premier plan.", GameSettings.PauseOnFocusLoss));
        game.rows.Add(Header("Nombres de dégâts"));
        game.rows.Add(Toggle("Dégâts infligés", "Affiche les dégâts infligés aux ennemis.", GameSettings.DamageDealt));
        game.rows.Add(Toggle("Dégâts subis", "Affiche les dégâts que reçoit le joueur.", GameSettings.DamageTaken));
        game.rows.Add(Slider("Taille des nombres", "Taille des chiffres qui apparaissent au-dessus des cibles.", GameSettings.DamageSize, 0.60f, 1.60f));
        cats.Add(game);

        // ---------------------------------------------------------------- INTERFACE
        CategoryDef ui = new CategoryDef { id = "interface", title = "INTERFACE", subtitle = "Ce qui s'affiche pendant la partie" };
        ui.rows.Add(Slider("Taille de l'interface", "Taille de l'affichage en partie : barres, jauges et textes.", GameSettings.HudScale, 0.80f, 1.20f));
        ui.rows.Add(Slider("Opacité de l'interface", "Transparence de l'affichage en partie.", GameSettings.HudOpacity, 0.40f, 1.00f));
        ui.rows.Add(Header("Éléments affichés"));
        ui.rows.Add(Toggle("Chronomètre", "Le temps écoulé depuis le début de la partie.", GameSettings.ShowTimer));
        ui.rows.Add(Toggle("Or de la partie", "L'or ramassé pendant cette partie.", GameSettings.ShowGold));
        ui.rows.Add(Toggle("Ennemis vaincus", "Le nombre d'ennemis éliminés.", GameSettings.ShowKills));
        ui.rows.Add(Toggle("Légende des touches", "Rappel des touches (dash, ultime, clone, orbites, pause) affiché à gauche de l'écran.", GameSettings.ShowKeyLegend));
        cats.Add(ui);

        // ---------------------------------------------------------------- COMMANDES
        CategoryDef controls = new CategoryDef { id = "controls", title = "COMMANDES", subtitle = "Clavier, souris et manette" };
        controls.rows.Add(Note("Cliquez sur une touche pour la modifier.   Échap : annuler   •   Retour arrière : effacer."));
        controls.rows.Add(new RowDef { kind = RowKind.Presets, label = "Disposition du clavier", description = "Règle le déplacement et les orbites. Les autres touches ne changent pas" });
        RowDef move = Header("Déplacement"); move.keyColumns = true; controls.rows.Add(move);   // titres des colonnes de touches
        controls.rows.Add(Key(GameAction.MoveUp));
        controls.rows.Add(Key(GameAction.MoveDown));
        controls.rows.Add(Key(GameAction.MoveLeft));
        controls.rows.Add(Key(GameAction.MoveRight));
        controls.rows.Add(Header("Combat"));
        controls.rows.Add(Key(GameAction.Dash));
        controls.rows.Add(Key(GameAction.Ultimate));
        controls.rows.Add(Key(GameAction.PhantomClone));
        controls.rows.Add(Header("Montée de niveau"));
        controls.rows.Add(Key(GameAction.Card1));
        controls.rows.Add(Key(GameAction.Card2));
        controls.rows.Add(Key(GameAction.Card3));
        controls.rows.Add(Header("Armes"));
        controls.rows.Add(Key(GameAction.OrbitShrink));
        controls.rows.Add(Key(GameAction.OrbitGrow));
        controls.rows.Add(Header("Système"));
        controls.rows.Add(Key(GameAction.Pause));
        controls.rows.Add(Header("Manette"));
        controls.rows.Add(Slider("Zone morte du stick", "Ignore les petits mouvements du stick. À augmenter si le personnage bouge sans action.", GameSettings.PadDeadZone, 0.05f, 0.50f));
        controls.rows.Add(Toggle("Vibrations", "Fait vibrer la manette aux coups reçus, au dash, à l'ultime et à la mort d'un boss.", GameSettings.PadRumble));
        controls.rows.Add(Slider("Intensité des vibrations", "Force des vibrations de la manette.", GameSettings.PadRumbleStrength, 0.20f, 1.00f));
        cats.Add(controls);

        // ---------------------------------------------------------------- LANGUE (AJOUTÉ 2026-10-14)
        // Titre bilingue : un joueur qui ne lit pas la langue affichée doit quand même trouver cet onglet.
        CategoryDef lang = new CategoryDef { id = "language", title = "LANGUE / LANGUAGE", subtitle = "Langue de tous les textes du jeu" };
        lang.rows.Add(new RowDef
        {
            kind = RowKind.Choice, label = "Langue du jeu", key = GameSettings.Language, options = LanguageNames,
            description = "Tous les textes du jeu changent immédiatement, menus et parties compris"
        });
        cats.Add(lang);

        // Tous les textes ci-dessus sont écrits en français : traduits ici d'un seul coup dans la langue courante (Loc).
        // Les libellés des touches viennent déjà traduits de GameInput.ActionLabel / ActionHint.
        foreach (CategoryDef c in cats)
        {
            c.title = Loc.T(c.title);
            c.subtitle = Loc.T(c.subtitle);
            foreach (RowDef r in c.rows)
            {
                if (r.kind != RowKind.Keybind)
                {
                    r.label = Loc.T(r.label);
                    r.description = Loc.T(r.description);
                }
                r.disabledHint = Loc.T(r.disabledHint);
                // AJOUTÉ (2026-10-25) - pas de point final sous les réglages, dans aucune langue (demande de l'utilisateur).
                r.description = NoFinalStop(r.description);
                r.disabledHint = NoFinalStop(r.disabledHint);
                if (r.kind == RowKind.Note) r.label = NoFinalStop(r.label);
                if (r.options != null && r.options != LanguageNames)
                    r.options = Array.ConvertAll(r.options, o => Loc.Has(o) ? Loc.T(o) : o);   // "60 FPS" reste tel quel
            }
        }
        return cats;
    }

    // Point final latin, chinois / japonais (。) ou coréen (．, voir Tools/loc/langs.js) ; les points de suspension restent.
    private static string NoFinalStop(string s)
    {
        if (string.IsNullOrEmpty(s) || s.EndsWith("...") || s.EndsWith("…")) return s;
        return s.TrimEnd().TrimEnd('.', '。', '．').TrimEnd();
    }

    // Noms des langues toujours écrits dans leur propre langue (jamais traduits).
    // Dans l'ordre de l'enum GameLanguage ; chaque langue est écrite dans sa propre langue (jamais traduite).
    public static readonly string[] LanguageNames = { "Français", "English", "Deutsch", "Español", "Português (Brasil)", "Italiano", "Polski", "Русский", "简体中文", "日本語", "한국어" };

    // Remet à zéro tout ce qu'une catégorie contient (réglages ET touches).
    public static void ResetCategory(CategoryDef cat)
    {
        foreach (RowDef r in cat.rows)
        {
            if (r.reset != null) { r.reset(); continue; }
            foreach (string k in r.Keys()) GameSettings.Reset(k);
            if (r.kind == RowKind.Keybind) InputBindings.Reset(r.action);
        }
        if (cat.id == "controls") InputBindings.ResetAll();
    }
}
