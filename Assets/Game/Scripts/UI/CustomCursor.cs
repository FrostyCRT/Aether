using UnityEngine;
using UnityEngine.UI;

// AJOUTÉ (2026-10-11, retour utilisateur : "j'ai fais faire un curseur de souris à mettre en jeu pour remplacer le
// curseur de base blanc") - remplace le curseur système par défaut (flèche blanche de l'OS) par le gantelet
// fantasy fourni (Assets/Resources/Cursor_Gauntlet.png, déplacé ici depuis Assets/Game/Assets 2D/UI/System/ pour
// pouvoir être chargé par Resources.Load sans référence de scène - même pattern que SettingsApplier/UI_Exposure.mat).
// MODIFIÉ (2026-10-18) - un second curseur (Cursor_v2.png, même dossier) a été essayé puis comparé à celui-ci à
// qualité d'import égale (RGBA32 non compressé + mipmaps sur les deux, voir aussi _displaySize ci-dessous - le
// gantelet d'origine paraissait "en low" avec l'ancien import DXT5/sans mipmaps). Retour utilisateur après
// comparaison en jeu : "on garde la V1" - Cursor_Gauntlet.png reste le curseur définitif.
//
// Objet persistant créé tout seul au lancement (RuntimeInitializeOnLoadMethod, comme SettingsApplier : rien à
// poser dans une scène, fonctionne dans TOUTES les scènes y compris LoadingScreen) : cache le curseur système, puis
// affiche une Image UI (Canvas Overlay, même sortingOrder que le reste de la superposition système) qui suit
// Input.mousePosition chaque frame.
//
// Approche "Image UI qui suit la souris" plutôt que Cursor.SetCursor : la texture source est un gros visuel
// stylisé (1490x1490) bien plus grand qu'un curseur matériel (souvent plafonné à 32x32 selon l'OS/le pilote sans
// passer par CursorMode.ForceSoftware) - une Image UI redimensionnée donne un rendu net et prévisible quelle que
// soit la plateforme, et reste cohérente avec le reste de l'UI (CanvasScaler, résolution de référence 1920x1080).
//
// CORRIGÉ (2026-11-07, retour utilisateur : "quand on change de fenêtre et qu'on revient, on a la souris Windows classique
// et en même temps le curseur du jeu, ça le fait aussi avec Alt + Tab [...] vérifie qu'il n'y ait pas le curseur
// Windows") - deux causes : (1) dans les menus, le curseur système n'était masqué qu'UNE fois (Cursor.visible = false
// dans Awake) ; au retour dans la fenêtre, Windows remet sa flèche et rien ne la recachait ; (2) en partie, le masquage
// passait par un curseur LOGICIEL vide (ForceSoftware), que Windows remplace lui aussi par sa flèche au retour du focus.
// Désormais (HideSystemCursor, seul point d'entrée, utilisé aussi par GameManager) : le curseur MATÉRIEL de la fenêtre
// est remplacé par une image 100 % transparente (CursorMode.Auto) - même si Windows le déclare « visible », il n'y a
// rien à voir dans la fenêtre - en plus de Cursor.visible = false et du confinement. Réappliqué au retour du focus
// (plusieurs frames de suite : Windows rend parfois sa flèche juste après l'événement), à chaque chargement de scène,
// et vérifié à chaque frame tant que la fenêtre a le focus.
public class CustomCursor : MonoBehaviour
{
    public static CustomCursor Instance { get; private set; }

    private static Texture2D _blankCursor;
    private int _reapplyFrames;

    // AJOUTÉ (2026-11-10, retour utilisateur : "si on est en fenêtré, j'aimerais que si on appuie sur la touche Échap, on
    // puisse sortir le curseur de la fenêtre du jeu") - en fenêtré (avec ou sans bordure), Échap libère la souris ; le
    // prochain clic dans le jeu la retient de nouveau. En plein écran exclusif, elle reste toujours retenue.
    private static bool _released;

    private static bool CanRelease => Screen.fullScreenMode != FullScreenMode.ExclusiveFullScreen;

    public static CursorLockMode DesiredLockState =>
        _released && CanRelease ? CursorLockMode.None : CursorLockMode.Confined;

    // Masque le curseur Windows dans la fenêtre du jeu (le gantelet, lui, est une image UI : voir Update).
    public static void HideSystemCursor()
    {
        if (_blankCursor == null)
        {
            _blankCursor = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            _blankCursor.SetPixels32(new Color32[32 * 32]);
            _blankCursor.Apply();
            _blankCursor.hideFlags = HideFlags.HideAndDontSave;
        }
        Cursor.SetCursor(_blankCursor, Vector2.zero, CursorMode.Auto);
        Cursor.visible = false;
        Cursor.lockState = DesiredLockState;
    }

