using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

// AJOUTE (2026-09-19) - lecture des commandes du joueur À TRAVERS la configuration des touches (InputBindings).
// Le code du jeu n'appelle plus Input.GetKey(KeyCode.X) : il demande GameInput.Down(GameAction.Dash), etc.
//
//   GameInput.Held(action)   touche maintenue (l'un ou l'autre des 2 emplacements, ou le bouton de manette)
//   GameInput.Down(action)   touche enfoncée CETTE image
//   GameInput.Move()         vecteur de déplacement (clavier + stick gauche + croix), norme <= 1
//   GameInput.Aim()          visée au stick droit (zéro sous la zone morte)
//
// MODIFIÉ (2026-10-28, session manette) - la manette passe par le package Input System (Gamepad.current) au lieu des
// axes "PadMoveX/PadMoveY" et des JoystickButtonN de l'ancien système : mêmes boutons sur Xbox, PlayStation et
// Switch, et on sait quelle manette est branchée (PadStyle, pour les icônes). Le clavier et la souris restent lus par
// l'ancien système (Active Input Handling = Both), leurs KeyCode sont ceux de la page Commandes.
// La zone morte des sticks est un réglage du joueur (GameSettings.PadDeadZone), appliquée sur la valeur brute.
public static class GameInput
{
    // AJOUTÉ (2026-10-03) - même esprit que DebugCheats.cs (F6/F8) : permet à un script de test (ex. un "bot" de
    // playtest automatisé pour juger l'équilibrage) de piloter le déplacement sans clavier, sans toucher à
    // PlayerController. Ignoré par défaut (null) ; jamais actif en dehors de l'Éditeur/d'un build de développement.
    public static Vector2? DebugMoveOverride = null;

    public static bool Held(GameAction action)
    {
        for (int s = 0; s < InputBindings.Slots; s++)
        {
            KeyCode k = InputBindings.Get(action, s);
            if (k != KeyCode.None && Input.GetKey(k)) return true;
        }
        ButtonControl b = PadControl(InputBindings.GetPad(action));
        return b != null && b.isPressed;
    }

    public static bool Down(GameAction action)
    {
        for (int s = 0; s < InputBindings.Slots; s++)
        {
            KeyCode k = InputBindings.Get(action, s);
            if (k != KeyCode.None && Input.GetKeyDown(k)) return true;
        }
        ButtonControl b = PadControl(InputBindings.GetPad(action));
        return b != null && b.wasPressedThisFrame;
    }

    public static Vector2 Move()
    {
        if (DebugMoveOverride.HasValue && (Application.isEditor || Debug.isDebugBuild))
            return DebugMoveOverride.Value;

        float x = (Held(GameAction.MoveRight) ? 1f : 0f) - (Held(GameAction.MoveLeft) ? 1f : 0f);
        float y = (Held(GameAction.MoveUp) ? 1f : 0f) - (Held(GameAction.MoveDown) ? 1f : 0f);
        Vector2 v = new Vector2(x, y);

        Gamepad g = Gamepad.current;
        if (g != null)
        {
            v += Stick(g.leftStick);
            v += g.dpad.ReadValue();
        }

        return v.sqrMagnitude > 1f ? v.normalized : v;
    }

    // Visée manuelle à la manette (WeaponBase, quand la visée automatique est coupée).
    public static Vector2 Aim()
    {
        Gamepad g = Gamepad.current;
        return g != null ? Stick(g.rightStick) : Vector2.zero;
    }

    // Zone morte radiale sur la valeur BRUTE (sans le traitement par défaut d'Input System), sortie remise à l'échelle.
    internal static Vector2 Stick(StickControl stick)
    {
        Vector2 raw = stick.ReadUnprocessedValue();
        float dz = GameSettings.GetFloat(GameSettings.PadDeadZone);
        float mag = raw.magnitude;
        if (mag <= dz) return Vector2.zero;
        return raw / mag * Mathf.Clamp01((mag - dz) / (1f - dz));
    }

