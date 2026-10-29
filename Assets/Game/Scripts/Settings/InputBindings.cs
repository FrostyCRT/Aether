using System;
using System.Collections.Generic;
using UnityEngine;

// AJOUTE (2026-09-19) - configuration des touches (page Paramètres > Commandes).
//
// Pourquoi : le jeu lisait des KeyCode en dur (LeftShift, F, C, A/E, 1/2/3, Échap) et un axe "Horizontal/Vertical"
// configuré pour AZERTY (ZQSD + flèches). Un joueur QWERTY ou gaucher n'avait aucun recours. Désormais chaque
// ACTION du jeu (GameAction) a DEUX emplacements de touche (principal / secondaire : clavier, souris ou bouton de
// manette), personnalisables, mémorisés (PlayerPrefs "bind_<Action>_<0|1>") et remis à zéro par préréglage.
//
// Lecture en jeu : voir GameInput (Held / Down / Move). Ce fichier ne contient que la DONNÉE :
// défauts, préréglages, lecture/écriture, détection de conflits.
public enum GameAction
{
    MoveUp, MoveDown, MoveLeft, MoveRight,
    Dash, Ultimate, PhantomClone,
    Card1, Card2, Card3,
    OrbitShrink, OrbitGrow,
    Pause,
}

public static class InputBindings
{
    public const int Slots = 2;

    // Ordre d'affichage dans la page Commandes.
    public static readonly GameAction[] All = (GameAction[])Enum.GetValues(typeof(GameAction));

    // ---- défauts (= comportement d'origine du jeu) -----------------------------------------------------------
    // MODIFIÉ (2026-10-28, session manette) - les 2 emplacements sont maintenant réservés au clavier / à la souris :
    // la manette a son propre emplacement (PadButton, voir plus bas), lu par le package Input System, qui reconnaît
    // les boutons de la même façon sur Xbox, PlayStation et Switch (l'ancien système numérotait JoystickButtonN
    // différemment selon la marque). Les anciens défauts manette (JoystickButton0/1/2/7) sont devenus Espace (dash) ou
    // vides ; une touche JoystickButton encore enregistrée est ignorée au chargement.
    //
    // MODIFIÉ (2026-10-14, demande utilisateur) : la disposition par défaut suit la langue de Windows - Windows en
    // français -> AZERTY (ZQSD, orbites A/E), sinon QWERTY (WASD, orbites Q/E). Seuls le déplacement et les orbites
    // changent (exactement le périmètre des préréglages). Un joueur qui a déjà personnalisé ses touches les garde.
    public static bool SystemIsAzerty => Application.systemLanguage == SystemLanguage.French;

    private static Dictionary<GameAction, KeyCode[]> BuildDefaults()
    {
        var d = new Dictionary<GameAction, KeyCode[]>(AzertyDefaults);
        if (!SystemIsAzerty)
            foreach (GameAction a in PresetActionsStatic) d[a] = PresetKeys(Preset.Qwerty, a);
        return d;
    }

    private static readonly GameAction[] PresetActionsStatic =
    {
        GameAction.MoveUp, GameAction.MoveDown, GameAction.MoveLeft, GameAction.MoveRight,
        GameAction.OrbitShrink, GameAction.OrbitGrow,
    };

    private static readonly Dictionary<GameAction, KeyCode[]> AzertyDefaults = new Dictionary<GameAction, KeyCode[]>
    {
        { GameAction.MoveUp,       new[] { KeyCode.Z,          KeyCode.UpArrow } },
        { GameAction.MoveDown,     new[] { KeyCode.S,          KeyCode.DownArrow } },
        { GameAction.MoveLeft,     new[] { KeyCode.Q,          KeyCode.LeftArrow } },
        { GameAction.MoveRight,    new[] { KeyCode.D,          KeyCode.RightArrow } },
        { GameAction.Dash,         new[] { KeyCode.LeftShift,  KeyCode.Space } },
        { GameAction.Ultimate,     new[] { KeyCode.F,          KeyCode.None } },
        { GameAction.PhantomClone, new[] { KeyCode.C,          KeyCode.None } },
        { GameAction.Card1,        new[] { KeyCode.Alpha1,     KeyCode.Keypad1 } },
        { GameAction.Card2,        new[] { KeyCode.Alpha2,     KeyCode.Keypad2 } },
        { GameAction.Card3,        new[] { KeyCode.Alpha3,     KeyCode.Keypad3 } },
        { GameAction.OrbitShrink,  new[] { KeyCode.A,          KeyCode.None } },
        { GameAction.OrbitGrow,    new[] { KeyCode.E,          KeyCode.None } },
        { GameAction.Pause,        new[] { KeyCode.Escape,     KeyCode.None } },
    };

