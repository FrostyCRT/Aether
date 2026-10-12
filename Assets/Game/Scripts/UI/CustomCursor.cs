using UnityEngine;
using UnityEngine.UI;

// AJOUTÉ (2026-10-11, retour utilisateur : "j'ai fais faire un curseur de souris à mettre en jeu pour remplacer le
// curseur de base blanc") - remplace le curseur système par défaut (flèche blanche de l'OS) par le gantelet
// fantasy fourni (Assets/Resources/Cursor_Gauntlet.png, déplacé ici depuis Assets/Game/Assets 2D/UI/System/ pour
// pouvoir être chargé par Resources.Load sans référence de scène - même pattern que SettingsApplier/UI_Exposure.mat).
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
public class CustomCursor : MonoBehaviour
{
    public static CustomCursor Instance { get; private set; }

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
    // du doigt pointé, proche du coin haut-gauche de l'image (mesuré visuellement sur le PNG source : la pointe du
    // doigt est à environ 15% de la largeur depuis la gauche, tout en haut). (0,1) = coin haut-gauche exact,
    // (0.5,0.5) = centre.
    [SerializeField] private Vector2 _hotspotPivot = new Vector2(0.15f, 0.98f);

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

        Cursor.visible = false;

        // CORRIGÉ (2026-10-11, retour utilisateur : "dans le menu je peux faire sortir la souris du cadre de
        // 1920x1080p") - rien ne confinait la souris système à la fenêtre du jeu avant ce correctif (comportement
        // normal d'une fenêtre non verrouillée) ; invisible dès qu'elle est dans la fenêtre, elle n'y réapparaissait
        // donc qu'une fois ressortie - aucun repère visuel au moment de sortir. Confined la garde dans les limites
        // de la fenêtre (mais toujours déplaçable librement dedans, contrairement à Locked qui la fige au centre).
        Cursor.lockState = CursorLockMode.Confined;
    }

    private void Update()
    {
        if (_rect == null) return;

        // CORRIGÉ (2026-10-11, retour utilisateur : "je veux le même système qu'avant") - le gantelet s'affichait
        // en permanence, y compris en pleine partie, alors que l'ancien système (GameManager.ApplyCursor) masquait
        // totalement le curseur pendant le jeu actif et ne le réaffichait que sur un panneau (pause, paramètres,
        // level-up, Game Over). GameManager.NeedsCursor porte maintenant cette même logique ; absent de la scène
        // (MainMenu, LoadingScreen...), on affiche le gantelet par défaut, comme un curseur de menu normal.
        bool shouldShow = GameManager.Instance == null || GameManager.Instance.NeedsCursor;
        if (_image.enabled != shouldShow) _image.enabled = shouldShow;

        // Conversion écran -> espace local du Canvas Overlay (le CanvasScaler change le facteur d'échelle selon la
        // résolution réelle - division nécessaire pour que le curseur suive exactement le pixel sous la souris).
        Vector2 screenPos = Input.mousePosition;
        float scale = _canvas.scaleFactor > 0f ? _canvas.scaleFactor : 1f;
        _rect.anchoredPosition = screenPos / scale;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }
    }
}