    public static ButtonControl PadControl(PadButton b)
    {
        Gamepad g = Gamepad.current;
        if (g == null) return null;
        switch (b)
        {
            case PadButton.South: return g.buttonSouth;
            case PadButton.East: return g.buttonEast;
            case PadButton.West: return g.buttonWest;
            case PadButton.North: return g.buttonNorth;
            case PadButton.LeftShoulder: return g.leftShoulder;
            case PadButton.RightShoulder: return g.rightShoulder;
            case PadButton.LeftTrigger: return g.leftTrigger;
            case PadButton.RightTrigger: return g.rightTrigger;
            case PadButton.Start: return g.startButton;
            case PadButton.Select: return g.selectButton;
            case PadButton.LeftStickPress: return g.leftStickButton;
            case PadButton.RightStickPress: return g.rightStickButton;
        }
        return null;
    }

    public static bool PadDown(PadButton b)
    {
        ButtonControl c = PadControl(b);
        return c != null && c.wasPressedThisFrame;
    }

    // N'importe quelle touche, bouton de souris ou bouton de manette (écrans « appuie sur une touche »). L'ancien
    // système ne voit pas toutes les manettes lues par Input System : les deux sont interrogés.
    public static bool AnyDown()
    {
        if (Input.anyKeyDown) return true;
        for (int i = 1; i < AllPadButtons.Length; i++)
            if (PadDown(AllPadButtons[i])) return true;
        return false;
    }

    // « Retour » dans les menus : Échap au clavier, B (Rond sur PlayStation) à la manette. Le B qui fait sortir du mode
    // défilement (voir PadCursor) ne compte pas : il ne doit pas aussi fermer la page.
    public static bool BackDown() => Input.GetKeyDown(KeyCode.Escape) || PadBackDown();
    public static bool PadBackDown() => PadDown(PadButton.East) && PadCursor.BackConsumedFrame != Time.frameCount;

    // Position du pointeur à l'écran (pixels) : le curseur rond de la manette quand il est actif, sinon la souris.
    public static Vector2 PointerPosition => PadCursor.Active ? PadCursor.Position : (Vector2)Input.mousePosition;

    // Clic gauche de la souris ou clic du curseur de la manette (A), cette image.
    public static bool PointerClickDown() => Input.GetMouseButtonDown(0) || PadCursor.ClickedThisFrame;

    // ---- appareil utilisé ---------------------------------------------------------------------------------------
    // AJOUTÉ (2026-10-28, session manette) - dernier appareil touché par le joueur : manette ou clavier/souris.
    // Sert aux icônes de touches (HUD, légendes), au curseur (masqué à la manette) et à la visée manuelle.
    // Mis à jour une fois par image par InputDeviceTracker.
    public enum PadKind { Xbox, PlayStation, Nintendo }

    public static bool UsingGamepad { get; private set; }
    public static PadKind PadStyle { get; private set; }
    public static event Action DeviceChanged;

    private static readonly PadButton[] AllPadButtons = (PadButton[])Enum.GetValues(typeof(PadButton));
    private const float StickWake = 0.5f;   // un stick ne « prend la main » qu'au-delà de ce seuil (dérive)
    private const float MouseWake = 4f;     // pixels de déplacement souris par image pour revenir au clavier/souris

    internal static void TrackDevice()
    {
        bool pad = UsingGamepad;
        PadKind style = PadStyle;

        // Style des icônes : celui de la manette branchée, dès sa détection (au lancement, au branchement, au
        // changement de manette), sans attendre qu'on la touche ; Xbox par défaut s'il n'y en a aucune.
        Gamepad g = Gamepad.current;
        if (g != _styledPad)
        {
            _styledPad = g;
            _styledKind = g != null ? StyleOf(g) : PadKind.Xbox;
        }
        style = _styledKind;
        if (g != null)
        {
            bool touched = g.leftStick.ReadUnprocessedValue().sqrMagnitude > StickWake * StickWake
                || g.rightStick.ReadUnprocessedValue().sqrMagnitude > StickWake * StickWake
                || g.dpad.ReadValue().sqrMagnitude > 0f;
            for (int i = 1; !touched && i < AllPadButtons.Length; i++)
                touched = PadControl(AllPadButtons[i]).wasPressedThisFrame;
            if (touched) pad = true;
        }

        Keyboard kb = Keyboard.current;
        if ((kb != null && kb.anyKey.wasPressedThisFrame) || RealMouseUsed())
            pad = false;

        if (pad == UsingGamepad && style == PadStyle) return;
        UsingGamepad = pad;
        PadStyle = style;
        DeviceChanged?.Invoke();
    }