    // Déclaré APRÈS AzertyDefaults / PresetActionsStatic : les initialiseurs statiques s'exécutent dans l'ordre du fichier.
    private static readonly Dictionary<GameAction, KeyCode[]> Defaults = BuildDefaults();

    // ---- préréglages de déplacement ------------------------------------------------------------------
    // Un préréglage fixe le déplacement ET les deux touches d'orbite (sinon "A" gauche du QWERTY entrerait en
    // conflit avec l'orbite). Les autres actions ne bougent pas.
    public enum Preset { Azerty, Qwerty, Arrows }

    public static readonly string[] PresetNames = { "AZERTY (ZQSD)", "QWERTY (WASD)", "Flèches seules" };

    private static KeyCode[] PresetKeys(Preset p, GameAction a)
    {
        switch (p)
        {
            case Preset.Qwerty:
                switch (a)
                {
                    case GameAction.MoveUp: return new[] { KeyCode.W, KeyCode.UpArrow };
                    case GameAction.MoveDown: return new[] { KeyCode.S, KeyCode.DownArrow };
                    case GameAction.MoveLeft: return new[] { KeyCode.A, KeyCode.LeftArrow };
                    case GameAction.MoveRight: return new[] { KeyCode.D, KeyCode.RightArrow };
                    case GameAction.OrbitShrink: return new[] { KeyCode.Q, KeyCode.None };
                    case GameAction.OrbitGrow: return new[] { KeyCode.E, KeyCode.None };
                }
                break;
            case Preset.Arrows:
                switch (a)
                {
                    case GameAction.MoveUp: return new[] { KeyCode.UpArrow, KeyCode.None };
                    case GameAction.MoveDown: return new[] { KeyCode.DownArrow, KeyCode.None };
                    case GameAction.MoveLeft: return new[] { KeyCode.LeftArrow, KeyCode.None };
                    case GameAction.MoveRight: return new[] { KeyCode.RightArrow, KeyCode.None };
                    case GameAction.OrbitShrink: return new[] { KeyCode.A, KeyCode.None };
                    case GameAction.OrbitGrow: return new[] { KeyCode.E, KeyCode.None };
                }
                break;
        }
        return AzertyDefaults[a];   // AZERTY = défauts d'origine du jeu
    }

    private static readonly GameAction[] PresetActions =
    {
        GameAction.MoveUp, GameAction.MoveDown, GameAction.MoveLeft, GameAction.MoveRight,
        GameAction.OrbitShrink, GameAction.OrbitGrow,
    };

    // ---- état ----------------------------------------------------------------------------------------------
    private static KeyCode[][] _current;      // [action][slot], chargé paresseusement
    private static readonly PadButton[] _pad = new PadButton[All.Length];   // emplacement manette, chargé avec _current
    public static event Action<GameAction> Changed;

    private static string PrefKey(GameAction a, int slot) => "bind_" + a + "_" + slot;

    private static void EnsureLoaded()
    {
        if (_current != null) return;
        _current = new KeyCode[All.Length][];
        foreach (GameAction a in All)
        {
            KeyCode[] d = Defaults[a];
            KeyCode[] v = new KeyCode[Slots];
            for (int s = 0; s < Slots; s++)
            {
                string k = PrefKey(a, s);
                // absent = défaut ; présent (même 0) = choix explicite du joueur ("effacé" = None)
                v[s] = PlayerPrefs.HasKey(k) ? (KeyCode)PlayerPrefs.GetInt(k) : d[s];
                if (IsJoystickKey(v[s])) v[s] = KeyCode.None;   // ancien bouton de manette : remplacé par l'emplacement manette
            }
            _current[(int)a] = v;
            _pad[(int)a] = PlayerPrefs.HasKey(PadPrefKey(a)) ? (PadButton)PlayerPrefs.GetInt(PadPrefKey(a)) : PadDefaults[a];
        }
    }

    public static bool IsJoystickKey(KeyCode k) => k >= KeyCode.JoystickButton0 && k <= KeyCode.Joystick8Button19;

    public static KeyCode Get(GameAction a, int slot)
    {
        EnsureLoaded();
        return _current[(int)a][slot];
    }

    public static KeyCode GetDefault(GameAction a, int slot) => Defaults[a][slot];

    public static bool IsDefault(GameAction a)
    {
        for (int s = 0; s < Slots; s++)
            if (Get(a, s) != Defaults[a][s]) return false;
        return GetPad(a) == PadDefaults[a];
    }

    // ---- écriture --------------------------------------------------------------------------------------------
    public struct RebindResult
    {
        public bool applied;
        public bool blocked;             // refusé : l'autre action se retrouverait sans aucune touche
        public bool hasDisplaced;
        public GameAction displaced;     // action à qui on a pris la touche
        public int displacedSlot;
        public KeyCode key;
    }

