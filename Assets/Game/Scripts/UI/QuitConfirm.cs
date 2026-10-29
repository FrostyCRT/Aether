using TMPro;
using UnityEngine;
using UnityEngine.UI;

// AJOUTÉ (2026-10-28, retour utilisateur : "faire un écran de validation de quitter si un joueur Alt + F4 par erreur") -
// Alt+F4 ou la croix de la fenêtre ne ferment plus le jeu directement : Application.wantsToQuit est refusé une première
// fois et cette fenêtre demande confirmation, partout (menus et partie). En partie, le jeu passe d'abord en pause
// (menu Pause ouvert derrière la fenêtre) : annuler ramène le joueur sur le menu Pause, jamais en plein combat.
// Les fermetures DÉJÀ confirmées (bouton Quitter des Paramètres) passent par QuitNow(), sans seconde question.
// Fenêtre construite par Aether > Rebuild Quit Confirm Dialog (même style que les fenêtres de la page Paramètres) et
// chargée depuis Resources ; elle vit hors scène (DontDestroyOnLoad), au-dessus de toute autre interface.
public class QuitConfirm : MonoBehaviour
{
    [SerializeField] private GameObject _dialogRoot;
    [SerializeField] private TextMeshProUGUI _title;
    [SerializeField] private TextMeshProUGUI _body;
    [SerializeField] private TextMeshProUGUI _yesText;
    [SerializeField] private TextMeshProUGUI _noText;
    [SerializeField] private Button _yes;
    [SerializeField] private Button _no;

    private const string ResourcePath = "UI/QuitConfirmDialog";

    private static QuitConfirm _instance;
    private static bool _quitConfirmed;

    public static bool IsOpen => _instance != null && _instance._dialogRoot != null && _instance._dialogRoot.activeSelf;
    public static int ClosedFrame { get; private set; } = -1;   // le B qui ferme la fenêtre ne doit pas aussi reprendre la partie

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _instance = null;
        _quitConfirmed = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        Application.wantsToQuit -= OnWantsToQuit;
        Application.wantsToQuit += OnWantsToQuit;
    }

    private static bool OnWantsToQuit()
    {
#if UNITY_EDITOR
        return true;   // arrêt du Play dans l'éditeur : jamais bloqué (tester avec QuitConfirm.Show())
#else
        if (_quitConfirmed) return true;
        Show();
        return false;
#endif
    }

    // Ferme le jeu sans demander (fermeture déjà confirmée ailleurs).
    public static void QuitNow()
    {
        _quitConfirmed = true;
        GameSettings.Flush();
        InputBindings.Flush();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public static void Show()
    {
        if (_instance == null)
        {
            GameObject prefab = Resources.Load<GameObject>(ResourcePath);
            if (prefab == null) { QuitNow(); return; }   // fenêtre introuvable : ne jamais empêcher de quitter
            GameObject go = Instantiate(prefab);
            go.name = "QuitConfirm";
            DontDestroyOnLoad(go);
            _instance = go.GetComponent<QuitConfirm>();
            _instance._yes.onClick.AddListener(QuitNow);
            _instance._no.onClick.AddListener(_instance.Close);
        }
        _instance.Open();
    }

    private void Open()
    {
        // En partie : pause d'abord (le menu Pause s'ouvre derrière), sauf si le jeu est déjà arrêté (pause, choix de
        // carte, écran de fin).
        bool inRun = GameManager.Instance != null && !GameManager.Instance.IsGameOver;
        if (inRun && !GameManager.Instance.IsPaused) GameManager.Instance.TogglePause();

        _title.text = Loc.T("QUITTER LE JEU ?");
        _body.text = inRun ? Loc.T("Ta partie en cours sera perdue") : Loc.T("Le jeu va se fermer. Tes réglages sont enregistrés.");
        _yesText.text = Loc.T("Quitter");
        _noText.text = Loc.T("Annuler");
        _dialogRoot.SetActive(true);
    }

    private void Close()
    {
        _dialogRoot.SetActive(false);
        ClosedFrame = Time.frameCount;
    }

    private void Update()
    {
        if (!_dialogRoot.activeSelf) return;
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) QuitNow();
        else if (GameInput.BackDown()) Close();   // Échap / B : annuler
    }
}