    // Vraie souris seulement : la souris virtuelle du curseur de manette (PadCursor) bouge aussi, elle ne doit pas
    // faire repasser le jeu en mode clavier / souris.
    private static bool RealMouseUsed()
    {
        Mouse virtualMouse = PadCursor.VirtualMouse;
        foreach (InputDevice d in InputSystem.devices)
        {
            if (!(d is Mouse m) || m == virtualMouse) continue;
            if (m.delta.ReadValue().sqrMagnitude > MouseWake * MouseWake
                || m.leftButton.wasPressedThisFrame || m.rightButton.wasPressedThisFrame) return true;
        }
        return false;
    }

    private static Gamepad _styledPad;
    private static PadKind _styledKind = PadKind.Xbox;

    private static PadKind StyleOf(Gamepad g)
    {
        if (g is UnityEngine.InputSystem.DualShock.DualShockGamepad) return PadKind.PlayStation;   // DualShock 4 + DualSense
        string layout = g.layout ?? "";
        if (layout.IndexOf("Switch", StringComparison.OrdinalIgnoreCase) >= 0) return PadKind.Nintendo;
        return PadKind.Xbox;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        UsingGamepad = false;
        PadStyle = PadKind.Xbox;
        _styledPad = null;
        _styledKind = PadKind.Xbox;
        DeviceChanged = null;
    }

    // ---- capture d'une touche (page Commandes) ------------------------------------------------------------
    private static readonly KeyCode[] AllKeys = (KeyCode[])Enum.GetValues(typeof(KeyCode));

    public enum Capture { None, Key, Cancel, Clear }

    // À appeler chaque image pendant l'attente d'une touche. Échap = annuler, Retour arrière / Suppr = effacer.
    // Le clic gauche n'est jamais une touche assignable (c'est lui qui a ouvert la capture / sert à la fermer).
    public static Capture PollCapture(out KeyCode key)
    {
        key = KeyCode.None;
        if (!Input.anyKeyDown) return Capture.None;

        if (Input.GetKeyDown(KeyCode.Escape)) return Capture.Cancel;
        if (Input.GetKeyDown(KeyCode.Backspace) || Input.GetKeyDown(KeyCode.Delete)) return Capture.Clear;

        foreach (KeyCode k in AllKeys)
        {
            if (k == KeyCode.None || k == KeyCode.Mouse0 || InputBindings.IsJoystickKey(k)) continue;   // manette : PollPadCapture
            if (!Input.GetKeyDown(k)) continue;
            key = k;
            return Capture.Key;
        }
        return Capture.None;
    }

    // Capture d'un bouton de manette (colonne Manette de la page Commandes) : le premier bouton assignable pressé.
    public static bool PollPadCapture(out PadButton button)
    {
        for (int i = 1; i < AllPadButtons.Length; i++)
        {
            if (!PadDown(AllPadButtons[i])) continue;
            button = AllPadButtons[i];
            return true;
        }
        button = PadButton.None;
        return false;
    }

    // Nom d'un bouton de manette selon la marque branchée (positions physiques : South = A Xbox = Croix PS = B Switch).
    public static string PadLabel(PadButton b) => PadLabel(b, PadStyle);