    // Assigne `key` à (action, slot). Si la touche servait déjà à une AUTRE action, elle lui est retirée (ou
    // échangée contre l'ancienne touche de cet emplacement) : deux actions ne partagent jamais une touche.
    // KeyCode.None = effacer l'emplacement (refusé s'il ne resterait plus aucune touche : action inutilisable).
    public static RebindResult Rebind(GameAction action, int slot, KeyCode key)
    {
        EnsureLoaded();
        RebindResult r = new RebindResult { key = key };

        KeyCode previous = _current[(int)action][slot];
        if (key == KeyCode.None)
        {
            int other = 1 - slot;
            if (_current[(int)action][other] == KeyCode.None) return r;   // dernière touche : on ne l'efface pas
            Store(action, slot, KeyCode.None);
            r.applied = true;
            Changed?.Invoke(action);
            return r;
        }

        // même touche déjà sur cet emplacement : rien à faire
        if (previous == key) { r.applied = true; return r; }

        // la même touche sur l'AUTRE emplacement de la même action : on la déplace ici (pas de doublon)
        int otherSlot = 1 - slot;
        if (_current[(int)action][otherSlot] == key)
            Store(action, otherSlot, KeyCode.None);

        // refus si prendre la touche laisserait une autre action SANS aucune touche (previous vide + autre emplacement vide)
        foreach (GameAction b in All)
        {
            if (b == action) continue;
            for (int s = 0; s < Slots; s++)
                if (_current[(int)b][s] == key && previous == KeyCode.None && _current[(int)b][1 - s] == KeyCode.None)
                {
                    r.blocked = true; r.displaced = b; r.displacedSlot = s;
                    return r;
                }
        }

        // conflit avec une autre action ?
        foreach (GameAction b in All)
        {
            if (b == action) continue;
            for (int s = 0; s < Slots; s++)
            {
                if (_current[(int)b][s] != key) continue;
                // échange : l'autre action récupère l'ancienne touche de cet emplacement (si elle en avait une)
                Store(b, s, previous);
                r.hasDisplaced = true; r.displaced = b; r.displacedSlot = s;
                Changed?.Invoke(b);
            }
        }

        Store(action, slot, key);
        r.applied = true;
        Changed?.Invoke(action);
        return r;
    }

    private static void Store(GameAction a, int slot, KeyCode key)
    {
        _current[(int)a][slot] = key;
        PlayerPrefs.SetInt(PrefKey(a, slot), (int)key);
        _dirty = true;
    }

    private static bool _dirty;

    public static void Flush()
    {
        if (!_dirty) return;
        _dirty = false;
        PlayerPrefs.Save();
    }

    // Remet UNE action à ses touches d'origine. Si une de ces touches sert maintenant à une autre action, elle lui est
    // reprise (même règle qu'un réassignement manuel).
    public static void Reset(GameAction a)
    {
        EnsureLoaded();
        for (int s = 0; s < Slots; s++)
        {
            KeyCode d = Defaults[a][s];
            if (d == KeyCode.None) Store(a, s, KeyCode.None);
            else Rebind(a, s, d);
        }
        RebindPad(a, PadDefaults[a]);
        Changed?.Invoke(a);
    }

    // Tout remettre d'origine (les défauts n'ont aucun conflit entre eux : on repart de zéro, sans arbitrage).
    public static void ResetAll()
    {
        EnsureLoaded();
        foreach (GameAction a in All)
            for (int s = 0; s < Slots; s++)
            {
                PlayerPrefs.DeleteKey(PrefKey(a, s));
                _current[(int)a][s] = Defaults[a][s];
            }
        foreach (GameAction a in All)
        {
            PlayerPrefs.DeleteKey(PadPrefKey(a));
            _pad[(int)a] = PadDefaults[a];
        }
        _dirty = true;
        foreach (GameAction a in All) Changed?.Invoke(a);
    }

    public static void ApplyPreset(Preset p)
    {
        EnsureLoaded();
        // 1) on efface d'abord tout ce que le préréglage va redéfinir (évite les conflits intermédiaires)
        foreach (GameAction a in PresetActions)
            for (int s = 0; s < Slots; s++) Store(a, s, KeyCode.None);
        // 2) puis on pose les nouvelles touches ; une touche déjà prise par une action hors préréglage lui est retirée
        foreach (GameAction a in PresetActions)
        {
            KeyCode[] keys = PresetKeys(p, a);
            for (int s = 0; s < Slots; s++)
            {
                KeyCode k = keys[s];
                if (k == KeyCode.None) continue;
                foreach (GameAction b in All)
                {
                    if (Array.IndexOf(PresetActions, b) >= 0) continue;
                    for (int t = 0; t < Slots; t++)
                        if (_current[(int)b][t] == k) Store(b, t, KeyCode.None);
                }
                Store(a, s, k);
            }
        }
        foreach (GameAction a in PresetActions) Changed?.Invoke(a);
    }