    // CORRIGÉ (2026-11-07, retour utilisateur : "quand on Alt+Tab et qu'on met le curseur sur la page sans avoir cliqué
    // pour actualiser le focus, la souris disparaît et il n'y a aucun des deux") - fenêtre SANS le focus : le gantelet ne
    // suit plus la souris (le jeu ne reçoit plus ses mouvements), il est donc masqué, et c'est la flèche Windows normale
    // qui s'affiche au-dessus de la fenêtre, comme pour n'importe quelle application en arrière-plan. Au clic qui rend le
    // focus, HideSystemCursor reprend la main et le gantelet revient.
    // MODIFIÉ (2026-11-10, retour utilisateur : "quand je Alt + Tab du jeu, ça me reprend la souris de Windows au lieu de
    // garder la souris du jeu") - sans le focus, le curseur de la fenêtre n'est plus la flèche Windows mais le gantelet,
    // en curseur matériel : Windows l'affiche lui-même au-dessus du jeu (l'image UI, elle, reste masquée : jamais deux).
    public static void ShowSystemCursor()
    {
        Vector2 hotspot = Vector2.zero;
        Texture2D gauntlet = Instance != null ? Instance.HardwareGauntlet(out hotspot) : null;
        if (gauntlet != null) Cursor.SetCursor(gauntlet, hotspot, CursorMode.Auto);
        else Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private Sprite _sprite;
    private Texture2D _hardware;

    // Copie du gantelet à la taille affichée en jeu (48 px en 1080p), créée à la demande puis gardée.
    private Texture2D HardwareGauntlet(out Vector2 hotspot)
    {
        int size = Mathf.Clamp(Mathf.RoundToInt(_displaySize.y * Screen.height / 1080f), 32, 128);
        hotspot = new Vector2(_hotspotPivot.x * size, (1f - _hotspotPivot.y) * size);
        if (_sprite == null) return null;
        if (_hardware != null && _hardware.width == size) return _hardware;
        if (_hardware != null) Destroy(_hardware);

        // Comme l'image UI (preserveAspect) : le dessin garde ses proportions, centré en largeur dans un carré de size px,
        // pour que le point de clic (_hotspotPivot, exprimé dans ce carré) tombe au même endroit du doigt.
        Texture source = _sprite.texture;
        Rect r = _sprite.textureRect;
        int w = Mathf.Clamp(Mathf.RoundToInt(size * r.width / r.height), 1, size);
        RenderTexture rt = RenderTexture.GetTemporary(w, size, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        RenderTexture previous = RenderTexture.active;   // AVANT Blit, qui change la texture active (2026-11-11)
        Graphics.Blit(source, rt, new Vector2(r.width / source.width, r.height / source.height),
                      new Vector2(r.x / source.width, r.y / source.height));
        RenderTexture.active = rt;
        var drawing = new Texture2D(w, size, TextureFormat.RGBA32, false, false);
        drawing.ReadPixels(new Rect(0, 0, w, size), 0, 0, false);
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(rt);

        _hardware = new Texture2D(size, size, TextureFormat.RGBA32, false, false);
        _hardware.SetPixels32(new Color32[size * size]);
        _hardware.SetPixels32((size - w) / 2, 0, w, size, drawing.GetPixels32());
        _hardware.Apply();
        _hardware.hideFlags = HideFlags.HideAndDontSave;
        Destroy(drawing);
        return _hardware;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Boot()
    {
        if (Instance != null) return;
        GameObject go = new GameObject("[CustomCursor]");
        DontDestroyOnLoad(go);
        go.AddComponent<CustomCursor>();
    }

    // Taille affichée à l'écran (en pixels de référence 1920x1080, via le CanvasScaler) - la texture source est
    // bien plus grande, voir commentaire ci-dessus.
    [SerializeField] private Vector2 _displaySize = new Vector2(48f, 48f);

    // Point de la texture qui correspond à la VRAIE position de la souris (où les clics se produisent) - le bout
    // du doigt pointé, proche du coin haut-gauche de l'image. (0,1) = coin haut-gauche exact, (0.5,0.5) = centre.
    // MODIFIÉ (2026-10-18) - mesuré par échantillonnage de pixels (pixel opaque le plus haut de la texture
    // 1490x1490), pas à l'œil : pointe à x=0.149, y=0.995 de la largeur/hauteur.
    [SerializeField] private Vector2 _hotspotPivot = new Vector2(0.15f, 0.995f);

    private RectTransform _rect;
    private Canvas _canvas;
    private Image _image;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        Sprite sprite = Resources.Load<Sprite>("Cursor_Gauntlet");
        if (sprite == null)
        {
            Debug.LogWarning("[CustomCursor] Resources/Cursor_Gauntlet introuvable - curseur système laissé tel quel.");
            Destroy(gameObject);
            return;
        }
        _sprite = sprite;

        GameObject canvasGO = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler));
        canvasGO.transform.SetParent(transform, false);
        _canvas = canvasGO.GetComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 32761; // au-dessus du ScreenOverlay de SettingsApplier (32760)
        CanvasScaler scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject imgGO = new GameObject("CursorImage", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        imgGO.transform.SetParent(canvasGO.transform, false);
        _rect = (RectTransform)imgGO.transform;
        _rect.anchorMin = _rect.anchorMax = Vector2.zero;
        _rect.pivot = _hotspotPivot;
        _rect.sizeDelta = _displaySize;

        _image = imgGO.GetComponent<Image>();
        _image.sprite = sprite;
        _image.raycastTarget = false; // ne bloque jamais un clic sur l'UI en dessous
        _image.preserveAspect = true;

        HideSystemCursor();
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += (s, m) => { HideSystemCursor(); _reapplyFrames = 10; };

        // CORRIGÉ (2026-10-11, retour utilisateur : "dans le menu je peux faire sortir la souris du cadre de
        // 1920x1080p") - rien ne confinait la souris système à la fenêtre du jeu avant ce correctif (comportement
        // normal d'une fenêtre non verrouillée) ; invisible dès qu'elle est dans la fenêtre, elle n'y réapparaissait
        // donc qu'une fois ressortie - aucun repère visuel au moment de sortir. Confined la garde dans les limites
        // de la fenêtre (mais toujours déplaçable librement dedans, contrairement à Locked qui la fige au centre).
    }

    private void OnApplicationFocus(bool focus)
    {
        if (!focus) { _reapplyFrames = 0; ShowSystemCursor(); return; }
        HideSystemCursor();
        _reapplyFrames = 30;   // ~0,5 s : Windows peut rendre sa flèche quelques frames après le retour du focus
    }

    private void Update()
    {
        if (_rect == null) return;

        if (Application.isFocused)
        {
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (CanRelease && keyboard != null && keyboard.escapeKey.wasPressedThisFrame) _released = true;
            else if (_released && mouse != null && mouse.leftButton.wasPressedThisFrame) _released = false;

            if (_reapplyFrames > 0) { _reapplyFrames--; HideSystemCursor(); }
            else if (Cursor.visible || Cursor.lockState != DesiredLockState) HideSystemCursor();
        }

        // CORRIGÉ (2026-10-11, retour utilisateur : "je veux le même système qu'avant") - le gantelet s'affichait
        // en permanence, y compris en pleine partie, alors que l'ancien système (GameManager.ApplyCursor) masquait
        // totalement le curseur pendant le jeu actif et ne le réaffichait que sur un panneau (pause, paramètres,
        // level-up, Game Over). GameManager.NeedsCursor porte maintenant cette même logique ; absent de la scène
        // (MainMenu, LoadingScreen...), on affiche le gantelet par défaut, comme un curseur de menu normal.
        // MODIFIÉ (2026-10-28, session manette) - masqué tant que le joueur utilise la manette (on navigue à la
        // croix/au stick, un curseur immobile au milieu de l'écran gênerait) ; revient au premier mouvement de souris.
        bool shouldShow = Application.isFocused
                          && (GameManager.Instance == null || GameManager.Instance.NeedsCursor) && !GameInput.UsingGamepad;
        if (_image.enabled != shouldShow) _image.enabled = shouldShow;

        // Conversion écran -> espace local du Canvas Overlay (le CanvasScaler change le facteur d'échelle selon la
        // résolution réelle - division nécessaire pour que le curseur suive exactement le pixel sous la souris).
        Vector2 screenPos = Input.mousePosition;
        float scale = _canvas.scaleFactor > 0f ? _canvas.scaleFactor : 1f;
        _rect.anchoredPosition = screenPos / scale;
    }

    private void OnDestroy()
    {
        if (_hardware != null) Destroy(_hardware);
        if (Instance == this)
        {
            Instance = null;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }
    }
}