    public static string PadLabel(PadButton b, PadKind kind)
    {
        switch (kind)
        {
            case PadKind.PlayStation:
                switch (b)
                {
                    case PadButton.South: return Loc.T("Croix");
                    case PadButton.East: return Loc.T("Rond");
                    case PadButton.West: return Loc.T("Carré");
                    case PadButton.North: return Loc.T("Triangle");
                    case PadButton.LeftShoulder: return "L1";
                    case PadButton.RightShoulder: return "R1";
                    case PadButton.LeftTrigger: return "L2";
                    case PadButton.RightTrigger: return "R2";
                    case PadButton.Start: return "Options";
                    case PadButton.Select: return "Create";
                    case PadButton.LeftStickPress: return "L3";
                    case PadButton.RightStickPress: return "R3";
                }
                break;
            case PadKind.Nintendo:
                switch (b)
                {
                    case PadButton.South: return "B";
                    case PadButton.East: return "A";
                    case PadButton.West: return "Y";
                    case PadButton.North: return "X";
                    case PadButton.LeftShoulder: return "L";
                    case PadButton.RightShoulder: return "R";
                    case PadButton.LeftTrigger: return "ZL";
                    case PadButton.RightTrigger: return "ZR";
                    case PadButton.Start: return "+";
                    case PadButton.Select: return "−";
                    case PadButton.LeftStickPress: return "L3";
                    case PadButton.RightStickPress: return "R3";
                }
                break;
            default:
                switch (b)
                {
                    case PadButton.South: return "A";
                    case PadButton.East: return "B";
                    case PadButton.West: return "X";
                    case PadButton.North: return "Y";
                    case PadButton.LeftShoulder: return "LB";
                    case PadButton.RightShoulder: return "RB";
                    case PadButton.LeftTrigger: return "LT";
                    case PadButton.RightTrigger: return "RT";
                    case PadButton.Start: return "Menu";
                    case PadButton.Select: return "View";
                    case PadButton.LeftStickPress: return "LS";
                    case PadButton.RightStickPress: return "RS";
                }
                break;
        }
        return "—";
    }