    // Quel préréglage correspond aux touches actuelles (ou -1 si personnalisé) : pour surligner le bouton.
    public static int MatchingPreset()
    {
        EnsureLoaded();
        for (int p = 0; p < PresetNames.Length; p++)
        {
            bool same = true;
            foreach (GameAction a in PresetActions)
            {
                KeyCode[] keys = PresetKeys((Preset)p, a);
                for (int s = 0; s < Slots; s++)
                    if (_current[(int)a][s] != keys[s]) { same = false; break; }
                if (!same) break;
            }
            if (same) return p;
        }
        return -1;
    }

    // Action qui utilise une touche (ou null) : pour les messages de conflit.
    public static GameAction? FindOwner(KeyCode key, GameAction? except = null)
    {
        EnsureLoaded();
        foreach (GameAction a in All)
        {
            if (except.HasValue && a == except.Value) continue;
            for (int s = 0; s < Slots; s++)
                if (_current[(int)a][s] == key) return a;
        }
        return null;
    }

    // ---- manette ------------------------------------------------------------------------------------------------
    // AJOUTÉ (2026-10-28, session manette) - UN bouton de manette par action, à part des 2 emplacements clavier/souris.
    // Le déplacement n'en a pas : stick gauche + croix directionnelle, fixes (GameInput.Move). Les cartes de montée
    // de niveau non plus : à la manette on les choisit en naviguant (croix/stick + A), comme tous les menus.
    // Défauts au format Xbox (A = bas, B = droite, X = gauche, Y = haut) : sur PlayStation c'est la même position
    // physique (Croix, Rond, Carré, Triangle), l'icône affichée suit la manette branchée (GameInput.PadStyle).
    private static readonly Dictionary<GameAction, PadButton> PadDefaults = new Dictionary<GameAction, PadButton>
    {
        { GameAction.MoveUp,       PadButton.None },
        { GameAction.MoveDown,     PadButton.None },
        { GameAction.MoveLeft,     PadButton.None },
        { GameAction.MoveRight,    PadButton.None },
        { GameAction.Dash,         PadButton.South },
        { GameAction.Ultimate,     PadButton.North },
        { GameAction.PhantomClone, PadButton.West },
        { GameAction.Card1,        PadButton.None },
        { GameAction.Card2,        PadButton.None },
        { GameAction.Card3,        PadButton.None },
        { GameAction.OrbitShrink,  PadButton.LeftShoulder },
        { GameAction.OrbitGrow,    PadButton.RightShoulder },
        { GameAction.Pause,        PadButton.Start },
    };

    // Actions qui acceptent un bouton de manette (page Commandes : les autres affichent « — » sans capture).
    public static bool PadBindable(GameAction a) =>
        a != GameAction.MoveUp && a != GameAction.MoveDown && a != GameAction.MoveLeft && a != GameAction.MoveRight
        && a != GameAction.Card1 && a != GameAction.Card2 && a != GameAction.Card3;

    private static string PadPrefKey(GameAction a) => "padbind_" + a;

    public static PadButton GetPad(GameAction a)
    {
        EnsureLoaded();
        return _pad[(int)a];
    }

    public static PadButton GetPadDefault(GameAction a) => PadDefaults[a];

    // Même règle que le clavier : un bouton ne sert qu'à une action ; s'il était pris, l'autre action récupère
    // l'ancien bouton de celle-ci (échange). Renvoie l'action déplacée, s'il y en a une.
    public static GameAction? RebindPad(GameAction action, PadButton button)
    {
        EnsureLoaded();
        PadButton previous = _pad[(int)action];
        if (previous == button) return null;
        GameAction? displaced = null;
        if (button != PadButton.None)
            foreach (GameAction b in All)
            {
                if (b == action || _pad[(int)b] != button) continue;
                StorePad(b, previous);
                displaced = b;
                Changed?.Invoke(b);
            }
        StorePad(action, button);
        Changed?.Invoke(action);
        return displaced;
    }

    private static void StorePad(GameAction a, PadButton b)
    {
        _pad[(int)a] = b;
        PlayerPrefs.SetInt(PadPrefKey(a), (int)b);
        _dirty = true;
    }
}

// Boutons de manette assignables (positions physiques, indépendantes de la marque). La croix directionnelle n'en
// fait pas partie : elle déplace le personnage et navigue dans les menus.
public enum PadButton
{
    None,
    South, East, West, North,
    LeftShoulder, RightShoulder, LeftTrigger, RightTrigger,
    Start, Select,
    LeftStickPress, RightStickPress,
}