    // ---- libellés (français, traduits par Loc) ---------------------------------------------------------------------------------
    public static string KeyLabel(KeyCode k)
    {
        switch (k)
        {
            case KeyCode.None: return "—";
            case KeyCode.LeftShift: return Loc.T("Maj gauche");
            case KeyCode.RightShift: return Loc.T("Maj droite");
            case KeyCode.LeftControl: return Loc.T("Ctrl gauche");
            case KeyCode.RightControl: return Loc.T("Ctrl droite");
            case KeyCode.LeftAlt: return Loc.T("Alt gauche");
            case KeyCode.RightAlt: return Loc.T("Alt droite");
            case KeyCode.LeftCommand: return Loc.T("Cmd gauche");
            case KeyCode.RightCommand: return Loc.T("Cmd droite");
            case KeyCode.Space: return Loc.T("Espace");
            case KeyCode.Return: return Loc.T("Entrée");
            case KeyCode.KeypadEnter: return Loc.T("Entrée (pavé)");
            case KeyCode.Escape: return Loc.T("Échap");
            case KeyCode.Backspace: return Loc.T("Retour arrière");
            case KeyCode.Delete: return Loc.T("Suppr");
            case KeyCode.Insert: return Loc.T("Inser");
            case KeyCode.Tab: return Loc.T("Tab");
            case KeyCode.CapsLock: return Loc.T("Verr. maj");
            case KeyCode.Home: return Loc.T("Début");
            case KeyCode.End: return Loc.T("Fin");
            case KeyCode.PageUp: return Loc.T("Page haut");
            case KeyCode.PageDown: return Loc.T("Page bas");
            case KeyCode.UpArrow: return Loc.T("Flèche haut");
            case KeyCode.DownArrow: return Loc.T("Flèche bas");
            case KeyCode.LeftArrow: return Loc.T("Flèche gauche");
            case KeyCode.RightArrow: return Loc.T("Flèche droite");
            case KeyCode.Comma: return Loc.T("Virgule");
            case KeyCode.Period: return Loc.T("Point");
            case KeyCode.Semicolon: return Loc.T("Point-virgule");
            case KeyCode.Colon: return Loc.T("Deux-points");
            case KeyCode.Slash: return Loc.T("Barre oblique");
            case KeyCode.Backslash: return Loc.T("Barre inverse");
            case KeyCode.Minus: return Loc.T("Moins");
            case KeyCode.Equals: return Loc.T("Égal");
            case KeyCode.Quote: return Loc.T("Apostrophe");
            case KeyCode.DoubleQuote: return Loc.T("Guillemet");
            case KeyCode.BackQuote: return Loc.T("Accent grave");
            case KeyCode.LeftBracket: return Loc.T("Crochet gauche");
            case KeyCode.RightBracket: return Loc.T("Crochet droit");
            case KeyCode.KeypadPlus: return Loc.T("Pavé +");
            case KeyCode.KeypadMinus: return Loc.T("Pavé −");
            case KeyCode.KeypadMultiply: return Loc.T("Pavé ×");
            case KeyCode.KeypadDivide: return Loc.T("Pavé ÷");
            case KeyCode.KeypadPeriod: return Loc.T("Pavé .");
            case KeyCode.Mouse0: return Loc.T("Clic gauche");
            case KeyCode.Mouse1: return Loc.T("Clic droit");
            case KeyCode.Mouse2: return Loc.T("Clic molette");
        }

        int v = (int)k;
        if (k >= KeyCode.Alpha0 && k <= KeyCode.Alpha9) return ((char)('0' + (v - (int)KeyCode.Alpha0))).ToString();
        if (k >= KeyCode.Keypad0 && k <= KeyCode.Keypad9) return Loc.F("Pavé {0}", v - (int)KeyCode.Keypad0);
        if (k >= KeyCode.Mouse3 && k <= KeyCode.Mouse6) return Loc.F("Souris {0}", v - (int)KeyCode.Mouse0 + 1);
        if (k >= KeyCode.JoystickButton0 && k <= KeyCode.JoystickButton19) return Loc.F("Manette {0}", v - (int)KeyCode.JoystickButton0);

        string name = k.ToString();
        if (name.StartsWith("Joystick")) return Loc.T("Manette");   // boutons d'une manette précise (Joystick2Button3...) : libellé court
        return name.Length == 1 ? name.ToUpperInvariant() : name;
    }

    // Libellé très court d'une touche, pour une pastille de l'interface (ex. l'icône du clone dans le HUD) :
    // une lettre / un chiffre tel quel ; « Maj », « Ctrl », « Alt », « Esp », « Tab », « ↵ », flèches, « M1 » (souris),
    // « B3 » (manette).
    public static string ShortKeyLabel(KeyCode k)
    {
        switch (k)
        {
            case KeyCode.None: return "";
            case KeyCode.LeftShift: case KeyCode.RightShift: return Loc.T("Maj");
            case KeyCode.LeftControl: case KeyCode.RightControl: return "Ctrl";
            case KeyCode.LeftAlt: case KeyCode.RightAlt: return "Alt";
            case KeyCode.LeftCommand: case KeyCode.RightCommand: return "Cmd";
            case KeyCode.Space: return Loc.T("Esp");
            case KeyCode.Return: case KeyCode.KeypadEnter: return "↵";
            case KeyCode.Escape: return Loc.T("Échap");
            case KeyCode.Backspace: return "⌫";
            case KeyCode.Tab: return "Tab";
            case KeyCode.UpArrow: return "↑";
            case KeyCode.DownArrow: return "↓";
            case KeyCode.LeftArrow: return "←";
            case KeyCode.RightArrow: return "→";
            case KeyCode.Mouse0: return "M1";
            case KeyCode.Mouse1: return "M2";
            case KeyCode.Mouse2: return "M3";
        }
        int v = (int)k;
        if (k >= KeyCode.Alpha0 && k <= KeyCode.Alpha9) return ((char)('0' + (v - (int)KeyCode.Alpha0))).ToString();
        if (k >= KeyCode.Keypad0 && k <= KeyCode.Keypad9) return "P" + (v - (int)KeyCode.Keypad0);
        if (k >= KeyCode.Mouse3 && k <= KeyCode.Mouse6) return "M" + (v - (int)KeyCode.Mouse0 + 1);
        if (k >= KeyCode.JoystickButton0 && k <= KeyCode.JoystickButton19) return "B" + (v - (int)KeyCode.JoystickButton0);
        string full = KeyLabel(k);
        return full.Length <= 4 ? full : full.Substring(0, 4);
    }

    // Touche à afficher pour une action : celle du 1er emplacement, sinon celle du 2e.
    // À la manette (UsingGamepad), le bouton de manette de l'action s'il en a un.
    public static string ShortBindingLabel(GameAction a)
    {
        if (UsingGamepad && !InputBindings.PadBindable(a))
            return a == GameAction.MoveUp || a == GameAction.MoveDown || a == GameAction.MoveLeft || a == GameAction.MoveRight
                ? Loc.T("Stick") : "";
        if (UsingGamepad && InputBindings.GetPad(a) != PadButton.None) return PadLabel(InputBindings.GetPad(a));
        KeyCode k0 = InputBindings.Get(a, 0), k1 = InputBindings.Get(a, 1);
        return ShortKeyLabel(k0 != KeyCode.None ? k0 : k1);
    }

    public static string ActionLabel(GameAction a)
    {
        switch (a)
        {
            case GameAction.MoveUp: return Loc.T("Avancer");
            case GameAction.MoveDown: return Loc.T("Reculer");
            case GameAction.MoveLeft: return Loc.T("Aller à gauche");
            case GameAction.MoveRight: return Loc.T("Aller à droite");
            case GameAction.Dash: return Loc.T("Dash");
            case GameAction.Ultimate: return Loc.T("Ultime");
            case GameAction.PhantomClone: return Loc.T("Clone spectral");
            case GameAction.Card1: return Loc.T("Carte n°1");
            case GameAction.Card2: return Loc.T("Carte n°2");
            case GameAction.Card3: return Loc.T("Carte n°3");
            case GameAction.OrbitShrink: return Loc.T("Orbites : rapprocher");
            case GameAction.OrbitGrow: return Loc.T("Orbites : éloigner");
            case GameAction.Pause: return Loc.T("Pause");
        }
        return a.ToString();
    }

    public static string ActionHint(GameAction a)
    {
        switch (a)
        {
            case GameAction.Dash: return Loc.T("Dash : déplacement rapide, invincible un court instant, absorbe les projectiles");
            case GameAction.Ultimate: return Loc.T("Déclenche l'ultime quand le cristal est chargé");
            case GameAction.PhantomClone: return Loc.T("Invoque le clone spectral. Nécessite la compétence correspondante");
            case GameAction.Card1: return Loc.T("Choisit la 1re carte à la montée de niveau");
            case GameAction.Card2: return Loc.T("Choisit la 2ème carte à la montée de niveau");
            case GameAction.Card3: return Loc.T("Choisit la 3ème carte à la montée de niveau");
            case GameAction.OrbitShrink: return Loc.T("Réduit le rayon des orbes de l'arme orbitale");
            case GameAction.OrbitGrow: return Loc.T("Augmente le rayon des orbes de l'arme orbitale");
            case GameAction.Pause: return Loc.T("Ouvre / ferme le menu pause");
        }
        return "";
    }

    // "F  /  Manette 2" : texte des touches d'une action (pour des indices d'interface).
    public static string BindingText(GameAction a)
    {
        if (UsingGamepad && InputBindings.GetPad(a) != PadButton.None) return PadLabel(InputBindings.GetPad(a));
        KeyCode k0 = InputBindings.Get(a, 0), k1 = InputBindings.Get(a, 1);
        if (k0 != KeyCode.None && k1 != KeyCode.None) return KeyLabel(k0) + " / " + KeyLabel(k1);
        return KeyLabel(k0 != KeyCode.None ? k0 : k1);
    }
}
